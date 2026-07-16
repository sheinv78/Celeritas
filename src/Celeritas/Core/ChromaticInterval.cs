// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Chromatic interval measured in semitones.
/// Can be negative (descending) or larger than an octave (compound).
/// </summary>
/// <param name="Semitones">The signed semitone size of the interval.</param>
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

    /// <summary>Direction of the interval: -1 descending, 0 unison, +1 ascending.</summary>
    public int Direction => Math.Sign(Semitones);

    /// <summary>Short quality/size name of the simplified interval (e.g. "M3", "P5", "TT").</summary>
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

    /// <summary>Returns <see cref="SimpleName"/>.</summary>
    public override string ToString() => SimpleName;

    /// <summary>Negates the interval (reverses its direction).</summary>
    public static ChromaticInterval operator -(ChromaticInterval i) => new(-i.Semitones);

    /// <summary>Unison (0 semitones).</summary>
    public static readonly ChromaticInterval Unison = new(0);
    /// <summary>Minor second (1 semitone).</summary>
    public static readonly ChromaticInterval MinorSecond = new(1);
    /// <summary>Major second (2 semitones).</summary>
    public static readonly ChromaticInterval MajorSecond = new(2);
    /// <summary>Minor third (3 semitones).</summary>
    public static readonly ChromaticInterval MinorThird = new(3);
    /// <summary>Major third (4 semitones).</summary>
    public static readonly ChromaticInterval MajorThird = new(4);
    /// <summary>Perfect fourth (5 semitones).</summary>
    public static readonly ChromaticInterval PerfectFourth = new(5);
    /// <summary>Tritone (6 semitones).</summary>
    public static readonly ChromaticInterval Tritone = new(6);
    /// <summary>Perfect fifth (7 semitones).</summary>
    public static readonly ChromaticInterval PerfectFifth = new(7);
    /// <summary>Minor sixth (8 semitones).</summary>
    public static readonly ChromaticInterval MinorSixth = new(8);
    /// <summary>Major sixth (9 semitones).</summary>
    public static readonly ChromaticInterval MajorSixth = new(9);
    /// <summary>Minor seventh (10 semitones).</summary>
    public static readonly ChromaticInterval MinorSeventh = new(10);
    /// <summary>Major seventh (11 semitones).</summary>
    public static readonly ChromaticInterval MajorSeventh = new(11);
    /// <summary>Octave (12 semitones).</summary>
    public static readonly ChromaticInterval Octave = new(12);
}
