// Copyright (c) 2025 Vladimir V. Shein

using System.IO.Compression;
using System.Text;
using Celeritas.Core;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

/// <summary>
/// The MusicXML paths ordinary round-trips miss: compressed <c>.mxl</c> containers, the guard
/// that refuses to inflate a zip bomb, the named dynamic marks, and the sharp spellings above
/// G. A wrong spelling still imports and still plays — it just prints as the wrong note.
/// </summary>
public class MusicXmlEdgeTests
{
    private static string MinimalScore(int step = 0) => $"""
        <score-partwise version="4.0">
          <part-list><score-part id="P1"><part-name>Music</part-name></score-part></part-list>
          <part id="P1">
            <measure number="1">
              <attributes><divisions>1</divisions></attributes>
              <note>
                <pitch><step>{"CDEFGAB"[step]}</step><octave>4</octave></pitch>
                <duration>4</duration><type>whole</type>
              </note>
            </measure>
          </part>
        </score-partwise>
        """;

    private static MemoryStream Mxl(params (string Name, string Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        ms.Position = 0;
        return ms;
    }

    // ---------- compressed containers ----------

    [Fact]
    public void AnMxlNamedByItsContainer_IsRead()
    {
        const string container = """
            <container><rootfiles><rootfile full-path="scores/song.xml"/></rootfiles></container>
            """;
        using var archive = Mxl(("META-INF/container.xml", container), ("scores/song.xml", MinimalScore()));

        using var buffer = MusicXmlIo.Import(archive);

        Assert.Equal(1, buffer.Count);
        Assert.Equal(60, buffer.Get(0).Pitch);
    }

    [Fact]
    public void AnMxlWhoseContainerNamesAMissingFile_FallsBackToTheScoreItFinds()
    {
        const string container = """
            <container><rootfiles><rootfile full-path="not/here.xml"/></rootfiles></container>
            """;
        using var archive = Mxl(("META-INF/container.xml", container), ("elsewhere.musicxml", MinimalScore(4)));

        using var buffer = MusicXmlIo.Import(archive);

        Assert.Equal(67, buffer.Get(0).Pitch);      // G4
    }

    [Fact]
    public void AnMxlWithAnUnreadableContainer_FallsBackToTheScoreItFinds()
    {
        using var archive = Mxl(("META-INF/container.xml", "<container><<<broken"), ("song.xml", MinimalScore(2)));

        using var buffer = MusicXmlIo.Import(archive);

        Assert.Equal(64, buffer.Get(0).Pitch);      // E4
    }

    [Fact]
    public void AnMxlWithNoContainerAtAll_StillFindsTheScore()
    {
        using var archive = Mxl(("song.musicxml", MinimalScore()));

        using var buffer = MusicXmlIo.Import(archive);

        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void AnMxlHoldingNoScore_SaysSo()
    {
        using var archive = Mxl(("readme.txt", "no music here"));

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Import(archive));
        Assert.Contains("no MusicXML score", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SomethingThatOnlyLooksLikeAZip_IsRejectedAsSuch()
    {
        // The sniff is the two-byte "PK" signature; a file that starts with it but is not an
        // archive must come back as a data error, not as a ZIP library exception.
        using var fake = new MemoryStream(Encoding.ASCII.GetBytes("PK not really an archive at all"));

        var ex = Assert.Throws<InvalidDataException>(() => MusicXmlIo.Import(fake));
        Assert.Contains(".mxl", ex.Message, StringComparison.Ordinal);
    }

    // ---------- the decompression cap ----------

    [Fact]
    public void TheCappedStream_RefusesToReadPastItsLimit()
    {
        using var inner = new MemoryStream(new byte[1024]);
        using var capped = new MusicXmlIo.CappedReadStream(inner, maxBytes: 100);

        var buffer = new byte[64];
        Assert.Equal(64, capped.Read(buffer, 0, 64));       // 64 of 100

        var ex = Assert.Throws<InvalidDataException>(() => capped.Read(buffer, 0, 64));
        Assert.Contains("safety limit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCappedStream_CountsWhatIsActuallyRead_NotWhatIsClaimed()
    {
        using var inner = new MemoryStream(new byte[50]);
        using var capped = new MusicXmlIo.CappedReadStream(inner, maxBytes: 100);

        var buffer = new byte[1000];
        Assert.Equal(50, capped.Read(buffer, 0, 1000));
        Assert.Equal(0, capped.Read(buffer, 0, 1000));      // exhausted, still under the cap
    }

    [Fact]
    public void TheCappedStream_IsReadOnlyAndForwardOnly()
    {
        using var capped = new MusicXmlIo.CappedReadStream(new MemoryStream([1, 2, 3]), maxBytes: 10);

        Assert.True(capped.CanRead);
        Assert.False(capped.CanSeek);
        Assert.False(capped.CanWrite);
        Assert.Throws<NotSupportedException>(() => capped.Length);
        Assert.Throws<NotSupportedException>(() => capped.Position);
        Assert.Throws<NotSupportedException>(() => capped.Position = 0);
        Assert.Throws<NotSupportedException>(() => capped.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => capped.SetLength(1));
        Assert.Throws<NotSupportedException>(() => capped.Write(new byte[1], 0, 1));
        capped.Flush();     // a no-op, not a failure
    }

    // ---------- named dynamics ----------

    public static TheoryData<string, int> DynamicMarks => new()
    {
        { "pppp", 8 }, { "ppp", 16 }, { "pp", 33 }, { "p", 49 },
        { "mp", 64 }, { "mf", 80 }, { "f", 96 }, { "ff", 112 },
        { "fff", 120 }, { "ffff", 127 },
    };

    [Theory]
    [MemberData(nameof(DynamicMarks))]
    public void EveryNamedDynamic_SetsItsOwnVelocity(string mark, int midiVelocity)
    {
        var xml = $"""
            <score-partwise version="4.0">
              <part-list><score-part id="P1"/></part-list>
              <part id="P1">
                <measure number="1">
                  <attributes><divisions>1</divisions></attributes>
                  <direction><direction-type><dynamics><{mark}/></dynamics></direction-type></direction>
                  <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
                </measure>
              </part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(midiVelocity / 127f, buffer.Get(0).Velocity, 4);
    }

    [Fact]
    public void DynamicsGetLouderInTheOrderTheyAreNamed()
    {
        var velocities = DynamicMarks
            .Select(row => (string)row[0])
            .Select(mark =>
            {
                var xml = $"""
                    <score-partwise version="4.0">
                      <part-list><score-part id="P1"/></part-list>
                      <part id="P1"><measure number="1">
                        <attributes><divisions>1</divisions></attributes>
                        <direction><direction-type><dynamics><{mark}/></dynamics></direction-type></direction>
                        <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
                      </measure></part>
                    </score-partwise>
                    """;
                using var buffer = MusicXmlIo.Parse(xml);
                return buffer.Get(0).Velocity;
            })
            .ToArray();

        Assert.Equal(velocities.OrderBy(v => v), velocities);
    }

    [Fact]
    public void AnUnknownDynamicMark_LeavesTheDefaultVelocity()
    {
        var xml = """
            <score-partwise version="4.0">
              <part-list><score-part id="P1"/></part-list>
              <part id="P1"><measure number="1">
                <attributes><divisions>1</divisions></attributes>
                <direction><direction-type><dynamics><sfzp/></dynamics></direction-type></direction>
                <note><pitch><step>C</step><octave>4</octave></pitch><duration>4</duration></note>
              </measure></part>
            </score-partwise>
            """;

        using var buffer = MusicXmlIo.Parse(xml);

        Assert.Equal(0.8f, buffer.Get(0).Velocity, 4);
    }

    // ---------- spelling on the way out ----------

    [Theory]
    [InlineData(60, "C", 0, 4)]
    [InlineData(61, "C", 1, 4)]
    [InlineData(62, "D", 0, 4)]
    [InlineData(63, "D", 1, 4)]
    [InlineData(64, "E", 0, 4)]
    [InlineData(65, "F", 0, 4)]
    [InlineData(66, "F", 1, 4)]
    [InlineData(67, "G", 0, 4)]
    [InlineData(68, "G", 1, 4)]
    [InlineData(69, "A", 0, 4)]
    [InlineData(70, "A", 1, 4)]
    [InlineData(71, "B", 0, 4)]
    [InlineData(72, "C", 0, 5)]
    [InlineData(48, "C", 0, 3)]
    public void EveryPitchClassIsSpelledWithSharps(int midi, string step, int alter, int octave)
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(midi, Rational.Zero, Rational.Whole);

        var xml = MusicXmlIo.ToXml(buffer);

        Assert.Contains($"<step>{step}</step>", xml, StringComparison.Ordinal);
        Assert.Contains($"<octave>{octave}</octave>", xml, StringComparison.Ordinal);
        if (alter != 0)
            Assert.Contains($"<alter>{alter}</alter>", xml, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("<alter>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnexportableScoreDoesNotDestroyTheFileAtThePath()
    {
        // MusicXML cannot place a note before time zero. The document is built before the
        // destination is opened, so the previous export survives the rejection.
        var work = Directory.CreateTempSubdirectory("celeritas-mxlexport").FullName;
        try
        {
            var path = System.IO.Path.Combine(work, "score.musicxml");

            using var good = new NoteBuffer(1);
            good.AddNote(60, Rational.Zero, Rational.Whole);
            MusicXmlIo.Export(good, path);
            var before = File.ReadAllText(path);

            using var bad = new NoteBuffer(1);
            bad.AddNote(60, new Rational(-1, 4), Rational.Whole);

            Assert.Throws<ArgumentException>(() => MusicXmlIo.Export(bad, path));
            Assert.Equal(before, File.ReadAllText(path));
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { /* best effort */ }
        }
    }

    [Fact]
    public void AChromaticScaleSurvivesTheRoundTrip()
    {
        using var original = new NoteBuffer(12);
        for (var i = 0; i < 12; i++)
            original.AddNote(60 + i, new Rational(i, 4), Rational.Quarter);

        using var reread = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));

        Assert.Equal(12, reread.Count);
        Assert.Equal(
            Enumerable.Range(60, 12),
            Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch));
    }
}
