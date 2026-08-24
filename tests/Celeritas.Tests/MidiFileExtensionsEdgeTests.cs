// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// The MIDI extension paths that ordinary files never take: a non-tick time division, a file
/// with no track at all, an explicit round-trip clone, and the tempo-replacement walk. A MIDI
/// file that comes out wrong here still opens in a sequencer, so only an assertion catches it.
/// </summary>
public class MidiFileExtensionsEdgeTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-midiedge").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static MidiFile SmpteFile() => new()
    {
        TimeDivision = new SmpteTimeDivision(SmpteFormat.ThirtyDrop, 80),
    };

    private static NoteEvent[] OneNote() => [new(60, Rational.Zero, Rational.Quarter)];

    // ---------- a file whose time division is not ticks-per-quarter ----------

    [Fact]
    public void AddTrack_OnASmpteFile_SwitchesItToTicksPerQuarter()
    {
        var file = SmpteFile();

        file.AddTrack(OneNote(), "melody", new MidiExportOptions { TicksPerQuarterNote = 480 });

        var division = Assert.IsType<TicksPerQuarterNoteTimeDivision>(file.TimeDivision);
        Assert.Equal(480, division.TicksPerQuarterNote);
        Assert.Single(file.GetTrackChunks());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(short.MaxValue + 1)]
    public void AddTrack_OnASmpteFile_RejectsATickResolutionMidiCannotStore(int ticks)
    {
        var file = SmpteFile();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => file.AddTrack(OneNote(), options: new MidiExportOptions { TicksPerQuarterNote = ticks }));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void GetStatistics_OnASmpteFile_SaysItIsNotSupported()
    {
        // Notes cannot be placed in musical time without a tick-per-quarter reference, so the
        // honest answer is a refusal rather than a plausible-looking duration.
        var file = SmpteFile();

        Assert.Throws<NotSupportedException>(() => file.GetStatistics());
    }

    // ---------- tempo ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void SetTempo_RejectsANonPositiveBpm(int bpm)
    {
        var file = new MidiFile();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => file.SetTempo(bpm));
        Assert.Equal("bpm", ex.ParamName);
    }

    [Fact]
    public void SetTempo_OnAFileWithNoTrack_CreatesTheConductorTrack()
    {
        var file = new MidiFile();

        file.SetTempo(120);

        var track = Assert.Single(file.GetTrackChunks());
        Assert.Contains(track.Events, e => e is SetTempoEvent);
    }

    [Fact]
    public void SetTempo_ReplacesTheExistingTempo_KeepingTheOtherEvents()
    {
        var file = new MidiFile();
        file.AddTrack(OneNote());
        file.SetTempo(90);

        file.SetTempo(140);

        var track = file.GetTrackChunks().First();
        var tempos = track.Events.OfType<SetTempoEvent>().ToArray();

        Assert.Single(tempos);
        Assert.Equal(60_000_000L / 140, tempos[0].MicrosecondsPerQuarterNote);
        Assert.Contains(track.Events, e => e is NoteOnEvent);
    }

    [Fact]
    public void SetTempo_WalksPastTheOtherTickZeroEvents_AndKeepsLaterTempoChanges()
    {
        // The walk removes tempo events at tick 0 and steps over everything else there. A
        // mis-stepped index would drop what it walked past; a walk that ran too far would
        // swallow the tempo change at tick 10, which the contract keeps.
        var file = new MidiFile();
        var track = new TrackChunk(
            new TextEvent("start"),
            new SetTempoEvent(500_000),
            new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100),
            new SetTempoEvent(400_000) { DeltaTime = 10 },
            new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { DeltaTime = 10 });
        file.Chunks.Add(track);

        file.SetTempo(60);

        var tempos = track.Events.OfType<SetTempoEvent>().ToArray();
        Assert.Equal(2, tempos.Length);
        Assert.Equal(1_000_000L, tempos[0].MicrosecondsPerQuarterNote);   // the one just set
        Assert.Equal(400_000L, tempos[1].MicrosecondsPerQuarterNote);     // the later change
        Assert.Contains(track.Events, e => e is TextEvent);
        Assert.Contains(track.Events, e => e is NoteOnEvent);
        Assert.Contains(track.Events, e => e is NoteOffEvent);
    }

    // ---------- the round-trip clone ----------

    [Fact]
    public void TheRoundTripClone_ReproducesTheFile()
    {
        // MidiFile has its own Clone, and an instance method beats an extension, so this one
        // is only reachable through its static form. It writes and re-reads rather than
        // copying in memory.
        var file = new MidiFile();
        file.AddTrack([new NoteEvent(60, Rational.Zero, Rational.Quarter), new NoteEvent(64, Rational.Quarter, Rational.Quarter)]);
        file.SetTempo(100);

        var copy = MidiFileExtensions.Clone(file);

        Assert.NotSame(file, copy);
        Assert.Equal(
            file.GetTrackChunks().Sum(t => t.Events.OfType<NoteOnEvent>().Count()),
            copy.GetTrackChunks().Sum(t => t.Events.OfType<NoteOnEvent>().Count()));
        Assert.Equal(file.TimeDivision, copy.TimeDivision);
        Assert.Equal(
            file.GetTrackChunks().First().Events.OfType<SetTempoEvent>().First().MicrosecondsPerQuarterNote,
            copy.GetTrackChunks().First().Events.OfType<SetTempoEvent>().First().MicrosecondsPerQuarterNote);
    }

    [Fact]
    public void TheRoundTripClone_IsIndependentOfTheOriginal()
    {
        var file = new MidiFile();
        file.AddTrack(OneNote());

        var copy = MidiFileExtensions.Clone(file);
        file.AddTrack([new NoteEvent(72, Rational.Zero, Rational.Quarter)], "extra");

        Assert.Single(copy.GetTrackChunks());
    }

    [Fact]
    public void TheRoundTripClone_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => MidiFileExtensions.Clone(null!));
    }

    // ---------- merging and splitting the awkward cases ----------

    [Fact]
    public void MergeToSingleTrack_InterleavesTheSourcesByTime()
    {
        // DryWetMidi keeps the end-of-track marker out of Events entirely, so the merge only
        // has to order what it is given: the later note must come second even though it
        // arrives from the second file.
        var a = new MidiFile(new TrackChunk(
            new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100),
            new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { DeltaTime = 200 }));
        var b = new MidiFile(new TrackChunk(
            new NoteOnEvent((SevenBitNumber)67, (SevenBitNumber)100) { DeltaTime = 50 },
            new NoteOffEvent((SevenBitNumber)67, (SevenBitNumber)0) { DeltaTime = 100 }));

        var merged = a.MergeToSingleTrack(b);

        var track = Assert.Single(merged.GetTrackChunks());
        Assert.Equal(2, track.Events.OfType<NoteOnEvent>().Count());
        Assert.Equal([60, 67], track.Events.OfType<NoteOnEvent>().Select(e => (int)e.NoteNumber));
        Assert.Empty(track.Events.OfType<EndOfTrackEvent>());
    }

    [Fact]
    public void MergeToSingleTrack_RefusesFilesWithDifferentTimeDivisions()
    {
        var a = new MidiFile(new TrackChunk()) { TimeDivision = new TicksPerQuarterNoteTimeDivision(96) };
        var b = new MidiFile(new TrackChunk()) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };

        Assert.Throws<ArgumentException>(() => a.MergeToSingleTrack(b));
    }

    [Fact]
    public void Split_ByChannel_OnAFileWithNoNotes_YieldsNothing()
    {
        var file = new MidiFile();
        file.Chunks.Add(new TrackChunk());

        Assert.Empty(file.Split(MidiSplitMode.Channel));
    }

    private MidiFile WrittenAndReread(params NoteEvent[] notes)
    {
        var file = new MidiFile();
        file.AddTrack(notes);
        var path = System.IO.Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
        file.Write(path);
        return MidiFile.Read(path);
    }
}
