using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;

namespace Celeritas.Tests;

/// <summary>
/// Guards the on-disk encoding of the time-signature meta event.
/// </summary>
/// <remarks>
/// These assert the raw bytes rather than a write/read round-trip on purpose. The encoding
/// bug these cover was symmetric — the writer double-encoded the log2 exponent and the reader
/// undid it — so the library agreed with itself perfectly while every file it produced was
/// wrong for other readers. Only the bytes can tell the difference.
/// </remarks>
public class MidiEventsTimeSignatureEncodingTests
{
    // MIDI spec: a time signature meta event is FF 58 04 nn dd cc bb,
    // where nn is the numerator and dd is log2(denominator).
    private static (byte Numerator, byte DenominatorLog2) ReadTimeSignatureBytes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        for (var i = 0; i < bytes.Length - 4; i++)
        {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0x58 && bytes[i + 2] == 0x04)
                return (bytes[i + 3], bytes[i + 4]);
        }

        throw new InvalidOperationException("No time signature meta event (FF 58 04) found in the file.");
    }

    private static string WriteFileWithTimeSignature(int numerator, int denominator)
    {
        var track = new TrackChunk();
        MidiEvents.AddTimeSignatureChange(track, Rational.Zero, numerator, denominator, 480);

        var path = Path.Combine(Path.GetTempPath(), $"celeritas_ts_{Guid.NewGuid():N}.mid");
        new MidiFile(track).Write(path, overwriteFile: true);
        return path;
    }

    [Theory]
    [InlineData(4, 4, 2)]   // 4/4  -> dd = log2(4) = 2
    [InlineData(2, 2, 1)]   // 2/2  -> dd = log2(2) = 1
    [InlineData(6, 8, 3)]   // 6/8  -> dd = log2(8) = 3
    [InlineData(7, 16, 4)]  // 7/16 -> dd = log2(16) = 4
    [InlineData(3, 1, 0)]   // 3/1  -> dd = log2(1) = 0
    public void AddTimeSignatureChange_WritesSpecCompliantBytes(int numerator, int denominator, byte expectedDd)
    {
        var path = WriteFileWithTimeSignature(numerator, denominator);
        try
        {
            var (nn, dd) = ReadTimeSignatureBytes(path);

            Assert.Equal((byte)numerator, nn);
            Assert.Equal(expectedDd, dd);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddTimeSignatureChange_AcceptsCompoundMeter()
    {
        // 6/8 used to throw: the pre-computed log2 (3) was rejected downstream as
        // "not a power of two", so the most common compound meter was unwritable.
        var path = WriteFileWithTimeSignature(6, 8);
        File.Delete(path);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(6, 8)]
    [InlineData(7, 16)]
    public void TimeSignature_SurvivesRoundTrip(int numerator, int denominator)
    {
        var path = WriteFileWithTimeSignature(numerator, denominator);
        try
        {
            var changes = MidiEvents.GetTimeSignatureChanges(path);

            var change = Assert.Single(changes);
            Assert.Equal(numerator, change.Numerator);
            Assert.Equal(denominator, change.Denominator);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
