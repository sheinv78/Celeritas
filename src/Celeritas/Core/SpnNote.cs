// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Musical note in scientific pitch notation (pitch class + octave).
/// Backed by MIDI semantics: C-1 = 0, C4 = 60.
/// </summary>
public readonly record struct SpnNote(PitchClass PitchClass, int Octave)
{
    public int MidiPitch => ToMidiPitch();

    public static SpnNote C(int octave) => new(PitchClass.C, octave);
    public static SpnNote CSharp(int octave) => new(PitchClass.CSharp, octave);
    public static SpnNote Db(int octave) => new(PitchClass.Db, octave);
    public static SpnNote D(int octave) => new(PitchClass.D, octave);
    public static SpnNote DSharp(int octave) => new(PitchClass.DSharp, octave);
    public static SpnNote Eb(int octave) => new(PitchClass.Eb, octave);
    public static SpnNote E(int octave) => new(PitchClass.E, octave);
    public static SpnNote F(int octave) => new(PitchClass.F, octave);
    public static SpnNote FSharp(int octave) => new(PitchClass.FSharp, octave);
    public static SpnNote Gb(int octave) => new(PitchClass.Gb, octave);
    public static SpnNote G(int octave) => new(PitchClass.G, octave);
    public static SpnNote GSharp(int octave) => new(PitchClass.GSharp, octave);
    public static SpnNote Ab(int octave) => new(PitchClass.Ab, octave);
    public static SpnNote A(int octave) => new(PitchClass.A, octave);
    public static SpnNote ASharp(int octave) => new(PitchClass.ASharp, octave);
    public static SpnNote Bb(int octave) => new(PitchClass.Bb, octave);
    public static SpnNote B(int octave) => new(PitchClass.B, octave);

    public static SpnNote FromMidi(int midiPitch)
    {
        if ((uint)midiPitch > 127u)
        {
            throw new ArgumentOutOfRangeException(nameof(midiPitch), "MIDI pitch must be 0-127");
        }

        var octave = (midiPitch / 12) - 1;
        var pc = new PitchClass(midiPitch % 12);
        return new SpnNote(pc, octave);
    }

    public static SpnNote Parse(string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (!TryParse(notation.AsSpan(), out var note))
        {
            throw new ArgumentException($"Invalid note notation: {notation}", nameof(notation));
        }

        return note;
    }

    public static bool TryParse(ReadOnlySpan<char> notation, out SpnNote note)
    {
        if (!MusicNotation.TryParseNote(notation, out var midi))
        {
            note = default;
            return false;
        }

        note = FromMidi(midi);
        return true;
    }

    public int ToMidiPitch()
    {
        // MIDI number: (octave + 1) * 12 + pitchClass, where C-1 = 0
        var midi = (Octave + 1) * 12 + PitchClass.Value;
        if ((uint)midi > 127u)
        {
            throw new ArgumentOutOfRangeException(nameof(Octave), "Resulting MIDI pitch is out of range 0-127");
        }

        return midi;
    }

    public SpnNote Transpose(ChromaticInterval interval) => FromMidi(MidiPitch.Transpose(interval));

    public string ToNotation(bool preferSharps = true) => MusicNotation.ToNotation(MidiPitch, preferSharps);

    public override string ToString() => ToNotation(preferSharps: true);

    public static SpnNote operator +(SpnNote note, ChromaticInterval interval) => note.Transpose(interval);

    public static SpnNote operator -(SpnNote note, ChromaticInterval interval) => note.Transpose(-interval);

    public static ChromaticInterval operator -(SpnNote to, SpnNote from) => new(to.MidiPitch - from.MidiPitch);
}
