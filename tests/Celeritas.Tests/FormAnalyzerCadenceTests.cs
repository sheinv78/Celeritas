// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Form analysis: the default options (nothing had ever called <c>Analyze</c> without passing
/// its own), the empty result, the section accessors, and the cadence table. A cadence named
/// wrongly still reads as a cadence, so each arm is asked for by name.
/// </summary>
public class FormAnalyzerCadenceTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature CMinor = new(0, false);

    /// <summary>Builds a phrase of block chords, one quarter each, from the given pitch sets.</summary>
    private static NoteBuffer PhraseOf(params int[][] chords)
    {
        var buffer = new NoteBuffer(Math.Max(4, chords.Sum(c => c.Length)));
        for (var i = 0; i < chords.Length; i++)
            foreach (var pitch in chords[i])
                buffer.AddNote(pitch, new Rational(i, 4), Rational.Quarter);
        return buffer;
    }

    private static CadenceType CadenceOf(KeySignature key, params int[][] chords)
    {
        using var buffer = PhraseOf(chords);
        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = key });

        return result.Cadences.Count > 0 ? result.Cadences[0].Type : CadenceType.None;
    }

    // ---------- defaults and degenerate input ----------

    [Fact]
    public void TheDefaultOptionsAreUsedWhenNoneAreGiven()
    {
        using var buffer = PhraseOf([60, 64, 67], [67, 71, 74], [60, 64, 67]);

        var withDefaults = FormAnalyzer.Analyze(buffer);
        var withExplicitDefaults = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default);

        Assert.Equal(withExplicitDefaults.Phrases.Count, withDefaults.Phrases.Count);
        Assert.Equal(withExplicitDefaults.FormLabel, withDefaults.FormLabel);
        Assert.Equal(withExplicitDefaults.TotalLength, withDefaults.TotalLength);
    }

    [Fact]
    public void TheDefaultOptionsSayWhatTheyDocument()
    {
        var defaults = FormAnalysisOptions.Default;

        Assert.Equal(new Rational(1, 2), defaults.MinRestForPhraseBoundary);
        Assert.Equal(2, defaults.MinNotesPerPhrase);
        Assert.Equal(new Rational(1, 4), defaults.PeriodLengthTolerance);
        Assert.True(defaults.DetectCadences);
        Assert.Null(defaults.Key);
        Assert.True(defaults.DetectSections);
        Assert.Equal(0.7f, defaults.SectionSimilarityThreshold);
    }

    [Fact]
    public void AnEmptyBuffer_AnalyzesToAnEmptyForm()
    {
        using var buffer = new NoteBuffer(4);

        var result = FormAnalyzer.Analyze(buffer);

        Assert.Empty(result.Phrases);
        Assert.Empty(result.Periods);
        Assert.Empty(result.Cadences);
        Assert.Empty(result.Sections);
        Assert.Equal(Rational.Zero, result.TotalLength);
        Assert.Equal("", result.FormLabel);
    }

    [Fact]
    public void ANullBuffer_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FormAnalyzer.Analyze(null!));
    }

    // ---------- the cadence table ----------

    [Fact]
    public void DominantToTonic_IsAuthentic()
    {
        Assert.Equal(CadenceType.Authentic, CadenceOf(CMajor, [67, 71, 74], [60, 64, 67]));
    }

    [Fact]
    public void TheLeadingToneChordToTonic_IsAlsoAuthentic()
    {
        // vii° stands in for the dominant, so it cadences the same way.
        Assert.Equal(CadenceType.Authentic, CadenceOf(CMajor, [59, 62, 65], [60, 64, 67]));
    }

    [Fact]
    public void SubdominantToTonic_IsPlagal()
    {
        Assert.Equal(CadenceType.Plagal, CadenceOf(CMajor, [65, 69, 72], [60, 64, 67]));
    }

    [Fact]
    public void DominantToSubmediant_IsDeceptive()
    {
        Assert.Equal(CadenceType.Deceptive, CadenceOf(CMajor, [67, 71, 74], [69, 72, 76]));
    }

    [Fact]
    public void AnythingElseArrivingOnTheDominant_IsAHalfCadence()
    {
        Assert.Equal(CadenceType.Half, CadenceOf(CMajor, [62, 65, 69], [67, 71, 74]));   // ii - V
        Assert.Equal(CadenceType.Half, CadenceOf(CMajor, [64, 67, 71], [67, 71, 74]));   // iii - V
    }

    [Fact]
    public void MinorSubdominantToDominantInMinor_IsAPhrygianHalfCadence()
    {
        Assert.Equal(CadenceType.Phrygian, CadenceOf(CMinor, [65, 68, 72], [67, 71, 74]));
    }

    [Fact]
    public void AProgressionThatCadencesNowhere_IsNotGivenACadence()
    {
        Assert.Equal(CadenceType.None, CadenceOf(CMajor, [60, 64, 67], [62, 65, 69]));   // I - ii
    }

    [Fact]
    public void ACadenceIsDescribedByItsRomanNumerals()
    {
        using var buffer = PhraseOf([67, 71, 74], [69, 72, 76]);

        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = CMajor });

        var cadence = Assert.Single(result.Cadences);
        Assert.Contains("V", cadence.Description, StringComparison.Ordinal);
        Assert.Contains("vi", cadence.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAKey_NoCadencesAreClaimed()
    {
        using var buffer = PhraseOf([67, 71, 74], [60, 64, 67]);

        Assert.Empty(FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default).Cadences);
    }

    [Fact]
    public void WithCadenceDetectionOff_NoneAreReported()
    {
        using var buffer = PhraseOf([67, 71, 74], [60, 64, 67]);

        var result = FormAnalyzer.Analyze(
            buffer, FormAnalysisOptions.Default with { Key = CMajor, DetectCadences = false });

        Assert.Empty(result.Cadences);
    }

    // ---------- sections ----------

    [Fact]
    public void ASectionKnowsItsLengthAndHowManyPhrasesItHolds()
    {
        // Two phrases separated by a rest longer than the boundary threshold.
        var buffer = new NoteBuffer(8);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(62, Rational.Quarter, Rational.Quarter);
        buffer.AddNote(64, new Rational(3, 2), Rational.Quarter);
        buffer.AddNote(65, new Rational(7, 4), Rational.Quarter);

        using (buffer)
        {
            var result = FormAnalyzer.Analyze(buffer);

            Assert.NotEmpty(result.Sections);
            Assert.All(result.Sections, s =>
            {
                Assert.True(s.PhraseCount >= 1, "a section held no phrases");
                Assert.True(s.Length > Rational.Zero, "a section had no length");
                Assert.Equal(s.End - s.Start, s.Length);
                Assert.Equal(s.EndPhraseIndex - s.StartPhraseIndex + 1, s.PhraseCount);
            });
            Assert.False(string.IsNullOrWhiteSpace(result.FormLabel));
        }
    }

    [Fact]
    public void WithSectionDetectionOff_TheFormLabelIsEmpty()
    {
        using var buffer = PhraseOf([60, 64, 67], [67, 71, 74]);

        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { DetectSections = false });

        Assert.Empty(result.Sections);
        Assert.Equal("", result.FormLabel);
    }
}
