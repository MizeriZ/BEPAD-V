using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Displays the saved single-player FREESTYLE result.</summary>
public sealed class ResultScreenController : MonoBehaviour
{
    [Header("Result Text")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestComboText;
    [SerializeField] private TMP_Text rankText;
    [Header("Judgment Counts")]
    [SerializeField] private TMP_Text perfectCountText;
    [SerializeField] private TMP_Text greatCountText;
    [SerializeField] private TMP_Text goodCountText;
    [SerializeField] private TMP_Text coolCountText;
    [SerializeField] private TMP_Text missCountText;
    [SerializeField] private string modeSelectSceneName = "ModeSelect";

    private void Start()
    {
        GameSession session = GameSession.Instance;
        if (playerNameText != null) playerNameText.text = session.SinglePlayerNickname;
        if (scoreText != null) scoreText.text = $"SCORE\n{session.SinglePlayerScore:N0}";
        if (bestComboText != null) bestComboText.text = $"BEST COMBO\n{session.SinglePlayerBestCombo}";
        if (rankText != null) rankText.text = GetRank(session.SinglePlayerScore, session.SinglePlayerMaxScore);
        SetCount(perfectCountText, "PERFECT", session.PerfectCount);
        SetCount(greatCountText, "GREAT", session.GreatCount);
        SetCount(goodCountText, "GOOD", session.GoodCount);
        SetCount(coolCountText, "COOL", session.CoolCount);
        SetCount(missCountText, "MISS", session.MissCount);
    }

    public void ReturnToModeSelect()
    {
        SceneManager.LoadScene(modeSelectSceneName);
    }

    private static string GetRank(int score, int maximumScore)
    {
        float rate = maximumScore <= 0 ? 0f : score / (float)maximumScore;
        if (rate >= 0.9f) return "S";
        if (rate >= 0.75f) return "A";
        if (rate >= 0.55f) return "B";
        if (rate >= 0.35f) return "C";
        return "D";
    }

    private static void SetCount(TMP_Text target, string label, int count)
    {
        if (target != null) target.text = $"{label}  {count}";
    }
}
