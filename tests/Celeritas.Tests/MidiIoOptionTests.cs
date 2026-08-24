// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace Celeritas.Tests;

/// <summary>
/// Import filters and export guards. The guards matter because every rejected value would
/// otherwise be cast into a byte or a tick count: a channel of 16 wraps to 0 and a tick
/// division of 40000 wraps negative, and both produce a file that opens and plays wrong.
/// </summary>
public class MidiIoOptionTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-midiio").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string TwoChannelFile()
    {
        var track = new TrackChunk(
            new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100) { Channel = (FourBitNumber)0 },
            new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { Channel = (FourBitNumber)0, DeltaTime = 480 },
            new NoteOnEvent((SevenBitNumber)72, (SevenBitNumber)100) { Channel = (FourBitNumber)3 },
            new NoteOffEvent((SevenBitNumber)72, (SevenBitNumber)0) { Channel = (FourBitNumber)3, DeltaTime = 480 });

        var path = Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
        new MidiFile(track) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) }.Write(path);
        return path;
    }

    private static int[] Pitches(NoteBuffer buffer) =>
        [.. Enumerable.Range(0, buffer.Count).Select(i => buffer.Get(i).Pitch)];

    // ---------- import filters ----------

    [Fact]
    public void WithoutAChannelFilter_EveryNoteIsImported()
    {
        using var buffer = MidiIo.Import(TwoChannelFile());

        Assert.Equal([60, 72], Pitches(buffer).Order());
    }

    [Fact]
    public void AChannelFilterKeepsOnlyThatChannel()
    {
        using var buffer = MidiIo.Import(TwoChannelFile(), new MidiImportOptions(Channel: 3));

        Assert.Equal([72], Pitches(buffer));
    }

    [Fact]
    public void AChannelNothingIsOn_ImportsNothing()
    {
        using var buffer = MidiIo.Import(TwoChannelFile(), new MidiImportOptions(Channel: 9));

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void MaxNotesStopsTheImportEarly()
    {
        using var buffer = MidiIo.Import(TwoChannelFile(), new MidiImportOptions(MaxNotes: 1));

        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void TheFiltersCompose()
    {
        using var buffer = MidiIo.Import(TwoChannelFile(), new MidiImportOptions(Channel: 0, MaxNotes: 5));

        Assert.Equal([60], Pitches(buffer));
    }

    // ---------- export guards ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-480)]
    [InlineData(short.MaxValue + 1)]
    [InlineData(40000)]
    public void ExportRejectsATickDivisionMidiCannotStore(int ticks)
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);

        var path = Path.Combine(_work, "bad.mid");
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiIo.Export(buffer, path, new MidiExportOptions(TicksPerQuarterNote: ticks)));

        Assert.Equal("options", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(255)]
    public void ExportRejectsAChannelOutsideTheSixteen(int channel)
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiIo.Export(buffer, Path.Combine(_work, "bad.mid"), new MidiExportOptions(Channel: channel)));

        Assert.Equal("options", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void ExportRejectsANonPositiveTempo(int bpm)
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiIo.Export(buffer, Path.Combine(_work, "bad.mid"), new MidiExportOptions(Bpm: bpm)));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void ARejectedExportLeavesNoHalfWrittenFile()
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        var path = Path.Combine(_work, "rejected.mid");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiIo.Export(buffer, path, new MidiExportOptions(Channel: 99)));

        Assert.False(File.Exists(path), "a rejected export left a file behind");
    }

    [Fact]
    public void ARejectedExportDoesNotDestroyTheFileAlreadyThere()
    {
        // File.Create truncates. Validating the options only after opening the file meant a
        // mistyped channel wiped the caller's previous export before it threw.
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        var path = Path.Combine(_work, "existing.mid");

        MidiIo.Export(buffer, path);
        var before = File.ReadAllBytes(path);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MidiIo.Export(buffer, path, new MidiExportOptions(Channel: 99)));

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ANoteMidiCannotRepresent_DoesNotDestroyTheFileEither()
    {
        // A negative offset is rejected while the file is being built, which now happens
        // before the destination is opened.
        using var good = new NoteBuffer(1);
        good.AddNote(60, Rational.Zero, Rational.Quarter);
        var path = Path.Combine(_work, "keepme.mid");
        MidiIo.Export(good, path);
        var before = File.ReadAllBytes(path);

        using var bad = new NoteBuffer(1);
        bad.AddNote(60, new Rational(-1, 4), Rational.Quarter);

        Assert.Throws<ArgumentException>(() => MidiIo.Export(bad, path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void EveryValidChannelRoundTrips()
    {
        for (var channel = 0; channel <= 15; channel++)
        {
            using var buffer = new NoteBuffer(1);
            buffer.AddNote(60, Rational.Zero, Rational.Quarter);
            var path = Path.Combine(_work, $"ch{channel}.mid");

            MidiIo.Export(buffer, path, new MidiExportOptions(Channel: channel));

            using var reread = MidiIo.Import(path, new MidiImportOptions(Channel: channel));
            Assert.Equal(1, reread.Count);
        }
    }
}
