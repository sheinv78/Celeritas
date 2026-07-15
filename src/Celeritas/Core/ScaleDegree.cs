// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Scale degree in roman numeral notation
/// </summary>
public enum ScaleDegree
{
    I = 0,      // Tonic
    Ii = 2,     // Supertonic
    Iii = 4,    // Mediant
    Iv = 5,     // Subdominant
    V = 7,      // Dominant
    Vi = 9,     // Submediant
    Vii = 11    // Leading tone
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
public readonly record struct KeySignature
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
    private byte GetScaleDegreeOffset(ScaleDegree degree)
    {
        var index = DegreeToIndex(degree);
        var steps = IsMajor ? MajorScaleSteps : MinorScaleSteps;
        return steps[index];
    }

    /// <summary>
    /// Returns the pitch class (0-11) for a diatonic scale degree in this key.
    /// Uses major or natural minor.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degree"/> is not a defined <see cref="ScaleDegree"/> value.</exception>
    public byte GetScaleDegreePitchClass(ScaleDegree degree)
    {
        if (!Enum.IsDefined(degree))
            throw new ArgumentOutOfRangeException(nameof(degree), degree, "Not a defined ScaleDegree value.");

        return (byte)((Root + GetScaleDegreeOffset(degree)) % 12);
    }

    /// <summary>
    /// Returns a 12-bit pitch-class mask for this key's diatonic scale (major or natural minor).
    /// </summary>
    public ushort GetScaleMask()
    {
        var steps = IsMajor ? MajorScaleSteps : MinorScaleSteps;

        byte b = Root;
        return steps.Aggregate<byte, ushort>(0, (current, t) => (ushort)(current | (ushort)(1 << ((b + t) % 12))));
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
            ScaleDegree.Ii => 1,
            ScaleDegree.Iii => 2,
            ScaleDegree.Iv => 3,
            ScaleDegree.V => 4,
            ScaleDegree.Vi => 5,
            ScaleDegree.Vii => 6,
            _ => 0
        };
    }

    private static byte ParseNoteName(string name)
    {
        // Delegate to the shared parser so all note-name parsing agrees and
        // unknown names throw instead of silently becoming C.
        return ChordLibrary.GetPitchClass(name);
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
