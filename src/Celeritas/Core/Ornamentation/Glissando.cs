// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Glissando - continuous pitch slide between two notes.
/// </summary>
public class Glissando : Ornament
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
    /// Number of intermediate steps in the glissando.
    /// Higher values = smoother glide.
    /// </summary>
    public int Steps { get; init; } = 8;

    /// <summary>
    /// Whether to use chromatic (semitone) or diatonic (scale) steps.
    /// </summary>
    public bool Chromatic { get; init; } = true;

    public override NoteEvent[] Expand()
    {
        var targetPitch = IsAbsolute ? TargetPitch : BaseNote.Pitch + TargetPitch;
        var pitchDifference = targetPitch - BaseNote.Pitch;

        if (pitchDifference == 0 || Steps <= 0)
        {
            // No glissando needed
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
            var currentPitch = BaseNote.Pitch + (int)Math.Round(pitchStep * i);
            notes[i] = new NoteEvent(currentPitch, currentOffset, stepDuration, BaseNote.Velocity);
            currentOffset += stepDuration;
        }

        return notes;
    }
}
