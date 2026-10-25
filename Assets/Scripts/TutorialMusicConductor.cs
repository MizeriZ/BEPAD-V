using UnityEngine;

/// <summary>Provides a shared BPM clock for music-synchronised tutorial steps.</summary>
public sealed class TutorialMusicConductor : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource;
    [SerializeField, Min(30f)] private float bpm = 120f;

    private double startedAt;
    private Coroutine fadeCoroutine;
    public bool IsRunning { get; private set; }
    public float SecondsPerBeat => 60f / bpm;
    public float CurrentBeat => IsRunning ? Mathf.Max(0f, (float)((AudioSettings.dspTime - startedAt) / SecondsPerBeat)) : 0f;

    public void StartMusic()
    {
        if (IsRunning) return;
        startedAt = AudioSettings.dspTime;
        IsRunning = true;
        if (musicSource != null && musicSource.clip != null)
            musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
        IsRunning = false;
    }

    public void FadeOutAndStop(float duration)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private System.Collections.IEnumerator FadeOutRoutine(float duration)
    {
        if (musicSource == null || !musicSource.isPlaying) yield break;
        float startingVolume = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startingVolume, 0f, elapsed / duration);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = startingVolume;
        IsRunning = false;
        fadeCoroutine = null;
    }
}
