using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using Xunit;

namespace Celeritas.Tests;

public class HarmonicColorAnalyzerTests
{
    [Fact]
    public void Analyze_ChromaticNotes_DetectsNonDiatonicPitch()
    {
        var key = new KeySignature(0, true); // C major

        var melody = new[]
        {
            new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.8f), // C
            new NoteEvent(66, Rational.Quarter, Rational.Quarter, 0.8f), // F# (#4)
            new NoteEvent(67, Rational.Half, Rational.Quarter, 0.8f), // G
        };

        var chords = new[]
        {
            new ChordAssignment(Rational.Zero, Rational.Whole, new ChordInfo(0, ChordQuality.Major), new[] { 60, 64, 67 }, 0)
        };

        var analysis = HarmonicColorAnalyzer.Analyze(melody, chords, key);

        Assert.Single(analysis.ChromaticNotes);
        Assert.Equal(66, analysis.ChromaticNotes[0].Pitch);
        Assert.Equal(6, analysis.ChromaticNotes[0].PitchClass);
        Assert.Equal("#4", analysis.ChromaticNotes[0].Alteration);
    }

    [Fact]
    public void Analyze_MelodicHarmony_ClassifiesPassingTone()
    {
        var key = new KeySignature(0, true); // C major

        // Melody: C - D - E over C major triad.
        var melody = new[]
        {
            new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.8f),
            new NoteEvent(62, Rational.Quarter, Rational.Quarter, 0.8f),
            new NoteEvent(64, Rational.Half, Rational.Quarter, 0.8f),
        };

        var chords = new[]
        {
            new ChordAssignment(Rational.Zero, Rational.Whole, new ChordInfo(0, ChordQuality.Major), new[] { 60, 64, 67 }, 0)
        };

        var analysis = HarmonicColorAnalyzer.Analyze(melody, chords, key);

        Assert.Equal(3, analysis.MelodicHarmony.Count);
        Assert.Equal(MelodicHarmonyEventType.ChordTone, analysis.MelodicHarmony[0].Type);
        Assert.Equal(MelodicHarmonyEventType.PassingTone, analysis.MelodicHarmony[1].Type);
        Assert.Equal(MelodicHarmonyEventType.ChordTone, analysis.MelodicHarmony[2].Type);
    }

    [Fact]
    public void Analyze_ModalTurns_DetectsMixolydianLikeMixture()
    {
        var key = new KeySignature(0, true); // C major

        // Progression: C - Bb - F - C (bVII suggests Mixolydian color)
        var chords = new[]
        {
            ChordAt(0, "C"),
            ChordAt(1, "Bb"),
            ChordAt(2, "F"),
            ChordAt(3, "C"),
        };

        // Melody doesn't matter much for this test.
        var melody = new[]
        {
            new NoteEvent(60, Rational.Zero, Rational.Whole, 0.8f)
        };

        var analysis = HarmonicColorAnalyzer.Analyze(melody, chords, key);

        Assert.True(analysis.ModalTurns.Count > 0);
        Assert.Contains(analysis.ModalTurns, t => t.Mode == Mode.Mixolydian);
        Assert.Contains(analysis.ModalTurns, t => t.OutOfKeyPitchClasses.Contains((byte)10)); // Bb
    }

    private static ChordAssignment ChordAt(int index, string symbol)
    {
        var start = new Rational(index, 1);
        var end = new Rational(index + 1, 1);
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
        var mask = ChordAnalyzer.GetMask(pitches);
        var info = ChordLibrary.GetChord(mask);
        return new ChordAssignment(start, end, info, pitches, 0);
    }
}
