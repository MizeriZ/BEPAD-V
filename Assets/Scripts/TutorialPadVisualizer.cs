using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays one centered or two side-by-side 8x8 Launchpad-style boards.</summary>
public sealed class TutorialPadVisualizer : MonoBehaviour
{
    [SerializeField] private GameObject singlePlayerLayout;
    [SerializeField] private RectTransform singlePlayerBoard;
    [SerializeField] private GameObject multiplayerLayout;
    [SerializeField] private RectTransform playerABoard;
    [SerializeField] private RectTransform playerBBoard;
    [SerializeField, Min(0.2f)] private float cueFadePeriod = 1.2f;

    private readonly Dictionary<RectTransform, Image[]> boards = new();
    private readonly Dictionary<string, RectTransform> playerBoards = new();
    private bool isMultiplayer;
    private int pulsingNote = -1;
    private bool pulsingLongNote;

    private readonly Color idle = new Color(0.08f, 0.1f, 0.14f);
    private readonly Color preview = new Color(0.08f, 0.25f, 0.58f);
    private readonly Color active = new Color(0.12f, 0.62f, 1f);
    private readonly Color holdPreview = new Color(0.58f, 0.08f, 0.1f);
    private readonly Color holdActive = new Color(1f, 0.12f, 0.16f);
    private readonly Color success = new Color(0.1f, 0.8f, 0.3f);
    private readonly Color miss = new Color(0.95f, 0.15f, 0.2f);

    private void Awake()
    {
        isMultiplayer = GameSession.Instance.Mode == GameMode.Multi;
        if (singlePlayerLayout != null) singlePlayerLayout.SetActive(!isMultiplayer);
        if (multiplayerLayout != null) multiplayerLayout.SetActive(isMultiplayer);
        BuildBoard(singlePlayerBoard);
        BuildBoard(playerABoard);
        BuildBoard(playerBBoard);
    }

    private void Start()
    {
        // Canvas layout values are reliable after the first layout pass.
        Canvas.ForceUpdateCanvases();
        ResizeAllBoards();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            ResizeAllBoards();
    }

    private void Update()
    {
        if (pulsingNote < 0) return;

        float wave = Mathf.PingPong(Time.unscaledTime / cueFadePeriod, 1f);
        float easedWave = Mathf.SmoothStep(0f, 1f, wave);
        Color dim = pulsingLongNote ? holdPreview : preview;
        Color bright = pulsingLongNote ? holdActive : active;
        SetAllBoards(pulsingNote, Color.Lerp(dim, bright, easedWave));
    }

    public void RegisterPlayer(LaunchpadDevice device)
    {
        if (!isMultiplayer || playerBoards.ContainsKey(device.DeviceKey)) return;
        GameSession session = GameSession.Instance;
        bool hasAssignments = !string.IsNullOrEmpty(session.PlayerADeviceKey) && !string.IsNullOrEmpty(session.PlayerBDeviceKey);
        bool isB = hasAssignments ? device.DeviceKey == session.PlayerBDeviceKey : playerBoards.Count != 0;
        playerBoards.Add(device.DeviceKey, isB ? playerBBoard : playerABoard);
    }

    public void ShowPreview(int note, bool isLongNote = false)
    {
        pulsingNote = note;
        pulsingLongNote = isLongNote;
        SetAllBoards(note, isLongNote ? holdPreview : preview);
    }

    public void ShowActive(int note, bool isLongNote = false)
    {
        StopPulse(note);
        SetAllBoards(note, isLongNote ? holdActive : active);
    }

    public void ShowFeedback(int note, bool wasSuccess)
    {
        StopPulse(note);
        SetAllBoards(note, wasSuccess ? success : miss);
    }

    public void ClearPad(int note)
    {
        StopPulse(note);
        SetAllBoards(note, idle);
    }

    public void ShowPlayerFeedback(LaunchpadDevice device, int note, bool wasSuccess)
    {
        if (isMultiplayer && playerBoards.TryGetValue(device.DeviceKey, out RectTransform board))
            SetBoardPad(board, note, wasSuccess ? success : miss);
        else if (!isMultiplayer)
            SetBoardPad(singlePlayerBoard, note, wasSuccess ? success : miss);
    }

    public void Clear()
    {
        pulsingNote = -1;
        foreach (Image[] board in boards.Values)
            foreach (Image pad in board) pad.color = idle;
    }

    private void StopPulse(int note)
    {
        if (pulsingNote == note) pulsingNote = -1;
    }

    private void BuildBoard(RectTransform board)
    {
        if (board == null || boards.ContainsKey(board)) return;
        GridLayoutGroup layout = board.GetComponent<GridLayoutGroup>() ?? board.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;
        layout.padding = new RectOffset(8, 8, 8, 8);

        Image[] pads = new Image[64];
        for (int i = 0; i < pads.Length; i++)
        {
            GameObject pad = new GameObject($"Pad {i + 1}", typeof(RectTransform), typeof(Image));
            pad.transform.SetParent(board, false);
            pads[i] = pad.GetComponent<Image>();
            pads[i].color = idle;
        }
        boards.Add(board, pads);
        ResizeBoard(board);
    }

    private void ResizeAllBoards()
    {
        foreach (RectTransform board in boards.Keys)
            ResizeBoard(board);
    }

    private static void ResizeBoard(RectTransform board)
    {
        if (board == null) return;
        GridLayoutGroup layout = board.GetComponent<GridLayoutGroup>();
        if (layout == null) return;

        float usableSize = Mathf.Min(board.rect.width, board.rect.height) -
                           layout.padding.left - layout.padding.right;
        float gap = Mathf.Clamp(usableSize * 0.018f, 2f, 8f);
        float cellSize = (usableSize - gap * 7f) / 8f;
        if (cellSize > 0f)
        {
            layout.spacing = new Vector2(gap, gap);
            layout.cellSize = new Vector2(cellSize, cellSize);
        }
    }

    private void SetAllBoards(int note, Color color)
    {
        if (isMultiplayer)
        {
            SetBoardPad(playerABoard, note, color);
            SetBoardPad(playerBBoard, note, color);
        }
        else SetBoardPad(singlePlayerBoard, note, color);
    }

    private void SetBoardPad(RectTransform board, int note, Color color)
    {
        if (board == null || !boards.TryGetValue(board, out Image[] pads)) return;
        int column = note % 10 - 1;
        int row = note / 10 - 1;
        if (column is < 0 or > 7 || row is < 0 or > 7) return;
        pads[(7 - row) * 8 + column].color = color;
    }
}
