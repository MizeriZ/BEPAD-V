using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows the correct nickname fields for the mode stored in GameSession and
/// saves the entered names before moving to the configured next scene.
/// </summary>
public sealed class NicknameInputController : MonoBehaviour
{
    [Header("Single player")]
    [SerializeField] private GameObject singlePlayerPanel;
    [SerializeField] private TMP_InputField singlePlayerInput;

    [Header("Multiplayer")]
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private TMP_InputField playerAInput;
    [SerializeField] private TMP_InputField playerBInput;

    [Header("Flow")]
    [SerializeField] private GameObject modeSelectMenu;
    [SerializeField] private GameObject freestyleButton;
    [SerializeField] private GameObject multiplayButton;
    [SerializeField] private string nextSceneName;

    private void Start()
    {
        GameSession session = GameSession.Instance;
        bool isMultiplayer = session.Mode == GameMode.Multi;
        bool returningFromTutorial = session.HasCompletedTutorial;

        if (singlePlayerPanel != null)
            singlePlayerPanel.SetActive(!returningFromTutorial && !isMultiplayer);
        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(!returningFromTutorial && isMultiplayer);
        if (modeSelectMenu != null)
            modeSelectMenu.SetActive(returningFromTutorial);

        ApplyModeSpecificMenu(isMultiplayer);

        if (returningFromTutorial)
            return;

        if (isMultiplayer)
        {
            if (playerAInput != null)
            {
                playerAInput.text = session.PlayerANickname;
                playerAInput.Select();
            }
            if (playerBInput != null)
                playerBInput.text = session.PlayerBNickname;
        }
        else if (singlePlayerInput != null)
        {
            singlePlayerInput.text = session.SinglePlayerNickname;
            singlePlayerInput.Select();
        }
    }

    /// <summary>Connect this method to the Continue button's OnClick event.</summary>
    public void SaveAndContinue()
    {
        GameSession session = GameSession.Instance;

        if (session.Mode == GameMode.Multi)
        {
            session.SetMultiplayerNicknames(
                playerAInput != null ? playerAInput.text : null,
                playerBInput != null ? playerBInput.text : null);
        }
        else
        {
            session.SetSinglePlayerNickname(singlePlayerInput != null ? singlePlayerInput.text : null);
        }

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        if (singlePlayerPanel != null)
            singlePlayerPanel.SetActive(false);
        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(false);
        if (modeSelectMenu != null)
            modeSelectMenu.SetActive(true);
    }

    /// <summary>Connects the ModeSelect TUTORIAL button to the Tutorial scene.</summary>
    public void LoadTutorial()
    {
        SceneManager.LoadScene("Tutorial");
    }

    /// <summary>Connects the ModeSelect FREESTYLE button to the FREESTYLE scene.</summary>
    public void LoadFreestyle()
    {
        GameSession.Instance.ResetRoundScores();
        SceneManager.LoadScene("FREESTYLE");
    }

    public void LoadMultiplay()
    {
        GameSession.Instance.ResetRoundScores();
        SceneManager.LoadScene("MultiPlay");
    }

    private void ApplyModeSpecificMenu(bool isMultiplayer)
    {
        if (freestyleButton != null)
            freestyleButton.SetActive(!isMultiplayer);
        if (multiplayButton != null)
            multiplayButton.SetActive(isMultiplayer);
    }
}
