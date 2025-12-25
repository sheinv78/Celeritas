// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Celeritas.Core.Midi;

public sealed record MidiImportOptions(
    int? Channel = null,
    int? MaxNotes = null,
    bool SortByOffset = true);

public sealed record MidiExportOptions(
    int TicksPerQuarterNote = 480,
    int Bpm = 120,
    int Channel = 0,
    byte DefaultVelocity = 100);

public static class MidiIo
{
    public static NoteBuffer Import(string path, MidiImportOptions? options = null)
    {
        using var stream = File.OpenRead(path);
        return Import(stream, options);
    }

    public static NoteBuffer Import(Stream stream, MidiImportOptions? options = null)
    {
        options ??= new MidiImportOptions();

        var midiFile = MidiFile.Read(stream);

        if (midiFile.TimeDivision is not TicksPerQuarterNoteTimeDivision tpq)
            throw new NotSupportedException("Only ticks-per-quarter-note MIDI files are supported.");

        var ticksPerQuarter = tpq.TicksPerQuarterNote;
        if (ticksPerQuarter <= 0)
            throw new InvalidOperationException("Invalid ticks-per-quarter-note value.");

        var notes = midiFile.GetNotes();

        // Pre-size where possible.
        var capacity = options.MaxNotes is { } maxNotes
            ? Math.Min(notes.Count, maxNotes)
            : notes.Count;

        var buffer = new NoteBuffer(Math.Max(capacity, 1));

        var taken = 0;
        foreach (var note in notes)
        {
            if (options.Channel is { } ch && note.Channel != ch)
                continue;

            var pitch = (int)note.NoteNumber;
            var offset = TicksToBeats(note.Time, ticksPerQuarter);
            var duration = TicksToBeats(note.Length, ticksPerQuarter);
            var velocity = note.Velocity / 127f;

            buffer.AddNote(pitch, offset, duration, velocity);

            taken++;
            if (options.MaxNotes is { } limit && taken >= limit)
                break;
        }

        if (options.SortByOffset)
            buffer.Sort();

        return buffer;
    }

    public static void Export(NoteBuffer buffer, string path, MidiExportOptions? options = null)
    {
        using var stream = File.Create(path);
        Export(buffer, stream, options);
    }

    public static void Export(NoteBuffer buffer, Stream stream, MidiExportOptions? options = null)
    {
        options ??= new MidiExportOptions();

        if (options.TicksPerQuarterNote <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.TicksPerQuarterNote, "TicksPerQuarterNote must be positive.");

        if (options.Channel < 0 || options.Channel > 15)
            throw new ArgumentOutOfRangeException(nameof(options), options.Channel, "Channel must be in [0..15].");

        if (options.TicksPerQuarterNote > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), options.TicksPerQuarterNote, "TicksPerQuarterNote must be <= 32767.");

        var midiFile = new MidiFile
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision((short)options.TicksPerQuarterNote)
        };

        var track = new TrackChunk();

        // Set tempo (optional but useful for DAWs).
        var tempo = Tempo.FromBeatsPerMinute(options.Bpm);
        track.Events.Add(new SetTempoEvent(tempo.MicrosecondsPerQuarterNote));

        using var notesManager = track.ManageNotes();
        var channel = (FourBitNumber)options.Channel;

        for (var i = 0; i < buffer.Count; i++)
        {
            var e = buffer.Get(i);
            var noteNumber = ClampToMidiNote(e.Pitch);

            var timeTicks = BeatsToTicks(e.Offset, options.TicksPerQuarterNote);
            var lengthTicks = Math.Max(1, BeatsToTicks(e.Duration, options.TicksPerQuarterNote));
            var lengthTicksInt = lengthTicks > int.MaxValue ? int.MaxValue : (int)lengthTicks;

            var velocity = (byte)Math.Clamp((int)Math.Round(e.Velocity * 127.0), 1, 127);
            if (velocity == 0)
                velocity = options.DefaultVelocity;

            var note = new Note((SevenBitNumber)noteNumber, lengthTicksInt, timeTicks)
            {
                Channel = channel,
                Velocity = (SevenBitNumber)velocity
            };

            notesManager.Objects.Add(note);
        }

        notesManager.SaveChanges();

        midiFile.Chunks.Add(track);
        midiFile.Write(stream);
    }

    private static int ClampToMidiNote(int pitch) => Math.Clamp(pitch, 0, 127);

    private static Rational TicksToBeats(long ticks, int ticksPerQuarter)
    {
        // beats = ticks / ticksPerQuarter
        return new Rational(ticks, ticksPerQuarter);
    }

    private static long BeatsToTicks(Rational beats, int ticksPerQuarter)
    {
        // ticks = round(beats * ticksPerQuarter)
        try
        {
            checked
            {
                var scaled = beats.Numerator * (long)ticksPerQuarter;
                var den = beats.Denominator;
                if (den == 0)
                    return 0;

                // Round half up.
                return (scaled + (den / 2)) / den;
            }
        }
        catch (OverflowException)
        {
            return (long)Math.Round(beats.ToDouble() * ticksPerQuarter);
        }
    }

}
