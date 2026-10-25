using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local two-device battle. One audio clock, independent judgments and scores.</summary>
public sealed class MultiplayRhythmController : MonoBehaviour
{
    [SerializeField] private SongChart songChart;
    [SerializeField] private MultiplayHud hud;
    [SerializeField] private float previewSeconds = 0.75f;
    [SerializeField] private float hitWindowSeconds = 0.35f;
    private sealed class Player
    {
        public LaunchpadDevice? Device;
        public bool Ready;
        public long Points;
        public int Combo;
        public int Score;
        public byte[] States; // 0: waiting, 1: hold pressed, 2: judged
        public byte[] Led = new byte[128];
        public float[] FeedbackUntil = new float[128];
        public byte[] Feedback = new byte[128];
    }

    private readonly Player[] players = { new Player(), new Player() };
    private readonly List<ChartNote> notes = new();
    [SerializeField] private LaunchpadMidiInput midi;
    [SerializeField] private AudioSource music;
    private long maximumPoints;
    private double startsAt;
    private double countdownEndsAt;
    private float endTime;
    private double resultShowsAt;
    private bool waitingForResult;
    private bool countingDown;
    private bool playing;
    private bool finished;
    private bool valid;
    private readonly Color idle = new Color(0.055f, 0.08f, 0.13f);

    private string Nickname(int index) => index == 0 ? GameSession.Instance.PlayerANickname : GameSession.Instance.PlayerBNickname;
    private float SongTime => (float)(AudioSettings.dspTime - startsAt);
    private float NoteTime(ChartNote note) => note.beat * songChart.SecondsPerBeat + songChart.chartOffsetSeconds;

    private void Start()
    {
        if (midi == null || music == null || hud == null)
        {
            Debug.LogError("Multiplay: assign Midi, Music and Hud in the Inspector.", this);
            enabled = false;
            return;
        }
        music.playOnAwake = false;
        music.loop = false;
        valid = songChart != null && songChart.music != null && songChart.bpm > 0 && songChart.notes != null && songChart.notes.Count > 0;
        if (valid)
        {
            foreach (ChartNote note in songChart.notes)
            {
                if (note == null || note.padNote / 10 < 1 || note.padNote / 10 > 8 || note.padNote % 10 < 1 || note.padNote % 10 > 8 || note.beat < 0 || (note.type == ChartNoteType.Hold && note.holdBeats <= 0))
                { valid = false; break; }
                notes.Add(note);
                maximumPoints += note.type == ChartNoteType.Hold ? 2000 : 1000;
            }
        }
        if (!valid) { hud.SetInstruction("Song Chart / audio / notes must be configured."); return; }
        notes.Sort((a, b) => a.beat.CompareTo(b.beat));
        // End on the final chart note, including hold release and the late judgment window.
        endTime = float.NegativeInfinity;
        foreach (ChartNote note in notes)
            endTime = Mathf.Max(endTime, NoteTime(note) + (note.type == ChartNoteType.Hold ? note.holdBeats * songChart.SecondsPerBeat : 0) + hitWindowSeconds);
        InitializeRound();
    }

    private void InitializeRound()
    {
        music.Stop();
        midi.ClearAllLeds();
        GameSession.Instance.ResetRoundScores();
        countingDown = playing = finished = false;
        waitingForResult = false;
        foreach (Player player in players)
        {
            player.Ready = false;
            player.Points = 0;
            player.Score = player.Combo = 0;
            player.States = new byte[notes.Count];
            Array.Clear(player.Led, 0, player.Led.Length);
            Array.Clear(player.FeedbackUntil, 0, player.FeedbackUntil.Length);
            hud.SetJudgment(Array.IndexOf(players, player), "");
            hud.ClearBoard(Array.IndexOf(players, player));
        }
        hud.SetInstruction("Press a grid pad on each Launchpad to join and READY");
        hud.SetResult(string.Empty);
        UpdateScores();
    }

    private void Update()
    {
        UpdateConnections();
        if (!valid) return;
        if (!finished && (countingDown || playing))
        {
            foreach (Player player in players)
                if (player.Device.HasValue && !midi.IsConnected(player.Device.Value))
                {
                    AbortRound();
                    return;
                }
        }
        if (countingDown)
        {
            double now = AudioSettings.dspTime;
            float remaining = (float)(countdownEndsAt - now);
            hud.SetInstruction(remaining > 0 ? Mathf.CeilToInt(remaining).ToString() : "");
            hud.ShowCountdown(remaining > 0);
            // Stay in the pre-song state for two more seconds after 3, 2, 1.
            if (now >= startsAt) { countingDown = false; playing = true; }
        }
        // Expire before accepting inputs, so a late release cannot rescue a missed hold.
        if (playing) ExpireNotes();
        while (midi.TryConsumePadPress(out LaunchpadPadPress press))
        {
            if (press.IsControlChange || press.Note / 10 < 1 || press.Note / 10 > 8 || press.Note % 10 < 1 || press.Note % 10 > 8) continue;
            int index = Array.FindIndex(players, p => p.Device.HasValue && p.Device.Value.DeviceKey == press.Device.DeviceKey);
            if (!playing && !countingDown && !finished && press.IsPressed)
            {
                if (index < 0)
                {
                    GameSession session = GameSession.Instance;
                    if (!string.IsNullOrEmpty(session.PlayerADeviceKey) && !string.IsNullOrEmpty(session.PlayerBDeviceKey))
                        index = press.Device.DeviceKey == session.PlayerADeviceKey ? 0 : press.Device.DeviceKey == session.PlayerBDeviceKey ? 1 : -1;
                    else index = Array.FindIndex(players, p => !p.Device.HasValue);
                }
                if (index < 0) continue;
                players[index].Device = press.Device;
                players[index].Ready = true;
                UpdateScores();
                if (players[0].Ready && players[1].Ready) BeginCountdown();
            }
            else if ((playing || (countingDown && AudioSettings.dspTime >= countdownEndsAt)) && index >= 0) Judge(players[index], press);
        }
        if (playing || (countingDown && AudioSettings.dspTime >= countdownEndsAt)) RenderPads();
        if (playing && SongTime > endTime)
        {
            playing = false;
            finished = true;
            music.Stop();
            midi.ClearAllLeds();
            resultShowsAt = AudioSettings.dspTime + 2.0;
            waitingForResult = true;
        }
        if (waitingForResult && AudioSettings.dspTime >= resultShowsAt)
        {
            waitingForResult = false;
            int difference = players[0].Score - players[1].Score;
            string result = difference == 0 ? "DRAW" : $"{Nickname(difference > 0 ? 0 : 1)} WINS!";
            hud.SetInstruction(result);
            hud.SetMatchResult(result, players[0].Score, players[1].Score);
        }
    }

    private void AbortRound()
    {
        waitingForResult = false;
        music.Stop();
        midi.ClearAllLeds();
        playing = countingDown = false;
        finished = true;
        hud.SetInstruction("Launchpad disconnected. Round cancelled.");
        hud.SetResult("ROUND CANCELLED");
    }

    private void BeginCountdown()
    {
        foreach (Player player in players)
            if (!midi.IsConnected(player.Device.Value)) { AbortRound(); return; }
        countdownEndsAt = AudioSettings.dspTime + 3.0;
        startsAt = countdownEndsAt + 2.0;
        music.clip = songChart.music;
        music.PlayScheduled(startsAt);
        countingDown = true;
        hud.SetInstruction("3");
        hud.ShowCountdown(true);
    }

    private void Judge(Player player, LaunchpadPadPress press)
    {
        float now = SongTime;
        int candidate = -1;
        float closest = float.MaxValue;
        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote note = notes[i];
            if (note.padNote != press.Note || player.States[i] == 2) continue;
            if (!press.IsPressed && note.type == ChartNoteType.Hold && player.States[i] == 1)
            {
                float releaseDelta = Mathf.Abs(now - NoteTime(note) - note.holdBeats * songChart.SecondsPerBeat);
                Complete(player, i, releaseDelta <= hitWindowSeconds ? JudgmentPoints(releaseDelta) * 2 : 400);
                return;
            }
            float delta = Mathf.Abs(now - NoteTime(note));
            if (press.IsPressed && player.States[i] == 0 && delta <= hitWindowSeconds && delta < closest)
            { candidate = i; closest = delta; }
        }
        if (candidate < 0) return;
        if (notes[candidate].type == ChartNoteType.Hold) player.States[candidate] = 1;
        else Complete(player, candidate, JudgmentPoints(closest));
    }

    private static int JudgmentPoints(float delta) => MultiplayScoring.JudgmentPoints(delta);

    private void ExpireNotes()
    {
        float now = SongTime;
        foreach (Player player in players)
            for (int i = 0; i < notes.Count; i++)
            {
                if (player.States[i] == 2) continue;
                ChartNote note = notes[i];
                float deadline = NoteTime(note) + hitWindowSeconds;
                if (player.States[i] == 1) deadline += note.holdBeats * songChart.SecondsPerBeat;
                if (now > deadline) Complete(player, i, player.States[i] == 1 ? 400 : 0);
            }
    }

    private void Complete(Player player, int index, int points)
    {
        player.States[index] = 2;
        player.Points += points;
        player.Combo = points == 0 ? 0 : player.Combo + 1;
        player.Score = MultiplayScoring.Normalize(player.Points, maximumPoints);
        int unit = notes[index].type == ChartNoteType.Hold ? points / 2 : points;
        hud.SetJudgment(Array.IndexOf(players, player), unit == 1000 ? "PERFECT" : unit == 800 ? "GREAT" : unit == 500 ? "GOOD" : unit > 0 ? "COOL" : "MISS");
        int pad = notes[index].padNote;
        player.FeedbackUntil[pad] = Time.unscaledTime + 0.18f;
        player.Feedback[pad] = points > 0 ? (byte)19 : (byte)5;
        UpdateScores();
    }

    private void UpdateScores()
    {
        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            hud.SetPlayer(i, Nickname(i), p.Score, p.Combo);
        }
        GameSession.Instance.SetMultiplayerScores(players[0].Score, players[1].Score);
        hud.SetScores(players[0].Score, players[1].Score);
    }
    private void RenderPads()
    {
        float now = SongTime;
        foreach (Player player in players)
        {
            if (!player.Device.HasValue) continue;
            for (int row = 1; row <= 8; row++)
                for (int col = 1; col <= 8; col++)
                {
                    int pad = row * 10 + col;
                    byte colour = 0;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < notes.Count; i++)
                    {
                        if (player.States[i] == 2 || notes[i].padNote != pad) continue;
                        float at = NoteTime(notes[i]);
                        if (now < at - previewSeconds || at >= nearest) continue;
                        nearest = at;
                        colour = player.States[i] == 1 ? (byte)5 : now >= at ? (byte)19 : (byte)45;
                    }
                    if (colour == 0 && player.FeedbackUntil[pad] > Time.unscaledTime) colour = player.Feedback[pad];
                    if (player.Led[pad] != colour)
                    { midi.SetPadLed(player.Device.Value, pad, colour); player.Led[pad] = colour; }
                    hud.SetPad(Array.IndexOf(players, player), pad, colour == 19 ? new Color(0.1f, 1f, 0.55f) : colour == 45 ? new Color(0.15f, 0.5f, 1f) : colour == 5 ? new Color(1f, 0.18f, 0.25f) : idle);
                }
        }
    }

    private void UpdateConnections()
    {
        if (midi == null || hud == null) return;
        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            string key = player.Device?.DeviceKey ?? (i == 0 ? GameSession.Instance.PlayerADeviceKey : GameSession.Instance.PlayerBDeviceKey);
            bool assigned = !string.IsNullOrEmpty(key);
            bool connected = assigned && midi.IsDeviceConnected(key);
            if (!connected && !playing && !countingDown) player.Ready = false;
            hud.SetConnection(i, assigned, connected, player.Ready);
        }
    }

    // Optional: wire a user-created Menu button's OnClick to this method.
    public void ReturnToMenu() => SceneManager.LoadScene("ModeSelect");
    private void OnDisable()
    {
        if (music != null) music.Stop();
        if (midi != null) midi.ClearAllLeds();
    }
}
