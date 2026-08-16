// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Core.Midi;

/// <summary>
/// How to split a MIDI file into multiple files.
/// </summary>
public enum MidiSplitMode
{
    /// <summary>One output file per track chunk.</summary>
    Track,

    /// <summary>One output file per MIDI channel.</summary>
    Channel
}

/// <summary>
/// How to combine multiple MIDI files when merging.
/// </summary>
public enum MidiMergeMode
{
    /// <summary>
    /// Keep tracks separate (default behavior of
    /// <see cref="MidiFileExtensions.Merge(Melanchall.DryWetMidi.Core.MidiFile, Melanchall.DryWetMidi.Core.MidiFile)"/>).
    /// </summary>
    AppendTracks,

    /// <summary>
    /// Merge all track events into a single track, sorted by absolute time.
    /// </summary>
    SingleTrack
}

/// <summary>
/// Summary statistics for a MIDI file.
/// </summary>
public sealed record MidiFileStatistics
{
    // Produced by MIDI analysis; not constructible by consumers (#18 API freeze).
    internal MidiFileStatistics(
        int trackCount,
        int noteCount,
        long totalTicks,
        Rational totalDuration,
        int? minNoteNumber,
        int? maxNoteNumber,
        IReadOnlyList<int> channels)
    {
        TrackCount = trackCount;
        NoteCount = noteCount;
        TotalTicks = totalTicks;
        TotalDuration = totalDuration;
        MinNoteNumber = minNoteNumber;
        MaxNoteNumber = maxNoteNumber;
        Channels = channels;
    }

    /// <summary>Number of track chunks in the file.</summary>
    public int TrackCount { get; init; }

    /// <summary>Total number of notes across all tracks.</summary>
    public int NoteCount { get; init; }

    /// <summary>End of the file in absolute MIDI ticks.</summary>
    public long TotalTicks { get; init; }

    /// <summary>Total duration in whole-note units (one 4/4 measure = 1).</summary>
    public Rational TotalDuration { get; init; } // in whole-note units (one 4/4 measure = 1)

    /// <summary>Lowest MIDI note number present, or <see langword="null"/> if there are no notes.</summary>
    public int? MinNoteNumber { get; init; }

    /// <summary>Highest MIDI note number present, or <see langword="null"/> if there are no notes.</summary>
    public int? MaxNoteNumber { get; init; }

    /// <summary>Distinct MIDI channels used, in ascending order.</summary>
    public IReadOnlyList<int> Channels { get; init; }
}

/// <summary>
/// Extension members for reading, writing, and transforming <see cref="MidiFile"/> instances.
/// </summary>
public static class MidiFileExtensions
{
    extension(MidiFile file)
    {
        /// <summary>Writes the file to <paramref name="path"/>.</summary>
        public void Save(string path)
        {
            ArgumentNullException.ThrowIfNull(file);

            using var stream = File.Create(path);
            file.Write(stream);
        }

        /// <summary>Adds a new track built from <paramref name="notes"/> and returns it.</summary>
        /// <param name="notes">Notes to place on the track.</param>
        /// <param name="name">Optional track name written as a sequence/track-name meta event.</param>
        /// <param name="options">Export options controlling channel, velocity, and ticks-per-quarter-note.</param>
        /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">A note has a negative offset, which MIDI cannot represent.</exception>
        public TrackChunk AddTrack(NoteEvent[] notes, string? name = null, MidiExportOptions? options = null)
        {
            // AsSpan() maps null to an empty span, so an unguarded null would quietly add an
            // empty named track — the file gets written, just without the music.
            ArgumentNullException.ThrowIfNull(notes);
            return file.AddTrack(notes.AsSpan(), name, options);
        }

        private TrackChunk AddTrack(ReadOnlySpan<NoteEvent> notes, string? name = null, MidiExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(file);

            options ??= new MidiExportOptions();

            if (file.TimeDivision is not TicksPerQuarterNoteTimeDivision tpq)
            {
                if (options.TicksPerQuarterNote is <= 0 or > short.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), options.TicksPerQuarterNote, "TicksPerQuarterNote must be in [1..32767].");
                }

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

            for (var i = 0; i < notes.Length; i++)
            {
                var e = notes[i];
                var noteNumber = Math.Clamp(e.Pitch, 0, 127);

                if (e.Offset < Rational.Zero)
                {
                    // Without this guard the negative tick time surfaces as a raw DryWetMidi
                    // exception; validate here with the same contract as MidiIo.Export.
                    throw new ArgumentException(
                        $"Note {i} has a negative offset ({e.Offset}); MIDI cannot represent events before time zero.",
                        nameof(notes));
                }

                var timeTicks = MidiIo.WholeNotesToTicks(e.Offset, ticksPerQuarter);
                var lengthTicks = Math.Max(1, MidiIo.WholeNotesToTicks(e.Duration, ticksPerQuarter));
                var lengthTicksInt = lengthTicks > int.MaxValue ? int.MaxValue : (int)lengthTicks;

                var rawVelocity = (int)Math.Round(e.Velocity * 127.0);
                var velocity = rawVelocity <= 0
                    ? Math.Clamp(options.DefaultVelocity, (byte)1, (byte)127)
                    : (byte)Math.Clamp(rawVelocity, 1, 127);

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

        /// <summary>
        /// Sets the tempo at time zero to <paramref name="bpm"/> beats per minute, replacing any
        /// tempo already there. Tempo changes later in the track are preserved.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bpm"/> is not positive.</exception>
        public void SetTempo(int bpm)
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

            // Replace semantics: with same-tick tempo events, the LAST one wins during playback,
            // so merely inserting at index 0 would leave a pre-existing tick-0 tempo in force.
            // Remove every tempo event at tick 0 first (their own delta is necessarily 0, so
            // removal shifts nothing), then insert the new one without moving later events.
            var events = track.Events;
            var index = 0;
            long absoluteTicks = 0;
            while (index < events.Count)
            {
                absoluteTicks += events[index].DeltaTime;
                if (absoluteTicks > 0)
                {
                    break;
                }

                if (events[index] is SetTempoEvent)
                {
                    events.RemoveAt(index);
                    continue;
                }

                index++;
            }

            events.Insert(0, new SetTempoEvent(tempo.MicrosecondsPerQuarterNote) { DeltaTime = 0 });
        }

        /// <summary>Returns a deep copy of the file via a write/read round-trip.</summary>
        public MidiFile Clone()
        {
            ArgumentNullException.ThrowIfNull(file);

            using var ms = new MemoryStream();
            file.Write(ms);
            ms.Position = 0;
            return MidiFile.Read(ms);
        }

        /// <summary>
        /// Merges this file with one other MIDI file, keeping tracks separate.
        /// </summary>
        public MidiFile Merge(MidiFile other)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(other);
            return MergeSources([file, other]);
        }

        /// <summary>
        /// Merges this file with multiple other MIDI files, keeping tracks separate.
        /// </summary>
        public MidiFile Merge(params MidiFile[] others)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(others);
            var sources = new List<MidiFile>(1 + others.Length) { file };
            foreach (var other in others)
            {
                ArgumentNullException.ThrowIfNull(other, nameof(others));
                sources.Add(other);
            }
            return MergeSources(sources);
        }

        /// <summary>
        /// Merges this file with one other MIDI file into a single track, sorted by absolute time.
        /// </summary>
        public MidiFile MergeToSingleTrack(MidiFile other)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(other);
            return MergeToSingleTrackSources([file, other]);
        }

        /// <summary>
        /// Merges this file with multiple other MIDI files into a single track, sorted by absolute time.
        /// </summary>
        public MidiFile MergeToSingleTrack(params MidiFile[] others)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(others);
            var sources = new List<MidiFile>(1 + others.Length) { file };
            foreach (var other in others)
            {
                ArgumentNullException.ThrowIfNull(other, nameof(others));
                sources.Add(other);
            }
            return MergeToSingleTrackSources(sources);
        }

        /// <summary>Splits the file into separate files, one per track or per channel.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a known split mode.</exception>
        public IReadOnlyList<MidiFile> Split(MidiSplitMode mode)
        {
            ArgumentNullException.ThrowIfNull(file);

            return mode switch
            {
                MidiSplitMode.Track => SplitByTrack(file),
                MidiSplitMode.Channel => SplitByChannel(file),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown split mode.")
            };
        }

        /// <summary>Computes summary statistics (track/note counts, pitch range, duration, channels) for the file.</summary>
        /// <exception cref="NotSupportedException">The file does not use ticks-per-quarter-note time division.</exception>
        public MidiFileStatistics GetStatistics()
        {
            ArgumentNullException.ThrowIfNull(file);

            var trackCount = file.Chunks.OfType<TrackChunk>().Count();

            if (file.TimeDivision is not TicksPerQuarterNoteTimeDivision tpq)
            {
                throw new NotSupportedException("Only ticks-per-quarter-note MIDI files are supported.");
            }

            var ticksPerQuarter = tpq.TicksPerQuarterNote;

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

                    if (evt is ChannelEvent ce)
                    {
                        channels.Add(ce.Channel);
                    }
                }
            }

            var totalTicks = Math.Max(maxNoteEnd, maxEventTime);
            var totalDuration = MidiIo.TicksToWholeNotes(totalTicks, ticksPerQuarter);

            return new MidiFileStatistics(
                trackCount: trackCount,
                noteCount: noteCount,
                totalTicks: totalTicks,
                totalDuration: totalDuration,
                minNoteNumber: minNoteNumber,
                maxNoteNumber: maxNoteNumber,
                channels: [.. channels.OrderBy(c => c)]);
        }
    }

    private static MidiFile MergeSources(IReadOnlyList<MidiFile> sources)
    {
        var timeDivision = sources[0].TimeDivision;
        foreach (var source in sources)
        {
            if (!Equals(source.TimeDivision, timeDivision))
                throw new ArgumentException("All MIDI files must have the same TimeDivision to merge.");
        }

        var merged = new MidiFile { TimeDivision = timeDivision };
        foreach (var source in sources)
        {
            var cloned = source.Clone();
            foreach (var chunk in cloned.Chunks)
                merged.Chunks.Add(chunk);
        }

        return merged;
    }

    private static MidiFile MergeToSingleTrackSources(IReadOnlyList<MidiFile> sources)
    {
        var timeDivision = sources[0].TimeDivision;
        foreach (var source in sources)
        {
            if (!Equals(source.TimeDivision, timeDivision))
                throw new ArgumentException("All MIDI files must have the same TimeDivision to merge.");
        }

        var collected = new List<(long Time, int Order, MidiEvent Event)>();
        var order = 0;
        MidiEvent? endOfTrackPrototype = null;

        foreach (var source in sources)
        {
            foreach (var chunk in source.Chunks)
            {
                if (chunk is not TrackChunk track)
                    continue;

                long abs = 0;
                foreach (var evt in track.Events)
                {
                    abs += evt.DeltaTime;
                    if (evt is EndOfTrackEvent)
                    {
                        endOfTrackPrototype ??= evt;
                        continue;
                    }
                    // Events are cloned individually, so no file-level clone is needed.
                    collected.Add((abs, order++, evt.Clone()));
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
        foreach (var (Time, Order, Event) in collected)
        {
            Event.DeltaTime = Math.Max(0, Time - prev);
            mergedTrack.Events.Add(Event);
            prev = Time;
        }

        if (endOfTrackPrototype is not null)
        {
            var eot = endOfTrackPrototype.Clone();
            eot.DeltaTime = 0;
            mergedTrack.Events.Add(eot);
        }

        return new MidiFile(mergedTrack) { TimeDivision = timeDivision };
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

            var clonedEvent = evt.Clone();
            clonedEvent.DeltaTime = abs - prevKept;
            prevKept = abs;

            filtered.Events.Add(clonedEvent);
        }

        return filtered;
    }
}
