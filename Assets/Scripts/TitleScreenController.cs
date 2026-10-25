using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>Animates the title prompt and starts the game after any Launchpad pad press.</summary>
public sealed class TitleScreenController : MonoBehaviour
{
    [SerializeField] private TMP_Text startPrompt;
    [SerializeField, Min(0.1f)] private float blinkPeriod = 0.8f;
    [SerializeField] private string gameSceneName;
    [SerializeField] private UnityEvent onGameStart;

    private LaunchpadMidiInput launchpadInput;
    private bool hasStarted;
    private Color promptColor;

    private void Awake()
    {
        launchpadInput = GetComponent<LaunchpadMidiInput>();
        if (launchpadInput == null)
            launchpadInput = gameObject.AddComponent<LaunchpadMidiInput>();

        if (startPrompt == null)
            startPrompt = GetComponentInChildren<TMP_Text>(true);

        if (startPrompt != null)
            promptColor = startPrompt.color;
        else
            Debug.LogWarning("Assign the 'Press Launchpad Button To Start' TMP text to Start Prompt.");
    }

    private void Update()
    {
        AnimatePrompt();

        if (!hasStarted && launchpadInput.TryConsumeButtonPress(out _))
            StartGame();
    }

    private void AnimatePrompt()
    {
        if (startPrompt == null)
            return;

        float phase = Mathf.PingPong(Time.unscaledTime / blinkPeriod, 1f);
        Color color = promptColor;
        color.a = Mathf.Lerp(0.2f, 1f, phase);
        startPrompt.color = color;
    }

    public void StartGame()
    {
        if (hasStarted)
            return;

        hasStarted = true;
        onGameStart?.Invoke();

        if (!string.IsNullOrWhiteSpace(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
    }
}
