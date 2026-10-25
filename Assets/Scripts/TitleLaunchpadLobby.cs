using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Collects one or two Launchpad participants before Start can be pressed.</summary>
public sealed class TitleLaunchpadLobby : MonoBehaviour
{
    [SerializeField] private LaunchpadMidiInput launchpadInput;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string modeSelectSceneName = "ModeSelect";

    private LaunchpadDevice? playerA;
    private LaunchpadDevice? playerB;

    private void Awake()
    {
        if (launchpadInput == null)
            launchpadInput = GetComponent<LaunchpadMidiInput>() ?? gameObject.AddComponent<LaunchpadMidiInput>();
        if (startButton != null) startButton.interactable = false;
        SetStatus("Press any pad to join");
    }

    private void Update()
    {
        RemoveDisconnectedPlayers();
        while (launchpadInput.TryConsumeButtonPress(out LaunchpadDevice device))
            RegisterParticipant(device);
    }

    public void StartAfterPlayersAreReady()
    {
        RemoveDisconnectedPlayers();
        if (!playerA.HasValue) return;
        bool isMultiplayer = playerB.HasValue;
        GameSession.Instance.SelectMode(isMultiplayer ? GameMode.Multi : GameMode.Single);
        GameSession.Instance.SetMultiplayerDevices(playerA.Value.DeviceKey, playerB?.DeviceKey);
        SceneManager.LoadScene(modeSelectSceneName);
    }

    private void RemoveDisconnectedPlayers()
    {
        if ((playerA.HasValue && !launchpadInput.IsConnected(playerA.Value)) ||
            (playerB.HasValue && !launchpadInput.IsConnected(playerB.Value)))
        {
            playerA = playerB = null;
            launchpadInput.ClearAllLeds();
            if (startButton != null) startButton.interactable = false;
            SetStatus("Launchpad disconnected. Press a pad on each device to join again.");
        }
    }

    private void RegisterParticipant(LaunchpadDevice device)
    {
        if (playerA.HasValue && playerA.Value.DeviceKey == device.DeviceKey ||
            playerB.HasValue && playerB.Value.DeviceKey == device.DeviceKey) return;
        if (!playerA.HasValue)
        {
            playerA = device;
            launchpadInput.SetAllLeds(device, playerB: false);
            SetStatus("Player A joined. Press START for single, or join with another Launchpad.");
        }
        else if (!playerB.HasValue)
        {
            playerB = device;
            launchpadInput.SetAllLeds(device, playerB: true);
            SetStatus("Player A and Player B ready. Press START.");
        }
        else return;
        if (startButton != null) startButton.interactable = true;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
