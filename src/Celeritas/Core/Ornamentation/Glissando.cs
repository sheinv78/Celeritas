// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Glissando - continuous pitch slide between two notes.
/// </summary>
public sealed class Glissando : Ornament
{
    /// <summary>
    /// Target pitch (absolute MIDI pitch or interval from base note).
    /// </summary>
    public int TargetPitch { get; init; }

    /// <summary>
    /// Whether TargetPitch is absolute or relative to base note.
    /// </summary>
    public bool IsAbsolute { get; init; } = false;

    /// <summary>
    /// Number of intermediate steps in the glissando (chromatic mode only).
    /// Higher values = smoother glide.
    /// </summary>
    public int Steps { get; init; } = 8;

    /// <summary>
    /// Whether to use chromatic (semitone) or diatonic (natural/white-key) steps.
    /// When <see langword="false"/>, the glissando touches every natural (white-key)
    /// pitch between the base note and the target; both endpoints are always included
    /// even if they are not natural, and <see cref="Steps"/> is ignored.
    /// </summary>
    public bool Chromatic { get; init; } = true;

    /// <summary>Expands into a series of stepped notes sliding from the base pitch to the target.</summary>
    public override NoteEvent[] Expand()
    {
        var targetPitch = IsAbsolute ? TargetPitch : BaseNote.Pitch + TargetPitch;
        var pitchDifference = targetPitch - BaseNote.Pitch;

        if (pitchDifference == 0)
        {
            // No glissando needed
            return [BaseNote];
        }

        if (!Chromatic)
        {
            return ExpandDiatonic(targetPitch);
        }

        if (Steps <= 0)
        {
            return [BaseNote];
        }

        var stepCount = Math.Min(Steps, Math.Abs(pitchDifference));
        var notes = new NoteEvent[stepCount + 1];
        var stepDuration = BaseNote.Duration / (stepCount + 1);

        var currentOffset = BaseNote.Offset;
        var pitchStep = pitchDifference / (double)stepCount;

        // Create intermediate steps
        for (int i = 0; i <= stepCount; i++)
        {
            var currentPitch = Playable(BaseNote.Pitch + (int)Math.Round(pitchStep * i));
            notes[i] = new NoteEvent(currentPitch, currentOffset, stepDuration, BaseNote.Velocity);
            currentOffset += stepDuration;
        }

        return notes;
    }

    /// <summary>
    /// Diatonic glissando: the base pitch, every natural (white-key) pitch strictly
    /// between base and target, and the target pitch, sharing the base duration equally.
    /// </summary>
    private NoteEvent[] ExpandDiatonic(int targetPitch)
    {
        // Put both ends on the keyboard before walking between them. The chromatic path above
        // holds every step with Playable and this one did not, so a diatonic glissando aimed
        // past either end of the keyboard emitted pitches like -28 — which nothing downstream
        // can write down, play, or name.
        var start = Playable(BaseNote.Pitch);
        var end = Playable(targetPitch);
        var direction = Math.Sign(end - start);

        if (direction == 0)
        {
            return [new NoteEvent(start, BaseNote.Offset, BaseNote.Duration, BaseNote.Velocity)];
        }

        var pitches = new List<int> { start };
        for (var p = start + direction; p != end; p += direction)
        {
            if (IsNatural(p))
            {
                pitches.Add(p);
            }
        }

        pitches.Add(end);

        var stepDuration = BaseNote.Duration / pitches.Count;
        var notes = new NoteEvent[pitches.Count];
        var currentOffset = BaseNote.Offset;

        for (var i = 0; i < pitches.Count; i++)
        {
            notes[i] = new NoteEvent(pitches[i], currentOffset, stepDuration, BaseNote.Velocity);
            currentOffset += stepDuration;
        }

        return notes;
    }

    // Bit set for the natural pitch classes C D E F G A B (0, 2, 4, 5, 7, 9, 11).
    private static bool IsNatural(int pitch) => ((1 << PitchMath.Fold(pitch)) & 0b1010_1011_0101) != 0;
}
