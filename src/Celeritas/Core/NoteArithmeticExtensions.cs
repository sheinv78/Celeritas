// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Extension members for note and pitch arithmetic on MIDI pitch integers and notation strings.
/// </summary>
public static class NoteArithmeticExtensions
{
    extension(int midiPitch)
    {
        /// <summary>The pitch class (0-11) of this MIDI pitch.</summary>
        public PitchClass PitchClass() => Core.PitchClass.FromMidi(midiPitch);

        /// <summary>The scientific-pitch-notation note for this MIDI pitch.</summary>
        public SpnNote ToSpnNote() => SpnNote.FromMidi(midiPitch);
    }

    /// <summary>Parses a scientific-pitch-notation string (e.g. "C#4") into an <c>SpnNote</c>.</summary>
    public static SpnNote ToSpnNote(this string notation) => SpnNote.Parse(notation);

    extension(int fromMidiPitch)
    {
        /// <summary>Returns the chromatic interval from this MIDI pitch to <paramref name="toMidiPitch"/>.</summary>
        public ChromaticInterval IntervalTo(int toMidiPitch) => new(toMidiPitch - fromMidiPitch);

        /// <summary>
        /// Transposes this MIDI pitch by the given chromatic interval.
        /// </summary>
        /// <returns>The resulting MIDI pitch.</returns>
        /// <exception cref="ArgumentOutOfRangeException">This pitch, or the result, is outside the MIDI range 0-127.</exception>
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
