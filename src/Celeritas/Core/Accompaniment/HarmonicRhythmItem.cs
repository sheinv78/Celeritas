// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Accompaniment;

/// <summary>
/// A chord (in roman-numeral form) with its duration.
/// </summary>
public readonly record struct HarmonicRhythmItem(RomanNumeralChord Chord, Rational Duration);
