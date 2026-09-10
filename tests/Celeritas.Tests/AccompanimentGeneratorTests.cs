using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

public sealed class AccompanimentGeneratorTests
{
    // AccompanimentOptions.Default: BassOctave=2 (C2=36), ChordOctave=4 (middle C), MaxChordTones=4
    private const int PitchClassG = 7;   // G in mod-12
    private const int PitchClassC = 0;   // C in mod-12

    [Fact]
    public void Generate_FromRoman_Block_ProducesBassPlusChordTones()
    {
        var key = new KeySignature("C", isMajor: true);
        var progression = new[]
        {
            new HarmonicRhythmItem(new RomanNumeralChord(ScaleDegree.I,  ChordQuality.Major,     HarmonicFunction.Tonic),    Rational.Whole),
            new HarmonicRhythmItem(new RomanNumeralChord(ScaleDegree.V,  ChordQuality.Dominant7, HarmonicFunction.Dominant), Rational.Whole)
        };

        // All values match AccompanimentOptions.Default — no override needed.
        var options = AccompanimentOptions.Default;

        var events = AccompanimentGenerator.Generate(progression, key, options);

        // Segment 1 (I):  1 bass + 3 chord tones = 4
        // Segment 2 (V7): 1 bass + 4 chord tones = 5  →  total = 9
        const int expectedEventCount = 4 + 5;
        Assert.Equal(expectedEventCount, events.Length);

        // First segment starts at 0 and lasts 1/1.
        Assert.Equal(Rational.Zero, events[0].Offset);
        Assert.Equal(Rational.Whole, events[0].Duration);

        // Second segment starts at 1/1.
        const int secondSegmentStart = 4; // index after first segment's 4 events
        Assert.Equal(Rational.Whole, events[secondSegmentStart].Offset);
        Assert.Equal(Rational.Whole, events[secondSegmentStart].Duration);

        // Pitch-class sanity: C major bass = C (0), V7 bass = G (7)
        Assert.Equal(PitchClassC, events[0].Pitch % 12);
        Assert.Equal(PitchClassG, events[secondSegmentStart].Pitch % 12);
    }

    [Fact]
    public void Generate_FromChordAssignments_FoldsNegativePitchesInsteadOfCrashing()
    {
        // ChordAssignment is a public record struct with a raw int[] of pitches, so a
        // caller can hand in a pitch below 0. GetUniquePitchClasses used a bare `% 12`,
        // whose sign survives in C#: -1 % 12 == -1, and the (byte) cast then wrapped it
        // to 255, indexing a stackalloc bool[12] out of bounds (IndexOutOfRangeException).
        // Pitch classes are cyclic, so -1 must fold to B (11), not throw.
        var chords = new[]
        {
            new ChordAssignment(
                Start: Rational.Zero,
                End: new Rational(1, 1),
                Chord: new ChordInfo(0, ChordQuality.Major),
                Pitches: [-1, -8, -5]) // B, E, G one octave below MIDI 0 -> pcs 11, 4, 7
        };

        var events = AccompanimentGenerator.Generate(chords, AccompanimentOptions.Default);

        Assert.NotEmpty(events);
        // Every emitted pitch must be a real, non-negative MIDI note.
        Assert.All(events, e => Assert.True(e.Pitch >= 0, $"pitch {e.Pitch} is negative"));
        // The folded pitch classes {11, 4, 7} must appear among the chord tones.
        var producedPcs = events.Select(e => e.Pitch % 12).ToHashSet();
        Assert.Contains(11, producedPcs);
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
                Pitches: [MidiPitch.C4, MidiPitch.E4, MidiPitch.G4])
        };

        // Override only non-default values; BassOctave/ChordOctave remain at defaults (2 and 4).
        var options = AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = Rational.Quarter
        };

        var events = AccompanimentGenerator.Generate(chords, options);

        // 1/1 duration ÷ 1/4 step = 4 steps (bass on step 0, then 3 chord tones)
        const int stepsPerWholeNote = 4;
        Assert.Equal(stepsPerWholeNote, events.Length);
        Assert.Equal(Rational.Zero, events[0].Offset);
        Assert.Equal(new Rational(1, 4), events[0].Duration);
        Assert.Equal(new Rational(1, 4), events[1].Offset);
        Assert.Equal(new Rational(1, 2), events[2].Offset);
        Assert.Equal(new Rational(3, 4), events[3].Offset);

        // First event is bass — pitch class C (0).
        Assert.Equal(PitchClassC, events[0].Pitch % 12);
    }

    /// <summary>
    /// A chord asked for as a roman numeral and the same chord asked for as a harmonization must
    /// give up the same notes when there is not room for all of them. The roman-numeral path used
    /// to keep whichever came first in the quality's interval list — root, third, fifth, seventh —
    /// so a V7 at three tones came out G-B-D, a plain triad with the note that makes it a dominant
    /// thrown away, while the same chord through <see cref="ChordAssignment"/> gave G-B-F.
    /// </summary>
    [Theory]
    [InlineData(ScaleDegree.V, ChordQuality.Dominant7, 3, new[] { 5, 7, 11 })]    // G-B-F, not G-B-D
    [InlineData(ScaleDegree.Ii, ChordQuality.Minor7, 3, new[] { 0, 2, 5 })]       // D-F-C, not D-F-A
    [InlineData(ScaleDegree.I, ChordQuality.Major7, 3, new[] { 0, 4, 11 })]       // C-E-B, not C-E-G
    [InlineData(ScaleDegree.V, ChordQuality.Dominant7, 2, new[] { 7, 11 })]       // root and third
    [InlineData(ScaleDegree.V, ChordQuality.Dominant7, 4, new[] { 2, 5, 7, 11 })] // room for all
    [InlineData(ScaleDegree.I, ChordQuality.Major, 3, new[] { 0, 4, 7 })]         // a triad is whole
    public void AChordGivesUpTheSameNotesWhicheverWayItIsAskedFor(
        ScaleDegree degree,
        ChordQuality quality,
        int maxChordTones,
        int[] expectedPitchClasses)
    {
        var key = new KeySignature("C", isMajor: true);
        var roman = new RomanNumeralChord(degree, quality, HarmonicFunction.Tonic);
        var options = AccompanimentOptions.Default with { MaxChordTones = maxChordTones };

        var fromRoman = AccompanimentGenerator.Generate(
            [new HarmonicRhythmItem(roman, Rational.Whole)], key, options);

        var fromHarmonization = AccompanimentGenerator.Generate(
            [
                new ChordAssignment(
                    Start: Rational.Zero,
                    End: Rational.Whole,
                    Chord: new ChordInfo(roman.GetRootPitchClass(key), quality),
                    Pitches: [.. roman.GetPitchClasses(key).Select(pc => MidiPitch.C4 + pc)]),
            ],
            options);

        // The bass sounds the root an octave or more below; the chord tones are what is voiced.
        static int[] ChordTones(NoteEvent[] events) =>
            [.. events.Where(n => n.Pitch >= MidiPitch.C4).Select(n => n.Pitch % 12).Distinct().Order()];

        Assert.Equal(expectedPitchClasses, ChordTones(fromRoman));
        Assert.Equal(expectedPitchClasses, ChordTones(fromHarmonization));
    }
}
