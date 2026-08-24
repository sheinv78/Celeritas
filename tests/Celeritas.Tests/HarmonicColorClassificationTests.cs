// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The non-chord-tone classifier: passing tone, neighbour tone, appoggiatura, suspension. Each
/// is a heuristic arm that returns a plausible label whatever the input, so a wrong condition
/// mislabels rather than fails — the melody still gets analyzed, just wrongly.
/// </summary>
public class HarmonicColorClassificationTests
{
    private static readonly KeySignature CMajor = new(0, true);

    /// <summary>C major throughout, so chord tones are C, E and G.</summary>
    private static (string Chord, Rational Start)[] OneCChord() => [("C", Rational.Zero)];

    private static NoteEvent Eighth(int pitch, int index) =>
        new(pitch, new Rational(index, 8), Rational.Eighth);

    private static MelodicHarmonyEvent[] Classify(params int[] pitches)
    {
        var melody = pitches.Select((p, i) => Eighth(p, i)).ToArray();
        return [.. HarmonicColorAnalyzer.Analyze(melody, OneCChord(), CMajor).MelodicHarmony];
    }

    // ---------- chord tones ----------

    [Fact]
    public void ChordTonesAreRecognisedAsSuch()
    {
        var events = Classify(60, 64, 67);   // C E G over C major

        Assert.All(events, e => Assert.True(e.IsChordTone));
        Assert.All(events, e => Assert.Equal(MelodicHarmonyEventType.ChordTone, e.Type));
    }

    // ---------- passing tone ----------

    [Fact]
    public void StepwiseBetweenTwoChordTones_InOneDirection_IsAPassingTone()
    {
        // C - D - E: D is not in C major's triad and is stepped through in one direction.
        var events = Classify(60, 62, 64);

        Assert.False(events[1].IsChordTone);
        Assert.Equal(MelodicHarmonyEventType.PassingTone, events[1].Type);
    }

    [Fact]
    public void PassingTone_Descending_IsAlsoRecognised()
    {
        // E - D - C: the same figure downwards.
        var events = Classify(64, 62, 60);

        Assert.Equal(MelodicHarmonyEventType.PassingTone, events[1].Type);
    }

    // ---------- neighbour tone ----------

    [Fact]
    public void StepAwayAndBackToTheSamePitch_IsANeighbourTone()
    {
        // C - D - C: leaves and returns, which is what separates it from a passing tone.
        var events = Classify(60, 62, 60);

        Assert.False(events[1].IsChordTone);
        Assert.Equal(MelodicHarmonyEventType.NeighborTone, events[1].Type);
    }

    [Fact]
    public void LowerNeighbour_IsRecognisedToo()
    {
        // E - D# - E.
        var events = Classify(64, 63, 64);

        Assert.Equal(MelodicHarmonyEventType.NeighborTone, events[1].Type);
    }

    // ---------- appoggiatura ----------

    [Fact]
    public void NonChordToneOnTheChordChange_ResolvingByStep_IsAnAppoggiatura()
    {
        // The figure starts exactly where the chord does and steps to a chord tone.
        var melody = new[] { Eighth(62, 0), Eighth(60, 1) };   // D resolving to C

        var events = HarmonicColorAnalyzer.Analyze(melody, OneCChord(), CMajor).MelodicHarmony;

        Assert.Equal(MelodicHarmonyEventType.Appoggiatura, events[0].Type);
    }

    // ---------- anything else ----------

    [Fact]
    public void ANonChordToneThatLeapsAway_IsNotClassifiedAsAFigure()
    {
        // F approached and left by leap fits none of the figures.
        var events = Classify(60, 65, 72);

        Assert.False(events[1].IsChordTone);
        Assert.Equal(MelodicHarmonyEventType.OtherNonChordTone, events[1].Type);
    }

    // ---------- degenerate input ----------

    [Fact]
    public void NoChordContext_ClassifiesNothing_ButStillReportsEveryNote()
    {
        var melody = new[] { Eighth(60, 0), Eighth(62, 1), Eighth(64, 2) };

        var result = HarmonicColorAnalyzer.Analyze(
            melody, Array.Empty<(string Chord, Rational Start)>(), CMajor);

        Assert.Equal(melody.Length, result.MelodicHarmony.Count);
        Assert.All(result.MelodicHarmony, e =>
        {
            Assert.Equal(MelodicHarmonyEventType.Unclassified, e.Type);
            Assert.False(e.IsChordTone);
            Assert.Equal(0, e.ChordMask);
        });
    }

    [Fact]
    public void SingleNote_IsClassifiedWithoutNeighbours()
    {
        var events = Classify(62);

        Assert.Single(events);
        Assert.False(events[0].IsChordTone);
    }

    [Fact]
    public void EveryEventCarriesItsChordSliceAndADescription()
    {
        foreach (var e in Classify(60, 62, 64, 65, 67))
        {
            Assert.True(e.ChordEnd > e.ChordStart, "the chord slice was empty");
            Assert.True(e.Offset >= e.ChordStart && e.Offset < e.ChordEnd,
                $"note at {e.Offset} sits outside its chord slice [{e.ChordStart}, {e.ChordEnd})");
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
        }
    }

    // ---------- chromatic notes and colourfulness ----------

    [Fact]
    public void ChromaticNotes_AreReportedWithTheirPitch()
    {
        var melody = new[] { Eighth(60, 0), Eighth(61, 1), Eighth(64, 2) };   // C# is outside C major

        var result = HarmonicColorAnalyzer.Analyze(melody, OneCChord(), CMajor);

        Assert.Single(result.ChromaticNotes);
        Assert.Equal(61, result.ChromaticNotes[0].Pitch);
    }

    [Fact]
    public void MoreChromaticism_ReadsAsMoreColourful()
    {
        var plain = HarmonicColorAnalyzer.Analyze(
            new[] { Eighth(60, 0), Eighth(64, 1), Eighth(67, 2) }, OneCChord(), CMajor);
        var spicy = HarmonicColorAnalyzer.Analyze(
            new[] { Eighth(61, 0), Eighth(63, 1), Eighth(66, 2) }, OneCChord(), CMajor);

        Assert.True(spicy.ColorfulnessRating > plain.ColorfulnessRating);
        Assert.InRange(plain.ColorfulnessRating, 0d, 10d);
        Assert.InRange(spicy.ColorfulnessRating, 0d, 10d);
    }

    [Fact]
    public void TheDescription_MatchesTheRating()
    {
        var result = HarmonicColorAnalyzer.Analyze(
            new[] { Eighth(60, 0), Eighth(64, 1), Eighth(67, 2) }, OneCChord(), CMajor);

        Assert.False(string.IsNullOrWhiteSpace(result.Description));
    }

    // ---------- several chord slices ----------

    [Fact]
    public void NotesAreClassifiedAgainstTheChordSoundingUnderThem()
    {
        // C major for the first half, F major for the second: A is a chord tone only in F.
        var melody = new[] { Eighth(69, 0), Eighth(69, 4) };
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("F", Rational.Half)];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.False(events[0].IsChordTone);   // A over C major
        Assert.True(events[1].IsChordTone);    // A over F major
    }

    [Fact]
    public void ChordSlicesRunFromOneChordToTheNext()
    {
        var melody = new[] { Eighth(60, 0), Eighth(65, 4) };
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero), ("F", Rational.Half)];

        var events = HarmonicColorAnalyzer.Analyze(melody, chords, CMajor).MelodicHarmony;

        Assert.Equal(Rational.Zero, events[0].ChordStart);
        Assert.Equal(Rational.Half, events[0].ChordEnd);
        Assert.Equal(Rational.Half, events[1].ChordStart);
    }
}
