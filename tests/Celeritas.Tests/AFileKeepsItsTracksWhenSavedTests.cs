// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// Every one-track MIDI file this library wrote came back from disk as two. The writer's default
/// is SMF format 1, and in that format DryWetMidi moves the meta events of a lone track into a
/// first track of their own — so <see cref="MidiIo.Export(NoteBuffer, Stream, MidiExportOptions?)"/>,
/// documented as single-track, wrote a tempo track and a note track; a track added as "lead" was
/// saved as an empty track called "lead" beside an unnamed track holding its notes; a
/// <see cref="MidiFileExtensions.MergeToSingleTrack(MidiFile, MidiFile)"/> result saved as two
/// tracks; and <see cref="MidiFileExtensions.Clone"/> returned a file whose track count differed
/// from its original's. A file is now written in the format that holds its layout: format 0 for
/// one track, format 1 for more, format 2 kept for a file that was read as format 2.
/// </summary>
public class AFileKeepsItsTracksWhenSavedTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-layout").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string Path(string name) => System.IO.Path.Combine(_work, name);

    private static NoteEvent[] Phrase(int transpose = 0) =>
    [
        new(60 + transpose, Rational.Zero, Rational.Quarter),
        new(64 + transpose, Rational.Quarter, Rational.Quarter),
        new(67 + transpose, Rational.Half, Rational.Half),
    ];

    /// <summary>The names on each track chunk, in order, with the notes each one holds.</summary>
    private static string Layout(MidiFile file) =>
        string.Join(" | ", file.GetTrackChunks().Select(chunk =>
        {
            var name = chunk.Events.OfType<SequenceTrackNameEvent>().Select(e => e.Text).FirstOrDefault() ?? "-";
            var notes = chunk.Events.OfType<NoteOnEvent>().Count();
            return $"{name}:{notes}";
        }));

    private MidiFile SaveAndRead(MidiFile file)
    {
        var path = Path($"{Guid.NewGuid():N}.mid");
        file.Save(path);
        return MidiFile.Read(path);
    }

    [Fact]
    public void AnExportedBufferIsOneTrackAsItsDocumentationSays()
    {
        using var buffer = new NoteBuffer(4);
        foreach (var n in Phrase()) buffer.AddNote(n.Pitch, n.Offset, n.Duration, n.Velocity);

        using var stream = new MemoryStream();
        MidiIo.Export(buffer, stream);
        stream.Position = 0;
        var read = MidiFile.Read(stream);

        Assert.Equal(MidiFileFormat.SingleTrack, read.OriginalFormat);
        var track = Assert.Single(read.GetTrackChunks());
        Assert.Contains(track.Events, e => e is SetTempoEvent);
        Assert.Equal(3, track.Events.OfType<NoteOnEvent>().Count());
        Assert.Equal(1, read.GetStatistics().TrackCount);

        // ...and the same by path.
        var path = Path("exported.mid");
        MidiIo.Export(buffer, path);
        Assert.Equal(1, MidiFile.Read(path).GetStatistics().TrackCount);
    }

    [Fact]
    public void ATrackKeepsItsNameWithItsNotes()
    {
        var file = new MidiFile();
        file.AddTrack(Phrase(), "lead");

        Assert.Equal("lead:3", Layout(file));
        Assert.Equal("lead:3", Layout(SaveAndRead(file)));
        Assert.Equal("lead:3", Layout(MidiFileExtensions.Clone(file)));
    }

    [Fact]
    public void AMergeToASingleTrackIsStillOneTrackOnDisk()
    {
        var a = new MidiFile();
        a.AddTrack(Phrase(), "a");
        var b = new MidiFile();
        b.AddTrack(Phrase(7), "b");

        var merged = a.MergeToSingleTrack(b);
        var read = SaveAndRead(merged);

        Assert.Single(read.GetTrackChunks());
        Assert.Equal(6, read.GetStatistics().NoteCount);
        Assert.Single(read.Split(MidiSplitMode.Track));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void SavingAndCloningKeepTheLayoutWhateverTheTrackCount(int tracks)
    {
        var file = new MidiFile();
        for (var i = 0; i < tracks; i++)
        {
            file.AddTrack(Phrase(i), $"part {i}");
        }

        var expected = Layout(file);
        Assert.Equal(tracks, file.GetTrackChunks().Count());

        Assert.Equal(expected, Layout(SaveAndRead(file)));
        Assert.Equal(expected, Layout(MidiFileExtensions.Clone(file)));
        Assert.Equal(tracks, MidiFileExtensions.Clone(file).GetStatistics().TrackCount);
    }

    [Fact]
    public void AFileReadAsFormatTwoIsSavedAsFormatTwo()
    {
        // Format 2 means independent sequences, not simultaneous tracks. Saving it as format 1
        // would keep the chunks and change what they mean.
        var file = new MidiFile();
        file.AddTrack(Phrase(), "first sequence");
        file.AddTrack(Phrase(5), "second sequence");

        var path = Path("sequences.mid");
        using (var stream = File.Create(path))
        {
            file.Write(stream, MidiFileFormat.MultiSequence);
        }

        var read = MidiFile.Read(path);
        Assert.Equal(MidiFileFormat.MultiSequence, read.OriginalFormat);

        var saved = SaveAndRead(read);
        Assert.Equal(MidiFileFormat.MultiSequence, saved.OriginalFormat);
        Assert.Equal(Layout(read), Layout(saved));
    }

    [Fact]
    public void AFormatOneFileOfOneTrackIsWrittenAsFormatZeroWithNothingMoved()
    {
        // A format-1 file that happens to hold a single track: format 1 again would split it,
        // format 0 holds it as it is. The format byte changes; the music and its layout do not.
        var file = new MidiFile();
        file.AddTrack(Phrase(), "only");
        var path = Path("one-track-format-one.mid");
        using (var stream = File.Create(path))
        {
            // A single chunk with no meta events is what format 1 leaves alone.
            new MidiFile(new TrackChunk(file.GetTrackChunks().Single().Events.Where(e => e is not SequenceTrackNameEvent)))
                .Write(stream, MidiFileFormat.MultiTrack);
        }

        var read = MidiFile.Read(path);
        Assert.Equal(MidiFileFormat.MultiTrack, read.OriginalFormat);
        Assert.Single(read.GetTrackChunks());

        var saved = SaveAndRead(read);
        Assert.Equal(MidiFileFormat.SingleTrack, saved.OriginalFormat);
        Assert.Equal(Layout(read), Layout(saved));
        Assert.Equal(read.GetStatistics().NoteCount, saved.GetStatistics().NoteCount);
    }
}
