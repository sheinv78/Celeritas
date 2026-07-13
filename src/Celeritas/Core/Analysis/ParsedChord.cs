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
/// <param name="Info">The identified chord (root pitch class and quality).</param>
internal readonly record struct ParsedChord(string Symbol, int[] Pitches, ChordInfo Info);
