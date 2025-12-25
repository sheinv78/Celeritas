using Celeritas.Core;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

public class MelodyHarmonizerTests
{
    [Fact]
    public void Harmonize_EmptyMelody_ReturnsEmptyResult()
    {
        var harmonizer = new MelodyHarmonizer();
        var result = harmonizer.Harmonize([]);

        Assert.Empty(result.Chords);
        Assert.Equal(0, result.TotalCost);
    }

    [Fact]
    public void Harmonize_SingleNote_ReturnsOneChord()
    {
        var harmonizer = new MelodyHarmonizer();
        var melody = new[] { new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.8f) }; // C4

        var result = harmonizer.Harmonize(melody);

        Assert.Single(result.Chords);
        Assert.Equal(0, result.Key.Root); // Should detect C major (root = 0)
    }

    [Fact]
    public void Harmonize_CMajorScale_DetectsCMajor()
    {
        var harmonizer = new MelodyHarmonizer();
        // C D E F G A B C
        var melody = new[]
        {
            new NoteEvent(60, new Rational(0, 4), Rational.Quarter, 0.8f),
            new NoteEvent(62, new Rational(1, 4), Rational.Quarter, 0.8f),
            new NoteEvent(64, new Rational(2, 4), Rational.Quarter, 0.8f),
            new NoteEvent(65, new Rational(3, 4), Rational.Quarter, 0.8f),
            new NoteEvent(67, new Rational(4, 4), Rational.Quarter, 0.8f),
            new NoteEvent(69, new Rational(5, 4), Rational.Quarter, 0.8f),
            new NoteEvent(71, new Rational(6, 4), Rational.Quarter, 0.8f),
            new NoteEvent(72, new Rational(7, 4), Rational.Quarter, 0.8f),
        };

        var result = harmonizer.Harmonize(melody);

        Assert.Equal(0, result.Key.Root); // C = 0
        Assert.True(result.Key.IsMajor);
        Assert.True(result.Chords.Count >= 1);
    }

    [Fact]
    public void Harmonize_WithSpecifiedKey_UsesProvidedKey()
    {
        var harmonizer = new MelodyHarmonizer();
        var melody = new[] { new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.8f) };
        var key = new KeySignature(7, true); // G major

        var result = harmonizer.Harmonize(melody, key);

        Assert.Equal(7, result.Key.Root); // G = 7
    }

    [Fact]
    public void Harmonize_SimpleProgression_ProducesReasonableChords()
    {
        var harmonizer = new MelodyHarmonizer();
        // Melody emphasizing I-V-I: C - G - C
        var melody = new[]
        {
            new NoteEvent(60, new Rational(0, 4), Rational.Half, 0.8f),  // C (beat 1-2)
            new NoteEvent(67, new Rational(2, 4), Rational.Half, 0.8f),  // G (beat 3-4)
            new NoteEvent(60, new Rational(4, 4), Rational.Half, 0.8f),  // C (beat 5-6)
        };

        var result = harmonizer.Harmonize(melody);

        Assert.True(result.Chords.Count >= 1);
        // First chord should likely be C-based
        var firstChord = result.Chords[0];
        Assert.Contains(firstChord.Chord.Root, new[] { "C", "A" }); // C major or A minor
    }

    [Fact]
    public void Harmonize_FromNoteBuffer_Works()
    {
        using var buffer = new NoteBuffer(10);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);
        buffer.AddNote(67, Rational.Half, Rational.Quarter);

        var harmonizer = new MelodyHarmonizer();
        var result = harmonizer.Harmonize(buffer);

        Assert.True(result.Chords.Count >= 1);
    }

    [Fact]
    public void DefaultCandidateProvider_GeneratesDiatonicChords()
    {
        var provider = new DefaultChordCandidateProvider();
        var key = new KeySignature(0, true); // C major
        int[] melodyPitches = [60]; // C

        var candidates = provider.GetCandidates(melodyPitches, key).ToList();

        // Should include chords containing C: I (C), iii (Em), vi (Am), IV (F)
        Assert.True(candidates.Count >= 1);
        Assert.Contains(candidates, c => c.Chord.Root == "C");
    }

    [Fact]
    public void DefaultTransitionScorer_PrefersStrongMotions()
    {
        var scorer = new DefaultTransitionScorer();
        var key = new KeySignature(0, true);

        var cMajor = new ChordCandidate(
            new ChordInfo(0, ChordQuality.Major),
            [60, 64, 67], 0, "I");
        var gMajor = new ChordCandidate(
            new ChordInfo(7, ChordQuality.Major),
            [67, 71, 74], 0, "V");
        var fMajor = new ChordCandidate(
            new ChordInfo(5, ChordQuality.Major),
            [65, 69, 72], 0, "IV");

        var vToI = scorer.ScoreTransition(gMajor, cMajor, key);
        var ivToI = scorer.ScoreTransition(fMajor, cMajor, key);

        // V->I should be at least as good as IV->I
        Assert.True(vToI <= ivToI + 0.5f);
    }

    [Fact]
    public void HarmonizationResult_GetSymbols_ReturnsChordNames()
    {
        var harmonizer = new MelodyHarmonizer();
        var melody = new[]
        {
            new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.8f),
            new NoteEvent(67, Rational.Quarter, Rational.Quarter, 0.8f),
        };

        var result = harmonizer.Harmonize(melody);
        var symbols = result.GetSymbols().ToList();

        Assert.True(symbols.Count >= 1);
        Assert.All(symbols, s => Assert.False(string.IsNullOrEmpty(s)));
    }
}
