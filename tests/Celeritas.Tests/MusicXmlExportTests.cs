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

    private static (int pitch, Rational offset, Rational duration)[] Dump(NoteBuffer b) =>
        Enumerable.Range(0, b.Count)
            .Select(i => { var e = b.Get(i); return (e.Pitch, e.Offset, e.Duration); })
            .ToArray();

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
    public void Export_OverlappingPolyphony_Throws()
    {
        // C4 whole note from 0; E4 quarter starting at 1/4 overlaps it.
        using var b = Build(
            (60, Rational.Zero, Rational.Whole),
            (64, new Rational(1, 4), Rational.Quarter));
        Assert.Throws<NotSupportedException>(() => MusicXmlIo.ToXml(b));
    }

    [Fact]
    public void ToXml_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => MusicXmlIo.ToXml(null!));
}
