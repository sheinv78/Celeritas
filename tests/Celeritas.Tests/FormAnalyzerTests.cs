using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class FormAnalyzerTests
{
    [Fact]
    public void Analyze_SplitsPhrases_OnLongRest()
    {
        // Two phrases separated by a rest >= 1/2 beat.
        using var buffer = new NoteBuffer(6);

        // Phrase 1: 3 notes within beat 0..1
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4));
        buffer.AddNote(62, new Rational(1, 4), new Rational(1, 4));
        buffer.AddNote(64, new Rational(2, 4), new Rational(1, 4));

        // Rest: from 3/4 to 5/4 => 1/2 beat

        // Phrase 2: 3 notes starting at 5/4
        buffer.AddNote(65, new Rational(5, 4), new Rational(1, 4));
        buffer.AddNote(67, new Rational(6, 4), new Rational(1, 4));
        buffer.AddNote(69, new Rational(7, 4), new Rational(1, 4));

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(MinRestForPhraseBoundary: new Rational(1, 2), MinNotesPerPhrase: 2));

        Assert.Equal(2, result.Phrases.Count);
        Assert.Equal(3, result.Phrases[0].NoteCount);
        Assert.Equal(3, result.Phrases[1].NoteCount);
        Assert.Equal(new Rational(0, 1), result.Phrases[0].Start);
        Assert.Equal(new Rational(5, 4), result.Phrases[1].Start);
    }

    [Fact]
    public void Analyze_DetectsPeriod_WhenTwoConsecutivePhrasesSimilarLength()
    {
        using var buffer = new NoteBuffer(4);

        // Phrase A: 2 notes spanning 1 beat
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 2));
        buffer.AddNote(62, new Rational(1, 2), new Rational(1, 2));

        // Rest boundary 1/2 beat
        buffer.AddNote(64, new Rational(3, 2), new Rational(1, 2));
        buffer.AddNote(65, new Rational(2, 1), new Rational(1, 2));

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            PeriodLengthTolerance: new Rational(1, 4)));

        Assert.Equal(2, result.Phrases.Count);
        Assert.Single(result.Periods);
        Assert.Equal(0, result.Periods[0].FirstPhraseIndex);
        Assert.Equal(1, result.Periods[0].SecondPhraseIndex);
    }

    [Fact]
    public void Analyze_DetectsAuthenticCadence_WhenPhraseEndsWithVToI()
    {
        // Phrase ending with G major (V) -> C major (I) in C major
        using var buffer = new NoteBuffer(12);

        // Melodic notes at start
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C
        buffer.AddNote(62, new Rational(1, 4), new Rational(1, 4)); // D
        buffer.AddNote(64, new Rational(1, 2), new Rational(1, 4)); // E

        // G major chord (V) - beat 1
        buffer.AddNote(55, new Rational(1, 1), new Rational(1, 2)); // G
        buffer.AddNote(59, new Rational(1, 1), new Rational(1, 2)); // B
        buffer.AddNote(62, new Rational(1, 1), new Rational(1, 2)); // D

        // C major chord (I) - beat 1.5
        buffer.AddNote(48, new Rational(3, 2), new Rational(1, 2)); // C
        buffer.AddNote(52, new Rational(3, 2), new Rational(1, 2)); // E
        buffer.AddNote(55, new Rational(3, 2), new Rational(1, 2)); // G

        var key = new KeySignature("C", true);
        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectCadences: true,
            Key: key));

        Assert.Single(result.Phrases);
        Assert.Equal(CadenceType.Authentic, result.Phrases[0].EndingCadence);
        Assert.Single(result.Cadences);
        Assert.Equal(CadenceType.Authentic, result.Cadences[0].Type);
    }

    [Fact]
    public void Analyze_DetectsPlagalCadence_WhenPhraseEndsWithIVToI()
    {
        // Phrase ending with F major (IV) -> C major (I) in C major
        using var buffer = new NoteBuffer(9);

        // Melodic notes
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C

        // F major chord (IV)
        buffer.AddNote(53, new Rational(1, 2), new Rational(1, 2)); // F
        buffer.AddNote(57, new Rational(1, 2), new Rational(1, 2)); // A
        buffer.AddNote(60, new Rational(1, 2), new Rational(1, 2)); // C

        // C major chord (I)
        buffer.AddNote(48, new Rational(1, 1), new Rational(1, 2)); // C
        buffer.AddNote(52, new Rational(1, 1), new Rational(1, 2)); // E
        buffer.AddNote(55, new Rational(1, 1), new Rational(1, 2)); // G

        var key = new KeySignature("C", true);
        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectCadences: true,
            Key: key));

        Assert.Single(result.Phrases);
        Assert.Equal(CadenceType.Plagal, result.Phrases[0].EndingCadence);
    }

    [Fact]
    public void Analyze_DetectsHalfCadence_WhenPhraseEndsOnV()
    {
        // Phrase ending with any -> V in C major
        using var buffer = new NoteBuffer(9);

        // Melodic notes
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C

        // C major chord (I)
        buffer.AddNote(48, new Rational(1, 2), new Rational(1, 2)); // C
        buffer.AddNote(52, new Rational(1, 2), new Rational(1, 2)); // E
        buffer.AddNote(55, new Rational(1, 2), new Rational(1, 2)); // G

        // G major chord (V) - ends on dominant
        buffer.AddNote(55, new Rational(1, 1), new Rational(1, 2)); // G
        buffer.AddNote(59, new Rational(1, 1), new Rational(1, 2)); // B
        buffer.AddNote(62, new Rational(1, 1), new Rational(1, 2)); // D

        var key = new KeySignature("C", true);
        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectCadences: true,
            Key: key));

        Assert.Single(result.Phrases);
        Assert.Equal(CadenceType.Half, result.Phrases[0].EndingCadence);
    }

    [Fact]
    public void Analyze_NoCadence_WhenKeyNotProvided()
    {
        using var buffer = new NoteBuffer(6);

        buffer.AddNote(55, new Rational(0, 1), new Rational(1, 2)); // G
        buffer.AddNote(59, new Rational(0, 1), new Rational(1, 2)); // B
        buffer.AddNote(62, new Rational(0, 1), new Rational(1, 2)); // D

        buffer.AddNote(48, new Rational(1, 2), new Rational(1, 2)); // C
        buffer.AddNote(52, new Rational(1, 2), new Rational(1, 2)); // E
        buffer.AddNote(55, new Rational(1, 2), new Rational(1, 2)); // G

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectCadences: true,
            Key: null));

        Assert.Single(result.Phrases);
        Assert.Equal(CadenceType.None, result.Phrases[0].EndingCadence);
        Assert.Empty(result.Cadences);
    }

    [Fact]
    public void Analyze_DetectsABAForm_WhenFirstAndThirdPhrasesAreSimilar()
    {
        // Create A-B-A form: first and third phrases use similar pitch classes
        using var buffer = new NoteBuffer(12);

        // Phrase A: C, E, G (C major arpeggio)
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C
        buffer.AddNote(64, new Rational(1, 4), new Rational(1, 4)); // E
        buffer.AddNote(67, new Rational(1, 2), new Rational(1, 4)); // G

        // Rest: 3/4 to 5/4

        // Phrase B: D, F, A (D minor arpeggio - different pitch classes)
        buffer.AddNote(62, new Rational(5, 4), new Rational(1, 4)); // D
        buffer.AddNote(65, new Rational(6, 4), new Rational(1, 4)); // F
        buffer.AddNote(69, new Rational(7, 4), new Rational(1, 4)); // A

        // Rest: 8/4 to 10/4

        // Phrase A': C, E, G again (same pitch classes as A)
        buffer.AddNote(60, new Rational(10, 4), new Rational(1, 4)); // C
        buffer.AddNote(64, new Rational(11, 4), new Rational(1, 4)); // E
        buffer.AddNote(67, new Rational(12, 4), new Rational(1, 4)); // G

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectSections: true,
            SectionSimilarityThreshold: 0.7f));

        Assert.Equal(3, result.Phrases.Count);
        Assert.Equal(3, result.Sections.Count);
        Assert.Equal("A B A", result.FormLabel);
        Assert.Equal('A', result.Sections[0].Label);
        Assert.Equal('B', result.Sections[1].Label);
        Assert.Equal('A', result.Sections[2].Label);
    }

    [Fact]
    public void Analyze_DetectsSingleSection_WhenAllPhrasesAreSimilar()
    {
        // All phrases use same pitch classes
        using var buffer = new NoteBuffer(6);

        // Phrase 1: C, E
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C
        buffer.AddNote(64, new Rational(1, 4), new Rational(1, 4)); // E

        // Rest

        // Phrase 2: C, E again
        buffer.AddNote(60, new Rational(1, 1), new Rational(1, 4)); // C
        buffer.AddNote(64, new Rational(5, 4), new Rational(1, 4)); // E

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectSections: true,
            SectionSimilarityThreshold: 0.7f));

        Assert.Equal(2, result.Phrases.Count);
        // Similar phrases should merge into single A section
        Assert.Single(result.Sections);
        Assert.Equal("A", result.FormLabel);
    }

    [Fact]
    public void Analyze_DetectsABForm_WhenPhrasesAreDifferent()
    {
        using var buffer = new NoteBuffer(6);

        // Phrase A: C major (C, E, G)
        buffer.AddNote(60, new Rational(0, 1), new Rational(1, 4)); // C
        buffer.AddNote(64, new Rational(1, 4), new Rational(1, 4)); // E
        buffer.AddNote(67, new Rational(1, 2), new Rational(1, 4)); // G

        // Rest

        // Phrase B: F# diminished (F#, A, C) - very different pitch classes
        buffer.AddNote(66, new Rational(5, 4), new Rational(1, 4)); // F#
        buffer.AddNote(69, new Rational(6, 4), new Rational(1, 4)); // A
        buffer.AddNote(72, new Rational(7, 4), new Rational(1, 4)); // C (octave up)

        var result = FormAnalyzer.Analyze(buffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 2),
            MinNotesPerPhrase: 2,
            DetectSections: true,
            SectionSimilarityThreshold: 0.7f));

        Assert.Equal(2, result.Phrases.Count);
        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("A B", result.FormLabel);
    }
}
