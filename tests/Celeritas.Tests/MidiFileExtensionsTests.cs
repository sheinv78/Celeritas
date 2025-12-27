using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Tests;

public class MidiFileExtensionsTests
{
    [Fact]
    public void GetStatistics_ReturnsBasicCountsAndRanges()
    {
        var file = CreateMidiFile(
            new[]
            {
                new[]
                {
                    (note: 60, channel: 0, time: 0L, length: 240),
                    (note: 72, channel: 0, time: 240L, length: 240)
                }
            });

        var stats = file.GetStatistics();

        Assert.Equal(1, stats.TrackCount);
        Assert.Equal(2, stats.NoteCount);
        Assert.Equal(60, stats.MinNoteNumber);
        Assert.Equal(72, stats.MaxNoteNumber);
        Assert.Contains(0, stats.Channels);
        Assert.True(stats.TotalTicks >= 480);
        Assert.True(stats.TotalBeats.ToDouble() > 0);
    }

    [Fact]
    public void Clone_IsIndependentFromOriginal()
    {
        var file = CreateMidiFile(
            new[]
            {
                new[]
                {
                    (note: 60, channel: 0, time: 0L, length: 240)
                }
            });

        var clone = file.Clone();
        clone.Chunks.Add(new TrackChunk());

        Assert.NotEqual(file.Chunks.Count, clone.Chunks.Count);
        Assert.Single(file.GetNotes());
        Assert.Single(clone.GetNotes());
    }

    [Fact]
    public void Merge_CombinesTracksAndNotes()
    {
        var a = CreateMidiFile(
            new[]
            {
                new[] { (note: 60, channel: 0, time: 0L, length: 240) }
            });

        var b = CreateMidiFile(
            new[]
            {
                new[] { (note: 64, channel: 0, time: 0L, length: 240) }
            });

        var merged = a.Merge(b);

        Assert.Equal(2, merged.Chunks.OfType<TrackChunk>().Count());
        Assert.Equal(2, merged.GetNotes().Count);
    }

    [Fact]
    public void SplitByTrack_ReturnsOneTrackPerFile()
    {
        var file = CreateMidiFile(
            new[]
            {
                new[] { (note: 60, channel: 0, time: 0L, length: 240) },
                new[] { (note: 64, channel: 0, time: 0L, length: 240) }
            });

        var split = file.Split(MidiSplitMode.Track);

        Assert.Equal(2, split.Count);
        Assert.All(split, f => Assert.Single(f.Chunks.OfType<TrackChunk>()));
        Assert.All(split, f => Assert.Single(f.GetNotes()));
    }

    [Fact]
    public void SplitByChannel_FiltersNotes()
    {
        var file = CreateMidiFile(
            new[]
            {
                new[]
                {
                    (note: 60, channel: 0, time: 0L, length: 240),
                    (note: 64, channel: 1, time: 0L, length: 240),
                }
            });

        var split = file.Split(MidiSplitMode.Channel);

        Assert.Equal(2, split.Count);

        foreach (var f in split)
        {
            var notes = f.GetNotes();
            Assert.True(notes.Count > 0);

            var channel = notes.First().Channel;
            Assert.All(notes, n => Assert.Equal(channel, n.Channel));
        }
    }

    [Fact]
    public void MergeToSingleTrack_MergesEventsByTime()
    {
        var a = CreateMidiFile(
            new[]
            {
                new[] { (note: 60, channel: 0, time: 0L, length: 240) }
            });

        var b = CreateMidiFile(
            new[]
            {
                new[] { (note: 64, channel: 0, time: 480L, length: 240) }
            });

        var merged = a.MergeToSingleTrack(b);

        Assert.Single(merged.Chunks.OfType<TrackChunk>());
        Assert.Equal(2, merged.GetNotes().Count);

        var times = merged.GetNotes().Select(n => n.Time).OrderBy(t => t).ToArray();
        Assert.Equal(0L, times[0]);
        Assert.Equal(480L, times[1]);
    }

    private static MidiFile CreateMidiFile((int note, int channel, long time, int length)[][] tracks)
    {
        var midiFile = new MidiFile
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480)
        };

        foreach (var trackNotes in tracks)
        {
            var track = new TrackChunk();

            // Add a tempo event just to ensure stats can count it.
            track.Events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(120).MicrosecondsPerQuarterNote));

            using (var notesManager = track.ManageNotes())
            {
                foreach (var (note, channel, time, length) in trackNotes)
                {
                    var n = new Note((SevenBitNumber)note, length, time)
                    {
                        Channel = (FourBitNumber)channel,
                        Velocity = (SevenBitNumber)100
                    };

                    notesManager.Objects.Add(n);
                }

                notesManager.SaveChanges();
            }

            midiFile.Chunks.Add(track);
        }

        return midiFile;
    }
}
