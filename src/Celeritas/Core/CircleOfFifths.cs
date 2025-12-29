// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

public enum CircleDirection
{
    Clockwise,
    CounterClockwise
}

/// <summary>
/// Utilities for the circle of fifths/fourths.
/// 
/// Note: this is pitch-class based (12-TET). Enharmonic spellings are controlled
/// only by <c>preferSharps</c> (no E#/Cb style spellings).
/// </summary>
public static class CircleOfFifths
{
    private const int PerfectFifth = 7;
    private const int PerfectFourth = 5;

    /// <summary>
    /// Returns 12 pitch classes in circle-of-fifths order.
    /// Clockwise: C → G → D → ...
    /// CounterClockwise: C → F → Bb → ...
    /// </summary>
    public static PitchClass[] PitchClasses(PitchClass start, CircleDirection direction = CircleDirection.Clockwise)
    {
        var step = direction == CircleDirection.Clockwise ? PerfectFifth : PerfectFourth;

        var result = new PitchClass[12];
        var current = start;

        for (var i = 0; i < result.Length; i++)
        {
            result[i] = current;
            current = current + step;
        }

        return result;
    }

    public static PitchClass NextFifth(PitchClass pc) => pc + PerfectFifth;

    public static PitchClass PrevFifth(PitchClass pc) => pc - PerfectFifth;

    public static PitchClass NextFourth(PitchClass pc) => pc + PerfectFourth;

    public static PitchClass PrevFourth(PitchClass pc) => pc - PerfectFourth;

    public static KeySignature[] MajorKeys(PitchClass start, CircleDirection direction = CircleDirection.Clockwise)
    {
        var pcs = PitchClasses(start, direction);
        var keys = new KeySignature[pcs.Length];
        for (var i = 0; i < pcs.Length; i++)
        {
            keys[i] = new KeySignature(pcs[i].Value, isMajor: true);
        }
        return keys;
    }

    public static KeySignature[] MinorKeys(PitchClass start, CircleDirection direction = CircleDirection.Clockwise)
    {
        var pcs = PitchClasses(start, direction);
        var keys = new KeySignature[pcs.Length];
        for (var i = 0; i < pcs.Length; i++)
        {
            keys[i] = new KeySignature(pcs[i].Value, isMajor: false);
        }
        return keys;
    }

    /// <summary>
    /// Major triad chord symbols (pitch-class root) along the circle.
    /// Example (start=C): C, G, D, A, ...
    /// </summary>
    public static string[] MajorChordSymbols(PitchClass start, bool preferSharps = true, CircleDirection direction = CircleDirection.Clockwise)
    {
        var pcs = PitchClasses(start, direction);
        var symbols = new string[pcs.Length];
        for (var i = 0; i < pcs.Length; i++)
        {
            symbols[i] = pcs[i].ToName(preferSharps);
        }
        return symbols;
    }

    /// <summary>
    /// Minor triad chord symbols (pitch-class root) along the circle.
    /// Example (start=A): Am, Em, Bm, F#m, ...
    /// </summary>
    public static string[] MinorChordSymbols(PitchClass start, bool preferSharps = true, CircleDirection direction = CircleDirection.Clockwise)
    {
        var pcs = PitchClasses(start, direction);
        var symbols = new string[pcs.Length];
        for (var i = 0; i < pcs.Length; i++)
        {
            symbols[i] = pcs[i].ToName(preferSharps) + "m";
        }
        return symbols;
    }

    /// <summary>
    /// Returns pairs (Major, relative minor) along the major circle.
    /// Example: (C, Am), (G, Em), ...
    /// </summary>
    public static (string Major, string RelativeMinor)[] MajorWithRelativeMinors(PitchClass start, bool preferSharps = true, CircleDirection direction = CircleDirection.Clockwise)
    {
        var majors = PitchClasses(start, direction);
        var pairs = new (string Major, string RelativeMinor)[majors.Length];

        for (var i = 0; i < majors.Length; i++)
        {
            var majorName = majors[i].ToName(preferSharps);
            var relativeMinorRoot = new PitchClass((majors[i].Value + 9) % 12); // down m3
            var minorName = relativeMinorRoot.ToName(preferSharps) + "m";
            pairs[i] = (majorName, minorName);
        }

        return pairs;
    }
}
