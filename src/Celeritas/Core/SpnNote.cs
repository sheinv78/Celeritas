// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Musical note in scientific pitch notation (pitch class + octave).
/// Backed by MIDI semantics: C-1 = 0, C4 = 60.
/// </summary>
/// <param name="PitchClass">The pitch class (0..11).</param>
/// <param name="Octave">The scientific-pitch octave number (C4 is middle C).</param>
public readonly record struct SpnNote(PitchClass PitchClass, int Octave)
{
    /// <summary>MIDI pitch number for this note (middle C = 60).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The note falls outside MIDI range 0..127.</exception>
    public int MidiPitch => ToMidiPitch();

    /// <summary>Note C in the given <paramref name="octave"/>.</summary>
    public static SpnNote C(int octave) => new(PitchClass.C, octave);
    /// <summary>Note C# in the given <paramref name="octave"/>.</summary>
    public static SpnNote CSharp(int octave) => new(PitchClass.CSharp, octave);
    /// <summary>Note Db in the given <paramref name="octave"/>.</summary>
    public static SpnNote Db(int octave) => new(PitchClass.Db, octave);
    /// <summary>Note D in the given <paramref name="octave"/>.</summary>
    public static SpnNote D(int octave) => new(PitchClass.D, octave);
    /// <summary>Note D# in the given <paramref name="octave"/>.</summary>
    public static SpnNote DSharp(int octave) => new(PitchClass.DSharp, octave);
    /// <summary>Note Eb in the given <paramref name="octave"/>.</summary>
    public static SpnNote Eb(int octave) => new(PitchClass.Eb, octave);
    /// <summary>Note E in the given <paramref name="octave"/>.</summary>
    public static SpnNote E(int octave) => new(PitchClass.E, octave);
    /// <summary>Note F in the given <paramref name="octave"/>.</summary>
    public static SpnNote F(int octave) => new(PitchClass.F, octave);
    /// <summary>Note F# in the given <paramref name="octave"/>.</summary>
    public static SpnNote FSharp(int octave) => new(PitchClass.FSharp, octave);
    /// <summary>Note Gb in the given <paramref name="octave"/>.</summary>
    public static SpnNote Gb(int octave) => new(PitchClass.Gb, octave);
    /// <summary>Note G in the given <paramref name="octave"/>.</summary>
    public static SpnNote G(int octave) => new(PitchClass.G, octave);
    /// <summary>Note G# in the given <paramref name="octave"/>.</summary>
    public static SpnNote GSharp(int octave) => new(PitchClass.GSharp, octave);
    /// <summary>Note Ab in the given <paramref name="octave"/>.</summary>
    public static SpnNote Ab(int octave) => new(PitchClass.Ab, octave);
    /// <summary>Note A in the given <paramref name="octave"/>.</summary>
    public static SpnNote A(int octave) => new(PitchClass.A, octave);
    /// <summary>Note A# in the given <paramref name="octave"/>.</summary>
    public static SpnNote ASharp(int octave) => new(PitchClass.ASharp, octave);
    /// <summary>Note Bb in the given <paramref name="octave"/>.</summary>
    public static SpnNote Bb(int octave) => new(PitchClass.Bb, octave);
    /// <summary>Note B in the given <paramref name="octave"/>.</summary>
    public static SpnNote B(int octave) => new(PitchClass.B, octave);

    /// <summary>Creates a note from a MIDI pitch number (middle C = 60).</summary>
    /// <param name="midiPitch">MIDI pitch number, 0..127.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="midiPitch"/> is outside 0..127.</exception>
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

    /// <summary>Parses a note in scientific pitch notation such as "C4" or "F#5".</summary>
    /// <param name="notation">The note text to parse.</param>
    /// <returns>The parsed note.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="notation"/> is not valid note notation.</exception>
    public static SpnNote Parse(string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (!TryParse(notation.AsSpan(), out var note))
        {
            throw new ArgumentException($"Invalid note notation: {notation}", nameof(notation));
        }

        return note;
    }

    /// <summary>Attempts to parse a note in scientific pitch notation such as "C4" or "F#5".</summary>
    /// <param name="notation">The note text to parse.</param>
    /// <param name="note">On success, the parsed note; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
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

    private int ToMidiPitch()
    {
        // MIDI number: (octave + 1) * 12 + pitchClass, where C-1 = 0
        var midi = ((Octave + 1) * 12) + PitchClass.Value;
        return (uint)midi switch
        {
            > 127u => throw new ArgumentOutOfRangeException(nameof(Octave),
                "Resulting MIDI pitch is out of range 0-127"),
            _ => midi
        };
    }

    private SpnNote Transpose(ChromaticInterval interval) => FromMidi(MidiPitch.Transpose(interval));

    /// <summary>
    /// Renders this note in scientific pitch notation (e.g. "C4"). Formats directly from the
    /// pitch class and octave, so it never throws — even for notes outside the MIDI 0..127
    /// range (e.g. "A9" or "C-2").
    /// </summary>
    /// <param name="preferSharps">When <see langword="true"/>, spell black keys with sharps; otherwise flats.</param>
    public string ToNotation(bool preferSharps = true) => $"{PitchClass.ToName(preferSharps)}{Octave}";

    /// <summary>Returns the sharp-preferring scientific pitch notation. Never throws, even outside MIDI range.</summary>
    public override string ToString() => ToNotation(preferSharps: true);

    /// <summary>Transposes <paramref name="note"/> up by <paramref name="interval"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="note"/> itself, or the
    /// transposed result, falls outside the MIDI range 0..127.</exception>
    public static SpnNote operator +(SpnNote note, ChromaticInterval interval) => note.Transpose(interval);

    /// <summary>Transposes <paramref name="note"/> down by <paramref name="interval"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="note"/> itself, or the
    /// transposed result, falls outside the MIDI range 0..127.</exception>
    public static SpnNote operator -(SpnNote note, ChromaticInterval interval) => note.Transpose(-interval);

    /// <summary>
    /// The signed chromatic interval in semitones from <paramref name="from"/> to <paramref name="to"/>.
    /// Computed from the pitch-class and octave components, so it never throws — even when an
    /// operand falls outside the MIDI 0..127 range.
    /// </summary>
    public static ChromaticInterval operator -(SpnNote to, SpnNote from) =>
        new(((to.Octave - from.Octave) * 12) + (to.PitchClass.Value - from.PitchClass.Value));
}
