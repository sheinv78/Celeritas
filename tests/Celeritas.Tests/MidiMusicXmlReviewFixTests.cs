using Celeritas.Core;
using Celeritas.Core.Midi;
using Celeritas.Core.Notation;
using Melanchall.DryWetMidi.Core;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for the MIDI/MusicXML I/O review-fix batch: exact divisions for irregular
/// meters, SetTempo replace semantics, and import/export robustness fixes.
/// </summary>
public class MidiMusicXmlReviewFixTests
{
    private static string TempMidiPath() =>
        Path.Combine(Path.GetTempPath(), $"celeritas_reviewfix_{Guid.NewGuid():N}.mid");

    // ---- Fix 1: exported divisions must account for the measure length ----

    [Theory]
    [InlineData(7, 8)]
    [InlineData(3, 8)]
    public void Export_WholeNoteInIrregularMeter_RoundTripsToExactlyOneWholeNote(int beats, int beatUnit)
    {
        // A whole note barred into x/8 splits at barlines that are not integral at the divisions
        // chosen from the note alone; the old LCM ignored the measure length, truncating a tied
        // segment to <duration>0</duration> and shortening the note on re-import.
        using var original = new NoteBuffer(1);
        original.AddNote(60, Rational.Zero, Rational.Whole);
        original.Sort();

        var xml = MusicXmlIo.ToXml(original, new TimeSignature(beats, beatUnit));
        Assert.DoesNotContain("<duration>0<", xml);

        using var again = MusicXmlIo.Parse(xml);
        Assert.Equal(1, again.Count);
        Assert.Equal(60, again.Get(0).Pitch);
        Assert.Equal(Rational.Zero, again.Get(0).Offset);
        Assert.Equal(Rational.Whole, again.Get(0).Duration);   // tied segments reassemble to exactly 1/1
    }

    // ---- Fix 2: SetTempo replaces the initial tempo instead of stacking a second one ----

    [Fact]
    public void SetTempo_ReplacesExistingTempoAtTimeZero()
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);

        var path = TempMidiPath();
        try
        {
            MidiIo.Export(buffer, path);   // writes the default 120 BPM tempo at tick 0

            var file = MidiFile.Read(path);
            file.SetTempo(90);
            file.Save(path);

            // With same-tick tempo events the last one wins in playback, so the old insert-at-
            // index-0 left the original 120 in force. Replace semantics leave exactly one tempo.
            var change = Assert.Single(MidiEvents.GetTempoChanges(path));
            Assert.Equal(Rational.Zero, change.Offset);
            Assert.Equal(90, change.BeatsPerMinute);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SetTempo_PreservesTempoChangesAfterTimeZero()
    {
        var track = new TrackChunk();
        MidiEvents.AddTempoChange(track, Rational.Zero, 120, 480);
        MidiEvents.AddTempoChange(track, Rational.Whole, 140, 480);
        var file = new MidiFile(track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };

        file.SetTempo(90);

        var path = TempMidiPath();
        try
        {
            file.Save(path);
            var changes = MidiEvents.GetTempoChanges(path);

            Assert.Equal(2, changes.Count);
            Assert.Equal(Rational.Zero, changes[0].Offset);
            Assert.Equal(90, changes[0].BeatsPerMinute);      // tick-0 tempo replaced
            Assert.Equal(Rational.Whole, changes[1].Offset);
            Assert.Equal(140, changes[1].BeatsPerMinute);     // later change untouched
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- Fix 3: zero-duration notes must not vanish from MusicXML export ----

    [Fact]
    public void Export_ZeroDurationNote_SurvivesRoundTripWithItsPitch()
    {
        using var original = new NoteBuffer(2);
        original.AddNote(60, Rational.Zero, Rational.Quarter);
        original.AddNote(72, Rational.Quarter, Rational.Zero);   // would previously produce no segment at all
        original.Sort();

        var xml = MusicXmlIo.ToXml(original);
        Assert.DoesNotContain("<duration>0<", xml);

        using var again = MusicXmlIo.Parse(xml);
        Assert.Equal(2, again.Count);

        var clamped = Enumerable.Range(0, again.Count)
            .Select(i => again.Get(i))
            .Single(n => n.Pitch == 72);
        Assert.Equal(Rational.Quarter, clamped.Offset);
        Assert.True(clamped.Duration > Rational.Zero);   // clamped to one division unit
    }

    // ---- Fix 4: import tie tracking is per (voice, pitch), not per pitch ----

    [Fact]
    public void Import_TwoVoiceUnisonTieAcrossBarline_YieldsTwoNotes()
    {
        // Two voices in one part both sustain C4 across the barline. Keyed by pitch alone, the
        // second voice's tie-start flushed the first as a short note and the chains merged:
        // three notes came out instead of two.
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <score-partwise version="4.0">
              <part-list><score-part id="P1"><part-name>Music</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes><divisions>1</divisions></attributes>
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="start"/><voice>1</voice></note>
                  <backup><duration>4</duration></backup>
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="start"/><voice>2</voice></note>
                </measure>
                <measure number="2">
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="stop"/><voice>1</voice></note>
                  <backup><duration>4</duration></backup>
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration><tie type="stop"/><voice>2</voice></note>
                </measure>
              </part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(2, buffer.Count);
        Assert.All(Enumerable.Range(0, 2), i =>
        {
            Assert.Equal(60, buffer.Get(i).Pitch);
            Assert.Equal(Rational.Zero, buffer.Get(i).Offset);
            Assert.Equal(new Rational(2, 1), buffer.Get(i).Duration);   // two whole notes tied
        });
    }

    // ---- Fix 5: AddTimeSignatureChange rejects values the MIDI event cannot hold ----

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(256)]
    [InlineData(300)]   // used to wrap through the byte cast to 44
    public void AddTimeSignatureChange_NumeratorOutsideByteRange_Throws(int numerator) =>
        Assert.Throws<ArgumentOutOfRangeException>("numerator",
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), Rational.Zero, numerator, 4, 480));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(256)]   // power of two, but wraps through the byte cast to 0
    [InlineData(512)]
    public void AddTimeSignatureChange_UnrepresentableDenominator_Throws(int denominator) =>
        Assert.Throws<ArgumentOutOfRangeException>("denominator",
            () => MidiEvents.AddTimeSignatureChange(new TrackChunk(), Rational.Zero, 4, denominator, 480));

    [Fact]
    public void AddTimeSignatureChange_AcceptsTheFullRepresentableRange()
    {
        // 255/128 is the extreme DryWetMidi's TimeSignatureEvent can hold (byte numerator,
        // power-of-two byte denominator).
        var track = new TrackChunk();
        MidiEvents.AddTimeSignatureChange(track, Rational.Zero, 255, 128, 480);

        var ts = Assert.Single(track.Events.OfType<TimeSignatureEvent>());
        Assert.Equal(255, ts.Numerator);
        Assert.Equal(128, ts.Denominator);
    }

    // ---- Fix 6: negative-offset export throws the same clear ArgumentException everywhere ----

    private static NoteBuffer NegativeOffsetBuffer()
    {
        var buffer = new NoteBuffer(1);
        buffer.AddNote(60, new Rational(-1, 4), Rational.Quarter);
        return buffer;
    }

    [Fact]
    public void MidiIoExport_NegativeOffset_ThrowsClearArgumentException()
    {
        using var buffer = NegativeOffsetBuffer();
        using var ms = new MemoryStream();

        var ex = Assert.Throws<ArgumentException>(() => MidiIo.Export(buffer, ms));
        Assert.Equal("buffer", ex.ParamName);
        Assert.Contains("negative offset", ex.Message);
    }

    [Fact]
    public void AddTrack_NegativeOffset_ThrowsClearArgumentException()
    {
        var file = new MidiFile { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };
        Celeritas.Core.NoteEvent[] notes = [new(60, new Rational(-1, 4), Rational.Quarter)];

        // Previously leaked a raw DryWetMidi exception from the negative tick time.
        var ex = Assert.Throws<ArgumentException>(() => file.AddTrack(notes));
        Assert.Equal("notes", ex.ParamName);
        Assert.Contains("negative offset", ex.Message);
    }

    [Fact]
    public void MusicXmlToXml_NegativeOffset_ThrowsClearArgumentException()
    {
        // Previously mis-placed the note at time zero (MeasureIndexOf truncates toward zero).
        using var buffer = NegativeOffsetBuffer();

        var ex = Assert.Throws<ArgumentException>(() => MusicXmlIo.ToXml(buffer));
        Assert.Equal("buffer", ex.ParamName);
        Assert.Contains("negative offset", ex.Message);
    }

    [Fact]
    public void MusicXmlExport_NegativeOffset_ThrowsClearArgumentException()
    {
        using var buffer = NegativeOffsetBuffer();
        using var ms = new MemoryStream();

        var ex = Assert.Throws<ArgumentException>(() => MusicXmlIo.Export(buffer, ms));
        Assert.Equal("buffer", ex.ParamName);
        Assert.Contains("negative offset", ex.Message);
    }

    // ---- Fix 7: <duration> is xs:decimal; "1.5" must parse exactly, not kill the file ----

    [Fact]
    public void Import_DecimalDuration_ParsesExactly()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <score-partwise version="4.0">
              <part-list><score-part id="P1"><part-name>Music</part-name></score-part></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes><divisions>2</divisions></attributes>
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>1.5</duration></note>
                  <note><pitch><step>D</step><octave>4</octave></pitch><duration>1</duration></note>
                </measure>
              </part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        // 1.5 divisions at 2 divisions per quarter is exactly (15/10) / (2 * 4) whole notes.
        var expected = new Rational(15, 10) / (2 * 4);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
        Assert.Equal(expected, buffer.Get(0).Duration);
        Assert.Equal(expected, buffer.Get(1).Offset);   // the decimal advanced the cursor exactly
    }

    // ---- Fix 9: divisions LCM overflow surfaces as a descriptive exception, not garbage ----

    [Fact]
    public void Export_DivisionsLcmOverflow_ThrowsDescriptiveException()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, new Rational(1, 1L << 40));      // denominator 2^40
        buffer.AddNote(62, Rational.Zero, new Rational(1, 3486784401L));   // 3^20, coprime to 2^40
        buffer.Sort();

        // lcm(2^38, 3^20) overflows a long; unchecked this became garbage/negative <divisions>.
        var ex = Assert.Throws<InvalidOperationException>(() => MusicXmlIo.ToXml(buffer));
        Assert.Contains("divisions", ex.Message);
    }

    // ---- Fix 10: change lists from multi-track files come back in chronological order ----

    [Fact]
    public void GetTempoChanges_MultiTrackFile_ReturnsChronologicalOrder()
    {
        var track1 = new TrackChunk();
        MidiEvents.AddTempoChange(track1, Rational.Whole, 140, 480);   // later change, first track
        var track2 = new TrackChunk();
        MidiEvents.AddTempoChange(track2, Rational.Zero, 90, 480);     // earlier change, second track

        var file = new MidiFile(track1, track2) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };

        var path = TempMidiPath();
        try
        {
            file.Save(path);
            var changes = MidiEvents.GetTempoChanges(path);

            Assert.Equal(2, changes.Count);
            Assert.Equal(Rational.Zero, changes[0].Offset);
            Assert.Equal(90, changes[0].BeatsPerMinute);
            Assert.Equal(Rational.Whole, changes[1].Offset);
            Assert.Equal(140, changes[1].BeatsPerMinute);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetTimeSignatureChanges_MultiTrackFile_ReturnsChronologicalOrder()
    {
        var track1 = new TrackChunk();
        MidiEvents.AddTimeSignatureChange(track1, Rational.Whole, 3, 4, 480);
        var track2 = new TrackChunk();
        MidiEvents.AddTimeSignatureChange(track2, Rational.Zero, 7, 8, 480);

        var file = new MidiFile(track1, track2) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };

        var path = TempMidiPath();
        try
        {
            file.Save(path);
            var changes = MidiEvents.GetTimeSignatureChanges(path);

            Assert.Equal(2, changes.Count);
            Assert.Equal(Rational.Zero, changes[0].Offset);
            Assert.Equal(7, changes[0].Numerator);
            Assert.Equal(Rational.Whole, changes[1].Offset);
            Assert.Equal(3, changes[1].Numerator);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
