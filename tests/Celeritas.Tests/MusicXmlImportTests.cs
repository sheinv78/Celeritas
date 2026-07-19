using Celeritas.Core;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

public class MusicXmlImportTests
{
    private static string PartwiseWith(string measuresBody, string partAttrs = "<divisions>1</divisions>") =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <score-partwise version="4.0">
          <part-list><score-part id="P1"><part-name>Music</part-name></score-part></part-list>
          <part id="P1">
            <measure number="1">
              <attributes>{partAttrs}</attributes>
              {measuresBody}
            </measure>
          </part>
        </score-partwise>
        """;

    private static string Note(string step, int octave, int duration, int? alter = null, bool chord = false,
        bool rest = false, bool tieStart = false, bool tieStop = false)
    {
        var body = rest
            ? "<rest/>"
            : $"<pitch><step>{step}</step>{(alter is null ? "" : $"<alter>{alter}</alter>")}<octave>{octave}</octave></pitch>";
        var chordEl = chord ? "<chord/>" : "";
        var ties = (tieStop ? "<tie type=\"stop\"/>" : "") + (tieStart ? "<tie type=\"start\"/>" : "");
        return $"<note>{chordEl}{body}<duration>{duration}</duration>{ties}</note>";
    }

    [Fact]
    public void Import_SimpleMelody_YieldsPitchesOffsetsDurations()
    {
        var xml = PartwiseWith(
            Note("C", 4, 1) + Note("D", 4, 1) + Note("E", 4, 1) + Note("F", 4, 1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(4, buffer.Count);
        Assert.Equal([60, 62, 64, 65], Enumerable.Range(0, 4).Select(i => buffer.Get(i).Pitch));
        // divisions=1 => a quarter note is 1/4 of a whole note; onsets step by 1/4.
        Assert.Equal(Rational.Zero, buffer.Get(0).Offset);
        Assert.Equal(new Rational(1, 4), buffer.Get(1).Offset);
        Assert.Equal(new Rational(3, 4), buffer.Get(3).Offset);
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(new Rational(1, 4), buffer.Get(i).Duration));
    }

    [Fact]
    public void Import_Chord_PlacesNotesAtSameOnset()
    {
        // A whole-note C major triad: main note then two <chord/> notes.
        var xml = PartwiseWith(
            Note("C", 4, 4) + Note("E", 4, 4, chord: true) + Note("G", 4, 4, chord: true));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(3, buffer.Count);
        // All three share onset 0 and last a whole note; sorted, pitches ascend.
        Assert.All(Enumerable.Range(0, 3), i => Assert.Equal(Rational.Zero, buffer.Get(i).Offset));
        Assert.All(Enumerable.Range(0, 3), i => Assert.Equal(Rational.Whole, buffer.Get(i).Duration));
        Assert.Equal([60, 64, 67], Enumerable.Range(0, 3).Select(i => buffer.Get(i).Pitch));
    }

    [Fact]
    public void Import_Rest_AdvancesTimeWithoutEmittingNote()
    {
        var xml = PartwiseWith(
            Note("C", 4, 1) + Note("", 0, 1, rest: true) + Note("E", 4, 1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(Rational.Zero, buffer.Get(0).Offset);
        Assert.Equal(64, buffer.Get(1).Pitch);
        // The rest occupied the second quarter, so E lands at 2/4.
        Assert.Equal(new Rational(2, 4), buffer.Get(1).Offset);
    }

    [Fact]
    public void Import_Alterations_MapToMidiSharpsAndFlats()
    {
        var xml = PartwiseWith(
            Note("C", 4, 1, alter: 1) + Note("B", 3, 1, alter: -1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(61, buffer.Get(0).Pitch);   // C#4
        Assert.Equal(58, buffer.Get(1).Pitch);   // Bb3
    }

    [Fact]
    public void Import_Divisions_ScalesDurations()
    {
        // divisions=4 => a quarter note has duration 4; an eighth has duration 2.
        var xml = PartwiseWith(
            Note("C", 4, 4) + Note("D", 4, 2),
            partAttrs: "<divisions>4</divisions>");

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(new Rational(1, 4), buffer.Get(0).Duration);   // quarter
        Assert.Equal(new Rational(1, 8), buffer.Get(1).Duration);   // eighth
        Assert.Equal(new Rational(1, 4), buffer.Get(1).Offset);     // after the quarter
    }

    [Fact]
    public void Import_MultipleParts_MergeOntoOneTimeline()
    {
        var xml = $"""
            <?xml version="1.0"?>
            <score-partwise>
              <part id="P1"><measure number="1"><attributes><divisions>1</divisions></attributes>
                {Note("C", 4, 1)}</measure></part>
              <part id="P2"><measure number="1"><attributes><divisions>1</divisions></attributes>
                {Note("C", 3, 1)}</measure></part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        // Both parts start at 0; sorted by offset then insertion, both onset 0.
        Assert.All(Enumerable.Range(0, 2), i => Assert.Equal(Rational.Zero, buffer.Get(i).Offset));
        var pitches = Enumerable.Range(0, 2).Select(i => buffer.Get(i).Pitch).OrderBy(p => p).ToArray();
        Assert.Equal([48, 60], pitches);   // C3 and C4
    }

    [Fact]
    public void Import_WithDoctype_DoesNotFetchExternalDtd()
    {
        // Real MusicXML carries a DOCTYPE referencing an external DTD; import must not resolve it.
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE score-partwise PUBLIC "-//Recordare//DTD MusicXML 4.0 Partwise//EN"
                "http://www.musicxml.org/dtds/partwise.dtd">
            <score-partwise version="4.0">
              <part id="P1"><measure number="1"><attributes><divisions>1</divisions></attributes>
                <note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration></note>
              </measure></part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
    }

    [Fact]
    public void Import_TiedNotes_MergeIntoOneSustainedNote()
    {
        // C4 quarter (tie start) -> C4 quarter (tie stop), then an untied G4.
        var xml = PartwiseWith(
            Note("C", 4, 1, tieStart: true) + Note("C", 4, 1, tieStop: true) + Note("G", 4, 1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(Rational.Zero, buffer.Get(0).Offset);
        Assert.Equal(new Rational(1, 2), buffer.Get(0).Duration);   // two quarters merged
        Assert.Equal(67, buffer.Get(1).Pitch);
        Assert.Equal(new Rational(2, 4), buffer.Get(1).Offset);     // cursor advanced past both
    }

    [Fact]
    public void Import_TieChain_SumsAllSegments()
    {
        var xml = PartwiseWith(
            Note("C", 4, 1, tieStart: true)
            + Note("C", 4, 1, tieStart: true, tieStop: true)
            + Note("C", 4, 1, tieStop: true));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(new Rational(3, 4), buffer.Get(0).Duration);
    }

    [Fact]
    public void Import_TieAcrossBarline_Merges()
    {
        var xml = $"""
            <?xml version="1.0"?>
            <score-partwise>
              <part id="P1">
                <measure number="1"><attributes><divisions>1</divisions></attributes>
                  {Note("C", 4, 4, tieStart: true)}</measure>
                <measure number="2">
                  {Note("C", 4, 4, tieStop: true)}</measure>
              </part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(new Rational(2, 1), buffer.Get(0).Duration);   // two whole notes tied
    }

    [Fact]
    public void Import_NotationTiedFallback_Merges()
    {
        string NoteTied(string type) =>
            "<note><pitch><step>C</step><octave>4</octave></pitch><duration>1</duration>"
            + $"<notations><tied type=\"{type}\"/></notations></note>";

        using var buffer = MusicXmlIo.Parse(PartwiseWith(NoteTied("start") + NoteTied("stop")));

        Assert.Equal(1, buffer.Count);
        Assert.Equal(new Rational(1, 2), buffer.Get(0).Duration);
    }

    [Fact]
    public void Import_DanglingTieStart_IsStillEmitted()
    {
        using var buffer = MusicXmlIo.Parse(PartwiseWith(Note("C", 4, 1, tieStart: true)));

        Assert.Equal(1, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(new Rational(1, 4), buffer.Get(0).Duration);
    }

    private static string Dynamic(string mark) =>
        $"<direction><direction-type><dynamics><{mark}/></dynamics></direction-type></direction>";

    [Fact]
    public void Import_NamedDynamic_SetsVelocity()
    {
        using var buffer = MusicXmlIo.Parse(PartwiseWith(Dynamic("f") + Note("C", 4, 1)));
        Assert.Equal(96d / 127d, buffer.Get(0).Velocity, 3);   // forte
    }

    [Fact]
    public void Import_SoundDynamics_SetsVelocity()
    {
        var xml = PartwiseWith("<direction><sound dynamics=\"50\"/></direction>" + Note("C", 4, 1));
        using var buffer = MusicXmlIo.Parse(xml);
        Assert.Equal(50d * 0.9 / 127d, buffer.Get(0).Velocity, 3);
    }

    [Fact]
    public void Import_DynamicChange_AppliesToLaterNotes()
    {
        var xml = PartwiseWith(Dynamic("p") + Note("C", 4, 1) + Dynamic("f") + Note("D", 4, 1));
        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(49d / 127d, buffer.Get(0).Velocity, 3);   // piano
        Assert.Equal(96d / 127d, buffer.Get(1).Velocity, 3);   // forte
    }

    [Fact]
    public void Import_NoDynamic_UsesDefaultVelocity()
    {
        using var buffer = MusicXmlIo.Parse(PartwiseWith(Note("C", 4, 1)));
        Assert.Equal(0.8d, buffer.Get(0).Velocity, 3);
    }

    [Fact]
    public void Import_TiedNotesWithDynamic_KeepStartVelocity()
    {
        var xml = PartwiseWith(
            Dynamic("p") + Note("C", 4, 1, tieStart: true) + Note("C", 4, 1, tieStop: true));
        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(49d / 127d, buffer.Get(0).Velocity, 3);   // piano at the chain start
    }

    [Fact]
    public void Parse_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => MusicXmlIo.Parse(null!));

    [Fact]
    public void Import_NonPartwiseRoot_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse("<foo/>"));
        Assert.Contains("score-partwise", ex.Message);
    }

    [Fact]
    public void Import_MalformedXml_ThrowsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse("<score-partwise><part>"));
}
