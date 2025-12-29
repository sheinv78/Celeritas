// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

public static class NoteArithmeticExtensions
{
    public static PitchClass PitchClass(this int midiPitch) => Celeritas.Core.PitchClass.FromMidi(midiPitch);

    public static SpnNote ToSpnNote(this int midiPitch) => SpnNote.FromMidi(midiPitch);

    public static SpnNote ToSpnNote(this string notation) => SpnNote.Parse(notation);

    public static ChromaticInterval IntervalTo(this int fromMidiPitch, int toMidiPitch) => new(toMidiPitch - fromMidiPitch);

    public static int Transpose(this int midiPitch, ChromaticInterval interval)
    {
        if ((uint)midiPitch > 127u)
        {
            throw new ArgumentOutOfRangeException(nameof(midiPitch), "MIDI pitch must be 0-127");
        }

        var result = midiPitch + interval.Semitones;
        if ((uint)result > 127u)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Resulting MIDI pitch is out of range 0-127");
        }

        return result;
    }
}
