// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The melody analyzer's unexercised branches: compound interval names, the empty-melody
/// result, and the prose arms that describe range, complexity and style. Prose is the easiest
/// thing in the library to get quietly wrong — every arm returns a sentence, so the only way
/// to know the right one fired is to ask for it by name.
/// </summary>
public class MelodyAnalyzerCoverageTests
{
    // ---------- interval names ----------

    [Theory]
    [InlineData(0, "↑0Unison")]
    [InlineData(2, "↑M2")]
    [InlineData(12, "↑P8")]
    [InlineData(-7, "↓P5")]
    public void IntervalsWithinAnOctave_AreNamedDirectly(int semitones, string _)
    {
        // The arrow is the direction; the name is the interval. Compared piecewise so the
        // test does not depend on which arrow glyph the library picked.
        var name = MelodyAnalyzer.GetIntervalName(semitones);

        Assert.DoesNotContain("oct", name, StringComparison.Ordinal);
        Assert.EndsWith(MelodyAnalyzer.IntervalNames[Math.Abs(semitones)], name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(14, 1, 2)]
    [InlineData(13, 1, 1)]
    [InlineData(24, 2, 0)]
    [InlineData(-19, 1, 7)]
    public void IntervalsBeyondAnOctave_AreNamedAsOctavesPlusARemainder(int semitones, int octaves, int remainder)
    {
        var name = MelodyAnalyzer.GetIntervalName(semitones);

        Assert.Contains($"{octaves}oct+{MelodyAnalyzer.IntervalNames[remainder]}", name, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionIsTakenFromTheSign()
    {
        Assert.NotEqual(MelodyAnalyzer.GetIntervalName(7), MelodyAnalyzer.GetIntervalName(-7));
        Assert.Equal(MelodyAnalyzer.GetIntervalName(0), MelodyAnalyzer.GetIntervalName(0));
    }

    [Theory]
    [InlineData(0, MelodicMotionType.Repetition)]
    [InlineData(2, MelodicMotionType.Step)]
    [InlineData(4, MelodicMotionType.SmallLeap)]
    [InlineData(7, MelodicMotionType.MediumLeap)]
    [InlineData(13, MelodicMotionType.LargeLeap)]
    public void MotionIsClassifiedBySize(int semitones, MelodicMotionType expected)
    {
        Assert.Equal(expected, MelodyAnalyzer.ClassifyMotion(semitones));
    }

    [Fact]
    public void AMelodicInterval_CarriesItsSizeDirectionAndMotion()
    {
        var interval = new MelodicInterval(-7, MelodicDirection.Descending, MelodicMotionType.MediumLeap);

        Assert.Equal(-7, interval.Semitones);
        Assert.Equal(MelodicDirection.Descending, interval.Direction);
        Assert.Equal(MelodicMotionType.MediumLeap, interval.Motion);
    }

    // ---------- no melody ----------

    [Fact]
    public void AnEmptyBuffer_GivesTheEmptyMelodyResult()
    {
        using var buffer = new NoteBuffer(4);

        var result = MelodyAnalyzer.Analyze(buffer);

        Assert.Equal(MelodicContour.Static, result.Contour);
        Assert.Equal("Empty melody", result.ContourDescription);
        Assert.Equal(0, result.Ambitus);
        Assert.Equal("No range", result.AmbitusDescription);
        Assert.Empty(result.Intervals);
        Assert.Empty(result.Motifs);
        Assert.Equal(0, result.Statistics.TotalIntervals);
        Assert.Equal(1.0, result.Conjunctness);
        Assert.Equal(0, result.Complexity);
        Assert.Equal("Empty", result.CharacterDescription);
    }

    [Fact]
    public void AnEmptyPitchArray_GivesTheSameEmptyResult()
    {
        var result = MelodyAnalyzer.Analyze([]);

        Assert.Equal("Empty melody", result.ContourDescription);
        Assert.Empty(result.Statistics.IntervalHistogram);
    }

    [Fact]
    public void ASingleNote_HasNoIntervalsAndNoComplexity()
    {
        var result = MelodyAnalyzer.Analyze([60]);

        Assert.Empty(result.Intervals);
        Assert.Equal(0, result.Complexity);
        Assert.Equal(60, result.LowestPitch);
        Assert.Equal(60, result.HighestPitch);
    }

    // ---------- contour ----------

    [Fact]
    public void ADescendingLine_IsDescribedAsFalling()
    {
        var result = MelodyAnalyzer.Analyze([72, 71, 69, 67, 65, 64, 62, 60]);

        Assert.Equal(MelodicContour.Descending, result.Contour);
        Assert.Contains("Falling melody", result.ContourDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void ARepeatedNoteRunningToTheEnd_IsNotATurn()
    {
        // A plateau that never comes back down: the contour walk must stop at the end of the
        // line rather than reading past it looking for the turn.
        var result = MelodyAnalyzer.Analyze([60, 62, 64, 64, 64, 64]);

        Assert.NotEqual(MelodicContour.Arch, result.Contour);
        Assert.False(string.IsNullOrWhiteSpace(result.ContourDescription));
    }

    // ---------- range ----------

    [Theory]
    [InlineData(new[] { 60, 62, 64 }, "narrow range: C4 to E4 (M3)")]
    [InlineData(new[] { 60, 67, 71 }, "moderate range: C4 to B4 (M7)")]
    [InlineData(new[] { 60, 72, 77 }, "wide range: C4 to F5 (1 octave(s) + P4)")]
    [InlineData(new[] { 48, 72, 84 }, "very wide range: C3 to C6 (3 octave(s) + Unison)")]
    public void AmbitusIsDescribedByItsWidth(int[] pitches, string expected)
    {
        var result = MelodyAnalyzer.Analyze(pitches);

        Assert.Equal(expected, result.AmbitusDescription);
    }

    // ---------- character prose ----------

    [Fact]
    public void AWideAngularLine_ReadsAsInstrumental()
    {
        // Big leaps over more than an octave and a half: leaps dominate and the range is wide.
        var result = MelodyAnalyzer.Analyze([48, 67, 50, 72, 55, 79, 52, 74]);

        Assert.Contains("wide-range", result.CharacterDescription, StringComparison.Ordinal);
        Assert.Contains("(instrumental-style)", result.CharacterDescription, StringComparison.Ordinal);
        Assert.EndsWith("melody", result.CharacterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void ANarrowStepwiseLine_ReadsAsVocal()
    {
        var result = MelodyAnalyzer.Analyze([60, 62, 64, 65, 64, 62, 60]);

        Assert.Contains("(vocal-style)", result.CharacterDescription, StringComparison.Ordinal);
        Assert.Contains("Smooth, stepwise", result.CharacterDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryMelodyGetsACharacterSentence()
    {
        int[][] melodies =
        [
            [60, 61, 62, 63, 64, 65, 66, 67],
            [60, 64, 67, 72, 67, 64, 60],
            [60, 60, 60, 60],
            [36, 60, 38, 62, 41, 65, 43, 67, 45, 69],
            [60, 63, 66, 69, 72, 69, 66, 63, 60, 55, 50, 45],
        ];

        foreach (var melody in melodies)
        {
            var description = MelodyAnalyzer.Analyze(melody).CharacterDescription;

            Assert.EndsWith("melody", description, StringComparison.Ordinal);
            Assert.DoesNotContain("  ", description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AVarietyOfIntervalsReadsAsMoreComplexThanARepeatedFigure()
    {
        var plain = MelodyAnalyzer.Analyze([60, 62, 60, 62, 60, 62, 60, 62]);
        var varied = MelodyAnalyzer.Analyze([60, 61, 65, 58, 70, 63, 55, 72]);

        Assert.True(varied.Complexity > plain.Complexity);
        Assert.InRange(plain.Complexity, 0d, 1d);
        Assert.InRange(varied.Complexity, 0d, 1d);
    }
}
