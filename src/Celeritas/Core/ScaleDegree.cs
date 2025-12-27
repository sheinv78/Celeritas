// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Scale degree in roman numeral notation
/// </summary>
public enum ScaleDegree
{
    I = 0,      // Tonic
    II = 2,     // Supertonic
    III = 4,    // Mediant
    IV = 5,     // Subdominant
    V = 7,      // Dominant
    VI = 9,     // Submediant
    VII = 11    // Leading tone
}

/// <summary>
/// Harmonic function of a chord
/// </summary>
public enum HarmonicFunction
{
    Tonic,          // I, vi, iii (rest)
    Subdominant,    // IV, ii (preparation)
    Dominant,       // V, vii° (tension)
    PreDominant,    // IV, ii (can substitute subdominant)
    Chromatic       // Borrowed/altered chords
}

/// <summary>
/// Key signature with root and mode
/// </summary>
public readonly struct KeySignature
{
    public readonly byte Root;      // 0-11 (C=0, C#=1, etc.)
    public readonly bool IsMajor;   // true for major, false for minor

    private static readonly byte[] MajorScaleSteps = [0, 2, 4, 5, 7, 9, 11];
    private static readonly byte[] MinorScaleSteps = [0, 2, 3, 5, 7, 8, 10];

    public KeySignature(byte root, bool isMajor)
    {
        Root = (byte)(root % 12);
        IsMajor = isMajor;
    }

    public KeySignature(string rootName, bool isMajor)
    {
        Root = ParseNoteName(rootName);
        IsMajor = isMajor;
    }

    /// <summary>
    /// Returns the semitone offset (0-11) for a diatonic scale degree in this key.
    /// Uses major or natural minor.
    /// </summary>
    public byte GetScaleDegreeOffset(ScaleDegree degree)
    {
        var index = DegreeToIndex(degree);
        var steps = IsMajor ? MajorScaleSteps : MinorScaleSteps;
        return steps[index];
    }

    /// <summary>
    /// Returns the pitch class (0-11) for a diatonic scale degree in this key.
    /// Uses major or natural minor.
    /// </summary>
    public byte GetScaleDegreePitchClass(ScaleDegree degree)
    {
        return (byte)((Root + GetScaleDegreeOffset(degree)) % 12);
    }

    /// <summary>
    /// Returns a 12-bit pitch-class mask for this key's diatonic scale (major or natural minor).
    /// </summary>
    public ushort GetScaleMask()
    {
        ushort mask = 0;
        var steps = IsMajor ? MajorScaleSteps : MinorScaleSteps;
        for (var i = 0; i < steps.Length; i++)
        {
            mask |= (ushort)(1 << ((Root + steps[i]) % 12));
        }

        return mask;
    }

    /// <summary>
    /// Returns the diatonic scale pitch classes (0-11) for this key.
    /// Uses major or natural minor.
    /// </summary>
    public int[] GetScale()
    {
        var steps = IsMajor ? MajorScaleSteps : MinorScaleSteps;
        var scale = new int[steps.Length];

        for (var i = 0; i < steps.Length; i++)
        {
            scale[i] = (Root + steps[i]) % 12;
        }

        return scale;
    }

    private static int DegreeToIndex(ScaleDegree degree)
    {
        return degree switch
        {
            ScaleDegree.I => 0,
            ScaleDegree.II => 1,
            ScaleDegree.III => 2,
            ScaleDegree.IV => 3,
            ScaleDegree.V => 4,
            ScaleDegree.VI => 5,
            ScaleDegree.VII => 6,
            _ => 0
        };
    }

    private static byte ParseNoteName(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "C" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" => 4,
            "F" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" => 11,
            _ => 0
        };
    }

    /// <summary>
    /// Gets the relative key (e.g., C Major → A Minor, A Minor → C Major).
    /// </summary>
    public KeySignature GetRelativeKey()
    {
        // Relative minor is 3 semitones below major; relative major is 3 above minor
        var newRoot = IsMajor ? (byte)((Root + 9) % 12) : (byte)((Root + 3) % 12);
        return new KeySignature(newRoot, !IsMajor);
    }

    /// <summary>
    /// Gets the parallel key (e.g., C Major → C Minor, A Minor → A Major).
    /// </summary>
    public KeySignature GetParallelKey() => new(Root, !IsMajor);

    /// <summary>
    /// Gets the dominant key (e.g., C Major → G Major, A Minor → E Minor).
    /// </summary>
    public KeySignature GetDominantKey() => new((byte)((Root + 7) % 12), IsMajor);

    /// <summary>
    /// Gets the subdominant key (e.g., C Major → F Major, A Minor → D Minor).
    /// </summary>
    public KeySignature GetSubdominantKey() => new((byte)((Root + 5) % 12), IsMajor);

    public override string ToString() => $"{ChordLibrary.NoteNames[Root]} {(IsMajor ? "Major" : "Minor")}";
}
