// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Core.Midi;

/// <summary>
/// Represents a tempo change event in a MIDI file.
/// </summary>
public sealed record TempoChange(
    Rational Offset,
    int BeatsPerMinute)
{
    public override string ToString() => $"Tempo {BeatsPerMinute} BPM at {Offset}";
}

/// <summary>
/// Represents a time signature change event in a MIDI file.
/// </summary>
public sealed record TimeSignatureChange(
    Rational Offset,
    int Numerator,
    int Denominator)
{
    public override string ToString() => $"Time Signature {Numerator}/{Denominator} at {Offset}";
}

/// <summary>
/// Extensions for working with MIDI tempo and time signature events.
/// </summary>
public static class MidiEvents
{
    /// <summary>
    /// Extract all tempo changes from a MIDI file.
    /// </summary>
    public static List<TempoChange> GetTempoChanges(string path)
    {
        using var stream = File.OpenRead(path);
        return GetTempoChanges(stream);
    }

    /// <summary>
    /// Extract all tempo changes from a MIDI file stream.
    /// </summary>
    public static List<TempoChange> GetTempoChanges(Stream stream)
    {
        var midiFile = MidiFile.Read(stream);
        var ticksPerQuarter = midiFile.TimeDivision is TicksPerQuarterNoteTimeDivision tpq
            ? tpq.TicksPerQuarterNote
            : 480;

        var tempoChanges = new List<TempoChange>();

        foreach (var chunk in midiFile.Chunks)
        {
            if (chunk is not TrackChunk trackChunk)
            {
                continue;
            }

            long currentTime = 0;
            foreach (var evt in trackChunk.Events)
            {
                currentTime += evt.DeltaTime;

                if (evt is SetTempoEvent tempoEvent)
                {
                    var offset = MidiIo.TicksToBeats(currentTime, ticksPerQuarter);
                    var microsecondsPerQuarter = tempoEvent.MicrosecondsPerQuarterNote;
                    var bpm = (int)Math.Round(60_000_000.0 / microsecondsPerQuarter);

                    tempoChanges.Add(new TempoChange(offset, bpm));
                }
            }
        }

        return tempoChanges;
    }

    /// <summary>
    /// Extract all time signature changes from a MIDI file.
    /// </summary>
    public static List<TimeSignatureChange> GetTimeSignatureChanges(string path)
    {
        using var stream = File.OpenRead(path);
        return GetTimeSignatureChanges(stream);
    }

    /// <summary>
    /// Extract all time signature changes from a MIDI file stream.
    /// </summary>
    public static List<TimeSignatureChange> GetTimeSignatureChanges(Stream stream)
    {
        var midiFile = MidiFile.Read(stream);
        var ticksPerQuarter = midiFile.TimeDivision is TicksPerQuarterNoteTimeDivision tpq
            ? tpq.TicksPerQuarterNote
            : 480;

        var timeSignatureChanges = new List<TimeSignatureChange>();

        foreach (var chunk in midiFile.Chunks)
        {
            if (chunk is not TrackChunk trackChunk)
            {
                continue;
            }

            long currentTime = 0;
            foreach (var evt in trackChunk.Events)
            {
                currentTime += evt.DeltaTime;

                if (evt is TimeSignatureEvent timeSignatureEvent)
                {
                    var offset = MidiIo.TicksToBeats(currentTime, ticksPerQuarter);
                    var numerator = timeSignatureEvent.Numerator;
                    var denominator = (int)Math.Pow(2, timeSignatureEvent.Denominator);

                    timeSignatureChanges.Add(new TimeSignatureChange(offset, numerator, denominator));
                }
            }
        }

        return timeSignatureChanges;
    }

    /// <summary>
    /// Add a tempo change event to a MIDI track chunk.
    /// </summary>
    public static void AddTempoChange(TrackChunk track, Rational offset, int beatsPerMinute, int ticksPerQuarterNote)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (beatsPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "BPM must be positive.");
        }

        if (ticksPerQuarterNote <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerQuarterNote), "Ticks per quarter note must be positive.");
        }

        var microsecondsPerQuarter = (long)(60_000_000.0 / beatsPerMinute);
        var ticks = MidiIo.BeatsToTicks(offset, ticksPerQuarterNote);
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
        }

        var tempoEvent = new SetTempoEvent(microsecondsPerQuarter);
        InsertEventAtAbsoluteTicks(track, tempoEvent, ticks);
    }

    /// <summary>
    /// Add a time signature change event to a MIDI track chunk.
    /// </summary>
    public static void AddTimeSignatureChange(TrackChunk track, Rational offset, int numerator, int denominator, int ticksPerQuarterNote)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (numerator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator), "Numerator must be positive.");
        }

        if (!IsPowerOfTwo(denominator))
        {
            throw new ArgumentException("Denominator must be a power of 2.", nameof(denominator));
        }

        if (ticksPerQuarterNote <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerQuarterNote), "Ticks per quarter note must be positive.");
        }

        var ticks = MidiIo.BeatsToTicks(offset, ticksPerQuarterNote);
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
        }
        var denominatorLog2 = (byte)Math.Log2(denominator);

        var timeSignatureEvent = new TimeSignatureEvent((byte)numerator, denominatorLog2);
        InsertEventAtAbsoluteTicks(track, timeSignatureEvent, ticks);
    }

    private static void InsertEventAtAbsoluteTicks(TrackChunk track, MidiEvent midiEvent, long absoluteTicks)
    {
        if (absoluteTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteTicks), "Absolute tick position must be non-negative.");
        }

        var events = track.Events;
        if (events.Count == 0)
        {
            midiEvent.DeltaTime = absoluteTicks;
            events.Add(midiEvent);
            return;
        }

        // Compute absolute times for existing events.
        var absTimes = new long[events.Count];
        long current = 0;
        for (var i = 0; i < events.Count; i++)
        {
            current += events[i].DeltaTime;
            absTimes[i] = current;
        }

        // Find insertion index (keep existing order for same-time events).
        var insertIndex = absTimes.Length;
        for (var i = 0; i < absTimes.Length; i++)
        {
            if (absTimes[i] > absoluteTicks)
            {
                insertIndex = i;
                break;
            }
        }

        if (insertIndex == 0)
        {
            // Before the first event.
            var firstAbs = absTimes[0];
            midiEvent.DeltaTime = absoluteTicks;
            events[0].DeltaTime = firstAbs - absoluteTicks;
            events.Insert(0, midiEvent);
            return;
        }

        if (insertIndex >= events.Count)
        {
            // Append at end.
            var lastAbs = absTimes[^1];
            midiEvent.DeltaTime = absoluteTicks - lastAbs;
            events.Add(midiEvent);
            return;
        }

        // Insert between insertIndex-1 and insertIndex.
        var prevAbs = absTimes[insertIndex - 1];
        var nextAbs2 = absTimes[insertIndex];

        midiEvent.DeltaTime = absoluteTicks - prevAbs;
        events[insertIndex].DeltaTime = nextAbs2 - absoluteTicks;
        events.Insert(insertIndex, midiEvent);
    }

    private static bool IsPowerOfTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }
}
