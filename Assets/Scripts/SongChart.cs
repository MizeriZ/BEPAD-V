using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChartNoteType
{
    Tap,
    Hold
}

[Serializable]
public sealed class ChartNote
{
    [Min(0f)] public float beat;
    [Range(11, 88)] public int padNote = 11;
    public ChartNoteType type;
    [Min(0.25f)] public float holdBeats = 1f;
}

/// <summary>
/// Authoritative song data used by Freestyle now and by the in-game editor later.
/// Beat values are measured from the moment the song begins.
/// </summary>
[CreateAssetMenu(fileName = "New Song Chart", menuName = "BEPAD V/Song Chart")]
public sealed class SongChart : ScriptableObject
{
    public string songTitle = "Untitled";
    public AudioClip music;
    [Min(30f)] public float bpm = 120f;
    [Tooltip("Positive values move chart notes and LEDs later than the music; negative values move them earlier.")]
    public float chartOffsetSeconds;
    [Min(1)] public int totalBeats = 32;
    public List<ChartNote> notes = new();

    public float SecondsPerBeat => 60f / bpm;

    public void SortNotesByBeat()
    {
        notes.Sort((left, right) => left.beat.CompareTo(right.beat));
    }
}
