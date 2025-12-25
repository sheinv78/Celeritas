// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Orchestration;

/// <summary>
/// Maps engine-native notes to simple orchestrated parts and constrains pitches to ranges.
/// </summary>
public static class OrchestrationMapper
{
    public static OrchestrationResult Map(NoteEvent[] notes, OrchestrationOptions? options = null)
    {
        var opt = options ?? OrchestrationOptions.Default;
        if (notes.Length == 0)
        {
            return new OrchestrationResult
            {
                Bass = new OrchestratedPart { Definition = opt.Bass, Notes = [] },
                Harmony = new OrchestratedPart { Definition = opt.Harmony, Notes = [] }
            };
        }

        var bass = new List<NoteEvent>(notes.Length / 2);
        var harmony = new List<NoteEvent>(notes.Length);

        for (var i = 0; i < notes.Length; i++)
        {
            var n = notes[i];
            var isBass = n.Pitch < opt.SplitPitch;
            if (isBass)
            {
                bass.Add(ClampToRange(n, opt.Bass.Range));
            }
            else
            {
                harmony.Add(ClampToRange(n, opt.Harmony.Range));
            }
        }

        return new OrchestrationResult
        {
            Bass = new OrchestratedPart { Definition = opt.Bass, Notes = bass.ToArray() },
            Harmony = new OrchestratedPart { Definition = opt.Harmony, Notes = harmony.ToArray() }
        };
    }

    private static NoteEvent ClampToRange(NoteEvent note, InstrumentRange range)
    {
        var pitch = note.Pitch;

        // Shift by octaves while preserving pitch class.
        while (pitch < range.MinPitch)
            pitch += 12;
        while (pitch > range.MaxPitch)
            pitch -= 12;

        // If still out of range (extremely narrow ranges), clamp.
        if (pitch < range.MinPitch)
            pitch = range.MinPitch;
        if (pitch > range.MaxPitch)
            pitch = range.MaxPitch;

        if (pitch == note.Pitch)
            return note;

        return new NoteEvent(pitch, note.Offset, note.Duration, note.Velocity);
    }
}
