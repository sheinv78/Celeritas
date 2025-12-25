// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.VoiceLeading;

/// <summary>
/// Voice in a chord voicing (SATB: Soprano, Alto, Tenor, Bass).
/// </summary>
public enum Voice
{
    Bass = 0,
    Tenor = 1,
    Alto = 2,
    Soprano = 3
}

/// <summary>
/// Represents the MIDI pitch ranges for each voice.
/// </summary>
public static class VoiceRanges
{
    // Standard SATB ranges (MIDI pitch numbers)
    public static readonly (int Min, int Max) Bass = (40, 60);      // E2 - C4
    public static readonly (int Min, int Max) Tenor = (48, 67);     // C3 - G4
    public static readonly (int Min, int Max) Alto = (55, 74);      // G3 - D5
    public static readonly (int Min, int Max) Soprano = (60, 81);   // C4 - A5

    public static (int Min, int Max) GetRange(Voice voice) => voice switch
    {
        Voice.Bass => Bass,
        Voice.Tenor => Tenor,
        Voice.Alto => Alto,
        Voice.Soprano => Soprano,
        _ => (0, 127)
    };

    public static bool IsInRange(Voice voice, int pitch)
    {
        var range = GetRange(voice);
        return pitch >= range.Min && pitch <= range.Max;
    }
}

/// <summary>
/// A specific voicing of a chord (4 pitches, one per voice).
/// Stored as a packed 32-bit integer for efficient comparison.
/// </summary>
public readonly struct Voicing : IEquatable<Voicing>
{
    // Packed: Bass(8 bits) | Tenor(8 bits) | Alto(8 bits) | Soprano(8 bits)
    private readonly uint _packed;

    public Voicing(int bass, int tenor, int alto, int soprano)
    {
        _packed = (uint)(
            (bass & 0xFF) |
            ((tenor & 0xFF) << 8) |
            ((alto & 0xFF) << 16) |
            ((soprano & 0xFF) << 24));
    }

    public int Bass => (int)(_packed & 0xFF);
    public int Tenor => (int)((_packed >> 8) & 0xFF);
    public int Alto => (int)((_packed >> 16) & 0xFF);
    public int Soprano => (int)((_packed >> 24) & 0xFF);

    public int this[Voice voice] => voice switch
    {
        Voice.Bass => Bass,
        Voice.Tenor => Tenor,
        Voice.Alto => Alto,
        Voice.Soprano => Soprano,
        _ => 0
    };

    public int[] ToPitches() => [Bass, Tenor, Alto, Soprano];

    public bool Equals(Voicing other) => _packed == other._packed;
    public override bool Equals(object? obj) => obj is Voicing v && Equals(v);
    public override int GetHashCode() => (int)_packed;

    public static bool operator ==(Voicing a, Voicing b) => a._packed == b._packed;
    public static bool operator !=(Voicing a, Voicing b) => a._packed != b._packed;

    public override string ToString()
    {
        return $"[{MusicNotation.ToNotation(Bass)}, {MusicNotation.ToNotation(Tenor)}, " +
               $"{MusicNotation.ToNotation(Alto)}, {MusicNotation.ToNotation(Soprano)}]";
    }
}

/// <summary>
/// Types of voice leading violations.
/// </summary>
[Flags]
public enum VoiceLeadingViolation : ushort
{
    None = 0,

    // Parallel motion violations (most severe)
    ParallelFifths = 1 << 0,      // Two voices move in parallel perfect 5ths
    ParallelOctaves = 1 << 1,     // Two voices move in parallel octaves/unisons

    // Hidden/direct parallels
    HiddenFifths = 1 << 2,        // Outer voices move to P5 in similar motion
    HiddenOctaves = 1 << 3,       // Outer voices move to P8 in similar motion

    // Voice crossing and overlap
    VoiceCrossing = 1 << 4,       // Voices cross each other
    VoiceOverlap = 1 << 5,        // Voice moves past previous position of adjacent voice

    // Melodic violations
    AugmentedInterval = 1 << 6,   // Voice moves by augmented interval
    LargeLeap = 1 << 7,           // Voice moves by more than an octave

    // Resolution violations
    UnresolvedLeadingTone = 1 << 8,   // Leading tone doesn't resolve up
    UnresolvedSeventh = 1 << 9,       // Seventh doesn't resolve down

    // Spacing violations
    ExcessiveSpacing = 1 << 10,   // More than octave between adjacent upper voices

    // Doubling violations
    DoubledLeadingTone = 1 << 11, // Leading tone is doubled
}

/// <summary>
/// Result of checking voice leading rules between two voicings.
/// </summary>
public readonly record struct VoiceLeadingCheck(
    VoiceLeadingViolation Violations,
    float Penalty)
{
    public bool IsValid => Violations == VoiceLeadingViolation.None;
    public bool HasViolation(VoiceLeadingViolation v) => (Violations & v) != 0;
}
