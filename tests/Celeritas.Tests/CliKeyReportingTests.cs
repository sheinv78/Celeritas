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

    // ---------- evidence, as distinct from margin ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Describe_TooFewPitchClasses_SaysUndecided_WhateverTheMargin(int distinct)
    {
        // A wide margin on thin evidence is the trap: two notes a fifth apart separate their
        // winner about as cleanly as a whole phrase does.
        var text = KeyConfidenceDescription.Describe(margin: 0.9f, distinctPitchClasses: distinct);

        Assert.Contains("undecided", text, StringComparison.Ordinal);
        Assert.DoesNotContain("margin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_EnoughPitchClasses_ReportsTheMargin()
    {
        var text = KeyConfidenceDescription.Describe(margin: 0.2f, distinctPitchClasses: 7);

        Assert.Contains("margin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("undecided", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_SingularPitchClass_ReadsGrammatically()
    {
        Assert.Contains("1 pitch class ", KeyConfidenceDescription.Describe(0.5f, 1), StringComparison.Ordinal);
        Assert.Contains("2 pitch classes", KeyConfidenceDescription.Describe(0.5f, 2), StringComparison.Ordinal);
    }

    [Fact]
    public void DetectionResult_ReportsHowMuchEvidenceItHad()
    {
        var chord = KeyProfiler.DetectFromPitches("C4 E4 G4 B4");
        var phrase = KeyProfiler.DetectFromPitches("C4 D4 E4 F4 G4 A4 B4 C5 C5");

        Assert.Equal(4, chord.DistinctPitchClasses);
        Assert.False(chord.IsDecidable);

        Assert.Equal(7, phrase.DistinctPitchClasses);
        Assert.True(phrase.IsDecidable);
    }

    [Fact]
    public void AWideMarginOnThinEvidence_IsStillUndecidable()
    {
        // The measurement that motivated this: two notes score a margin comparable to a
        // 48-note passage. The margin is honest about what it measures; only the evidence
        // count distinguishes the two situations.
        var twoNotes = KeyProfiler.DetectFromPitches("C4 G4");

        Assert.False(twoNotes.IsDecidable);
        Assert.Contains("undecided",
            KeyConfidenceDescription.Describe(twoNotes.Confidence, twoNotes.DistinctPitchClasses),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_ChordInput_SaysUndecidedRatherThanQuotingAMargin()
    {
        var (exit, output) = RunCli("analyze", "--notes", "C4 E4 G4 B4");

        Assert.Equal(0, exit);
        Assert.Contains("undecided", output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) RunCli(params string[] args)
    {
        var entryPoint = typeof(KeyConfidenceDescription).Assembly.EntryPoint!;
        var originalOut = Console.Out;
        var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            var result = entryPoint.Invoke(null, [args]);
            return (result is int code ? code : 0, captured.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
