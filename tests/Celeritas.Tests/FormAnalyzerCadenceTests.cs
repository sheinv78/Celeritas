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
    public void TheLeadingToneChordToTonic_IsNotCalledAuthentic()
    {
        // CadenceType.Authentic is defined as V → I, and ProgressionAdvisor.DetectCadence reports
        // no cadence for vii° → I; this analyzer used to call it authentic on its own — and, the
        // same arm firing on the degree alone, called the major subtonic of a minor key going to
        // i an authentic cadence from "vii°".
        Assert.Equal(CadenceType.None, CadenceOf(CMajor, [59, 62, 65], [60, 64, 67]));
        Assert.Equal(CadenceType.None, CadenceOf(CMinor, [70, 74, 77], [60, 63, 67]));   // Bb → Cm
    }

    [Fact]
    public void ACadenceIsReadTheSameWhicheverOrderTheFinalChordsNotesWereAdded()
    {
        // A held bass under a shorter chord is the ordinary shape of a final cadence, and the
        // notes of a chord arrive in whatever order the caller — or MusicXmlIo.Parse, or
        // MusicNotation.Parse — lists them. The analyzer used to walk back from the last note
        // in the list and stop at the first that ended earlier, so with the long bass entered
        // last the "chord" was one pitch and the V → I was not heard.
        foreach (var bassFirst in new[] { true, false })
        {
            var buffer = new NoteBuffer(8);
            void Chord(int at, int bass, int[] upper, Rational bassDuration)
            {
                var offset = new Rational(at, 4);
                if (bassFirst) buffer.AddNote(bass, offset, bassDuration);
                foreach (var p in upper) buffer.AddNote(p, offset, Rational.Quarter);
                if (!bassFirst) buffer.AddNote(bass, offset, bassDuration);
            }

            Chord(0, 55, [59, 62, 67], Rational.Quarter);   // G3 B3 D4 G4
            Chord(1, 48, [60, 64, 67], Rational.Half);      // C3 held under C4 E4 G4

            var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = CMajor });

            var cadence = Assert.Single(result.Cadences);
            Assert.Equal(CadenceType.Authentic, cadence.Type);
            Assert.Equal("V", cadence.FromChord);
            Assert.Equal("I", cadence.ToChord);
        }
    }

    [Fact]
    public void TheRomanNumeralsOfACadenceCarryTheirRealQuality()
    {
        // The analyzer kept a third copy of the numeral table that wrote "vii°" for any seventh
        // degree and "ii" for the diminished supertonic; it uses the one table now.
        using var buffer = PhraseOf([62, 65, 68], [67, 71, 74]);   // D° → G in C minor
        var result = FormAnalyzer.Analyze(buffer, FormAnalysisOptions.Default with { Key = CMinor });

        var cadence = Assert.Single(result.Cadences);
        Assert.Equal(CadenceType.Half, cadence.Type);
        Assert.Equal("ii°", cadence.FromChord);
        Assert.Equal("V", cadence.ToChord);
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
        // With the third in the bass — Ab under F and C, falling a semitone to G — it is the
        // Phrygian half cadence; with the root in the bass it is an ordinary half cadence, as
        // CadenceType.Phrygian documents and ProgressionAdvisor.DetectCadence answers. This test
        // used to pin the root-position shape as Phrygian.
        Assert.Equal(CadenceType.Phrygian, CadenceOf(CMinor, [56, 65, 72], [55, 59, 62]));
        Assert.Equal(CadenceType.Half, CadenceOf(CMinor, [65, 68, 72], [67, 71, 74]));
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
