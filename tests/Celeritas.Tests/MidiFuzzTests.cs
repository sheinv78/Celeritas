using Celeritas.Core;
using Celeritas.Core.Midi;

namespace Celeritas.Tests;

/// <summary>
/// Hardening / fuzz coverage for <see cref="MidiIo.Import(Stream, MidiImportOptions)"/>.
///
/// The contract these tests lock in: Import either returns a <see cref="NoteBuffer"/> for a
/// structurally valid file, or throws one of a small, documented set of *clean* exception
/// types for malformed input — never a library-internal DryWetMidi exception, never a hang,
/// and never an unbounded allocation. The hardened <c>ReadingSettings</c> in MidiIo abort on
/// truncation / declared-vs-actual size mismatches and skip unknown chunks, so a crafted file
/// declaring a huge chunk length fails fast instead of pre-allocating.
///
/// Exception types Import can surface (verified empirically against DryWetMidi 8.0.3):
///   - InvalidDataException  : malformed / truncated / corrupt data (wrapped MidiException)
///   - ArgumentException     : e.g. an empty stream ("Stream is already read")
///   - NotSupportedException : non-ticks-per-quarter-note (SMPTE) time division
///   - InvalidOperationException : non-positive ticks-per-quarter-note value
/// </summary>
public class MidiFuzzTests
{
    // Generous ceiling: a hardened read of any of these tiny inputs completes in well under a
    // second. If Import ever hangs (e.g. a regression re-enabling unbounded reads) the wait
    // trips and the test fails instead of blocking the suite.
    private const int ImportTimeoutMs = 15_000;

    // Standard MIDI header: "MThd", length 6, format, track count, division.
    private static readonly byte[] MThd = "MThd"u8.ToArray();
    private static readonly byte[] MTrk = "MTrk"u8.ToArray();

    // ---- malformed corpus generators -----------------------------------------------------

    // (a) truncated header: "MThd" plus a partial length field.
    private static byte[] TruncatedHeader() => [0x4D, 0x54, 0x68, 0x64, 0x00, 0x00];

    // (f) valid header declaring one track, but zero track chunks follow.
    private static byte[] HeaderOnly() =>
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x00, 0x01, 0xE0, // format 0, 0 tracks, tpq 480
    ];

    // (b) valid header (declares 1 track) but the track chunk is truncated mid-event.
    private static byte[] TruncatedTrack() =>
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x01, 0x01, 0xE0,
        0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x10, // MTrk, declares 16 bytes
        0x00, 0x90, 0x3C,                                // only 3 bytes present
    ];

    // (c) track chunk declaring a huge length (0x7FFFFFFF) with no data — a naive reader
    //     could try to pre-allocate ~2 GiB. Hardened settings must abort instead.
    private static byte[] HugeChunkLength() =>
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x01, 0x01, 0xE0,
        0x4D, 0x54, 0x72, 0x6B, 0x7F, 0xFF, 0xFF, 0xFF, // MTrk length = int.MaxValue
    ];

    // (e) arbitrary garbage that is not a MIDI file at all.
    private static byte[] Garbage() => [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x11, 0x22, 0x33];

    // (g) valid, round-trippable control produced by MidiIo itself.
    private static byte[] ValidRoundTrip()
    {
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, Rational.Zero, new Rational(1, 4));
        buffer.AddNote(64, new Rational(1, 4), new Rational(1, 4));
        buffer.AddNote(67, new Rational(1, 2), new Rational(1, 2));
        using var ms = new MemoryStream();
        MidiIo.Export(buffer, ms, new MidiExportOptions(TicksPerQuarterNote: 480, Bpm: 120));
        return ms.ToArray();
    }

    // ---- helpers -------------------------------------------------------------------------

    private static bool IsExpectedCleanException(Exception ex) =>
        ex is InvalidDataException
            or ArgumentException          // includes ArgumentNull/ArgumentOutOfRange
            or NotSupportedException
            or InvalidOperationException
            or IOException;               // includes EndOfStreamException

    /// <summary>
    /// Runs Import on a worker with a hard timeout so a hypothetical hang fails the test
    /// rather than blocking the whole run. Returns the buffer on success (caller disposes)
    /// or the thrown exception; asserts completion within the timeout.
    /// </summary>
    private static (NoteBuffer? Buffer, Exception? Error) Import(byte[] data)
    {
        NoteBuffer? buffer = null;
        var task = Task.Run(() =>
        {
            using var ms = new MemoryStream(data, writable: false);
            buffer = MidiIo.Import(ms);
        });

        // WaitAny (unlike Wait) does not itself throw when the task faults — it just reports
        // completion, letting us inspect the fault deliberately.
        var completed = Task.WaitAny([(Task)task], ImportTimeoutMs) == 0;
        Assert.True(completed, $"MidiIo.Import did not complete within {ImportTimeoutMs} ms — possible hang/unbounded read.");

        if (task.IsFaulted)
        {
            var error = task.Exception!.InnerException ?? task.Exception!;
            return (null, error);
        }

        return (buffer, null);
    }

    private static void AssertCleanThrow(byte[] data)
    {
        var (buffer, error) = Import(data);
        buffer?.Dispose();

        Assert.NotNull(error);
        Assert.True(
            IsExpectedCleanException(error!),
            $"Import threw an unexpected exception type: {error!.GetType().FullName}: {error.Message}");
    }

    // ---- malformed corpus tests ----------------------------------------------------------

    public static IEnumerable<object[]> MalformedInputs()
    {
        yield return new object[] { "truncated-header", TruncatedHeader() };
        yield return new object[] { "truncated-track", TruncatedTrack() };
        yield return new object[] { "huge-chunk-length", HugeChunkLength() };
        yield return new object[] { "empty-stream", Array.Empty<byte>() };
        yield return new object[] { "garbage", Garbage() };
    }

    [Theory]
    [MemberData(nameof(MalformedInputs))]
    public void Import_MalformedInput_ThrowsCleanException(string name, byte[] data)
    {
        _ = name; // shown in test output for diagnostics
        AssertCleanThrow(data);
    }

    [Fact]
    public void Import_TruncatedHeader_ThrowsInvalidData()
    {
        var (buffer, error) = Import(TruncatedHeader());
        buffer?.Dispose();
        Assert.IsType<InvalidDataException>(error);
    }

    [Fact]
    public void Import_HugeChunkLength_AbortsFastWithoutAllocating()
    {
        // The declared length is int.MaxValue with zero bytes of data. A best-effort reader
        // could attempt a ~2 GiB allocation; the hardened settings abort deterministically.
        var (buffer, error) = Import(HugeChunkLength());
        buffer?.Dispose();
        Assert.IsType<InvalidDataException>(error);
    }

    [Fact]
    public void Import_EmptyStream_ThrowsArgumentException()
    {
        var (buffer, error) = Import([]);
        buffer?.Dispose();
        Assert.IsAssignableFrom<ArgumentException>(error);
    }

    [Fact]
    public void Import_ValidHeaderZeroTracks_ReturnsEmptyBuffer()
    {
        var (buffer, error) = Import(HeaderOnly());
        Assert.Null(error);
        Assert.NotNull(buffer);
        using (buffer)
        {
            Assert.Equal(0, buffer!.Count);
        }
    }

    [Fact]
    public void Import_ValidRoundTrip_ReturnsNotes()
    {
        var (buffer, error) = Import(ValidRoundTrip());
        Assert.Null(error);
        Assert.NotNull(buffer);
        using (buffer)
        {
            Assert.Equal(3, buffer!.Count);
        }
    }

    // ---- committed fixture round-trip ----------------------------------------------------

    [Fact]
    public void Fixture_ValidTwoNotes_ImportsSuccessfully()
    {
        var path = FixturePath("valid_two_notes.mid");
        using var stream = File.OpenRead(path);
        using var buffer = MidiIo.Import(stream);
        Assert.Equal(2, buffer.Count);
    }

    [Theory]
    [InlineData("huge_chunk_length.mid")]
    [InlineData("truncated_track.mid")]
    public void Fixture_Malformed_ThrowsInvalidData(string fixture)
    {
        var path = FixturePath(fixture);
        using var stream = File.OpenRead(path);
        Assert.Throws<InvalidDataException>(() => MidiIo.Import(stream));
    }

    // ---- light randomized fuzz -----------------------------------------------------------

    [Fact]
    public void Import_RandomBytes_NeverThrowsUnexpectedType_NeverHangs()
    {
        // Seeded RNG => fully reproducible corpus. Never use an unseeded Random here.
        var rng = new Random(0x5E17A5);
        const int iterations = 500;

        for (var i = 0; i < iterations; i++)
        {
            var length = rng.Next(0, 64);
            var data = new byte[length];
            rng.NextBytes(data);

            // Occasionally prefix a valid MIDI header so the reader gets deeper into parsing.
            if (length >= MThd.Length && rng.Next(4) == 0)
            {
                Array.Copy(MThd, data, MThd.Length);
            }

            var (buffer, error) = Import(data);
            buffer?.Dispose();

            if (error is not null)
            {
                Assert.True(
                    IsExpectedCleanException(error),
                    $"Iteration {i} (len={length}) threw unexpected type {error.GetType().FullName}: {error.Message}");
            }
            // else: Import returned a buffer for input that happened to parse — also acceptable.
        }
    }

    [Fact]
    public void Import_RandomBytesWithValidHeaderPrefix_StaysWithinContract()
    {
        var rng = new Random(0xC0FFEE);
        const int iterations = 300;

        for (var i = 0; i < iterations; i++)
        {
            var tail = rng.Next(0, 48);
            var data = new byte[MThd.Length + tail];
            Array.Copy(MThd, data, MThd.Length);
            var tailBytes = new byte[tail];
            rng.NextBytes(tailBytes);
            Array.Copy(tailBytes, 0, data, MThd.Length, tail);

            // Sprinkle an MTrk marker in so track-chunk parsing paths get exercised too.
            if (tail >= MTrk.Length && rng.Next(3) == 0)
            {
                Array.Copy(MTrk, 0, data, MThd.Length, MTrk.Length);
            }

            var (buffer, error) = Import(data);
            buffer?.Dispose();

            if (error is not null)
            {
                Assert.True(
                    IsExpectedCleanException(error),
                    $"Iteration {i} (tail={tail}) threw unexpected type {error.GetType().FullName}: {error.Message}");
            }
        }
    }

    // ---- fixture location ----------------------------------------------------------------

    private static string FixturePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "test-data", "fixtures", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate test fixture '{fileName}' under any 'test-data/fixtures' directory above {AppContext.BaseDirectory}.");
    }
}
