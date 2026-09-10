// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Harmonization;

namespace Celeritas.Tests;

/// <summary>
/// <see cref="DefaultHarmonicRhythmStrategy"/> is documented as "one chord per beat (or per note
/// if longer than a beat)", and the second half of that was never true: a held whole note came
/// back as four quarter-beat slices, and the harmonizer put four chords under it — C, F, C, C
/// under one sustained C. The shipped example documented a chord change halfway through its
/// final half note.
/// </summary>
public class AHeldNoteGetsOneChordTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(2, 1)]
    public void ANoteLongerThanABeatIsOneSlice(int numerator, int denominator)
    {
        var duration = new Rational(numerator, denominator);
        NoteEvent[] held = [new(60, Rational.Zero, duration, 0.8f)];

        var slices = new DefaultHarmonicRhythmStrategy().Segment(held);

        var slice = Assert.Single(slices);
        Assert.Equal(Rational.Zero, slice.Start);
        Assert.Equal(duration, slice.End);
        Assert.Equal([60], slice.Pitches);
        Assert.True(slice.IsStrongBeat);
    }

    [Fact]
    public void ANewNoteStartsANewSlice()
    {
        // Two half notes are two chords; a whole note followed by a quarter is two chords; four
        // quarters are four. Only a beat in which nothing begins joins the one before it.
        var strategy = new DefaultHarmonicRhythmStrategy();

        NoteEvent[] halves = [new(60, Rational.Zero, Rational.Half, 0.8f), new(67, Rational.Half, Rational.Half, 0.8f)];
        Assert.Equal(2, strategy.Segment(halves).Count);

        NoteEvent[] wholeThenQuarter =
        [
            new(60, Rational.Zero, Rational.Whole, 0.8f),
            new(62, Rational.Whole, Rational.Quarter, 0.8f),
        ];
        Assert.Equal(2, strategy.Segment(wholeThenQuarter).Count);

        var quarters = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4");
        Assert.Equal(4, strategy.Segment(quarters).Count);
    }

    [Fact]
    public void ASustainedNoteUnderANewOneKeepsTheBeatsApart()
    {
        // A held C with a D entering on beat 2: beat 2 has a fresh onset, so it is its own
        // slice, and so is beat 3 — the notes sounding there are not the ones that started
        // slice 2.
        NoteEvent[] texture =
        [
            new(60, Rational.Zero, Rational.Whole, 0.8f),
            new(62, Rational.Quarter, Rational.Quarter, 0.8f),
        ];

        var slices = new DefaultHarmonicRhythmStrategy().Segment(texture);

        Assert.Equal(3, slices.Count);
        Assert.Equal([60], slices[0].Pitches);
        Assert.Equal([60, 62], slices[1].Pitches);
        Assert.Equal(new Rational(1, 2), slices[2].Start);
        Assert.Equal(Rational.Whole, slices[2].End);
    }

    [Fact]
    public void TheHarmonizerPutsOneChordUnderAHeldNote()
    {
        NoteEvent[] held = [new(60, Rational.Zero, Rational.Whole, 0.8f)];

        var result = new MelodyHarmonizer().Harmonize(held, new KeySignature(0, true));

        var chord = Assert.Single(result.Chords);
        Assert.Equal(Rational.Zero, chord.Start);
        Assert.Equal(Rational.Whole, chord.End);
    }

    [Fact]
    public void EverySliceStillCoversTheMelodyExactlyOnce()
    {
        // Folding beats must not open a gap or an overlap.
        var random = new Random(20260910);
        var strategy = new DefaultHarmonicRhythmStrategy();

        for (var iteration = 0; iteration < 300; iteration++)
        {
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var i = 0; i < random.Next(1, 10); i++)
            {
                var duration = new Rational(random.Next(1, 9), 4);
                notes.Add(new NoteEvent(random.Next(55, 80), time, duration, 0.8f));
                time += duration;
            }

            var slices = strategy.Segment(notes.ToArray());

            Assert.NotEmpty(slices);
            Assert.Equal(Rational.Zero, slices[0].Start);
            Assert.Equal(time, slices[^1].End);
            for (var i = 1; i < slices.Count; i++)
            {
                Assert.Equal(slices[i - 1].End, slices[i].Start);
            }
        }
    }
}
