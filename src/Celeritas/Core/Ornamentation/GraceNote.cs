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
public sealed class GraceNote : Ornament
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

    private readonly Rational? _durationRatio;

    /// <summary>
    /// Duration ratio of grace notes to main note.
    /// For appoggiatura: typically 1/2 or 1/3 of main note (default 1/32).
    /// For acciaccatura the default is an absolute 1/32 whole note per grace note;
    /// explicitly setting this property overrides that with a ratio of the main note.
    /// </summary>
    public Rational DurationRatio
    {
        get => _durationRatio ?? new Rational(1, 32);
        init => _durationRatio = value;
    }

    /// <summary>
    /// The ratio exactly as it was set, or <see langword="null"/> when the acciaccatura default
    /// — an absolute 1/32 whole note per grace note — still applies.
    /// </summary>
    /// <remarks>
    /// Copy this, not <see cref="DurationRatio"/>, when cloning: that getter substitutes 1/32
    /// for an unset ratio, and assigning the substitute turns the absolute default into a ratio
    /// of the main note. Re-basing a default acciaccatura onto a quarter note therefore gave it
    /// 1/4 * 1/32 = 1/128 — a grace note four times shorter than the one it was asked for, and
    /// shorter again on a longer note.
    /// </remarks>
    internal Rational? ExplicitDurationRatio
    {
        get => _durationRatio;
        init => _durationRatio = value;
    }

    /// <summary>Expands into the grace note(s) followed by the shortened main note.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Type"/> is not a defined <see cref="GraceNoteType"/> value.</exception>
    public override NoteEvent[] Expand()
    {
        if (!Enum.IsDefined(Type))
            throw new ArgumentOutOfRangeException(nameof(Type), Type, "Not a defined GraceNoteType value.");

        if (Intervals.Length == 0)
            return [BaseNote];

        Rational totalGraceDuration;
        var graceCount = Intervals.Length;

        // Calculate duration for each grace note
        if (Type == GraceNoteType.Acciaccatura && _durationRatio is null)
        {
            totalGraceDuration = new Rational(graceCount, 32); // 32nd note per grace note
        }
        else // Explicit ratio, Appoggiatura or Multiple
        {
            totalGraceDuration = BaseNote.Duration * DurationRatio;
        }

        // Grace notes never take more than half the base note, so the expansion
        // always fits inside the original duration (no overlap with the next note).
        var halfBase = BaseNote.Duration / 2;
        if (totalGraceDuration > halfBase)
            totalGraceDuration = halfBase;

        var graceDuration = totalGraceDuration / graceCount;
        var mainDuration = BaseNote.Duration - totalGraceDuration;

        var notes = new NoteEvent[graceCount + 1];
        var currentOffset = BaseNote.Offset;

        // Add grace notes
        for (int i = 0; i < graceCount; i++)
        {
            var gracePitch = Playable(BaseNote.Pitch + Intervals[i]);
            notes[i] = new NoteEvent(gracePitch, currentOffset, graceDuration, BaseNote.Velocity * 0.8f);
            currentOffset += graceDuration;
        }

        // Add main note
        notes[graceCount] = new NoteEvent(BaseNote.Pitch, currentOffset, mainDuration, BaseNote.Velocity);

        return notes;
    }
}
