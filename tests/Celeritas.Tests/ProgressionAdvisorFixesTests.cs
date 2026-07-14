// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for ProgressionAdvisor fixes: Phrygian cadence ordering,
/// slash-chord secondary dominants, and borrowed-chord suggestions.
/// </summary>
public class ProgressionAdvisorFixesTests
{
    [Fact]
    public void DetectCadence_Iv6ToV_InMinor_IsPhrygian()
    {
        // iv in first inversion (Dm/F) -> V (E) in A minor = Phrygian half cadence.
        // The generic "-> V = Half" arm previously shadowed this check.
        var key = new KeySignature("A", false);

        var cadence = ProgressionAdvisor.DetectCadence(["Am", "Dm/F", "E"], key);

        Assert.Equal(CadenceType.Phrygian, cadence);
    }

    [Fact]
    public void DetectCadence_RootPositionIvToV_InMinor_IsHalf()
    {
        // Root-position iv -> V is a plain half cadence, not Phrygian.
        var key = new KeySignature("A", false);

        var cadence = ProgressionAdvisor.DetectCadence(["Am", "Dm", "E"], key);

        Assert.Equal(CadenceType.Half, cadence);
    }

    [Fact]
    public void DetectCadence_VToI_IsAuthentic()
    {
        var key = new KeySignature("C", true);
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["F", "G", "C"], key));
    }

    [Fact]
    public void Analyze_SecondaryDominantInInversion_IsDetectedByRootNotBass()
    {
        // A7/C# resolving to Dm: the chord ROOT (A) is a fifth above D even though
        // the BASS (C#) is not. Using pitches[0] (the bass) previously missed this.
        var report = ProgressionAdvisor.Analyze(["C", "A7/C#", "Dm", "G7", "C"]);

        Assert.Contains(report.Modulations, m => m.ToKey.Root == 2); // D
    }

    [Fact]
    public void Analyze_DiatonicProgression_SuggestsBorrowedChord()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G"]);

        Assert.Contains(report.Suggestions, s => s.Contains("borrowed iv"));
    }

    [Fact]
    public void Analyze_ProgressionWithBorrowedChord_DoesNotSuggestBorrowing()
    {
        // Ab in C major is a borrowed (chromatic) chord, so the "try a borrowed
        // chord" suggestion must not fire. The old SpecialNote?.Contains("borrowed")
        // check never matched, so it always fired.
        var report = ProgressionAdvisor.Analyze(["C", "Ab", "F", "C"]);

        Assert.Equal(0, report.Key.Root);
        Assert.True(report.Key.IsMajor);
        Assert.Contains(report.Chords, c => c.IsBorrowed);
        Assert.DoesNotContain(report.Suggestions, s => s.Contains("(borrowed"));
    }
}
