using UnityEngine;

public enum GameMode
{
    None,
    Single,
    Multi
}

/// <summary>
/// Keeps the current play session alive while scenes are changed.
/// UI and gameplay scenes should read/write this component instead of passing
/// values through scene-specific objects.
/// </summary>
public sealed class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.None;
    public string SinglePlayerNickname { get; private set; } = "Player";
    public string PlayerANickname { get; private set; } = "Player A";
    public string PlayerBNickname { get; private set; } = "Player B";
    public string SelectedSongId { get; private set; } = string.Empty;
    public bool HasCompletedTutorial { get; private set; }
    public int SinglePlayerScore { get; private set; }
    public int SinglePlayerBestCombo { get; private set; }
    public int SinglePlayerMaxScore { get; private set; }
    public int PerfectCount { get; private set; }
    public int GreatCount { get; private set; }
    public int GoodCount { get; private set; }
    public int CoolCount { get; private set; }
    public int MissCount { get; private set; }
    public int PlayerAScore { get; private set; }
    public int PlayerBScore { get; private set; }
    public string PlayerADeviceKey { get; private set; }
    public string PlayerBDeviceKey { get; private set; }

    public void SetMultiplayerDevices(string playerA, string playerB)
    {
        PlayerADeviceKey = playerA;
        PlayerBDeviceKey = playerB;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeFirstScene()
    {
        if (Instance != null)
            return;

        GameObject sessionObject = new GameObject(nameof(GameSession));
        sessionObject.AddComponent<GameSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectMode(GameMode mode)
    {
        Mode = mode;
        PlayerADeviceKey = PlayerBDeviceKey = null;
        HasCompletedTutorial = false;
        ResetRoundScores();
    }

    public void SetSinglePlayerNickname(string nickname)
    {
        SinglePlayerNickname = SanitizeNickname(nickname, "Player");
    }

    public void SetMultiplayerNicknames(string playerA, string playerB)
    {
        PlayerANickname = SanitizeNickname(playerA, "Player A");
        PlayerBNickname = SanitizeNickname(playerB, "Player B");
    }

    public void SelectSong(string songId)
    {
        SelectedSongId = songId?.Trim() ?? string.Empty;
        ResetRoundScores();
    }

    public void CompleteTutorial()
    {
        HasCompletedTutorial = true;
    }

    public void SetSinglePlayerScore(int score)
    {
        SinglePlayerScore = ClampScore(score);
    }

    public void SetSinglePlayerResult(int score, int bestCombo, int maxScore, int perfectCount, int greatCount, int goodCount, int coolCount, int missCount)
    {
        SinglePlayerScore = ClampScore(score);
        SinglePlayerBestCombo = Mathf.Max(0, bestCombo);
        SinglePlayerMaxScore = Mathf.Max(1, maxScore);
        PerfectCount = Mathf.Max(0, perfectCount);
        GreatCount = Mathf.Max(0, greatCount);
        GoodCount = Mathf.Max(0, goodCount);
        CoolCount = Mathf.Max(0, coolCount);
        MissCount = Mathf.Max(0, missCount);
    }

    public void SetMultiplayerScores(int playerA, int playerB)
    {
        PlayerAScore = ClampScore(playerA);
        PlayerBScore = ClampScore(playerB);
    }

    public void ResetRoundScores()
    {
        SinglePlayerScore = 0;
        SinglePlayerBestCombo = 0;
        SinglePlayerMaxScore = 0;
        PerfectCount = 0;
        GreatCount = 0;
        GoodCount = 0;
        CoolCount = 0;
        MissCount = 0;
        PlayerAScore = 0;
        PlayerBScore = 0;
    }

    public void ResetSession()
    {
        Mode = GameMode.None;
        PlayerADeviceKey = PlayerBDeviceKey = null;
        SelectedSongId = string.Empty;
        HasCompletedTutorial = false;
        ResetRoundScores();
    }

    private static int ClampScore(int score)
    {
        return Mathf.Clamp(score, 0, 1_000_000);
    }

    private static string SanitizeNickname(string nickname, string fallback)
    {
        string trimmed = nickname?.Trim();
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
    }
}
