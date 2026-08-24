// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.CLI;
using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The CLI's `analyze` prints a detected key. Key detection ranks candidates rather than
/// establishing facts, and a few notes often decide nothing at all, so the line must carry
/// the margin behind it. These pin both halves: the phrasing, and the library invariant the
/// phrasing depends on.
/// </summary>
public class CliKeyReportingTests
{
    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.05f)]
    [InlineData(0.099f)]
    public void Describe_ThinMargin_SaysTheMaterialDoesNotSettleAKey(float margin)
    {
        var text = KeyConfidenceDescription.Describe(margin);

        Assert.Contains("weak", text);
        Assert.DoesNotContain("margin", text);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.13f)]
    [InlineData(0.35f)]
    [InlineData(1.0f)]
    public void Describe_RealMargin_ReportsTheNumberItWasChosenBy(float margin)
    {
        var text = KeyConfidenceDescription.Describe(margin);

        Assert.Contains("margin", text);
        Assert.DoesNotContain("weak", text);
    }

    [Fact]
    public void Describe_NeverStatesAKeyWithoutQualification()
    {
        // Every reachable margin produces a suffix; there is no value for which the CLI
        // would print a bare key. Guards against the qualifier being made conditional.
        for (var m = 0f; m <= 1f; m += 0.01f)
        {
            Assert.False(string.IsNullOrWhiteSpace(KeyConfidenceDescription.Describe(m)));
        }
    }

    [Fact]
    public void Describe_WeakThresholdSitsBelowTheConfidentBand()
    {
        // Confidence here is a margin over the runner-up, not a fit score: a clear detection
        // reads ~0.1-0.35. A threshold at the 0.5 a fit score would suggest would call every
        // genuine detection weak — the same misreading that once broke modulation detection.
        Assert.InRange(KeyConfidenceDescription.WeakMargin, 0.01f, 0.1f);
    }

    [Fact]
    public void AmbiguousChord_BothDetectorsAgree_SoTheCliCannotContradictItself()
    {
        // A lone Cmaj7 sits in C major, G major, A minor and E minor alike. `analyze` prints
        // the chord from one analyzer and the key from another; if they disagreed the output
        // would read as a contradiction ("Chord: C Major7 / Detected key: E Minor").
        const string cmaj7 = "C4 E4 G4 B4";

        var chord = ChordAnalyzer.Identify(cmaj7);
        var key = KeyAnalyzer.DetectKey(cmaj7);
        var profile = KeyProfiler.DetectFromPitches(cmaj7);

        Assert.Equal(ChordQuality.Major7, chord.Quality);

        // The two key detectors must not diverge: the CLI shows one and the margin of the other.
        Assert.Equal(profile.Key.Root, key.Root);
        Assert.Equal(profile.Key.IsMajor, key.IsMajor);

        // And the margin must be modest enough that the printed line reads as a ranking.
        Assert.InRange(profile.Confidence, 0f, 0.4f);
    }

    [Fact]
    public void EmphasisDecidesTheKey_SoTheReportedMarginGrowsWithEvidence()
    {
        // The relative-key fix: emphasis, not mere scale membership, picks the key. More
        // evidence must not make the answer less certain.
        var bare = KeyProfiler.DetectFromPitches("C4 E4 G4 B4");
        var emphasized = KeyProfiler.DetectFromPitches("C4 E4 G4 B4 C5 C5 C5 G4 G4");

        Assert.Equal((byte)0, KeyAnalyzer.DetectKey("C4 E4 G4 B4 C5 C5 C5 G4 G4").Root);
        Assert.True(KeyAnalyzer.DetectKey("C4 E4 G4 B4 C5 C5 C5 G4 G4").IsMajor);
        Assert.True(emphasized.Confidence > bare.Confidence,
            $"emphasized {emphasized.Confidence} should exceed bare {bare.Confidence}");
    }
}
