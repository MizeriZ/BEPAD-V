using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the single 8x8 Launchpad board used by FREESTYLE.</summary>
public sealed class FreestylePadVisualizer : MonoBehaviour
{
    private enum AnimationMode { None, Pulse, FadeToTiming, FadeHoldRelease }

    [SerializeField] private RectTransform board;
    [SerializeField, Min(0.2f)] private float cueFadePeriod = 1.2f;

    private Image[] pads;
    private int pulsingNote = -1;
    private bool pulsingLongNote;
    private AnimationMode animationMode;
    private float animationStartedAt;
    private float animationDuration;

    private readonly Color idle = new(0.08f, 0.1f, 0.14f);
    private readonly Color preview = new(0.08f, 0.25f, 0.58f);
    private readonly Color active = new(0.12f, 0.62f, 1f);
    private readonly Color holdPreview = new(0.58f, 0.08f, 0.1f);
    private readonly Color holdActive = new(1f, 0.12f, 0.16f);
    private readonly Color success = new(0.1f, 0.8f, 0.3f);
    private readonly Color miss = new(0.95f, 0.15f, 0.2f);
    private readonly Color timingGreen = new(0.1f, 0.8f, 0.3f);

    private void Awake() => BuildBoard();

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        ResizeBoard();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) ResizeBoard();
    }

    private void Update()
    {
        if (pulsingNote < 0) return;

        if (animationMode == AnimationMode.Pulse)
        {
            float wave = Mathf.PingPong(Time.unscaledTime / cueFadePeriod, 1f);
            Color dim = pulsingLongNote ? holdPreview : preview;
            Color bright = pulsingLongNote ? holdActive : active;
            SetPad(pulsingNote, Color.Lerp(dim, bright, Mathf.SmoothStep(0f, 1f, wave)));
            return;
        }

        float progress = Mathf.Clamp01((Time.unscaledTime - animationStartedAt) / animationDuration);
        if (animationMode == AnimationMode.FadeToTiming)
            SetPad(pulsingNote, FadeIntoTimingColor(pulsingLongNote ? holdActive : active, progress));
        else if (animationMode == AnimationMode.FadeHoldRelease)
            SetPad(pulsingNote, FadeOutToTimingColor(progress));
    }

    public void ShowPreview(int note, bool isLongNote = false)
    {
        pulsingNote = note;
        pulsingLongNote = isLongNote;
        animationMode = AnimationMode.Pulse;
        SetPad(note, isLongNote ? holdPreview : preview);
    }

    public void ShowFadeToTiming(int note, bool isLongNote, float duration)
    {
        pulsingNote = note;
        pulsingLongNote = isLongNote;
        animationMode = AnimationMode.FadeToTiming;
        animationStartedAt = Time.unscaledTime;
        animationDuration = Mathf.Max(0.01f, duration);
        SetPad(note, idle);
    }

    public void ShowHoldReleaseFade(int note, float duration)
    {
        pulsingNote = note;
        pulsingLongNote = true;
        animationMode = AnimationMode.FadeHoldRelease;
        animationStartedAt = Time.unscaledTime;
        animationDuration = Mathf.Max(0.01f, duration);
        SetPad(note, holdActive);
    }

    public void ShowTimingTarget(int note)
    {
        StopPulse(note);
        SetPad(note, timingGreen);
    }

    public void ShowActive(int note, bool isLongNote = false)
    {
        StopPulse(note);
        SetPad(note, isLongNote ? holdActive : active);
    }

    public void ShowFeedback(int note, bool wasSuccess)
    {
        StopPulse(note);
        SetPad(note, wasSuccess ? success : miss);
    }

    public void ClearPad(int note)
    {
        StopPulse(note);
        SetPad(note, idle);
    }

    public void Clear()
    {
        pulsingNote = -1;
        animationMode = AnimationMode.None;
        if (pads == null) return;
        foreach (Image pad in pads) pad.color = idle;
    }

    private void BuildBoard()
    {
        if (board == null) return;
        GridLayoutGroup layout = board.GetComponent<GridLayoutGroup>() ?? board.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;
        layout.padding = new RectOffset(8, 8, 8, 8);

        pads = new Image[64];
        for (int i = 0; i < pads.Length; i++)
        {
            GameObject pad = new($"Pad {i + 1}", typeof(RectTransform), typeof(Image));
            pad.transform.SetParent(board, false);
            pads[i] = pad.GetComponent<Image>();
            pads[i].color = idle;
        }
        ResizeBoard();
    }

    private void ResizeBoard()
    {
        if (board == null) return;
        GridLayoutGroup layout = board.GetComponent<GridLayoutGroup>();
        if (layout == null) return;
        float usableSize = Mathf.Min(board.rect.width, board.rect.height) - layout.padding.left - layout.padding.right;
        float gap = Mathf.Clamp(usableSize * 0.018f, 2f, 8f);
        float cellSize = (usableSize - gap * 7f) / 8f;
        if (cellSize <= 0f) return;
        layout.spacing = new Vector2(gap, gap);
        layout.cellSize = new Vector2(cellSize, cellSize);
    }

    private void StopPulse(int note)
    {
        if (pulsingNote != note) return;
        pulsingNote = -1;
        animationMode = AnimationMode.None;
    }

    private Color FadeIntoTimingColor(Color cueColor, float progress)
    {
        if (progress < 0.85f)
            return Color.Lerp(idle, cueColor, Mathf.SmoothStep(0f, 1f, progress / 0.85f));
        return Color.Lerp(cueColor, timingGreen, Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.15f));
    }

    private Color FadeOutToTimingColor(float progress)
    {
        if (progress < 0.85f)
            return Color.Lerp(holdActive, idle, Mathf.SmoothStep(0f, 1f, progress / 0.85f));
        return Color.Lerp(idle, timingGreen, Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.15f));
    }

    private void SetPad(int note, Color color)
    {
        if (pads == null) return;
        int column = note % 10 - 1;
        int row = note / 10 - 1;
        if (column is < 0 or > 7 || row is < 0 or > 7) return;
        pads[(7 - row) * 8 + column].color = color;
    }
}
