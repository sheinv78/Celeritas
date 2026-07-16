// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Pitch class in 12-TET: values 0..11 (C…B) with modulo-12 arithmetic.
/// </summary>
public readonly record struct PitchClass
{
    /// <summary>The pitch-class value, 0..11 (C…B).</summary>
    public byte Value { get; }

    /// <summary>Sharp-preferring note name for this pitch class (e.g. "C#").</summary>
    public string Name => ToName(preferSharps: true);

    /// <summary>Note name for this pitch class.</summary>
    /// <param name="preferSharps">When <see langword="true"/>, spell black keys with sharps (C#); otherwise flats (Db).</param>
    /// <returns>The one- or two-character note name.</returns>
    public string ToName(bool preferSharps = true) => Value switch
    {
        0 => "C",
        1 => preferSharps ? "C#" : "Db",
        2 => "D",
        3 => preferSharps ? "D#" : "Eb",
        4 => "E",
        5 => "F",
        6 => preferSharps ? "F#" : "Gb",
        7 => "G",
        8 => preferSharps ? "G#" : "Ab",
        9 => "A",
        10 => preferSharps ? "A#" : "Bb",
        11 => "B",
        _ => "?"
    };

    /// <summary>Pitch class C (0).</summary>
    public static PitchClass C => new(0);
    /// <summary>Pitch class C# (1).</summary>
    public static PitchClass CSharp => new(1);
    /// <summary>Pitch class Db (1).</summary>
    public static PitchClass Db => new(1);
    /// <summary>Pitch class D (2).</summary>
    public static PitchClass D => new(2);
    /// <summary>Pitch class D# (3).</summary>
    public static PitchClass DSharp => new(3);
    /// <summary>Pitch class Eb (3).</summary>
    public static PitchClass Eb => new(3);
    /// <summary>Pitch class E (4).</summary>
    public static PitchClass E => new(4);
    /// <summary>Pitch class F (5).</summary>
    public static PitchClass F => new(5);
    /// <summary>Pitch class F# (6).</summary>
    public static PitchClass FSharp => new(6);
    /// <summary>Pitch class Gb (6).</summary>
    public static PitchClass Gb => new(6);
    /// <summary>Pitch class G (7).</summary>
    public static PitchClass G => new(7);
    /// <summary>Pitch class G# (8).</summary>
    public static PitchClass GSharp => new(8);
    /// <summary>Pitch class Ab (8).</summary>
    public static PitchClass Ab => new(8);
    /// <summary>Pitch class A (9).</summary>
    public static PitchClass A => new(9);
    /// <summary>Pitch class A# (10).</summary>
    public static PitchClass ASharp => new(10);
    /// <summary>Pitch class Bb (10).</summary>
    public static PitchClass Bb => new(10);
    /// <summary>Pitch class B (11).</summary>
    public static PitchClass B => new(11);

    /// <summary>Creates a pitch class from an integer, reduced modulo 12 (negatives wrap up).</summary>
    /// <param name="value">Any integer; folded into 0..11.</param>
    public PitchClass(int value)
    {
        Value = Normalize(value);
    }

    /// <summary>Pitch class of a MIDI pitch (its remainder modulo 12).</summary>
    /// <param name="midiPitch">MIDI pitch number, 0..127.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="midiPitch"/> is outside 0..127.</exception>
    public static PitchClass FromMidi(int midiPitch)
    {
        return (uint)midiPitch switch
        {
            > 127u => throw new ArgumentOutOfRangeException(nameof(midiPitch), "MIDI pitch must be 0-127"),
            _ => new PitchClass(midiPitch % 12)
        };
    }

    /// <summary>Parses a pitch-class name such as "C", "F#", or "Bb".</summary>
    /// <param name="text">The pitch-class text to parse.</param>
    /// <returns>The parsed pitch class.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not a valid pitch class.</exception>
    public static PitchClass Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text.AsSpan(), out var pitchClass))
        {
            throw new ArgumentException($"Invalid pitch class: {text}", nameof(text));
        }

        return pitchClass;
    }

    /// <summary>Attempts to parse a pitch-class name such as "C", "F#", or "Bb".</summary>
    /// <param name="text">The pitch-class text to parse.</param>
    /// <param name="pitchClass">On success, the parsed pitch class; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, out PitchClass pitchClass)
    {
        if (!MusicNotation.TryParsePitchClass(text, out var pc, out _))
        {
            pitchClass = default;
            return false;
        }

        pitchClass = new PitchClass(pc);
        return true;
    }

    /// <summary>
    /// Ascending pitch-class interval from this pitch class to <paramref name="to"/>.
    /// Result is always in 0..11.
    /// </summary>
    public ChromaticInterval IntervalTo(PitchClass to) => new((to.Value - Value + 12) % 12);


    /// <summary>
    /// Signed shortest pitch-class interval from this pitch class to <paramref name="to"/>.
    /// Result is in -6…+6 (tritone is returned as +6).
    /// </summary>
    public ChromaticInterval SignedIntervalTo(PitchClass to)
    {
        var asc = (to.Value - Value + 12) % 12; // 0..11
        return asc switch
        {
            <= 6 => new ChromaticInterval(asc),
            _ => new ChromaticInterval(asc - 12)
        };
    }

    /// <summary>Transposes this pitch class by <paramref name="semitones"/> (result wraps modulo 12).</summary>
    public PitchClass Transpose(int semitones) => new(Value + semitones);

    /// <summary>Transposes this pitch class by <paramref name="interval"/> (result wraps modulo 12).</summary>
    public PitchClass Transpose(ChromaticInterval interval) => new(Value + interval.Semitones);

    /// <summary>Transposes <paramref name="pc"/> up by <paramref name="semitones"/> (wraps modulo 12).</summary>
    public static PitchClass operator +(PitchClass pc, int semitones) => pc.Transpose(semitones);

    /// <summary>Transposes <paramref name="pc"/> down by <paramref name="semitones"/> (wraps modulo 12).</summary>
    public static PitchClass operator -(PitchClass pc, int semitones) => pc.Transpose(-semitones);

    /// <summary>Transposes <paramref name="pc"/> up by <paramref name="interval"/> (wraps modulo 12).</summary>
    public static PitchClass operator +(PitchClass pc, ChromaticInterval interval) => pc.Transpose(interval);

    /// <summary>Transposes <paramref name="pc"/> down by <paramref name="interval"/> (wraps modulo 12).</summary>
    public static PitchClass operator -(PitchClass pc, ChromaticInterval interval) => pc.Transpose(-interval.Semitones);

    /// <summary>
    /// Ascending pitch-class interval from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static ChromaticInterval operator -(PitchClass to, PitchClass from) => from.IntervalTo(to);

    /// <summary>
    /// Signed shortest pitch-class interval from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    [Obsolete("Use SignedIntervalTo(). The ^ operator is non-obvious.")]
    public static ChromaticInterval operator ^(PitchClass from, PitchClass to) => from.SignedIntervalTo(to);

    /// <summary>Returns the sharp-preferring note name.</summary>
    public override string ToString() => Name;

    private static byte Normalize(int value)
    {
        var mod = value % 12;
        if (mod < 0)
        {
            mod += 12;
        }

        return (byte)mod;
    }
}
