// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Celeritas.Core;

namespace Celeritas.Core.Midi;

public enum MidiSplitMode
{
    Track,
    Channel
}

public enum MidiMergeMode
{
    /// <summary>
    /// Keep tracks separate (default behavior of <see cref="MidiFileExtensions.Merge"/>).
    /// </summary>
    AppendTracks,

    /// <summary>
    /// Merge all track events into a single track, sorted by absolute time.
    /// </summary>
    SingleTrack
}

public sealed record MidiFileStatistics(
    int TrackCount,
    int NoteCount,
    long TotalTicks,
    Rational TotalBeats,
    int? MinNoteNumber,
    int? MaxNoteNumber,
    IReadOnlyList<int> Channels,
    int TempoChangeCount,
    int TimeSignatureChangeCount);

public static class MidiFileExtensions
{
    public static void Save(this MidiFile file, string path)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var stream = File.Create(path);
        file.Write(stream);
    }

    public static TrackChunk AddTrack(this MidiFile file, NoteEvent[] notes, string? name = null, MidiExportOptions? options = null)
        => AddTrack(file, notes.AsSpan(), name, options);

    public static TrackChunk AddTrack(this MidiFile file, ReadOnlySpan<NoteEvent> notes, string? name = null, MidiExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        options ??= new MidiExportOptions();

        if (file.TimeDivision is not TicksPerQuarterNoteTimeDivision tpq)
        {
            file.TimeDivision = new TicksPerQuarterNoteTimeDivision((short)options.TicksPerQuarterNote);
            tpq = (TicksPerQuarterNoteTimeDivision)file.TimeDivision;
        }

        var ticksPerQuarter = tpq.TicksPerQuarterNote;
        if (ticksPerQuarter <= 0)
        {
            throw new InvalidOperationException("Invalid ticks-per-quarter-note value.");
        }

        var track = new TrackChunk();
        if (!string.IsNullOrWhiteSpace(name))
        {
            track.Events.Add(new SequenceTrackNameEvent(name));
        }

        using var notesManager = track.ManageNotes();
        var channel = (FourBitNumber)options.Channel;

        foreach (var e in notes)
        {
            var noteNumber = Math.Clamp(e.Pitch, 0, 127);

            var timeTicks = MidiIo.BeatsToTicks(e.Offset, ticksPerQuarter);
            var lengthTicks = Math.Max(1, MidiIo.BeatsToTicks(e.Duration, ticksPerQuarter));
            var lengthTicksInt = lengthTicks > int.MaxValue ? int.MaxValue : (int)lengthTicks;

            var velocity = (byte)Math.Clamp((int)Math.Round(e.Velocity * 127.0), 1, 127);
            if (velocity == 0)
            {
                velocity = options.DefaultVelocity;
            }

            var note = new Note((SevenBitNumber)noteNumber, lengthTicksInt, timeTicks)
            {
                Channel = channel,
                Velocity = (SevenBitNumber)velocity
            };

            notesManager.Objects.Add(note);
        }

        notesManager.SaveChanges();
        file.Chunks.Add(track);
        return track;
    }

    public static void SetTempo(this MidiFile file, int bpm)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (bpm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "BPM must be positive.");
        }

        var tempo = Tempo.FromBeatsPerMinute(bpm);

        // Use the first track as a conductor track if present.
        var track = file.Chunks.OfType<TrackChunk>().FirstOrDefault();
        if (track == null)
        {
            track = new TrackChunk();
            file.Chunks.Add(track);
        }

        // Insert tempo at time=0 without shifting existing events.
        track.Events.Insert(0, new SetTempoEvent(tempo.MicrosecondsPerQuarterNote) { DeltaTime = 0 });
    }

    public static MidiFile Clone(this MidiFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var ms = new MemoryStream();
        file.Write(ms);
        ms.Position = 0;
        return MidiFile.Read(ms);
    }

    public static MidiFile Merge(this MidiFile file, params MidiFile[] others)
    {
        ArgumentNullException.ThrowIfNull(file);

        var sources = new List<MidiFile>(1 + (others?.Length ?? 0)) { file };
        if (others is { Length: > 0 })
        {
            foreach (var other in others)
            {
                if (other == null)
                {
                    throw new ArgumentNullException(nameof(others), "Merge sources must not contain null.");
                }
                sources.Add(other);
            }
        }

        var timeDivision = file.TimeDivision;
        foreach (var source in sources)
        {
            if (!Equals(source.TimeDivision, timeDivision))
            {
                throw new ArgumentException("All MIDI files must have the same TimeDivision to merge.", nameof(others));
            }
        }

        var merged = new MidiFile
        {
            TimeDivision = timeDivision
        };

        foreach (var source in sources)
        {
            var cloned = source.Clone();
            foreach (var chunk in cloned.Chunks)
            {
                merged.Chunks.Add(chunk);
            }
        }

        return merged;
    }

    /// <summary>
    /// Merge multiple MIDI files into a single-track MIDI file.
    /// This preserves timing by merging events by absolute tick time and recomputing delta-times.
    /// </summary>
    public static MidiFile MergeToSingleTrack(this MidiFile file, params MidiFile[] others)
    {
        ArgumentNullException.ThrowIfNull(file);

        var sources = new List<MidiFile>(1 + (others?.Length ?? 0)) { file };
        if (others is { Length: > 0 })
        {
            foreach (var other in others)
            {
                if (other == null)
                {
                    throw new ArgumentNullException(nameof(others), "Merge sources must not contain null.");
                }
                sources.Add(other);
            }
        }

        var timeDivision = file.TimeDivision;
        foreach (var source in sources)
        {
            if (!Equals(source.TimeDivision, timeDivision))
            {
                throw new ArgumentException("All MIDI files must have the same TimeDivision to merge.", nameof(others));
            }
        }

        // Collect all events from all tracks with their absolute times.
        var collected = new List<(long Time, int Order, MidiEvent Event)>();
        var order = 0;
        MidiEvent? endOfTrackPrototype = null;

        foreach (var source in sources)
        {
            var cloned = source.Clone();

            foreach (var chunk in cloned.Chunks)
            {
                if (chunk is not TrackChunk track)
                {
                    continue;
                }

                long abs = 0;
                foreach (var evt in track.Events)
                {
                    abs += evt.DeltaTime;

                    // We'll add a single EndOfTrack at the end.
                    if (evt is EndOfTrackEvent)
                    {
                        endOfTrackPrototype ??= evt;
                        continue;
                    }

                    var clonedEvent = CloneEvent(evt);
                    collected.Add((abs, order++, clonedEvent));
                }
            }
        }

        collected.Sort(static (a, b) =>
        {
            var cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
        });

        var mergedTrack = new TrackChunk();
        long prev = 0;
        foreach (var item in collected)
        {
            var delta = item.Time - prev;
            if (delta < 0)
            {
                delta = 0;
            }

            item.Event.DeltaTime = delta;
            mergedTrack.Events.Add(item.Event);
            prev = item.Time;
        }

        // EndOfTrackEvent has no public constructors in this DryWetMIDI version.
        // Clone one from sources and append it once.
        if (endOfTrackPrototype is not null)
        {
            var eot = CloneEvent(endOfTrackPrototype);
            eot.DeltaTime = 0;
            mergedTrack.Events.Add(eot);
        }

        return new MidiFile(mergedTrack)
        {
            TimeDivision = timeDivision
        };
    }

    public static IReadOnlyList<MidiFile> Split(this MidiFile file, MidiSplitMode mode)
    {
        ArgumentNullException.ThrowIfNull(file);

        return mode switch
        {
            MidiSplitMode.Track => SplitByTrack(file),
            MidiSplitMode.Channel => SplitByChannel(file),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown split mode.")
        };
    }

    public static MidiFileStatistics GetStatistics(this MidiFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var trackCount = file.Chunks.OfType<TrackChunk>().Count();

        var ticksPerQuarter = file.TimeDivision is TicksPerQuarterNoteTimeDivision tpq
            ? tpq.TicksPerQuarterNote
            : 480;

        var noteCollection = file.GetNotes();
        var noteCount = noteCollection.Count;

        int? minNoteNumber = null;
        int? maxNoteNumber = null;
        var channels = new HashSet<int>();

        long maxNoteEnd = 0;
        foreach (var note in noteCollection)
        {
            var nn = (int)note.NoteNumber;
            minNoteNumber = minNoteNumber.HasValue ? Math.Min(minNoteNumber.Value, nn) : nn;
            maxNoteNumber = maxNoteNumber.HasValue ? Math.Max(maxNoteNumber.Value, nn) : nn;

            channels.Add(note.Channel);

            var end = note.Time + note.Length;
            if (end > maxNoteEnd)
            {
                maxNoteEnd = end;
            }
        }

        var tempoCount = 0;
        var timeSignatureCount = 0;
        long maxEventTime = 0;

        foreach (var chunk in file.Chunks)
        {
            if (chunk is not TrackChunk track)
            {
                continue;
            }

            long abs = 0;
            foreach (var evt in track.Events)
            {
                abs += evt.DeltaTime;
                if (abs > maxEventTime)
                {
                    maxEventTime = abs;
                }

                if (evt is SetTempoEvent)
                {
                    tempoCount++;
                }
                else if (evt is TimeSignatureEvent)
                {
                    timeSignatureCount++;
                }
                else if (evt is ChannelEvent ce)
                {
                    channels.Add(ce.Channel);
                }
            }
        }

        var totalTicks = Math.Max(maxNoteEnd, maxEventTime);
        var totalBeats = new Rational(totalTicks, ticksPerQuarter);

        return new MidiFileStatistics(
            TrackCount: trackCount,
            NoteCount: noteCount,
            TotalTicks: totalTicks,
            TotalBeats: totalBeats,
            MinNoteNumber: minNoteNumber,
            MaxNoteNumber: maxNoteNumber,
            Channels: channels.OrderBy(c => c).ToArray(),
            TempoChangeCount: tempoCount,
            TimeSignatureChangeCount: timeSignatureCount);
    }

    private static IReadOnlyList<MidiFile> SplitByTrack(MidiFile file)
    {
        // Clone first so outputs don't share references with the input.
        var cloned = file.Clone();

        var results = new List<MidiFile>();
        foreach (var track in cloned.Chunks.OfType<TrackChunk>())
        {
            var outFile = new MidiFile
            {
                TimeDivision = cloned.TimeDivision
            };

            outFile.Chunks.Add(track);
            results.Add(outFile);
        }

        return results;
    }

    private static IReadOnlyList<MidiFile> SplitByChannel(MidiFile file)
    {
        var cloned = file.Clone();

        var channels = DetectChannels(cloned);
        if (channels.Count == 0)
        {
            return [];
        }

        var results = new List<MidiFile>(channels.Count);

        foreach (var channel in channels.OrderBy(c => c))
        {
            var outFile = new MidiFile
            {
                TimeDivision = cloned.TimeDivision
            };

            foreach (var chunk in cloned.Chunks)
            {
                if (chunk is not TrackChunk track)
                {
                    continue;
                }

                var filteredTrack = FilterTrackChunkByChannel(track, (FourBitNumber)channel);

                // Keep the track if it contains any channel events (notes, etc.)
                // or any global/meta events.
                if (filteredTrack.Events.Count > 0)
                {
                    outFile.Chunks.Add(filteredTrack);
                }
            }

            results.Add(outFile);
        }

        return results;
    }

    private static HashSet<int> DetectChannels(MidiFile file)
    {
        var channels = new HashSet<int>();

        foreach (var note in file.GetNotes())
        {
            channels.Add(note.Channel);
        }

        foreach (var chunk in file.Chunks)
        {
            if (chunk is not TrackChunk track)
            {
                continue;
            }

            foreach (var evt in track.Events)
            {
                if (evt is ChannelEvent ce)
                {
                    channels.Add(ce.Channel);
                }
            }
        }

        return channels;
    }

    private static TrackChunk FilterTrackChunkByChannel(TrackChunk track, FourBitNumber channel)
    {
        var filtered = new TrackChunk();

        long abs = 0;
        long prevKept = 0;

        foreach (var evt in track.Events)
        {
            abs += evt.DeltaTime;

            var keep = evt is not ChannelEvent ce || ce.Channel == channel;
            if (!keep)
            {
                continue;
            }

            // Clone via serialization to avoid depending on DryWetMIDI's clone API.
            var clonedEvent = CloneEvent(evt);
            clonedEvent.DeltaTime = abs - prevKept;
            prevKept = abs;

            filtered.Events.Add(clonedEvent);
        }

        return filtered;
    }

    private static MidiEvent CloneEvent(MidiEvent evt)
    {
        using var ms = new MemoryStream();
        var tempTrack = new TrackChunk();
        tempTrack.Events.Add(evt);

        var tempFile = new MidiFile(tempTrack);
        tempFile.Write(ms);

        ms.Position = 0;
        var readBack = MidiFile.Read(ms);
        var track = readBack.Chunks.OfType<TrackChunk>().First();
        return track.Events.First();
    }
}
