using Celeritas.Core;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

/// <summary>
/// A transposing instrument's part is written at written pitch and carries a <c>&lt;transpose&gt;</c>
/// in its <c>&lt;attributes&gt;</c> saying how far the sound lies from the page. The engine holds
/// sounding pitch, so import must apply that offset — a reader that took the written pitch handed
/// every analyzer a clarinet part a whole tone sharp.
/// </summary>
public class MusicXmlTransposeTests
{
    private static string Transpose(int chromatic, int? diatonic = null, int? octaveChange = null, int? staff = null) =>
        $"<transpose{(staff is null ? "" : $" number=\"{staff}\"")}>"
        + (diatonic is null ? "" : $"<diatonic>{diatonic}</diatonic>")
        + $"<chromatic>{chromatic}</chromatic>"
        + (octaveChange is null ? "" : $"<octave-change>{octaveChange}</octave-change>")
        + "</transpose>";

    private static string Note(string step, int octave, int duration = 1, int? alter = null, int? staff = null,
        bool tieStart = false, bool tieStop = false) =>
        "<note><pitch>"
        + $"<step>{step}</step>{(alter is null ? "" : $"<alter>{alter}</alter>")}<octave>{octave}</octave>"
        + $"</pitch><duration>{duration}</duration>"
        + (tieStop ? "<tie type=\"stop\"/>" : "") + (tieStart ? "<tie type=\"start\"/>" : "")
        + (staff is null ? "" : $"<staff>{staff}</staff>")
        + "</note>";

    private static string Part(string id, string attributes, string body) =>
        $"<part id=\"{id}\"><measure number=\"1\"><attributes><divisions>1</divisions>{attributes}</attributes>{body}</measure></part>";

    private static string Score(params string[] parts) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><score-partwise version=\"4.0\"><part-list>"
        + string.Concat(parts.Select((_, i) => $"<score-part id=\"P{i + 1}\"><part-name>P{i + 1}</part-name></score-part>"))
        + "</part-list>" + string.Concat(parts) + "</score-partwise>";

    private static int[] Pitches(NoteBuffer buffer) =>
        Enumerable.Range(0, buffer.Count).Select(i => buffer.Get(i).Pitch).ToArray();

    [Fact]
    public void Import_BbClarinet_SoundsAMajorSecondBelowTheWrittenPitch()
    {
        // Bb clarinet: written D5 E5 F#5 G5 sounds C5 D5 E5 F5.
        var xml = Score(Part("P1", Transpose(chromatic: -2, diatonic: -1),
            Note("D", 5) + Note("E", 5) + Note("F", 5, alter: 1) + Note("G", 5)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([72, 74, 76, 77], Pitches(buffer));
    }

    [Fact]
    public void Import_HornInF_SoundsAPerfectFifthBelowTheWrittenPitch()
    {
        // Horn in F: written C5 sounds F4, written G4 sounds C4.
        var xml = Score(Part("P1", Transpose(chromatic: -7, diatonic: -4), Note("C", 5) + Note("G", 4)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([65, 60], Pitches(buffer));
    }

    [Fact]
    public void Import_OctaveChangeAlone_SoundsAnOctaveBelowTheWrittenPitch()
    {
        // Guitar and double bass are written an octave above where they sound: chromatic 0,
        // octave-change -1. Written E4 sounds E3.
        var xml = Score(Part("P1", Transpose(chromatic: 0, octaveChange: -1), Note("E", 4) + Note("A", 4)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([52, 57], Pitches(buffer));
    }

    [Fact]
    public void Import_ChromaticAndOctaveChange_Combine()
    {
        // Eb alto saxophone sounds a major sixth below: some writers encode -9 as chromatic 3
        // plus octave-change -1. Written C5 sounds Eb4 either way.
        var split = Score(Part("P1", Transpose(chromatic: 3, octaveChange: -1), Note("C", 5)));
        var flat = Score(Part("P1", Transpose(chromatic: -9, diatonic: -5), Note("C", 5)));

        using var fromSplit = MusicXmlIo.Parse(split);
        using var fromFlat = MusicXmlIo.Parse(flat);

        Assert.Equal([63], Pitches(fromSplit));
        Assert.Equal([63], Pitches(fromFlat));
    }

    [Fact]
    public void Import_TwoParts_OnlyTheTransposingPartIsShifted()
    {
        // A clarinet and a piano playing the same sounding C: the clarinet writes D5, the piano C5.
        var xml = Score(
            Part("P1", Transpose(chromatic: -2, diatonic: -1), Note("D", 5)),
            Part("P2", "", Note("C", 5)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.All(Enumerable.Range(0, 2), i => Assert.Equal(Rational.Zero, buffer.Get(i).Offset));
        Assert.Equal([72, 72], Pitches(buffer));
    }

    [Fact]
    public void Import_Transpose_AppliesFromThePointItAppears()
    {
        // Measure 1 has no transposition; measure 2 declares one. Only measure 2 shifts.
        var xml = "<score-partwise><part id=\"P1\">"
            + "<measure number=\"1\"><attributes><divisions>1</divisions></attributes>" + Note("C", 4) + "</measure>"
            + "<measure number=\"2\"><attributes>" + Transpose(chromatic: -2) + "</attributes>" + Note("C", 4) + "</measure>"
            + "</part></score-partwise>";

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([60, 58], Pitches(buffer));
    }

    [Fact]
    public void Import_TransposeWithStaffNumber_ShiftsOnlyThatStaff()
    {
        // A transpose carrying number="2" belongs to staff 2; staff 1 (and notes naming no staff,
        // which MusicXML places on staff 1) stay at written pitch.
        var xml = Score(Part("P1", "<staves>2</staves>" + Transpose(chromatic: -2, staff: 2),
            Note("C", 4, staff: 1) + Note("C", 4, staff: 2) + Note("C", 4)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([60, 58, 60], Pitches(buffer));
    }

    [Fact]
    public void Import_UnnumberedTranspose_ResetsAStaffOverride()
    {
        // Measure 1 transposes staff 2 alone; measure 2's un-numbered <transpose> covers the whole
        // part again, so staff 2 follows it rather than its earlier override.
        var xml = "<score-partwise><part id=\"P1\">"
            + "<measure number=\"1\"><attributes><divisions>1</divisions><staves>2</staves>" + Transpose(chromatic: -2, staff: 2)
            + "</attributes>" + Note("C", 4, staff: 1) + Note("C", 4, staff: 2) + "</measure>"
            + "<measure number=\"2\"><attributes>" + Transpose(chromatic: 5) + "</attributes>"
            + Note("C", 4, staff: 1) + Note("C", 4, staff: 2) + "</measure>"
            + "</part></score-partwise>";

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal([60, 58, 65, 65], Pitches(buffer));
    }

    [Fact]
    public void Import_TransposedGraceNoteAndTieChain_KeepSoundingPitch()
    {
        // The grace note is transposed like any other, and the tie chain merges on the sounding
        // pitch into one note.
        var xml = Score(Part("P1", Transpose(chromatic: -2),
            "<note><grace/><pitch><step>E</step><octave>5</octave></pitch></note>"
            + Note("D", 5, tieStart: true) + Note("D", 5, tieStop: true)));

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        var grace = Enumerable.Range(0, 2).Select(i => buffer.Get(i)).Single(n => n.Duration == new Rational(1, 32));
        var tied = Enumerable.Range(0, 2).Select(i => buffer.Get(i)).Single(n => n.Duration == new Rational(1, 2));
        Assert.Equal(74, grace.Pitch);   // written E5 sounds D5
        Assert.Equal(72, tied.Pitch);    // written D5 sounds C5
    }

    [Fact]
    public void Export_WritesSoundingPitchWithoutATransposeElement()
    {
        // The buffer holds sounding pitch, so export has nothing to transpose: the clarinet's
        // written D5 comes back out as a concert C5 and the file carries no <transpose>.
        var xml = Score(Part("P1", Transpose(chromatic: -2, diatonic: -1), Note("D", 5)));
        using var imported = MusicXmlIo.Parse(xml);

        var exported = MusicXmlIo.ToXml(imported);
        using var again = MusicXmlIo.Parse(exported);

        Assert.DoesNotContain("<transpose", exported);
        Assert.Contains("<step>C</step>", exported);
        Assert.Equal([72], Pitches(again));
    }
}
