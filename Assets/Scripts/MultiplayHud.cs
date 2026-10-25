using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates Inspector-assigned UI; only the assigned pad panels receive generated pads.</summary>
[DisallowMultipleComponent]
public sealed class MultiplayHud : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerUi
    {
        [Tooltip("Assign the MultiplayPadBoard component attached to this player's pad panel.")]
        public MultiplayPadBoard padBoard;
        public TMP_Text nicknameText;
        public TMP_Text scoreText;
        public TMP_Text comboText;
        public TMP_Text deviceStatusText;
        public TMP_Text judgmentText;
    }

    [SerializeField] private PlayerUi playerA = new PlayerUi();
    [SerializeField] private PlayerUi playerB = new PlayerUi();
    [SerializeField] private TMP_Text scoreDifferenceText;
    [SerializeField] private Slider leadSlider;
    [Tooltip("Assign LeadSlider's Fill Image and optionally its Handle Image.")]
    [SerializeField] private Image leadFill;
    [SerializeField] private Image leadHandle;
    [SerializeField, Min(1)] private int leadFullScalePoints = 10000;
    [SerializeField] private Color playerAColor = new Color32(0, 128, 255, 255);
    [SerializeField] private Color playerBColor = new Color32(255, 48, 64, 255);
    [Header("Panels (assign your existing panels)")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private GameObject resultPanel;
    [Header("Optional text (auto-detected inside panels if empty)")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text resultText;

    private PlayerUi Player(int index) => index == 0 ? playerA : playerB;

    private void Awake()
    {
        ResolvePanelText();
        ShowCountdown(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private void ResolvePanelText()
    {
        if (instructionText == null && countdownPanel != null)
            instructionText = countdownPanel.GetComponentInChildren<TMP_Text>(true);
        if (resultText == null && resultPanel != null)
            resultText = resultPanel.GetComponentInChildren<TMP_Text>(true);
    }

    public void ShowCountdown(bool visible)
    {
        if (countdownPanel != null) countdownPanel.SetActive(visible);
        if (visible && resultPanel != null) resultPanel.SetActive(false);
    }

    private void Start()
    {
        playerA.padBoard?.Initialize();
        playerB.padBoard?.Initialize();
    }

    public void SetPlayer(int index, string nickname, int score, int combo)
    {
        PlayerUi ui = Player(index);
        SetText(ui.nicknameText, nickname);
        SetText(ui.scoreText, $"{score:N0} / 1,000,000");
        if (ui.comboText != null)
        {
            ui.comboText.color = new Color32(0x00, 0xFF, 0x75, 0xFF);
            ui.comboText.alignment = TextAlignmentOptions.Center;
            SetText(ui.comboText, $"COMBO\n{combo}");
        }
    }

    public void SetConnection(int index, bool assigned, bool connected, bool ready)
    {
        TMP_Text text = Player(index).deviceStatusText;
        if (text == null) return;
        text.color = assigned && !connected ? Color.red : Color.white;
        SetText(text, !assigned ? "WAITING" : !connected ? "DISCONNECTED" : ready ? "CONNECTED / READY" : "CONNECTED");
    }

    public void SetScores(int scoreA, int scoreB)
    {
        int difference = scoreA - scoreB;
        Color color = difference > 0 ? playerAColor : difference < 0 ? playerBColor : Color.white;
        SetText(scoreDifferenceText, difference == 0 ? "0" : $"+{Math.Abs(difference):N0}");
        if (scoreDifferenceText != null) scoreDifferenceText.color = color;
        if (leadSlider != null)
        {
            leadSlider.transition = Selectable.Transition.None;
            leadSlider.interactable = false;
            leadSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            leadSlider.minValue = 0;
            leadSlider.maxValue = 1;
            leadSlider.wholeNumbers = false;
            // Set direction in Inspector to Left To Right. A moves left, B moves right.
            leadSlider.SetValueWithoutNotify(0.5f - 0.5f * Mathf.Clamp((float)difference / Mathf.Max(1, leadFullScalePoints), -1, 1));
        }
        if (leadFill != null) leadFill.color = color;
        if (leadHandle != null) leadHandle.color = color;
    }

    public void SetPad(int index, int note, Color color) => Player(index).padBoard?.SetPad(note, color);
    public void ClearBoard(int index) => Player(index).padBoard?.Clear();
    public void SetJudgment(int index, string text) => SetText(Player(index).judgmentText, text);
    public void SetInstruction(string text)
    {
        ResolvePanelText();
        SetText(instructionText, text);
    }
    public void SetResult(string text)
    {
        ResolvePanelText();
        SetText(resultText, text);
        if (resultText != null) resultText.color = Color.white;
        ShowCountdown(false);
        if (resultPanel != null) resultPanel.SetActive(!string.IsNullOrEmpty(text));
    }

    public void SetMatchResult(string text, int scoreA, int scoreB)
    {
        SetResult(text);
        if (resultText != null)
            resultText.color = scoreA > scoreB ? playerAColor : scoreB > scoreA ? playerBColor : Color.white;
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target == null) return;
        target.richText = false;
        target.text = text;
    }
}
