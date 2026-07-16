// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.VoiceLeading;

/// <summary>
/// A voice part in a chord voicing (SATB: Soprano, Alto, Tenor, Bass).
/// </summary>
public enum VoicePart
{
    /// <summary>Bass (lowest voice).</summary>
    Bass = 0,

    /// <summary>Tenor (second-lowest voice).</summary>
    Tenor = 1,

    /// <summary>Alto (second-highest voice).</summary>
    Alto = 2,

    /// <summary>Soprano (highest voice).</summary>
    Soprano = 3
}

/// <summary>
/// Represents the MIDI pitch ranges for each voice.
/// </summary>
public static class VoiceRanges
{
    // Standard SATB ranges (MIDI pitch numbers)
    /// <summary>Bass range as inclusive MIDI pitch bounds, E2 to C4.</summary>
    public static readonly (int Min, int Max) Bass = (40, 60);      // E2 - C4

    /// <summary>Tenor range as inclusive MIDI pitch bounds, C3 to G4.</summary>
    public static readonly (int Min, int Max) Tenor = (48, 67);     // C3 - G4

    /// <summary>Alto range as inclusive MIDI pitch bounds, G3 to D5.</summary>
    public static readonly (int Min, int Max) Alto = (55, 74);      // G3 - D5

    /// <summary>Soprano range as inclusive MIDI pitch bounds, C4 to A5.</summary>
    public static readonly (int Min, int Max) Soprano = (60, 81);   // C4 - A5

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="voice"/> is not a defined <see cref="VoicePart"/> value.</exception>
    public static (int Min, int Max) GetRange(VoicePart voice)
    {
        if (!Enum.IsDefined(voice))
            throw new ArgumentOutOfRangeException(nameof(voice), voice, "Not a defined VoicePart value.");

        return voice switch
        {
            VoicePart.Bass => Bass,
            VoicePart.Tenor => Tenor,
            VoicePart.Alto => Alto,
            VoicePart.Soprano => Soprano,
            _ => (0, 127)
        };
    }
}

/// <summary>
/// A specific voicing of a chord (4 pitches, one per voice).
/// Stored as a packed 32-bit integer for efficient comparison.
/// </summary>
/// <param name="bass">Bass voice MIDI pitch, in [0, 127].</param>
/// <param name="tenor">Tenor voice MIDI pitch, in [0, 127].</param>
/// <param name="alto">Alto voice MIDI pitch, in [0, 127].</param>
/// <param name="soprano">Soprano voice MIDI pitch, in [0, 127].</param>
/// <exception cref="ArgumentOutOfRangeException">Any voice is outside the MIDI range [0, 127].</exception>
public readonly struct Voicing(int bass, int tenor, int alto, int soprano)
    : IEquatable<Voicing>
{
    // Packed: Bass(8 bits) | Tenor(8 bits) | Alto(8 bits) | Soprano(8 bits)
    private readonly uint _packed = Pack(bass, tenor, alto, soprano);

    /// <exception cref="ArgumentOutOfRangeException">Any voice is outside the MIDI range [0, 127].</exception>
    private static uint Pack(int bass, int tenor, int alto, int soprano)
    {
        // Each voice gets 8 bits, and `& 0xFF` truncates rather than complains: pitch 256 was
        // stored as 0 and read back as C-1, pitch 300 as G#2, and -1 as 255. The voicing looked
        // entirely plausible afterwards — it was simply a different chord than the caller built.
        ThrowIfNotMidiPitch(bass);
        ThrowIfNotMidiPitch(tenor);
        ThrowIfNotMidiPitch(alto);
        ThrowIfNotMidiPitch(soprano);

        return (uint)(
            (bass & 0xFF) |
            ((tenor & 0xFF) << 8) |
            ((alto & 0xFF) << 16) |
            ((soprano & 0xFF) << 24));
    }

    private static void ThrowIfNotMidiPitch(int pitch, [CallerArgumentExpression(nameof(pitch))] string? name = null)
    {
        if ((uint)pitch > 127)
            throw new ArgumentOutOfRangeException(name, pitch, "Voice pitch must be a MIDI pitch in [0, 127].");
    }

    /// <summary>Bass voice MIDI pitch.</summary>
    public int Bass => (int)(_packed & 0xFF);

    /// <summary>Tenor voice MIDI pitch.</summary>
    public int Tenor => (int)((_packed >> 8) & 0xFF);

    /// <summary>Alto voice MIDI pitch.</summary>
    public int Alto => (int)((_packed >> 16) & 0xFF);

    /// <summary>Soprano voice MIDI pitch.</summary>
    public int Soprano => (int)((_packed >> 24) & 0xFF);

    /// <summary>Gets the MIDI pitch of the given voice part.</summary>
    /// <param name="voice">The voice part to read.</param>
    /// <returns>The MIDI pitch for <paramref name="voice"/>, or 0 for an undefined value.</returns>
    public int this[VoicePart voice] => voice switch
    {
        VoicePart.Bass => Bass,
        VoicePart.Tenor => Tenor,
        VoicePart.Alto => Alto,
        VoicePart.Soprano => Soprano,
        _ => 0
    };

    /// <summary>Returns the four voice pitches in bass-to-soprano order.</summary>
    public int[] ToPitches() => [Bass, Tenor, Alto, Soprano];

    /// <summary>Determines whether this voicing has the same four pitches as <paramref name="other"/>.</summary>
    public bool Equals(Voicing other) => _packed == other._packed;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Voicing v && Equals(v);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)_packed;

    /// <summary>Determines whether two voicings have identical pitches.</summary>
    public static bool operator ==(Voicing a, Voicing b) => a._packed == b._packed;

    /// <summary>Determines whether two voicings differ in any pitch.</summary>
    public static bool operator !=(Voicing a, Voicing b) => a._packed != b._packed;

    /// <summary>Returns the voicing as bracketed note names, bass to soprano.</summary>
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
    /// <summary>No violation.</summary>
    None = 0,

    // Parallel motion violations (most severe)
    /// <summary>Two voices move in parallel perfect fifths.</summary>
    ParallelFifths = 1 << 0,      // Two voices move in parallel perfect 5ths

    /// <summary>Two voices move in parallel octaves or unisons.</summary>
    ParallelOctaves = 1 << 1,     // Two voices move in parallel octaves/unisons

    // Hidden/direct parallels
    /// <summary>Outer voices reach a perfect fifth in similar motion.</summary>
    HiddenFifths = 1 << 2,        // Outer voices move to P5 in similar motion

    /// <summary>Outer voices reach a perfect octave in similar motion.</summary>
    HiddenOctaves = 1 << 3,       // Outer voices move to P8 in similar motion

    // Voice crossing and overlap
    /// <summary>Two voices cross each other.</summary>
    VoiceCrossing = 1 << 4,       // Voices cross each other

    /// <summary>A voice moves past the previous position of an adjacent voice.</summary>
    VoiceOverlap = 1 << 5,        // VoicePart moves past previous position of adjacent voice

    // Melodic violations
    /// <summary>A voice moves by an augmented interval.</summary>
    AugmentedInterval = 1 << 6,   // VoicePart moves by augmented interval

    /// <summary>A voice leaps by more than an octave.</summary>
    LargeLeap = 1 << 7,           // VoicePart moves by more than an octave

    // Resolution violations
    /// <summary>The leading tone does not resolve up.</summary>
    UnresolvedLeadingTone = 1 << 8,   // Leading tone doesn't resolve up

    /// <summary>The chordal seventh does not resolve down.</summary>
    UnresolvedSeventh = 1 << 9,       // Seventh doesn't resolve down

    // Spacing violations
    /// <summary>More than an octave separates adjacent upper voices.</summary>
    ExcessiveSpacing = 1 << 10,   // More than octave between adjacent upper voices

    // Doubling violations
    /// <summary>The leading tone is doubled.</summary>
    DoubledLeadingTone = 1 << 11, // Leading tone is doubled
}

/// <summary>
/// Result of checking voice leading rules between two voicings.
/// </summary>
/// <param name="Violations">Bit flags of the violations detected.</param>
/// <param name="Penalty">Accumulated penalty cost for the detected violations.</param>
public readonly record struct VoiceLeadingCheck(
    VoiceLeadingViolation Violations,
    float Penalty)
{
    /// <summary>True when no violation was detected.</summary>
    public bool IsValid => Violations == VoiceLeadingViolation.None;

    /// <summary>Returns whether the given violation flag is set.</summary>
    public bool HasViolation(VoiceLeadingViolation v) => (Violations & v) != 0;
}
