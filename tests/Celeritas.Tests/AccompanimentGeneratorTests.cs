using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Harmonization;
using Xunit;

namespace Celeritas.Tests;

public sealed class AccompanimentGeneratorTests
{
    [Fact]
    public void Generate_FromRoman_Block_ProducesBassPlusChordTones()
    {
        var key = new KeySignature("C", isMajor: true);
        var progression = new[]
        {
            new HarmonicRhythmItem(new RomanNumeralChord(ScaleDegree.I, ChordQuality.Major, HarmonicFunction.Tonic), Rational.Whole),
            new HarmonicRhythmItem(new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant), Rational.Whole)
        };

        var options = AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Block,
            BassOctave = 2,
            ChordOctave = 4,
            MaxChordTones = 4
        };

        var events = AccompanimentGenerator.Generate(progression, key, options);

        // Segment 1: 1 bass + 3 tones; Segment 2: 1 bass + 4 tones
        Assert.Equal(9, events.Length);

        // First segment starts at 0 and lasts 1/1.
        Assert.Equal(Rational.Zero, events[0].Offset);
        Assert.Equal(Rational.Whole, events[0].Duration);

        // Second segment starts at 1/1.
        Assert.Equal(Rational.Whole, events[4].Offset);
        Assert.Equal(Rational.Whole, events[4].Duration);

        // Pitch-class sanity checks.
        // C major: bass C2 (36) and chord includes C/E/G (60/64/67-ish)
        Assert.Equal(0, events[0].Pitch % 12);

        // V7 in C: bass G2 (43) and chord includes G/B/D/F
        Assert.Equal(7, events[4].Pitch % 12);
    }

    [Fact]
    public void Generate_FromChordAssignments_Arpeggio_UsesSubdivisionUntilEnd()
    {
        var chords = new[]
        {
            new ChordAssignment(
                Start: Rational.Zero,
                End: new Rational(1, 1),
                Chord: new ChordInfo(0, ChordQuality.Major),
                Pitches: new[] { 60, 64, 67 },
                Cost: 0,
                Rationale: null)
        };

        var options = AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = Rational.Quarter,
            BassOctave = 2,
            ChordOctave = 4
        };

        var events = AccompanimentGenerator.Generate(chords, options);

        // For 1/1 duration with 1/4 step -> 4 events (bass + 3 tones).
        Assert.Equal(4, events.Length);
        Assert.Equal(Rational.Zero, events[0].Offset);
        Assert.Equal(new Rational(1, 4), events[0].Duration);
        Assert.Equal(new Rational(1, 4), events[1].Offset);
        Assert.Equal(new Rational(1, 2), events[2].Offset);
        Assert.Equal(new Rational(3, 4), events[3].Offset);

        // First event is bass.
        Assert.Equal(0, events[0].Pitch % 12);
    }
}
