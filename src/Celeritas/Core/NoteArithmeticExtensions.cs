// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

public static class NoteArithmeticExtensions
{
    extension(int midiPitch)
    {
        public PitchClass PitchClass() => Core.PitchClass.FromMidi(midiPitch);
        public SpnNote ToSpnNote() => SpnNote.FromMidi(midiPitch);
    }

    public static SpnNote ToSpnNote(this string notation) => SpnNote.Parse(notation);

    extension(int fromMidiPitch)
    {
        public ChromaticInterval IntervalTo(int toMidiPitch) => new(toMidiPitch - fromMidiPitch);

        public int Transpose(ChromaticInterval interval)
        {
            if ((uint)fromMidiPitch > 127u)
            {
                throw new ArgumentOutOfRangeException(nameof(fromMidiPitch), "MIDI pitch must be 0-127");
            }

            var result = fromMidiPitch + interval.Semitones;
            return (uint)result switch
            {
                > 127u => throw new ArgumentOutOfRangeException(nameof(interval),
                    "Resulting MIDI pitch is out of range 0-127"),
                _ => result
            };
        }
    }
}
