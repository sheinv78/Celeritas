// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Pitch class in 12-TET: values 0..11 (C..B) with modulo-12 arithmetic.
/// </summary>
public readonly record struct PitchClass
{
    public byte Value { get; }

    public string Name => ToName(preferSharps: true);

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

    public static PitchClass C => new(0);
    public static PitchClass CSharp => new(1);
    public static PitchClass Db => new(1);
    public static PitchClass D => new(2);
    public static PitchClass DSharp => new(3);
    public static PitchClass Eb => new(3);
    public static PitchClass E => new(4);
    public static PitchClass F => new(5);
    public static PitchClass FSharp => new(6);
    public static PitchClass Gb => new(6);
    public static PitchClass G => new(7);
    public static PitchClass GSharp => new(8);
    public static PitchClass Ab => new(8);
    public static PitchClass A => new(9);
    public static PitchClass ASharp => new(10);
    public static PitchClass Bb => new(10);
    public static PitchClass B => new(11);

    public PitchClass(int value)
    {
        Value = Normalize(value);
    }

    public static PitchClass FromMidi(int midiPitch)
    {
        if ((uint)midiPitch > 127u)
        {
            throw new ArgumentOutOfRangeException(nameof(midiPitch), "MIDI pitch must be 0-127");
        }

        return new PitchClass(midiPitch % 12);
    }

    public static PitchClass Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryParse(text.AsSpan(), out var pitchClass))
        {
            throw new ArgumentException($"Invalid pitch class: {text}", nameof(text));
        }

        return pitchClass;
    }

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
    /// Alias for <see cref="IntervalTo"/> with a more explicit name.
    /// </summary>
    public ChromaticInterval AscendingIntervalTo(PitchClass to) => IntervalTo(to);

    /// <summary>
    /// Signed shortest pitch-class interval from this pitch class to <paramref name="to"/>.
    /// Result is in -6..+6 (tritone is returned as +6).
    /// </summary>
    public ChromaticInterval SignedIntervalTo(PitchClass to)
    {
        var asc = (to.Value - Value + 12) % 12; // 0..11
        if (asc <= 6)
        {
            return new ChromaticInterval(asc);
        }

        return new ChromaticInterval(asc - 12); // -5..-1
    }

    public PitchClass Transpose(int semitones) => new(Value + semitones);

    public PitchClass Transpose(ChromaticInterval interval) => new(Value + interval.Semitones);

    public static PitchClass operator +(PitchClass pc, int semitones) => pc.Transpose(semitones);

    public static PitchClass operator -(PitchClass pc, int semitones) => pc.Transpose(-semitones);

    public static PitchClass operator +(PitchClass pc, ChromaticInterval interval) => pc.Transpose(interval);

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
