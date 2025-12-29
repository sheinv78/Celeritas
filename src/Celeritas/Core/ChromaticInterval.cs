// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Chromatic interval measured in semitones.
/// Can be negative (descending) or larger than an octave (compound).
/// </summary>
public readonly record struct ChromaticInterval(int Semitones)
{
    /// <summary>
    /// Absolute semitone count.
    /// </summary>
    public int AbsSemitones => Math.Abs(Semitones);

    /// <summary>
    /// Simple (within octave) semitone size in 0..12.
    /// For example: 14 -> 2 (M2), 12 -> 12 (P8).
    /// </summary>
    public int SimpleSemitones
    {
        get
        {
            var abs = AbsSemitones;
            if (abs == 0) return 0;

            var mod = abs % 12;
            return mod == 0 ? 12 : mod;
        }
    }

    /// <summary>
    /// Semitone class in 0..11 (mod 12). Always non-negative.
    /// Useful for pitch-class arithmetic.
    /// </summary>
    public int ClassSemitones => ((Semitones % 12) + 12) % 12;

    public int Direction => Math.Sign(Semitones);

    public string SimpleName => SimpleSemitones switch
    {
        0 => "Unison",
        1 => "m2",
        2 => "M2",
        3 => "m3",
        4 => "M3",
        5 => "P4",
        6 => "TT",
        7 => "P5",
        8 => "m6",
        9 => "M6",
        10 => "m7",
        11 => "M7",
        12 => "P8",
        _ => $"{SimpleSemitones}st"
    };

    /// <summary>
    /// Generic interval number (ignores quality): 1=unison, 2=second, ... 8=octave.
    /// Tritone is returned as 4 (closest generic class).
    /// </summary>
    public int GenericNumber => SimpleSemitones switch
    {
        0 => 1,
        1 or 2 => 2,
        3 or 4 => 3,
        5 => 4,
        6 => 4,
        7 => 5,
        8 or 9 => 6,
        10 or 11 => 7,
        12 => 8,
        _ => 0
    };

    public override string ToString() => SimpleName;

    public static ChromaticInterval operator -(ChromaticInterval i) => new(-i.Semitones);

    public static readonly ChromaticInterval Unison = new(0);
    public static readonly ChromaticInterval MinorSecond = new(1);
    public static readonly ChromaticInterval MajorSecond = new(2);
    public static readonly ChromaticInterval MinorThird = new(3);
    public static readonly ChromaticInterval MajorThird = new(4);
    public static readonly ChromaticInterval PerfectFourth = new(5);
    public static readonly ChromaticInterval Tritone = new(6);
    public static readonly ChromaticInterval PerfectFifth = new(7);
    public static readonly ChromaticInterval MinorSixth = new(8);
    public static readonly ChromaticInterval MajorSixth = new(9);
    public static readonly ChromaticInterval MinorSeventh = new(10);
    public static readonly ChromaticInterval MajorSeventh = new(11);
    public static readonly ChromaticInterval Octave = new(12);
}
