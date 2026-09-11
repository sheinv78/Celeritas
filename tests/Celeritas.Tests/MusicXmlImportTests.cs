using System.IO.Compression;
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
        static string NoteTied(string type) =>
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

    private static MemoryStream Mxl(string scoreXml, bool withContainer, string scoreName)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (withContainer)
            {
                using var w = new StreamWriter(zip.CreateEntry("META-INF/container.xml").Open());
                w.Write($"<container><rootfiles><rootfile full-path=\"{scoreName}\"/></rootfiles></container>");
            }
            using var s = new StreamWriter(zip.CreateEntry(scoreName).Open());
            s.Write(scoreXml);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Import_Mxl_WithContainer_ReadsNamedScore()
    {
        // Two entries; container points at the real score, a decoy .xml sits alongside.
        var score = PartwiseWith(Note("C", 4, 1) + Note("E", 4, 1));
        using var mxl = Mxl(score, withContainer: true, scoreName: "score.xml");

        using var buffer = MusicXmlIo.Import(mxl);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(64, buffer.Get(1).Pitch);
    }

    [Fact]
    public void Import_Mxl_NoContainer_FallsBackToFirstScore()
    {
        using var mxl = Mxl(PartwiseWith(Note("G", 4, 1)), withContainer: false, scoreName: "whatever.musicxml");

        using var buffer = MusicXmlIo.Import(mxl);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(67, buffer.Get(0).Pitch);
    }

    [Fact]
    public void Import_Stream_PlainXml_StillWorks()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(PartwiseWith(Note("C", 4, 1)));
        using var stream = new MemoryStream(bytes);

        using var buffer = MusicXmlIo.Import(stream);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
    }

    [Fact]
    public void Import_ScoreTimewise_TransposesAndReads()
    {
        // Timewise nests measures over parts; the reader should transpose and accumulate time.
        var xml = $"""
            <?xml version="1.0"?>
            <score-timewise>
              <measure number="1">
                <part id="P1"><attributes><divisions>1</divisions></attributes>
                  {Note("C", 4, 1)}{Note("D", 4, 1)}</part>
              </measure>
              <measure number="2">
                <part id="P1">{Note("E", 4, 1)}{Note("F", 4, 1)}</part>
              </measure>
            </score-timewise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(4, buffer.Count);
        Assert.Equal([60, 62, 64, 65], Enumerable.Range(0, 4).Select(i => buffer.Get(i).Pitch));
        Assert.Equal(new Rational(3, 4), buffer.Get(3).Offset);   // cursor carried across measures
    }

    [Fact]
    public void Import_GraceNote_PreservedWithoutAdvancingTime()
    {
        // A grace note (no <duration>) before a principal quarter, then another note.
        var xml = PartwiseWith(
            "<note><grace/><pitch><step>D</step><octave>4</octave></pitch></note>"
            + Note("C", 4, 1) + Note("E", 4, 1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(3, buffer.Count);
        var notes = Enumerable.Range(0, 3).Select(i => buffer.Get(i)).ToArray();

        // Grace D and principal C share onset 0 (the grace did not advance time).
        Assert.Equal([60, 62], notes.Where(n => n.Offset == Rational.Zero).Select(n => n.Pitch).OrderBy(p => p));
        Assert.Equal(new Rational(1, 32), notes.First(n => n.Pitch == 62).Duration);   // short nominal length
        Assert.Equal(new Rational(1, 4), notes.First(n => n.Pitch == 64).Offset);       // E follows the quarter
    }

    [Fact]
    public void Import_TupletDurations_AreExact()
    {
        // divisions=3 per quarter makes a triplet-eighth's duration 1; three of them fill a quarter.
        var xml = PartwiseWith(
            Note("C", 4, 1) + Note("D", 4, 1) + Note("E", 4, 1),
            partAttrs: "<divisions>3</divisions>");

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.All(Enumerable.Range(0, 3), i => Assert.Equal(new Rational(1, 12), buffer.Get(i).Duration));
        Assert.Equal(new Rational(1, 6), buffer.Get(2).Offset);   // 0, 1/12, 1/6
    }

    // A drum hit as MuseScore writes it: no <pitch>, a <display-step>/<display-octave> that only
    // says where on the staff the notehead sits, and an instrument id. Display F4 would be MIDI 65
    // if a reader mistook it for a pitch.
    private static string DrumHit(int duration, bool chord = false) =>
        $"<note>{(chord ? "<chord/>" : "")}<unpitched><display-step>F</display-step><display-octave>4</display-octave></unpitched>"
        + $"<duration>{duration}</duration><instrument id=\"P2-I42\"/><voice>1</voice><notehead>x</notehead></note>";

    private static string PianoPart(string body) =>
        $"<part id=\"P1\"><measure number=\"1\"><attributes><divisions>1</divisions></attributes>{body}</measure></part>";

    // Eight hi-hat eighths, the first and fifth with a kick under them — one 4/4 bar of a drum
    // kit at a finer <divisions> than the piano, so its cursor steps in a different unit.
    private static string DrumPart() =>
        "<part id=\"P2\"><measure number=\"1\"><attributes><divisions>2</divisions></attributes>"
        + DrumHit(1) + DrumHit(1, chord: true) + DrumHit(1) + DrumHit(1) + DrumHit(1)
        + DrumHit(1) + DrumHit(1, chord: true) + DrumHit(1) + DrumHit(1)
        + "</measure></part>";

    private static string ScoreOf(params string[] parts) =>
        "<?xml version=\"1.0\"?><score-partwise version=\"4.0\"><part-list>"
        + "<score-part id=\"P1\"><part-name>Piano</part-name></score-part>"
        + "<score-part id=\"P2\"><part-name>Drum Kit</part-name></score-part>"
        + "</part-list>" + string.Concat(parts) + "</score-partwise>";

    [Fact]
    public void Import_PianoAndDrumParts_ImportsThePianoAndLeavesTheDrumsOut()
    {
        var xml = ScoreOf(PianoPart(Note("C", 4, 1) + Note("E", 4, 1) + Note("G", 4, 1) + Note("C", 5, 1)), DrumPart());

        using var buffer = MusicXmlIo.Parse(xml);

        // The four piano notes and nothing else: no drum hit imported, and the drums'
        // display position F4 (65) was not mistaken for a pitch.
        Assert.Equal(4, buffer.Count);
        Assert.Equal([60, 64, 67, 72], Enumerable.Range(0, 4).Select(i => buffer.Get(i).Pitch));
    }

    [Fact]
    public void Import_OnlyDrums_ImportsAsEmpty()
    {
        using var buffer = MusicXmlIo.Parse(ScoreOf(DrumPart()));

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Import_DrumPart_DoesNotMoveThePianoPartsNotes()
    {
        // Two bars of piano — a rest, a tie across the barline, a chord — with and without the
        // drum part alongside. Leaving the drums out must leave the piano exactly as it was.
        var piano = "<part id=\"P1\">"
            + "<measure number=\"1\"><attributes><divisions>1</divisions></attributes>"
            + Note("C", 4, 1) + Note("", 0, 1, rest: true) + Note("E", 4, 1) + Note("G", 4, 1, tieStart: true)
            + "</measure><measure number=\"2\">"
            + Note("G", 4, 1, tieStop: true) + Note("C", 4, 2) + Note("E", 4, 2, chord: true) + Note("D", 4, 1)
            + "</measure></part>";
        var drums = "<part id=\"P2\"><measure number=\"1\"><attributes><divisions>2</divisions></attributes>"
            + string.Concat(Enumerable.Repeat(DrumHit(1), 8))
            + "</measure><measure number=\"2\">" + DrumHit(4) + DrumHit(4) + "</measure></part>";

        using var alone = MusicXmlIo.Parse(ScoreOf(piano));
        using var withDrums = MusicXmlIo.Parse(ScoreOf(piano, drums));

        Assert.Equal(alone.Count, withDrums.Count);
        for (var i = 0; i < alone.Count; i++)
        {
            var (expected, actual) = (alone.Get(i), withDrums.Get(i));
            Assert.Equal(expected.Pitch, actual.Pitch);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Duration, actual.Duration);
            Assert.Equal(expected.Velocity, actual.Velocity);
        }

        // And the piano itself is what the score says: the tied G spans the barline, the D
        // closes the second bar.
        Assert.Equal(new Rational(1, 2), withDrums.Get(2).Duration);   // G4, two quarters tied
        Assert.Equal(new Rational(7, 4), withDrums.Get(5).Offset);     // D4 on the last beat
    }

    [Fact]
    public void Import_UnpitchedNoteAmongPitchedOnes_OccupiesItsTimeLikeARest()
    {
        // A percussion part can hold a pitched staff and an unpitched one; a hit in the same
        // part as a pitched note still takes up its beat, so the E after it lands at 2/4.
        var xml = PartwiseWith(Note("C", 4, 1) + DrumHit(1) + Note("E", 4, 1));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(64, buffer.Get(1).Pitch);
        Assert.Equal(new Rational(2, 4), buffer.Get(1).Offset);
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
