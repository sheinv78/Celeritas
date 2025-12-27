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
                continue;

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
                continue;

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
        if (beatsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "BPM must be positive.");

        var microsecondsPerQuarter = (long)(60_000_000.0 / beatsPerMinute);
        var ticks = MidiIo.BeatsToTicks(offset, ticksPerQuarterNote);

        var tempoEvent = new SetTempoEvent(microsecondsPerQuarter)
        {
            DeltaTime = ticks
        };

        track.Events.Add(tempoEvent);
    }

    /// <summary>
    /// Add a time signature change event to a MIDI track chunk.
    /// </summary>
    public static void AddTimeSignatureChange(TrackChunk track, Rational offset, int numerator, int denominator, int ticksPerQuarterNote)
    {
        if (numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator), "Numerator must be positive.");

        if (!IsPowerOfTwo(denominator))
            throw new ArgumentException("Denominator must be a power of 2.", nameof(denominator));

        var ticks = MidiIo.BeatsToTicks(offset, ticksPerQuarterNote);
        var denominatorLog2 = (byte)Math.Log2(denominator);

        var timeSignatureEvent = new TimeSignatureEvent((byte)numerator, denominatorLog2)
        {
            DeltaTime = ticks
        };

        track.Events.Add(timeSignatureEvent);
    }

    private static bool IsPowerOfTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }
}
