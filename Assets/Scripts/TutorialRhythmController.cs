using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public enum TutorialNoteType { Tap, Hold }

[Serializable]
public sealed class TutorialStep
{
    [TextArea] public string instruction = "Press the highlighted pad";
    [Range(0, 127)] public int padNote = 11;
    [Min(0.1f)] public float previewTime = 0.75f;
    [Min(0.1f)] public float hitWindow = 0.5f;
    [Range(0.05f, 0.4f)] public float perfectWindow = 0.15f;
    [Min(0f)] public float pauseAfterSuccess = 0.9f;
    [Min(0.1f)] public float textDisplayTime = 2f;
    [Min(0f)] public float targetBeat = 4f;
    public TutorialNoteType noteType = TutorialNoteType.Tap;
    [Min(0.1f)] public float holdDuration = 1.2f;
}

/// <summary>Small reusable rhythm-game loop: preview, active cue, judgement, then next note.</summary>
public sealed class TutorialRhythmController : MonoBehaviour
{
    private const byte ActiveNoteBlue = 45;
    private const byte ActiveNoteRed = 5;
    private const byte SuccessGreen = 19;
    private const byte MissRed = 5;

    [SerializeField] private LaunchpadMidiInput launchpadInput;
    [SerializeField] private TutorialMusicConductor musicConductor;
    [SerializeField] private CanvasGroup tutorialCanvasGroup;
    [SerializeField] private TutorialPadVisualizer padVisualizer;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text timingText;
    [SerializeField] private TMP_Text playerATimingText;
    [SerializeField] private TMP_Text playerBTimingText;
    [SerializeField] private TutorialStep[] steps;
    [SerializeField, Min(0.1f)] private float retryPause = 0.55f;
    [SerializeField] private string completedSceneName;
    [SerializeField, Min(0.1f)] private float completionFadeDuration = 2f;

    private readonly Dictionary<string, LaunchpadDevice> tutorialDevices = new();
    private readonly List<string> playerOrder = new();
    private readonly HashSet<string> successfulPlayers = new();
    private readonly HashSet<string> holdingPlayers = new();
    private int requiredPlayerCount;
    private int currentStep;
    private float phaseStartedAt;
    private Phase phase = Phase.WaitingForPlayer;
    private bool lastJudgementWasSuccess;
    private bool isCompleting;

    private enum Phase { WaitingForPlayer, MusicWaiting, TextOnly, Preview, Active, Holding, ReleasePreview, ReleaseActive, Feedback, Complete }

    private void Awake()
    {
        if (launchpadInput == null)
            launchpadInput = GetComponent<LaunchpadMidiInput>() ?? gameObject.AddComponent<LaunchpadMidiInput>();
        if (padVisualizer == null)
            padVisualizer = GetComponent<TutorialPadVisualizer>();
        if (musicConductor == null)
            musicConductor = GetComponent<TutorialMusicConductor>();
        requiredPlayerCount = GameSession.Instance.Mode == GameMode.Multi ? 2 : 1;
        if (timingText != null) timingText.gameObject.SetActive(requiredPlayerCount == 1);
        if (playerATimingText != null) playerATimingText.gameObject.SetActive(requiredPlayerCount == 2);
        if (playerBTimingText != null) playerBTimingText.gameObject.SetActive(requiredPlayerCount == 2);
        SetInstruction(requiredPlayerCount == 1 ? "Press any Launchpad pad to begin tutorial" : "Each Launchpad: press any pad to join tutorial");
        SetProgress(string.Empty);
        SetAllTimings(string.Empty);
    }

    private void Update()
    {
        ConsumePresses();
        AdvancePhase();
    }

    private void ConsumePresses()
    {
        while (launchpadInput.TryConsumePadPress(out LaunchpadPadPress press))
        {
            if (phase == Phase.WaitingForPlayer)
            {
                if (press.IsPressed) RegisterTutorialDevice(press.Device);
                continue;
            }

            if (!tutorialDevices.ContainsKey(press.Device.DeviceKey))
                continue;

            // A device may have been unplugged and reconnected, which gives it a
            // new MIDI handle/device id while keeping the same physical key.
            tutorialDevices[press.Device.DeviceKey] = press.Device;

            TutorialStep step = steps[currentStep];
            if (!press.IsPressed)
            {
                HandleRelease(press, step);
                continue;
            }

            if (phase == Phase.Preview)
            {
                if (!press.IsControlChange && press.Note == step.padNote)
                    ShowFeedback(success: false, "TOO EARLY", press.Device);
                continue;
            }

            if (phase != Phase.Active)
                continue;

            if (!press.IsControlChange && press.Note == step.padNote)
            {
                float elapsed = Time.unscaledTime - phaseStartedAt;
                if (elapsed > step.hitWindow * 0.7f)
                {
                    ShowFeedback(success: false, "TOO LATE", press.Device);
                    continue;
                }

                if (step.noteType == TutorialNoteType.Hold)
                {
                    holdingPlayers.Add(press.Device.DeviceKey);
                    SetTimingForDevice(press.Device, "HOLD");
                    if (holdingPlayers.Count >= requiredPlayerCount)
                    {
                        phase = Phase.Holding;
                        phaseStartedAt = Time.unscaledTime;
                        SetInstruction("HOLD THE RED PAD");
                    }
                    continue;
                }

                successfulPlayers.Add(press.Device.DeviceKey);
                launchpadInput.SetPadLed(press.Device, step.padNote, SuccessGreen);
                padVisualizer?.ShowPlayerFeedback(press.Device, step.padNote, wasSuccess: true);
                string judgement = elapsed <= step.perfectWindow ? "PERFECT!" : "GOOD!";
                SetTimingForDevice(press.Device, judgement);
                if (successfulPlayers.Count >= requiredPlayerCount)
                    ShowFeedback(success: true, judgement, press.Device);
            }
            else
            {
                launchpadInput.SetPadLed(press.Device, press.Note, MissRed);
                padVisualizer?.ShowPlayerFeedback(press.Device, press.Note, wasSuccess: false);
                ShowFeedback(success: false, "WRONG PAD", press.Device);
            }
        }
    }

    private void HandleRelease(LaunchpadPadPress press, TutorialStep step)
    {
        if (step.noteType != TutorialNoteType.Hold || press.IsControlChange || press.Note != step.padNote)
            return;

        if (!holdingPlayers.Contains(press.Device.DeviceKey) ||
            phase is not (Phase.Active or Phase.Holding or Phase.ReleasePreview or Phase.ReleaseActive))
            return;

        successfulPlayers.Add(press.Device.DeviceKey);
        launchpadInput.SetPadLed(press.Device, step.padNote, SuccessGreen);
        padVisualizer?.ShowPlayerFeedback(press.Device, step.padNote, wasSuccess: true);
        SetTimingForDevice(press.Device, "GOOD");
        if (successfulPlayers.Count >= requiredPlayerCount)
            ShowFeedback(success: true, "GOOD", press.Device);
    }

    private void AdvancePhase()
    {
        if (steps == null || steps.Length == 0 || phase == Phase.WaitingForPlayer || phase == Phase.Complete)
            return;

        TutorialStep step = steps[currentStep];
        float elapsed = Time.unscaledTime - phaseStartedAt;
        if (phase == Phase.MusicWaiting)
        {
            if (musicConductor.CurrentBeat >= step.targetBeat - step.previewTime / musicConductor.SecondsPerBeat)
                BeginStep();
        }
        else if (phase == Phase.TextOnly && elapsed >= step.textDisplayTime)
        {
            currentStep++;
            if (currentStep >= steps.Length) CompleteTutorial(); else BeginStep();
        }
        else if (phase == Phase.Preview && elapsed >= step.previewTime)
        {
            phase = Phase.Active;
            phaseStartedAt = Time.unscaledTime;
            SetTargetLedOnAllDevices(step.padNote, CueColour(step));
            padVisualizer?.ShowActive(step.padNote, step.noteType == TutorialNoteType.Hold);
            SetInstruction("NOW! " + step.instruction);
        }
        else if (phase == Phase.Active && elapsed >= step.hitWindow)
        {
            ShowFeedback(success: false, "TOO LATE");
        }
        else if (phase == Phase.Holding && elapsed >= step.holdDuration)
        {
            phase = Phase.ReleasePreview;
            phaseStartedAt = Time.unscaledTime;
            successfulPlayers.Clear();
            SetTargetLedOnAllDevices(step.padNote, ActiveNoteRed, pulsing: true);
            padVisualizer?.ShowPreview(step.padNote, isLongNote: true);
            SetInstruction("GET READY TO RELEASE");
        }
        else if (phase == Phase.ReleasePreview && elapsed >= step.previewTime)
        {
            phase = Phase.ReleaseActive;
            phaseStartedAt = Time.unscaledTime;
            SetTargetLedOnAllDevices(step.padNote, ActiveNoteRed);
            padVisualizer?.ShowActive(step.padNote, isLongNote: true);
            SetInstruction("RELEASE NOW!");
        }
        else if (phase == Phase.ReleaseActive && elapsed >= step.hitWindow)
        {
            ShowFeedback(success: true, "GOOD");
        }
        else if (phase == Phase.Feedback && elapsed >= (lastJudgementWasSuccess ? step.pauseAfterSuccess : retryPause))
        {
            if (musicConductor != null && musicConductor.IsRunning)
            {
                SetTargetLedOnAllDevices(step.padNote, 0);
                padVisualizer?.ClearPad(step.padNote);
                currentStep++;
                if (currentStep >= steps.Length) CompleteTutorial(); else phase = Phase.MusicWaiting;
                return;
            }

            if (!lastJudgementWasSuccess)
                BeginStep();
            else
            {
                SetTargetLedOnAllDevices(step.padNote, 0);
                padVisualizer?.ClearPad(step.padNote);
                currentStep++;
                if (currentStep >= steps.Length) CompleteTutorial(); else BeginStep();
            }
        }
    }

    private void BeginStep()
    {
        if (steps == null || steps.Length == 0) { CompleteTutorial(); return; }
        TutorialStep step = steps[currentStep];
        if (step.padNote == 0)
        {
            phase = Phase.TextOnly;
            phaseStartedAt = Time.unscaledTime;
            SetInstruction(step.instruction);
            SetAllTimings(string.Empty);
            SetProgress($"READ THIS  {currentStep + 1} / {steps.Length}");
            return;
        }

        phase = Phase.Preview;
        phaseStartedAt = Time.unscaledTime;
        successfulPlayers.Clear();
        holdingPlayers.Clear();
        SetTargetLedOnAllDevices(step.padNote, CueColour(step), pulsing: true);
        padVisualizer?.ShowPreview(step.padNote, step.noteType == TutorialNoteType.Hold);
        SetInstruction(step.instruction);
        SetAllTimings(string.Empty);
        SetProgress($"FOLLOW THE PATTERN  {currentStep + 1} / {steps.Length}");
    }

    private void ShowFeedback(bool success, string judgement, LaunchpadDevice? device = null)
    {
        TutorialStep step = steps[currentStep];
        phase = Phase.Feedback;
        lastJudgementWasSuccess = success;
        phaseStartedAt = Time.unscaledTime;
        SetTargetLedOnAllDevices(step.padNote, success ? SuccessGreen : MissRed);
        padVisualizer?.ShowFeedback(step.padNote, success);
        if (device.HasValue) SetTimingForDevice(device.Value, judgement); else SetAllTimings(judgement);
        SetInstruction((success ? judgement + " " : judgement + " - TRY AGAIN: ") + step.instruction);
    }

    private void CompleteTutorial()
    {
        if (isCompleting) return;
        isCompleting = true;
        phase = Phase.Complete;
        launchpadInput.ClearAllLeds();
        padVisualizer?.Clear();
        GameSession.Instance.CompleteTutorial();
        SetInstruction("TUTORIAL COMPLETE");
        SetProgress(string.Empty);
        SetAllTimings(string.Empty);
        StartCoroutine(CompleteRoutine());
    }

    private IEnumerator CompleteRoutine()
    {
        if (tutorialCanvasGroup != null) tutorialCanvasGroup.alpha = 1f;
        if (musicConductor != null) musicConductor.FadeOutAndStop(completionFadeDuration);

        float elapsed = 0f;
        while (elapsed < completionFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (tutorialCanvasGroup != null)
                tutorialCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / completionFadeDuration);
            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(completedSceneName)) SceneManager.LoadScene(completedSceneName);
    }

    private void SetInstruction(string text) { if (instructionText != null) instructionText.text = text; }
    private void SetProgress(string text) { if (progressText != null) progressText.text = text; }
    private void SetAllTimings(string text)
    {
        if (requiredPlayerCount == 1)
        {
            if (timingText != null) timingText.text = text;
            return;
        }

        if (playerATimingText != null) playerATimingText.text = text;
        if (playerBTimingText != null) playerBTimingText.text = text;
    }

    private void SetTimingForDevice(LaunchpadDevice device, string text)
    {
        if (requiredPlayerCount == 1)
        {
            if (timingText != null) timingText.text = text;
            return;
        }

        int playerIndex = playerOrder.IndexOf(device.DeviceKey);
        if (device.DeviceKey == GameSession.Instance.PlayerADeviceKey) playerIndex = 0;
        else if (device.DeviceKey == GameSession.Instance.PlayerBDeviceKey) playerIndex = 1;
        if (playerIndex == 0 && playerATimingText != null) playerATimingText.text = text;
        if (playerIndex == 1 && playerBTimingText != null) playerBTimingText.text = text;
    }

    private void RegisterTutorialDevice(LaunchpadDevice device)
    {
        if (tutorialDevices.TryAdd(device.DeviceKey, device))
        {
            playerOrder.Add(device.DeviceKey);
            padVisualizer?.RegisterPlayer(device);
        }
        int remaining = requiredPlayerCount - tutorialDevices.Count;
        if (remaining <= 0)
        {
            if (musicConductor != null)
            {
                musicConductor.StartMusic();
                phase = Phase.MusicWaiting;
                SetInstruction("LISTEN TO THE BEAT...");
            }
            else BeginStep();
            return;
        }

        SetInstruction($"{remaining} more Launchpad player{(remaining == 1 ? string.Empty : "s")} needed");
    }

    private void SetTargetLedOnAllDevices(int note, byte colour, bool pulsing = false)
    {
        foreach (LaunchpadDevice device in tutorialDevices.Values)
            launchpadInput.SetPadLed(device, note, colour, pulsing);
    }

    private static byte CueColour(TutorialStep step) => step.noteType == TutorialNoteType.Hold ? ActiveNoteRed : ActiveNoteBlue;
}
