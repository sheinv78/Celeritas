// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// A chord symbol parsed into its MIDI pitches and identified chord info.
/// Replaces the pervasive <c>(string symbol, int[] pitches, ChordInfo info)</c>
/// tuple with a named type carrying identical field semantics.
/// </summary>
/// <param name="Symbol">The original chord symbol (e.g., "Cmaj7", "Dm/F").</param>
/// <param name="Pitches">MIDI pitches for the chord (octave 4 = middle C).</param>
/// <param name="Info">The identified chord: the root the symbol names, and the quality its
/// intervals above that root spell. Unlike a <see cref="ChordInfo"/> from
/// <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int})"/>, the root here is real even when the
/// quality is <see cref="ChordQuality.Unknown"/> — "G9" is Unknown on G.</param>
internal readonly record struct ParsedChord(string Symbol, int[] Pitches, ChordInfo Info)
{
    /// <summary>
    /// Parses <paramref name="symbol"/>, or returns <see langword="null"/> when it is not a
    /// chord symbol. The chord is rooted where the symbol says, whatever sits in the bass.
    /// </summary>
    /// <remarks>
    /// The readers used to identify the chord from its pitches with the bass as root, which a
    /// slash chord contradicts by design: "Am7/C" — A minor seventh over its third — came back
    /// as C6 and was reported as I6 in C major, and "Csus4/G" as a quartal chord on G that made
    /// an authentic cadence out of C - Csus4/G - C.
    /// </remarks>
    public static ParsedChord? FromSymbol(string symbol)
    {
        if (!ProgressionAdvisor.TryParseRootedChordSymbol(symbol, out var pitches, out var root) || pitches.Length == 0)
        {
            return null;
        }

        return new ParsedChord(symbol, pitches, ChordAnalyzer.Identify(pitches, root));
    }
}
