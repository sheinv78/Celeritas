// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core;

/// <summary>
/// The one place the engine folds an integer to a pitch class.
/// </summary>
/// <remarks>
/// <c>((value % 12) + 12) % 12</c> was hand-written ~20 times across the engine, and the copies
/// that got it slightly wrong were real bugs: a bare <c>% 12</c> keeps the sign in C#, so a
/// negative pitch indexed backwards out of a 12-element array (KeyProfiler, ModalSystem) or came
/// back as a negative "pitch class" (PitchClassSetAnalyzer.Transpose). Routing every fold through
/// one inlined method removes the class, not just the instances.
///
/// This is deliberately <em>not</em> the interval fold <c>(a - b + 12) % 12</c>, which reduces a
/// difference of two pitch classes to an interval class — a different operation with its own call
/// sites (VoiceLeadingRules, ModulationTypes, ChromaticInterval), left alone.
///
/// <see cref="MethodImplOptions.AggressiveInlining"/> so the hot pitch-class paths
/// (<c>KeyAnalyzer.GetScaleMask</c>) compile to the same branchless arithmetic they did inline —
/// verified against the ChordAnalysis benchmarks. The one place that must <em>not</em> use this is
/// <c>ChordAnalyzer.PitchClassIndex</c>, whose <c>(uint)p &lt;= 127u ? p : …</c> fast path skips the
/// fold entirely for in-range MIDI pitches and is on the unrolled GetMask loop.
/// </remarks>
internal static class PitchMath
{
    /// <summary>
    /// Fold any integer to a pitch class in <c>[0, 12)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Fold(int value) => ((value % 12) + 12) % 12;
}
