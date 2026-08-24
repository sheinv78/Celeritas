// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// The MidiFile extension surface — saving, adding tracks, merging, splitting and statistics.
/// These are file-shaped operations whose failures are quiet: a merge that drops a track still
/// returns a file, and a track written at the wrong tick still plays.
/// </summary>
public class MidiFileExtensionsTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-midiext").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string Path(string name) => System.IO.Path.Combine(_work, name);

    private static NoteEvent[] Phrase() =>
    [
        new(60, Rational.Zero, Rational.Quarter),
        new(64, Rational.Quarter, Rational.Quarter),
        new(67, Rational.Half, Rational.Quarter),
        new(72, new Rational(3, 4), Rational.Quarter),
    ];

    private MidiFile FileWith(params NoteEvent[] notes)
    {
        using var buffer = new NoteBuffer(Math.Max(1, notes.Length));
        foreach (var n in notes) buffer.AddNote(n.Pitch, n.Offset, n.Duration, n.Velocity);
        var path = Path($"src{Guid.NewGuid():N}.mid");
        MidiIo.Export(buffer, path);
        return MidiFile.Read(path);
    }

    // ---------- AddTrack ----------

    [Fact]
    public void AddTrack_AddsAChunkWhoseNotesSurviveAReRead()
    {
        var file = FileWith(Phrase());
        var before = file.GetTrackChunks().Count();

        file.AddTrack([new NoteEvent(48, Rational.Zero, Rational.Whole)], "bass");

        Assert.Equal(before + 1, file.GetTrackChunks().Count());

        var path = Path("added.mid");
        file.Write(path, true);
        using var reread = MidiIo.Import(path);
        Assert.Contains(Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch), p => p == 48);
    }

    [Fact]
    public void AddTrack_NegativeOffset_IsRejectedWithTheSameContractAsExport()
    {
        var file = FileWith(Phrase());

        var ex = Assert.Throws<ArgumentException>(
            () => file.AddTrack([new NoteEvent(60, new Rational(-1, 2), Rational.Quarter)]));

        Assert.Equal("notes", ex.ParamName);
    }

    [Fact]
    public void AddTrack_SilentNote_StillSoundsAtTheDefaultVelocity()
    {
        // MIDI velocity 0 is a note-off; a note asked for at zero velocity must not vanish.
        var file = FileWith(Phrase());

        file.AddTrack([new NoteEvent(55, Rational.Zero, Rational.Quarter, 0f)]);

        var path = Path("silent.mid");
        file.Write(path, true);
        using var reread = MidiIo.Import(path);
        Assert.Contains(Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch), p => p == 55);
    }

    [Fact]
    public void AddTrack_EmptyNotes_AddsAnEmptyTrackRatherThanThrowing()
    {
        var file = FileWith(Phrase());
        var before = file.GetTrackChunks().Count();

        file.AddTrack([], "empty");

        Assert.Equal(before + 1, file.GetTrackChunks().Count());
    }

    // ---------- Save ----------

    [Fact]
    public void Save_WritesAFileThatReadsBack()
    {
        var file = FileWith(Phrase());
        var path = Path("saved.mid");

        file.Save(path);

        Assert.True(File.Exists(path));
        using var reread = MidiIo.Import(path);
        Assert.Equal(Phrase().Length, reread.Count);
    }

    // ---------- Clone ----------

    [Fact]
    public void Clone_IsIndependentOfTheOriginal()
    {
        var file = FileWith(Phrase());

        var clone = file.Clone();
        clone.AddTrack([new NoteEvent(36, Rational.Zero, Rational.Whole)], "added to the clone");

        Assert.NotEqual(file.GetTrackChunks().Count(), clone.GetTrackChunks().Count());
    }

    // ---------- Merge ----------

    [Fact]
    public void Merge_KeepsTheTracksOfBothFiles()
    {
        var a = FileWith(new NoteEvent(60, Rational.Zero, Rational.Quarter));
        var b = FileWith(new NoteEvent(67, Rational.Zero, Rational.Quarter));

        var merged = a.Merge(b);

        Assert.Equal(a.GetTrackChunks().Count() + b.GetTrackChunks().Count(),
            merged.GetTrackChunks().Count());
    }

    [Fact]
    public void Merge_SeveralFiles_KeepsEveryNote()
    {
        var a = FileWith(new NoteEvent(60, Rational.Zero, Rational.Quarter));
        var b = FileWith(new NoteEvent(64, Rational.Zero, Rational.Quarter));
        var c = FileWith(new NoteEvent(67, Rational.Zero, Rational.Quarter));

        var merged = a.Merge(b, c);
        var path = Path("merged.mid");
        merged.Write(path, true);

        using var reread = MidiIo.Import(path);
        var pitches = Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch).ToHashSet();
        Assert.Equal(new[] { 60, 64, 67 }.ToHashSet(), pitches);
    }

    [Fact]
    public void MergeToSingleTrack_CollapsesToOneChunk_WithoutLosingNotes()
    {
        var a = FileWith(new NoteEvent(60, Rational.Zero, Rational.Quarter));
        var b = FileWith(new NoteEvent(67, Rational.Quarter, Rational.Quarter));

        var merged = a.MergeToSingleTrack(b);
        var path = Path("single.mid");
        merged.Write(path, true);

        using var reread = MidiIo.Import(path);
        var pitches = Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch).ToHashSet();
        Assert.Equal(new[] { 60, 67 }.ToHashSet(), pitches);
    }

    [Fact]
    public void MergeToSingleTrack_SeveralFiles_KeepsEveryNote()
    {
        var a = FileWith(new NoteEvent(60, Rational.Zero, Rational.Quarter));
        var b = FileWith(new NoteEvent(64, Rational.Quarter, Rational.Quarter));
        var c = FileWith(new NoteEvent(67, Rational.Half, Rational.Quarter));

        var merged = a.MergeToSingleTrack(b, c);
        var path = Path("single-many.mid");
        merged.Write(path, true);

        using var reread = MidiIo.Import(path);
        Assert.Equal(3, reread.Count);
    }

    // ---------- Split ----------

    [Theory]
    [InlineData(MidiSplitMode.Track)]
    [InlineData(MidiSplitMode.Channel)]
    public void Split_ProducesReadableFiles(MidiSplitMode mode)
    {
        var file = FileWith(Phrase());
        file.AddTrack([new NoteEvent(48, Rational.Zero, Rational.Whole)], "bass");

        var parts = file.Split(mode);

        Assert.NotEmpty(parts);
        for (var i = 0; i < parts.Count; i++)
        {
            var path = Path($"part{(int)mode}-{i}.mid");
            parts[i].Write(path, true);
            Assert.True(File.Exists(path));
        }
    }

    [Fact]
    public void Split_ByTrack_YieldsOnePartPerTrack()
    {
        var file = FileWith(Phrase());
        file.AddTrack([new NoteEvent(48, Rational.Zero, Rational.Whole)], "bass");

        var parts = file.Split(MidiSplitMode.Track);

        Assert.Equal(file.GetTrackChunks().Count(), parts.Count);
    }

    [Fact]
    public void Split_LosesNoNotes()
    {
        var file = FileWith(Phrase());

        var parts = file.Split(MidiSplitMode.Track);

        var recovered = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            var path = Path($"count-{i}.mid");
            parts[i].Write(path, true);
            using var buffer = MidiIo.Import(path);
            recovered += buffer.Count;
        }

        Assert.Equal(Phrase().Length, recovered);
    }

    // ---------- statistics ----------

    [Fact]
    public void Statistics_DescribeTheFile()
    {
        var file = FileWith(Phrase());

        var stats = file.GetStatistics();

        Assert.Equal(Phrase().Length, stats.NoteCount);
        Assert.True(stats.TrackCount >= 1);
        Assert.Equal(60, stats.MinNoteNumber);
        Assert.Equal(72, stats.MaxNoteNumber);
        Assert.True(stats.TotalTicks > 0);
        Assert.True(stats.TotalDuration > Rational.Zero);
        Assert.NotEmpty(stats.Channels);
    }

    [Fact]
    public void Statistics_EmptyFile_ReportsNoNotesRatherThanThrowing()
    {
        var file = new MidiFile();

        var stats = file.GetStatistics();

        Assert.Equal(0, stats.NoteCount);
        Assert.Null(stats.MinNoteNumber);
        Assert.Null(stats.MaxNoteNumber);
    }

    [Fact]
    public void Statistics_ToString_MentionsTheCounts()
    {
        var text = FileWith(Phrase()).GetStatistics().ToString();

        Assert.Contains("4", text, StringComparison.Ordinal);
    }
}
