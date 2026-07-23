using Celeritas.Core;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

public class MusicXmlExportTests
{
    private static NoteBuffer Build(params (int pitch, Rational offset, Rational duration)[] notes)
    {
        var b = new NoteBuffer(Math.Max(notes.Length, 1));
        foreach (var (p, o, d) in notes)
            b.AddNote(p, o, d);
        b.Sort();
        return b;
    }

    // Canonical order (offset, pitch, duration): the round-trip preserves the multiset of notes,
    // but NoteBuffer.Sort does not break offset ties by pitch, so sort explicitly before comparing.
    private static (int pitch, Rational offset, Rational duration)[] Dump(NoteBuffer b) =>
        [.. Enumerable.Range(0, b.Count)
            .Select(i => { var e = b.Get(i); return (e.Pitch, e.Offset, e.Duration); })
            .OrderBy(t => t.Offset).ThenBy(t => t.Pitch).ThenBy(t => t.Duration)];

    private static void AssertRoundTrips(NoteBuffer original)
    {
        var xml = MusicXmlIo.ToXml(original);
        using var reimported = MusicXmlIo.Parse(xml);
        Assert.Equal(Dump(original), Dump(reimported));
    }

    [Fact]
    public void RoundTrip_Melody_PreservesNotes()
    {
        using var original = Build(
            (60, Rational.Zero, Rational.Quarter),
            (62, new Rational(1, 4), Rational.Quarter),
            (64, new Rational(1, 2), Rational.Half));
        AssertRoundTrips(original);
    }

    [Fact]
    public void RoundTrip_BlockChord_PreservesNotes()
    {
        using var original = Build(
            (60, Rational.Zero, Rational.Whole),
            (64, Rational.Zero, Rational.Whole),
            (67, Rational.Zero, Rational.Whole),
            (72, Rational.Whole, Rational.Quarter));
        AssertRoundTrips(original);
    }

    [Fact]
    public void RoundTrip_WithRestGap_PreservesTiming()
    {
        using var original = Build(
            (60, Rational.Zero, Rational.Quarter),
            (64, Rational.Half, Rational.Quarter));   // a quarter-rest gap between them
        AssertRoundTrips(original);
    }

    [Fact]
    public void RoundTrip_EighthAndSixteenth_DivisionsExact()
    {
        using var original = Build(
            (60, Rational.Zero, Rational.Eighth),
            (62, Rational.Eighth, Rational.Sixteenth),
            (64, new Rational(3, 16), new Rational(1, 16)));
        AssertRoundTrips(original);
    }

    [Fact]
    public void ToXml_ProducesScorePartwiseWithPitchAndDivisions()
    {
        using var b = Build((61, Rational.Zero, Rational.Quarter));   // C#4
        var xml = MusicXmlIo.ToXml(b);

        Assert.Contains("score-partwise", xml);
        Assert.Contains("<divisions>", xml);
        Assert.Contains("<step>C</step>", xml);
        Assert.Contains("<alter>1</alter>", xml);   // the sharp
    }

    [Fact]
    public void RoundTrip_Polyphony_PreservesOverlappingNotes()
    {
        // A C3 half note under two soprano quarters: the bass overlaps both -> two voices.
        using var original = Build(
            (48, Rational.Zero, Rational.Half),
            (72, Rational.Zero, Rational.Quarter),
            (74, new Rational(1, 4), Rational.Quarter));
        AssertRoundTrips(original);
    }

    [Fact]
    public void ToXml_Polyphony_EmitsVoicesAndBackup()
    {
        using var b = Build(
            (48, Rational.Zero, Rational.Half),
            (72, Rational.Zero, Rational.Quarter));   // overlaps the half note
        var xml = MusicXmlIo.ToXml(b);

        Assert.Contains("<backup>", xml);
        Assert.Contains("<voice>1</voice>", xml);
        Assert.Contains("<voice>2</voice>", xml);
    }

    [Fact]
    public void ToXml_BlockChord_StaysOneVoice()
    {
        // A block chord shares onset + duration, so it is one unit in one voice: no split.
        using var b = Build(
            (60, Rational.Zero, Rational.Whole),
            (64, Rational.Zero, Rational.Whole),
            (67, Rational.Zero, Rational.Whole));
        var xml = MusicXmlIo.ToXml(b);

        Assert.DoesNotContain("<backup", xml);
        Assert.DoesNotContain("<voice>", xml);
    }

    [Fact]
    public void RoundTrip_Velocity_SingleVoice_Preserved()
    {
        using var original = new NoteBuffer(3);
        original.AddNote(60, Rational.Zero, Rational.Quarter, 0.5f);      // quieter than default
        original.AddNote(62, Rational.Quarter, Rational.Quarter, 0.5f);   // same -> no new dynamic
        original.AddNote(64, Rational.Half, Rational.Quarter, 0.95f);     // louder -> new dynamic
        original.Sort();

        using var again = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));

        Assert.Equal(3, again.Count);
        Assert.Equal(0.5, again.Get(0).Velocity, 2);
        Assert.Equal(0.5, again.Get(1).Velocity, 2);
        Assert.Equal(0.95, again.Get(2).Velocity, 2);
    }

    [Fact]
    public void ToXml_DefaultVelocity_EmitsNoDynamics()
    {
        // Default velocity matches the import default, so no dynamic marking is needed.
        using var b = Build((60, Rational.Zero, Rational.Quarter));
        Assert.DoesNotContain("<sound", MusicXmlIo.ToXml(b));
    }

    [Fact]
    public void RoundTrip_MultiMeasureMelody_Preserved()
    {
        using var original = new NoteBuffer(6);
        for (var i = 0; i < 6; i++)
            original.AddNote(60 + i, new Rational(i, 4), Rational.Quarter);   // 6 quarters = 1.5 bars of 4/4
        original.Sort();

        var xml = MusicXmlIo.ToXml(original);
        Assert.Contains("<measure number=\"2\"", xml);   // spilled into a second measure
        using var again = MusicXmlIo.Parse(xml);
        Assert.Equal(Dump(original), Dump(again));
    }

    [Fact]
    public void RoundTrip_NoteAcrossBarline_SplitAndTied()
    {
        // A whole note starting on beat 3 of 4/4 spans [1/2, 3/2], crossing the barline at 1.
        using var original = new NoteBuffer(1);
        original.AddNote(60, new Rational(1, 2), Rational.Whole);
        original.Sort();

        var xml = MusicXmlIo.ToXml(original);
        Assert.Contains("<tie type=\"start\"", xml);
        Assert.Contains("<tie type=\"stop\"", xml);

        using var again = MusicXmlIo.Parse(xml);
        Assert.Equal(1, again.Count);                       // reassembled into one note
        Assert.Equal(60, again.Get(0).Pitch);
        Assert.Equal(new Rational(1, 2), again.Get(0).Offset);
        Assert.Equal(Rational.Whole, again.Get(0).Duration);
    }

    [Fact]
    public void RoundTrip_ThreeFourMeter_Preserved()
    {
        using var original = new NoteBuffer(4);
        for (var i = 0; i < 4; i++)
            original.AddNote(60, new Rational(i, 4), Rational.Quarter);   // 4 quarters across 3/4 bars
        original.Sort();

        using var again = MusicXmlIo.Parse(MusicXmlIo.ToXml(original, new TimeSignature(3, 4)));
        Assert.Equal(Dump(original), Dump(again));
    }

    [Fact]
    public void ToXml_WritesTimeSignature()
    {
        using var b = Build((60, Rational.Zero, Rational.Quarter));
        var xml = MusicXmlIo.ToXml(b, new TimeSignature(3, 4));

        Assert.Contains("<beats>3</beats>", xml);
        Assert.Contains("<beat-type>4</beat-type>", xml);
    }

    [Fact]
    public void ToXml_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => MusicXmlIo.ToXml(null!));
}
