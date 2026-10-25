using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FreestyleCueStyle
{
    Pulse,
    FadeToTimingGreen
}

/// <summary>Plays a SongChart as a single-player rhythm game with score and combo.</summary>
public sealed class FreestyleRhythmController : MonoBehaviour
{
    private sealed class ActiveNote
    {
        public ChartNote Data;
        public bool Pressed;
        public bool ReachedTiming;
        public float CueDurationSeconds;
    }

    [Header("Song")]
    [SerializeField] private SongChart songChart;
    [SerializeField] private AudioSource musicSource;
    [SerializeField, Min(0.1f)] private float previewSeconds = 0.75f;
    [SerializeField, Min(0.05f)] private float hitWindowSeconds = 0.35f;
    [Header("LED Cue")]
    [SerializeField] private FreestyleCueStyle cueStyle = FreestyleCueStyle.Pulse;
    [SerializeField, Min(0.25f)] private float countdownStepSeconds = 1f;
    [SerializeField] private string resultSceneName = "Result";
    [SerializeField, Min(0f)] private float resultTransitionDelay = 1.5f;

    [Header("Scene")]
    [SerializeField] private LaunchpadMidiInput launchpadInput;
    [SerializeField] private FreestylePadVisualizer padVisualizer;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text judgmentText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;

    private readonly List<ActiveNote> activeNotes = new();
    private LaunchpadDevice playerDevice;
    private bool hasPlayer;
    private bool isCountingDown;
    private bool isPlaying;
    private int nextNoteIndex;
    private int score;
    private int combo;
    private int bestCombo;
    private int perfectCount;
    private int greatCount;
    private int goodCount;
    private int coolCount;
    private int missCount;
    private double songStartedAt;

    private readonly Color perfectColor = new(1f, 0.86f, 0.12f);
    private readonly Color greatColor = new(0.25f, 1f, 0.8f);
    private readonly Color goodColor = new(0.15f, 0.85f, 0.3f);
    private readonly Color coolColor = new(0.72f, 0.72f, 0.72f);
    private readonly Color missColor = new(1f, 0.18f, 0.18f);

    private void Awake()
    {
        if (launchpadInput == null)
            launchpadInput = GetComponent<LaunchpadMidiInput>() ?? gameObject.AddComponent<LaunchpadMidiInput>();
        if (padVisualizer == null)
            padVisualizer = GetComponent<FreestylePadVisualizer>();
        SetInstruction("Press any Launchpad pad to start FREESTYLE");
        SetJudgment(string.Empty, Color.white);
        UpdateScoreUi();
    }

    private void Update()
    {
        ConsumeInput();
        if (!isPlaying) return;

        float beat = CurrentBeat;
        SpawnUpcomingNotes(beat);
        PromoteTimingNotes(beat);
        ExpireMissedNotes(beat);

        if (beat >= songChart.totalBeats && activeNotes.Count == 0)
            EndSong();
    }

    private void ConsumeInput()
    {
        while (launchpadInput.TryConsumePadPress(out LaunchpadPadPress press))
        {
            if (!hasPlayer)
            {
                if (!press.IsPressed) continue;
                playerDevice = press.Device;
                hasPlayer = true;
                StartCountdown();
                continue;
            }

            if (!isPlaying || press.Device.DeviceKey != playerDevice.DeviceKey || press.IsControlChange)
                continue;

            ActiveNote note = activeNotes.Find(item => item.Data.padNote == press.Note &&
                (item.Data.type == ChartNoteType.Hold ? !item.Pressed || !press.IsPressed : press.IsPressed));
            if (note == null) continue;

            if (note.Data.type == ChartNoteType.Tap && press.IsPressed)
                JudgeTap(note);
            else if (note.Data.type == ChartNoteType.Hold)
                JudgeHold(note, press.IsPressed);
        }
    }

    private void StartCountdown()
    {
        if (songChart == null)
        {
            SetInstruction("Assign a Song Chart first");
            hasPlayer = false;
            return;
        }

        if (!isCountingDown)
            StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        isCountingDown = true;
        launchpadInput.SetAllLeds(playerDevice, playerB: false);
        for (int count = 3; count >= 1; count--)
        {
            SetInstruction(count.ToString());
            yield return new WaitForSecondsRealtime(countdownStepSeconds);
        }

        SetInstruction("START!");
        yield return new WaitForSecondsRealtime(countdownStepSeconds * 0.5f);
        launchpadInput.ClearAllLeds();
        BeginSong();
        isCountingDown = false;
    }

    private void BeginSong()
    {

        songChart.SortNotesByBeat();
        if (musicSource != null && songChart.music != null)
        {
            musicSource.clip = songChart.music;
            musicSource.Play();
        }
        songStartedAt = AudioSettings.dspTime;
        isPlaying = true;
        SetInstruction(string.Empty);
    }

    private float CurrentBeat => (float)((AudioSettings.dspTime - songStartedAt - songChart.chartOffsetSeconds) / songChart.SecondsPerBeat);

    private void SpawnUpcomingNotes(float currentBeat)
    {
        while (nextNoteIndex < songChart.notes.Count && GetCueStartBeat(nextNoteIndex) <= currentBeat)
        {
            ChartNote data = songChart.notes[nextNoteIndex++];
            ActiveNote note = new()
            {
                Data = data,
                CueDurationSeconds = Mathf.Max(0.01f, (data.beat - currentBeat) * songChart.SecondsPerBeat)
            };
            activeNotes.Add(note);
            bool isHold = data.type == ChartNoteType.Hold;
            if (cueStyle == FreestyleCueStyle.Pulse)
            {
                launchpadInput.SetPadLed(playerDevice, data.padNote, isHold ? (byte)5 : (byte)45, pulsing: true);
                padVisualizer?.ShowPreview(data.padNote, isHold);
            }
            else
            {
                launchpadInput.SetPadLed(playerDevice, data.padNote, 0);
                padVisualizer?.ShowFadeToTiming(data.padNote, isHold, note.CueDurationSeconds);
                StartCoroutine(FadeCueLed(note, isHold));
            }
        }
    }

    private float GetCueStartBeat(int noteIndex)
    {
        ChartNote note = songChart.notes[noteIndex];
        float previewBeats = previewSeconds / songChart.SecondsPerBeat;
        float cueStartBeat = note.beat - previewBeats;

        // Keep consecutive cues separate. Notes deliberately placed on the same beat are chords,
        // so they retain their simultaneous LED cue.
        for (int index = noteIndex - 1; index >= 0; index--)
        {
            ChartNote previous = songChart.notes[index];
            if (Mathf.Approximately(previous.beat, note.beat)) continue;

            float previousEndBeat = previous.beat + hitWindowSeconds / songChart.SecondsPerBeat;
            if (previous.type == ChartNoteType.Hold)
                previousEndBeat = previous.beat + previous.holdBeats + hitWindowSeconds / songChart.SecondsPerBeat;
            return Mathf.Max(cueStartBeat, previousEndBeat);
        }

        return cueStartBeat;
    }

    private void PromoteTimingNotes(float currentBeat)
    {
        foreach (ActiveNote note in activeNotes)
        {
            if (note.ReachedTiming || currentBeat < note.Data.beat) continue;
            note.ReachedTiming = true;
            if (cueStyle == FreestyleCueStyle.FadeToTimingGreen)
            {
                launchpadInput.SetPadLed(playerDevice, note.Data.padNote, 19);
                padVisualizer?.ShowTimingTarget(note.Data.padNote);
            }
            else
            {
                bool isHold = note.Data.type == ChartNoteType.Hold;
                launchpadInput.SetPadLed(playerDevice, note.Data.padNote, isHold ? (byte)5 : (byte)45);
                padVisualizer?.ShowActive(note.Data.padNote, isHold);
            }
        }
    }

    private void ExpireMissedNotes(float currentBeat)
    {
        for (int index = activeNotes.Count - 1; index >= 0; index--)
        {
            ActiveNote note = activeNotes[index];
            float deadline = note.Data.type == ChartNoteType.Hold && note.Pressed
                ? note.Data.beat + note.Data.holdBeats + hitWindowSeconds / songChart.SecondsPerBeat
                : note.Data.beat + hitWindowSeconds / songChart.SecondsPerBeat;
            if (currentBeat > deadline)
            {
                // Once a hold was started successfully, releasing it late is never a MISS.
                if (note.Data.type == ChartNoteType.Hold && note.Pressed)
                    Complete(note, 400, "COOL", coolColor);
                else
                    Miss(note);
            }
        }
    }

    private void JudgeTap(ActiveNote note)
    {
        float deltaSeconds = Mathf.Abs(CurrentBeat - note.Data.beat) * songChart.SecondsPerBeat;
        if (deltaSeconds > hitWindowSeconds) return;
        GetJudgment(deltaSeconds, out int points, out string judgment, out Color color);
        Complete(note, points, judgment, color);
    }

    private void JudgeHold(ActiveNote note, bool isPressed)
    {
        float currentBeat = CurrentBeat;
        float hitWindowBeats = hitWindowSeconds / songChart.SecondsPerBeat;
        if (!note.Pressed && isPressed)
        {
            if (Mathf.Abs(currentBeat - note.Data.beat) > hitWindowBeats) return;
            note.Pressed = true;
            launchpadInput.SetPadLed(playerDevice, note.Data.padNote, 5);
            padVisualizer?.ShowActive(note.Data.padNote, isLongNote: true);
            if (cueStyle == FreestyleCueStyle.FadeToTimingGreen)
                StartCoroutine(FadeHoldReleaseCue(note));
            return;
        }

        if (note.Pressed && !isPressed)
        {
            float releaseBeat = note.Data.beat + note.Data.holdBeats;
            float deltaSeconds = Mathf.Abs(currentBeat - releaseBeat) * songChart.SecondsPerBeat;
            if (deltaSeconds <= hitWindowSeconds)
            {
                GetJudgment(deltaSeconds, out int points, out string judgment, out Color color);
                Complete(note, points * 2, judgment, color);
            }
            else
                Complete(note, 400, "COOL", coolColor);
        }
    }

    private void Complete(ActiveNote note, int points, string judgment, Color judgmentColor)
    {
        activeNotes.Remove(note);
        score += points;
        RecordJudgment(judgment);
        combo++;
        bestCombo = Mathf.Max(bestCombo, combo);
        GameSession.Instance.SetSinglePlayerScore(score);
        launchpadInput.SetPadLed(playerDevice, note.Data.padNote, 19);
        padVisualizer?.ShowFeedback(note.Data.padNote, wasSuccess: true);
        SetJudgment(judgment, judgmentColor);
        UpdateScoreUi();
        StartCoroutine(ClearPadAfterFeedback(note.Data.padNote));
    }

    private void Miss(ActiveNote note)
    {
        activeNotes.Remove(note);
        combo = 0;
        missCount++;
        launchpadInput.SetPadLed(playerDevice, note.Data.padNote, 5);
        padVisualizer?.ShowFeedback(note.Data.padNote, wasSuccess: false);
        SetJudgment("MISS", missColor);
        UpdateScoreUi();
        StartCoroutine(ClearPadAfterFeedback(note.Data.padNote));
    }

    private IEnumerator ClearPadAfterFeedback(int padNote)
    {
        yield return new WaitForSecondsRealtime(0.2f);
        launchpadInput.SetPadLed(playerDevice, padNote, 0);
        padVisualizer?.ClearPad(padNote);
    }

    private IEnumerator FadeCueLed(ActiveNote note, bool isHold)
    {
        float elapsed = 0f;
        byte cueColor = isHold ? (byte)5 : (byte)45;
        while (elapsed < note.CueDurationSeconds && activeNotes.Contains(note) && !note.ReachedTiming)
        {
            // Launchpad palettes do not expose continuous brightness; short pulses make the
            // hardware cue grow while the on-screen board performs the smooth fade.
            bool lightOn = elapsed >= note.CueDurationSeconds * 0.25f;
            launchpadInput.SetPadLed(playerDevice, note.Data.padNote, lightOn ? cueColor : (byte)0, pulsing: elapsed >= note.CueDurationSeconds * 0.55f);
            yield return new WaitForSecondsRealtime(0.08f);
            elapsed += 0.08f;
        }
    }

    private IEnumerator FadeHoldReleaseCue(ActiveNote note)
    {
        float releaseBeat = note.Data.beat + note.Data.holdBeats;
        float duration = Mathf.Max(0.01f, (releaseBeat - CurrentBeat) * songChart.SecondsPerBeat);
        padVisualizer?.ShowHoldReleaseFade(note.Data.padNote, duration);
        yield return new WaitForSecondsRealtime(duration);
        if (!activeNotes.Contains(note) || !note.Pressed) yield break;
        launchpadInput.SetPadLed(playerDevice, note.Data.padNote, 19);
        padVisualizer?.ShowTimingTarget(note.Data.padNote);
    }

    private void EndSong()
    {
        isPlaying = false;
        launchpadInput.ClearAllLeds();
        GameSession.Instance.SetSinglePlayerResult(score, bestCombo, GetMaximumScore(), perfectCount, greatCount, goodCount, coolCount, missCount);
        if (!string.IsNullOrWhiteSpace(resultSceneName))
            StartCoroutine(LoadResultAfterDelay());
    }

    private IEnumerator LoadResultAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resultTransitionDelay);
        SceneManager.LoadScene(resultSceneName);
    }

    private void UpdateScoreUi()
    {
        if (scoreText != null) scoreText.text = $"SCORE  {score:N0}";
        if (comboText != null)
        {
            comboText.alignment = TextAlignmentOptions.Center;
            comboText.color = new Color32(0x00, 0xFF, 0x8F, 0xFF);
            comboText.text = $"COMBO\n{combo}";
        }
    }

    private void SetInstruction(string text)
    {
        if (instructionText != null) instructionText.text = text;
    }

    private int GetMaximumScore()
    {
        if (songChart == null || songChart.notes == null) return 1;
        int maximum = 0;
        foreach (ChartNote note in songChart.notes)
            maximum += note.type == ChartNoteType.Hold ? 2000 : 1000;
        return Mathf.Max(1, maximum);
    }

    private void RecordJudgment(string judgment)
    {
        switch (judgment)
        {
            case "PERFECT": perfectCount++; break;
            case "GREAT": greatCount++; break;
            case "GOOD": goodCount++; break;
            case "COOL": coolCount++; break;
        }
    }

    private void SetJudgment(string text, Color color)
    {
        if (judgmentText == null) return;
        judgmentText.text = text;
        judgmentText.color = color;
    }

    private void GetJudgment(float deltaSeconds, out int points, out string judgment, out Color color)
    {
        if (deltaSeconds <= 0.06f)
        {
            points = 1000;
            judgment = "PERFECT";
            color = perfectColor;
        }
        else if (deltaSeconds <= 0.12f)
        {
            points = 800;
            judgment = "GREAT";
            color = greatColor;
        }
        else if (deltaSeconds <= 0.22f)
        {
            points = 500;
            judgment = "GOOD";
            color = goodColor;
        }
        else
        {
            points = 200;
            judgment = "COOL";
            color = coolColor;
        }
    }
}
