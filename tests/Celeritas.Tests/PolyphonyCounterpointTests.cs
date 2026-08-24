// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The counterpoint rule checks. Three of the four violation arms — parallel octaves, hidden
/// perfect intervals and large leaps — had never fired in the suite, so a broken condition
/// would have shown up as a clean bill of health rather than as a failing test. Voice
/// separation resists all three (it penalises leaps and crossings), so each case here is
/// built to leave the analyzer no other reading.
/// </summary>
public class PolyphonyCounterpointTests
{
    private static NoteEvent Q(int pitch, int quarter) => new(pitch, new Rational(quarter, 4), Rational.Quarter);

    // ---------- interval naming and classification ----------

    [Theory]
    [InlineData(0, "P1", IntervalQuality.PerfectConsonance)]
    [InlineData(1, "m2", IntervalQuality.SharpDissonance)]
    [InlineData(2, "M2", IntervalQuality.MildDissonance)]
    [InlineData(3, "m3", IntervalQuality.ImperfectConsonance)]
    [InlineData(4, "M3", IntervalQuality.ImperfectConsonance)]
    [InlineData(5, "P4", IntervalQuality.PerfectConsonance)]
    [InlineData(6, "TT", IntervalQuality.SharpDissonance)]
    [InlineData(7, "P5", IntervalQuality.PerfectConsonance)]
    [InlineData(8, "m6", IntervalQuality.ImperfectConsonance)]
    [InlineData(9, "M6", IntervalQuality.ImperfectConsonance)]
    [InlineData(10, "m7", IntervalQuality.MildDissonance)]
    [InlineData(11, "M7", IntervalQuality.SharpDissonance)]
    public void EveryIntervalClass_IsNamedAndClassified(int semitones, string name, IntervalQuality quality)
    {
        var interval = new VoiceInterval { Pitch1 = 60 + semitones, Pitch2 = 60 };

        Assert.Equal(semitones, interval.Interval);
        Assert.Equal(semitones, interval.RawInterval);
        Assert.Equal(quality, interval.Quality);
        Assert.Equal($"{name} ({quality})", interval.ToString());
    }

    [Fact]
    public void AnOctave_ReadsAsAUnisonClass_ButKeepsItsRawDistance()
    {
        var interval = new VoiceInterval { Pitch1 = 72, Pitch2 = 60 };

        Assert.Equal(0, interval.Interval);
        Assert.Equal(12, interval.RawInterval);
        Assert.Equal(IntervalQuality.PerfectConsonance, interval.Quality);
    }

    [Fact]
    public void IntervalIsDirectionless()
    {
        Assert.Equal(
            new VoiceInterval { Pitch1 = 67, Pitch2 = 60 }.Interval,
            new VoiceInterval { Pitch1 = 60, Pitch2 = 67 }.Interval);
    }

    // ---------- the four violation arms ----------

    [Fact]
    public void ParallelFifths_AreAnError()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(67, 0), Q(60, 0), Q(69, 1), Q(62, 1) });

        Assert.Equal(1, result.ParallelFifths);
        var violation = Assert.Single(result.Violations, v => v.Type == "Parallel Fifths");
        Assert.Equal("Error", violation.Severity);
        Assert.Equal(Rational.Zero, violation.Time);
    }

    [Fact]
    public void ParallelOctaves_AreAnError()
    {
        // Both voices rise a whole step an octave apart.
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(72, 0), Q(60, 0), Q(74, 1), Q(62, 1) });

        Assert.Equal(1, result.ParallelOctaves);
        var violation = Assert.Single(result.Violations, v => v.Type == "Parallel Octaves");
        Assert.Equal("Error", violation.Severity);
        Assert.Contains("octaves", violation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void SimilarMotionIntoAPerfectFifth_InTheOuterVoices_IsAHiddenPerfectInterval()
    {
        // C5/G3 to G5/C4: both voices rise, by different amounts, and land on a perfect
        // fifth. That is the classic direct fifth.
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(72, 0), Q(55, 0), Q(79, 1), Q(60, 1) });

        Assert.Equal(1, result.HiddenParallels);
        var violation = Assert.Single(result.Violations, v => v.Type == "Hidden Perfect Interval");
        Assert.Equal("Warning", violation.Severity);
        Assert.Contains("fifth", violation.Description, StringComparison.Ordinal);
        Assert.Equal(0, violation.Voice1);          // only the outer voices are judged
    }

    [Fact]
    public void SimilarMotionIntoAnOctave_IsReportedAsAnOctave_NotAFifth()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(60, 0), Q(48, 0), Q(74, 1), Q(50, 1) }, maxVoices: 2);

        var violation = Assert.Single(result.Violations, v => v.Type == "Hidden Perfect Interval");
        Assert.Contains("octave", violation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ALeapOfMoreThanAnOctave_IsAStyleViolation_NamingTheVoiceThatLeapt()
    {
        // Two voices only, so the separator cannot hide the leap by opening a third voice.
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(72, 0), Q(48, 0), Q(85, 1), Q(50, 1) }, maxVoices: 2);

        var violation = Assert.Single(result.Violations, v => v.Type == "Large Leap");
        Assert.Equal("Style", violation.Severity);
        Assert.Equal("Voice 1 leaps more than an octave", violation.Description);
    }

    [Fact]
    public void ALeapInTheLowerVoice_NamesThatVoice()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { new NoteEvent(60, Rational.Zero, Rational.Whole), Q(55, 0), Q(70, 1) },
            maxVoices: 2);

        var violation = Assert.Single(result.Violations, v => v.Type == "Large Leap");
        Assert.Equal("Voice 2 leaps more than an octave", violation.Description);
    }

    [Fact]
    public void CleanContraryMotion_ProducesNoViolationsAtAll()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(64, 0), Q(60, 0), Q(62, 1), Q(65, 1) });

        Assert.Empty(result.Violations);
    }

    // ---------- crossings and spacing ----------

    [Fact]
    public void AVoiceSoundingBelowTheOneAboveIt_CountsAsACrossing()
    {
        // The lower voice climbs above the sustained upper one while it is still sounding.
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { new NoteEvent(60, Rational.Zero, Rational.Whole), Q(55, 0), Q(70, 1) },
            maxVoices: 2);

        Assert.Equal(1, result.VoiceCrossing);
    }

    [Fact]
    public void MoreThanAnOctaveBetweenAdjacentUpperVoices_IsASpacingViolation()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(90, 0), Q(40, 0), Q(90, 1), Q(40, 1) });

        Assert.Equal(2, result.SpacingViolations);      // one per onset
    }

    [Fact]
    public void CrossingsAndSpacingPushTheScoreDown()
    {
        var clean = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(64, 0), Q(60, 0), Q(62, 1), Q(65, 1) });
        var wide = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(90, 0), Q(40, 0), Q(90, 1), Q(40, 1) });

        Assert.True(wide.QualityScore < clean.QualityScore);
        Assert.InRange(wide.QualityScore, 0f, 1f);
    }

    // ---------- the enumerable overloads ----------

    [Fact]
    public void CheckCounterpointRules_AcceptsAnEnumerableThatIsNotAnArray()
    {
        var asList = new List<NoteEvent> { Q(67, 0), Q(60, 0), Q(69, 1), Q(62, 1) };

        var fromList = PolyphonyAnalyzer.CheckCounterpointRules(asList);
        var fromArray = PolyphonyAnalyzer.CheckCounterpointRules(asList.ToArray());

        Assert.Equal(fromArray.ParallelFifths, fromList.ParallelFifths);
        Assert.Equal(fromArray.QualityScore, fromList.QualityScore);
    }

    [Fact]
    public void CheckCounterpointRules_EmptyInput_IsSpotless()
    {
        var result = PolyphonyAnalyzer.CheckCounterpointRules(Array.Empty<NoteEvent>());

        Assert.Empty(result.Violations);
        Assert.Equal(0, result.VoiceCrossing);
        Assert.Equal(0, result.SpacingViolations);
    }

    [Fact]
    public void CheckCounterpointRules_NullNotes_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => PolyphonyAnalyzer.CheckCounterpointRules((IEnumerable<NoteEvent>)null!));
    }

    [Fact]
    public void DetectImitation_AcceptsAnEnumerableThatIsNotAnArray_AndFindsTheCanon()
    {
        // The same four-note subject, an octave lower and half a whole-note later.
        var canon = new List<NoteEvent>
        {
            Q(60, 0), Q(62, 1), Q(64, 2), Q(65, 3),
            Q(48, 2), Q(50, 3), Q(52, 4), Q(53, 5),
        };

        var result = PolyphonyAnalyzer.DetectImitation(canon);

        Assert.True(result.HasImitation);
        Assert.Equal("Canon", result.Type);
        Assert.Equal(new Rational(1, 2), result.TimeDelay);
        Assert.Equal(2, result.VoicesInvolved.Count);
    }

    [Fact]
    public void DetectImitation_ASingleVoice_FindsNothing()
    {
        var result = PolyphonyAnalyzer.DetectImitation(new List<NoteEvent> { Q(60, 0), Q(62, 1) });

        Assert.False(result.HasImitation);
        Assert.Empty(result.VoicesInvolved);
    }

    [Fact]
    public void DetectImitation_NullNotes_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => PolyphonyAnalyzer.DetectImitation((IEnumerable<NoteEvent>)null!));
    }

    // ---------- the consonance balance bonus ----------

    [Fact]
    public void AMixOfConsonanceAndDissonance_IsScoredAsBalanced()
    {
        // M3, P5, M6, m2 over a repeated bass: three consonances to one dissonance, which is
        // the band the quality score rewards.
        var result = PolyphonyAnalyzer.CheckCounterpointRules(
            new[] { Q(64, 0), Q(60, 0), Q(67, 1), Q(60, 1), Q(69, 2), Q(60, 2), Q(61, 3), Q(60, 3) });

        Assert.InRange(result.QualityScore, 0f, 1f);
        Assert.True(result.QualityScore > 0f);
    }
}
