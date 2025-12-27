// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Type of grace note.
/// </summary>
public enum GraceNoteType
{
    /// <summary>Acciaccatura (slashed grace note, very short).</summary>
    Acciaccatura,

    /// <summary>Appoggiatura (unslashed grace note, takes time from main note).</summary>
    Appoggiatura,

    /// <summary>Multiple grace notes before the main note.</summary>
    Multiple
}

/// <summary>
/// Grace note ornament - decorative note(s) before the main note.
/// </summary>
public class GraceNote : Ornament
{
    /// <summary>
    /// Type of grace note.
    /// </summary>
    public GraceNoteType Type { get; init; } = GraceNoteType.Acciaccatura;

    /// <summary>
    /// Pitches of the grace note(s) relative to the base note.
    /// For single grace note: array of length 1.
    /// For multiple grace notes: array of 2+ elements.
    /// </summary>
    public int[] Intervals { get; init; } = [2];

    /// <summary>
    /// Duration ratio of grace notes to main note.
    /// For acciaccatura: very small (1/32 or shorter).
    /// For appoggiatura: typically 1/2 or 1/3 of main note.
    /// </summary>
    public Rational DurationRatio { get; init; } = new(1, 32);

    public override NoteEvent[] Expand()
    {
        var totalGraceDuration = Rational.Zero;
        var graceCount = Intervals.Length;

        // Calculate duration for each grace note
        Rational graceDuration;
        if (Type == GraceNoteType.Acciaccatura)
        {
            graceDuration = new Rational(1, 32); // 32nd note per grace note
            totalGraceDuration = graceDuration * graceCount;
        }
        else // Appoggiatura or Multiple
        {
            totalGraceDuration = BaseNote.Duration * DurationRatio.Numerator / DurationRatio.Denominator;
            graceDuration = totalGraceDuration / graceCount;
        }

        var mainDuration = BaseNote.Duration - totalGraceDuration;
        if (mainDuration.Numerator <= 0)
            mainDuration = new Rational(1, 16); // Minimum main note duration

        var notes = new NoteEvent[graceCount + 1];
        var currentOffset = BaseNote.Offset;

        // Add grace notes
        for (int i = 0; i < graceCount; i++)
        {
            var gracePitch = BaseNote.Pitch + Intervals[i];
            notes[i] = new NoteEvent(gracePitch, currentOffset, graceDuration, BaseNote.Velocity * 0.8f);
            currentOffset += graceDuration;
        }

        // Add main note
        notes[graceCount] = new NoteEvent(BaseNote.Pitch, currentOffset, mainDuration, BaseNote.Velocity);

        return notes;
    }
}
