using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;

namespace Celeritas.Tests;

/// <summary>
/// The <see cref="MidiEvents"/> readers read untrusted files, so they must share the same
/// hardening and the same failure contract as <see cref="MidiIo"/>.
/// </summary>
/// <remarks>
/// Both readers called <c>MidiFile.Read(stream)</c> on default settings, bypassing the defenses
/// <see cref="MidiIo"/> documents for the identical format: a crafted chunk length could drive an
/// unbounded allocation, and a malformed file surfaced a library-internal <c>MidiException</c>
/// instead of the <see cref="InvalidDataException"/> the rest of the assembly promises. They now
/// go through <c>MidiIo.ReadHardened</c>, so there is one door and it cannot drift.
/// </remarks>
public class MidiEventsHardeningTests
{
    // Valid header (1 track) then MTrk declaring int.MaxValue bytes with none following. A naive
    // reader could try to pre-allocate ~2 GiB; hardened settings must abort instead. (Same shape
    // as MidiFuzzTests' HugeChunkLength, kept local so the two suites stay independent.)
    private static byte[] HugeChunkLength() =>
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x01, 0x01, 0xE0,
        0x4D, 0x54, 0x72, 0x6B, 0x7F, 0xFF, 0xFF, 0xFF,
    ];

    // Valid header declaring 1 track, then a track chunk truncated mid-event.
    private static byte[] TruncatedTrack() =>
    [
        0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06,
        0x00, 0x00, 0x00, 0x01, 0x01, 0xE0,
        0x4D, 0x54, 0x72, 0x6B, 0x00, 0x00, 0x00, 0x10,
        0x00, 0x90, 0x3C,
    ];

    public static TheoryData<string, byte[]> MalformedFiles() => new()
    {
        { "huge-chunk-length", HugeChunkLength() },
        { "truncated-track", TruncatedTrack() },
    };

    [Theory]
    [MemberData(nameof(MalformedFiles))]
    public void GetTempoChanges_MalformedFile_ThrowsInvalidData(string name, byte[] data)
    {
        _ = name;
        RunOnTempFile(data, path =>
            Assert.Throws<InvalidDataException>(() => MidiEvents.GetTempoChanges(path)));
    }

    [Theory]
    [MemberData(nameof(MalformedFiles))]
    public void GetTimeSignatureChanges_MalformedFile_ThrowsInvalidData(string name, byte[] data)
    {
        _ = name;
        RunOnTempFile(data, path =>
            Assert.Throws<InvalidDataException>(() => MidiEvents.GetTimeSignatureChanges(path)));
    }

    [Fact]
    public void GetTempoChanges_ValidFile_StillReads()
    {
        var track = new TrackChunk();
        MidiEvents.AddTempoChange(track, Rational.Zero, beatsPerMinute: 120, ticksPerQuarterNote: 480);

        RunOnMidiFile(track, path =>
        {
            var change = Assert.Single(MidiEvents.GetTempoChanges(path));
            Assert.Equal(120, change.BeatsPerMinute);
        });
    }

    [Fact]
    public void GetTimeSignatureChanges_ValidFile_StillReads()
    {
        var track = new TrackChunk();
        MidiEvents.AddTimeSignatureChange(track, Rational.Zero, numerator: 6, denominator: 8, ticksPerQuarterNote: 480);

        RunOnMidiFile(track, path =>
        {
            var change = Assert.Single(MidiEvents.GetTimeSignatureChanges(path));
            Assert.Equal(6, change.Numerator);
            Assert.Equal(8, change.Denominator);
        });
    }

    private static void RunOnTempFile(byte[] data, Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"celeritas_evt_{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(path, data);
        try
        {
            body(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RunOnMidiFile(TrackChunk track, Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"celeritas_evt_{Guid.NewGuid():N}.mid");
        new MidiFile(track).Write(path, overwriteFile: true);
        try
        {
            body(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
