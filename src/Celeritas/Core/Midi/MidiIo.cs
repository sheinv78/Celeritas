// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

using MidiException = Melanchall.DryWetMidi.Common.MidiException;

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

    // Hardened reading settings: fail fast on corruption instead of best-effort reading,
    // and avoid unbounded pre-allocation from crafted chunk lengths (DoS-adjacent).
    private static readonly ReadingSettings HardenedReadingSettings = new()
    {
        // Truncated data / declared-vs-actual size mismatches abort the read
        // rather than silently absorbing bytes (or, in the worst case, over-reading).
        NotEnoughBytesPolicy = NotEnoughBytesPolicy.Abort,
        InvalidChunkSizePolicy = InvalidChunkSizePolicy.Abort,

        // A missing/invalid header chunk means this is not a MIDI file we can trust.
        NoHeaderChunkPolicy = NoHeaderChunkPolicy.Abort,
        UnknownFileFormatPolicy = UnknownFileFormatPolicy.Abort,

        // Out-of-range parameter values are clamped (bounded, safe) instead of aborting
        // the entire read — tolerant of minor spec violations without leaking bad data.
        InvalidMetaEventParameterValuePolicy = InvalidMetaEventParameterValuePolicy.SnapToLimits,
        InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.SnapToLimits,
        InvalidSystemCommonEventParameterValuePolicy = InvalidSystemCommonEventParameterValuePolicy.SnapToLimits,

        // Skip unrecognized chunks rather than reading their (attacker-declared) length
        // into a byte[] — this is the primary defense against unbounded pre-allocation.
        UnknownChunkIdPolicy = UnknownChunkIdPolicy.Skip,
    };

    public static NoteBuffer Import(Stream stream, MidiImportOptions? options = null)
    {
        options ??= new MidiImportOptions();

        MidiFile midiFile;
        try
        {
            midiFile = MidiFile.Read(stream, HardenedReadingSettings);
        }
        catch (MidiException ex)
        {
            // Surface library-internal exception types as a clean, documented contract.
            throw new InvalidDataException("The MIDI stream is malformed or corrupt.", ex);
        }

        if (midiFile.TimeDivision is not TicksPerQuarterNoteTimeDivision tpq)
        {
            throw new NotSupportedException("Only ticks-per-quarter-note MIDI files are supported.");
        }

        var ticksPerQuarter = tpq.TicksPerQuarterNote;
        if (ticksPerQuarter <= 0)
        {
            throw new InvalidOperationException("Invalid ticks-per-quarter-note value.");
        }

        var notes = midiFile.GetNotes();

        // Pre-size where possible.
        var capacity = options.MaxNotes is { } maxNotes
            ? Math.Min(notes.Count, Math.Max(maxNotes, 0))
            : notes.Count;

        var buffer = new NoteBuffer(Math.Max(capacity, 1));
        try
        {
            var taken = 0;
            foreach (var note in notes)
            {
                if (options.MaxNotes is { } limit && taken >= limit)
                {
                    break;
                }

                if (options.Channel is { } ch && note.Channel != ch)
                {
                    continue;
                }

                var pitch = (int)note.NoteNumber;
                var offset = TicksToWholeNotes(note.Time, ticksPerQuarter);
                var duration = TicksToWholeNotes(note.Length, ticksPerQuarter);
                var velocity = note.Velocity / 127f;

                buffer.AddNote(pitch, offset, duration, velocity);
                taken++;
            }

            if (options.SortByOffset)
            {
                buffer.Sort();
            }

            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
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
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TicksPerQuarterNote, "TicksPerQuarterNote must be positive.");
        }

        if (options.Channel is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Channel, "Channel must be in [0..15].");
        }

        if (options.TicksPerQuarterNote > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TicksPerQuarterNote, "TicksPerQuarterNote must be <= 32767.");
        }

        if (options.Bpm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Bpm, "Bpm must be positive.");
        }

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

            if (e.Offset < Rational.Zero)
            {
                throw new ArgumentException($"Note {i} has a negative offset ({e.Offset}); MIDI cannot represent events before time zero.", nameof(buffer));
            }

            var timeTicks = WholeNotesToTicks(e.Offset, options.TicksPerQuarterNote);
            var lengthTicks = Math.Max(1, WholeNotesToTicks(e.Duration, options.TicksPerQuarterNote));
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

        midiFile.Chunks.Add(track);
        midiFile.Write(stream);
    }

    private static int ClampToMidiNote(int pitch) => Math.Clamp(pitch, 0, 127);

    // Celeritas time convention: Rational offsets/durations are fractions of a WHOLE note
    // (quarter note = 1/4), matching MusicNotation.Parse and Rational.Quarter.
    // One whole note = 4 quarter notes = 4 * ticksPerQuarter ticks.

    internal static Rational TicksToWholeNotes(long ticks, int ticksPerQuarter)
    {
        return new Rational(ticks, 4L * ticksPerQuarter);
    }

    internal static long WholeNotesToTicks(Rational wholeNotes, int ticksPerQuarter)
    {
        // ticks = round(wholeNotes * 4 * ticksPerQuarter), computed exactly in 128-bit
        var num = (Int128)wholeNotes.Numerator * 4 * ticksPerQuarter;
        var den = (Int128)wholeNotes.Denominator;
        var shifted = num + (den >> 1);
        var rounded = shifted >= 0
            ? shifted / den
            : -((-shifted + den - 1) / den);
        return checked((long)rounded);
    }
}
