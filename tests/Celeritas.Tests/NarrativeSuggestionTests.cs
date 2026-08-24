// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The advice a progression report gives back, and the note-event overload of the roman-numeral
/// analyzer. The advice is prose assembled from several independent rules; the only way to know
/// the intended rule fired is to ask for its sentence.
/// </summary>
public class NarrativeSuggestionTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static IReadOnlyList<string> Advice(params string[] chords) =>
        ProgressionAdvisor.Analyze(chords).Suggestions;

    // ---------- the roman-numeral analyzer's note-event overload ----------

    [Fact]
    public void ChordsGivenAsNoteEvents_AnalyzeLikeTheSamePitches()
    {
        NoteEvent[] notes =
        [
            new(67, Rational.Zero, Rational.Quarter),
            new(71, Rational.Zero, Rational.Quarter),
            new(74, Rational.Zero, Rational.Quarter),
        ];

        var fromEvents = KeyAnalyzer.Analyze(notes, CMajor);
        var fromPitches = KeyAnalyzer.Analyze([67, 71, 74], CMajor);

        Assert.Equal(fromPitches.Degree, fromEvents.Degree);
        Assert.Equal(fromPitches.Quality, fromEvents.Quality);
        Assert.Equal(ScaleDegree.V, fromEvents.Degree);
    }

    [Fact]
    public void NoNoteEventsAtAll_IsNotAChord()
    {
        Assert.False(KeyAnalyzer.Analyze(Array.Empty<NoteEvent>(), CMajor).IsValid);
    }

    [Fact]
    public void NullNoteEvents_AreRejectedRatherThanReadAsSilence()
    {
        Assert.Throws<ArgumentNullException>(() => KeyAnalyzer.Analyze((NoteEvent[])null!, CMajor));
    }

    [Fact]
    public void ALongChordStillAnalyzes()
    {
        // Past the stack-allocation threshold the analyzer copies to the heap instead; the
        // answer must not change.
        var notes = Enumerable.Range(0, 200)
            .Select(i => new NoteEvent(60 + (i % 3 == 0 ? 0 : i % 3 == 1 ? 4 : 7), Rational.Zero, Rational.Quarter))
            .ToArray();

        Assert.Equal(ScaleDegree.I, KeyAnalyzer.Analyze(notes, CMajor).Degree);
    }

    // ---------- what the report advises ----------

    [Fact]
    public void AHalfCadence_IsDescribedAsSuspense()
    {
        Assert.Contains(
            Advice("C", "F", "G"),
            s => s.Contains("Ending on the dominant (V) creates suspense", StringComparison.Ordinal));
    }

    [Fact]
    public void EndingOnTheDominantWithoutACadence_IsStillToldToResolve()
    {
        // Ab before G is chromatic, so the pair is not read as a half cadence — but the
        // progression still ends on V and still needs its tonic.
        Assert.Contains(
            Advice("C", "Ab", "G"),
            s => s.Contains("ends on the dominant", StringComparison.Ordinal)
                 && s.Contains("full closure", StringComparison.Ordinal));
    }

    [Fact]
    public void AProgressionEndingOnTheSubdominant_IsOfferedTheFullCadence()
    {
        Assert.Contains(Advice("C", "G", "F"), s => s.Contains("subdominant", StringComparison.Ordinal));
    }

    [Fact]
    public void AProgressionOnTwoChords_IsToldItIsThin()
    {
        Assert.Contains(
            Advice("C", "G", "C", "G"),
            s => s.Contains("few unique chords", StringComparison.Ordinal));
    }

    [Fact]
    public void AVariedProgression_IsNotToldItIsThin()
    {
        Assert.DoesNotContain(
            Advice("C", "Am", "F", "G", "Em", "Dm"),
            s => s.Contains("few unique chords", StringComparison.Ordinal));
    }

    [Fact]
    public void AProgressionResolvingWithoutADominant_IsOfferedTheTurnaround()
    {
        Assert.Contains(
            Advice("C", "Am", "F", "C"),
            s => s.Contains("ii-V-I", StringComparison.Ordinal));
    }

    [Fact]
    public void AnAuthenticCadenceIsPraisedRatherThanCorrected()
    {
        Assert.Contains(
            Advice("C", "F", "G", "C"),
            s => s.Contains("authentic cadence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NothingToAnalyze_MeansNoAdvice()
    {
        var report = ProgressionAdvisor.Analyze([]);

        Assert.Empty(report.Suggestions);
        Assert.Empty(report.Chords);
    }

    [Fact]
    public void EveryPieceOfAdviceIsASentence()
    {
        foreach (var suggestion in Advice("C", "F", "G", "Am", "D", "G"))
        {
            Assert.False(string.IsNullOrWhiteSpace(suggestion));
            Assert.DoesNotContain("  ", suggestion, StringComparison.Ordinal);
        }
    }

    // ---------- the narrative ----------

    [Fact]
    public void AChromaticChordIsDescribedAsSuchInTheNarrative()
    {
        // Db in C major has no diatonic function; the narrative must name it as colour rather
        // than force it into one of the three functions.
        var report = ProgressionAdvisor.Analyze(["C", "Db", "C"]);

        Assert.False(string.IsNullOrWhiteSpace(report.Narrative));
        Assert.Equal(3, report.Chords.Count);
    }

    [Fact]
    public void TheNarrativeCoversEveryChord()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C"]);

        Assert.False(string.IsNullOrWhiteSpace(report.Narrative));
        Assert.All(report.Chords, c => Assert.False(string.IsNullOrWhiteSpace(c.Function.ToString())));
    }
}
