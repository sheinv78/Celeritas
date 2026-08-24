// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

/// <summary>
/// MusicXML that is well-formed XML but not usable music: an unknown pitch letter, a note
/// before the file says how long a division is, and durations that are not numbers. Each has to
/// come back as a data error naming what was wrong, not as a silently mis-timed score.
/// </summary>
public class MusicXmlMalformedTests
{
    private static string Score(string body) => $"""
        <score-partwise version="4.0">
          <part-list><score-part id="P1"/></part-list>
          <part id="P1"><measure number="1">{body}</measure></part>
        </score-partwise>
        """;

    private const string Divisions = "<attributes><divisions>4</divisions></attributes>";

    [Fact]
    public void APitchLetterOutsideAToG_IsRejected()
    {
        var xml = Score($"{Divisions}<note><pitch><step>H</step><octave>4</octave></pitch><duration>4</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("Unknown pitch step 'H'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APitchWithNoStep_IsRejected()
    {
        var xml = Score($"{Divisions}<note><pitch><octave>4</octave></pitch><duration>4</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("missing <step>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APitchWithNoOctave_IsRejected()
    {
        var xml = Score($"{Divisions}<note><pitch><step>C</step></pitch><duration>4</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("missing <octave>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANoteBeforeDivisionsAreDeclared_IsRejected()
    {
        // Without divisions there is no way to turn <duration> into musical time, and
        // guessing would place every later note wrongly.
        var xml = Score("<note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("positive <divisions>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADurationThatIsNotANumber_IsRejected()
    {
        var xml = Score($"{Divisions}<note><pitch><step>C</step><octave>4</octave></pitch><duration>quaver</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("not a valid number", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADurationWithTooManyDecimalPlaces_IsRejected()
    {
        var xml = Score(
            $"{Divisions}<note><pitch><step>C</step><octave>4</octave></pitch><duration>1.0000000000000000000001</duration></note>");

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
        Assert.Contains("decimal places", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADecimalDurationIsReadExactly()
    {
        // MusicXML types <duration> as a decimal, and real files do write halves.
        var xml = Score(
            $"{Divisions}<note><pitch><step>C</step><octave>4</octave></pitch><duration>1.5</duration></note>");

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(new Rational(3, 32), buffer.Get(0).Duration);
    }

    // ---------- moving the cursor around the measure ----------

    [Fact]
    public void ForwardSkipsAheadWithoutSoundingAnything()
    {
        var xml = Score($"""
            {Divisions}
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
            <forward><duration>4</duration></forward>
            <note><pitch><step>E</step><octave>4</octave></pitch><duration>4</duration></note>
            """);

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(Rational.Zero, buffer.Get(0).Offset);
        Assert.Equal(Rational.Half, buffer.Get(1).Offset);      // skipped a quarter, then wrote
    }

    [Fact]
    public void BackupBeforeTheStartOfTheMeasure_ClampsToZero()
    {
        var xml = Score($"""
            {Divisions}
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
            <backup><duration>40</duration></backup>
            <note><pitch><step>E</step><octave>4</octave></pitch><duration>4</duration></note>
            """);

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.All(Enumerable.Range(0, buffer.Count), i => Assert.True(buffer.Get(i).Offset >= Rational.Zero));
        Assert.Equal(Rational.Zero, buffer.Get(1).Offset);
    }

    // ---------- ties ----------

    [Fact]
    public void ATiedPairSoundsAsOneLongerNote()
    {
        var xml = Score($"""
            {Divisions}
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="start"/></note>
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="stop"/></note>
            """);

        using var buffer = MusicXmlIo.Parse(xml);

        var note = Assert.Single(Enumerable.Range(0, buffer.Count).Select(buffer.Get));
        Assert.Equal(Rational.Half, note.Duration);
    }

    [Fact]
    public void AnUnfinishedTieIsFlushedRatherThanLost()
    {
        // Two tie starts in a row on the same pitch: the first chain is malformed, and its
        // note must still be written out rather than dropped.
        var xml = Score($"""
            {Divisions}
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="start"/></note>
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="start"/></note>
            <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="stop"/></note>
            """);

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.All(Enumerable.Range(0, buffer.Count), i => Assert.Equal(60, buffer.Get(i).Pitch));
    }

    // ---------- exporting several voices ----------

    [Fact]
    public void OverlappingNotesAreWrittenAsSeparateVoices()
    {
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, Rational.Zero, Rational.Whole);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

        var xml = MusicXmlIo.ToXml(buffer);

        Assert.Contains("<backup>", xml, StringComparison.Ordinal);

        using var reread = MusicXmlIo.Parse(xml);
        Assert.Equal(2, reread.Count);
    }
}
