// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

using MidiException = Melanchall.DryWetMidi.Common.MidiException;

namespace Celeritas.Core.Midi;

/// <summary>
/// Options controlling MIDI import into a <see cref="NoteBuffer"/>.
/// </summary>
/// <param name="Channel">If set, keep only notes on this MIDI channel.</param>
/// <param name="MaxNotes">If set, stop after importing this many notes.</param>
/// <param name="SortByOffset">Whether to sort the imported notes by offset.</param>
public sealed record MidiImportOptions(
    int? Channel = null,
    int? MaxNotes = null,
    bool SortByOffset = true);

/// <summary>
/// Options controlling MIDI export from a <see cref="NoteBuffer"/>.
/// </summary>
/// <param name="TicksPerQuarterNote">Ticks-per-quarter-note time division, in [1, 32767].</param>
/// <param name="Bpm">Tempo in beats per minute written as a set-tempo event.</param>
/// <param name="Channel">MIDI channel for the exported notes, in [0, 15].</param>
/// <param name="DefaultVelocity">Velocity substituted when a note's velocity rounds to zero.</param>
public sealed record MidiExportOptions(
    int TicksPerQuarterNote = 480,
    int Bpm = 120,
    int Channel = 0,
    byte DefaultVelocity = 100);

/// <summary>
/// Reads and writes MIDI files, converting between MIDI ticks and whole-note time units.
/// </summary>
public static class MidiIo
{
    /// <summary>Imports notes from the MIDI file at <paramref name="path"/>.</summary>
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

    /// <summary>
    /// Read a MIDI stream with the hardened settings above, surfacing a malformed file as
    /// <see cref="InvalidDataException"/> rather than a library-internal <see cref="MidiException"/>.
    /// </summary>
    /// <remarks>
    /// The single door every reader in this assembly goes through, so the DoS defenses and the
    /// exception contract cannot drift apart between entry points — which is exactly how
    /// <see cref="MidiEvents"/> ended up reading untrusted files on default settings.
    /// </remarks>
    internal static MidiFile ReadHardened(Stream stream)
    {
        try
        {
            return MidiFile.Read(stream, HardenedReadingSettings);
        }
        catch (MidiException ex)
        {
            // Surface library-internal exception types as a clean, documented contract.
            throw new InvalidDataException("The MIDI stream is malformed or corrupt.", ex);
        }
    }

    /// <summary>Imports notes from a MIDI <paramref name="stream"/> using hardened reading settings.</summary>
    /// <exception cref="InvalidDataException">The stream is malformed or corrupt.</exception>
    /// <exception cref="NotSupportedException">The file does not use ticks-per-quarter-note time division.</exception>
    public static NoteBuffer Import(Stream stream, MidiImportOptions? options = null)
    {
        options ??= new MidiImportOptions();

        var midiFile = ReadHardened(stream);

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

    /// <summary>Exports <paramref name="buffer"/> to a MIDI file at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An option (ticks-per-quarter-note, channel, or BPM) is out of range.</exception>
    public static void Export(NoteBuffer buffer, string path, MidiExportOptions? options = null)
    {
        // Build BEFORE opening the file. File.Create truncates, so a bad channel — or a note
        // MIDI cannot represent — used to destroy whatever was already at `path` and only
        // then throw: an argument mistake that cost the caller their previous export.
        var opts = options ?? new MidiExportOptions();
        ValidateExportOptions(opts);
        var midiFile = BuildMidiFile(buffer, opts);

        using var stream = File.Create(path);
        midiFile.Write(stream);
    }

    /// <summary>
    /// Checks the export options that must hold before anything is written. Shared so the
    /// path overload can reject bad arguments before it opens (and truncates) the file.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
    private static void ValidateExportOptions(MidiExportOptions options)
    {
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
    }

    /// <summary>Writes <paramref name="buffer"/> as a single-track MIDI file to <paramref name="stream"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An option (ticks-per-quarter-note, channel, or BPM) is out of range.</exception>
    /// <exception cref="ArgumentException">A note has a negative offset, which MIDI cannot represent.</exception>
    public static void Export(NoteBuffer buffer, Stream stream, MidiExportOptions? options = null)
    {
        options ??= new MidiExportOptions();
        ValidateExportOptions(options);

        BuildMidiFile(buffer, options).Write(stream);
    }

    /// <summary>
    /// Builds the whole file in memory. Separated from writing so a caller error cannot reach
    /// the destination file: nothing is opened until the file is known to be buildable.
    /// </summary>
    private static MidiFile BuildMidiFile(NoteBuffer buffer, MidiExportOptions options)
    {
        var midiFile = new MidiFile
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision((short)options.TicksPerQuarterNote)
        };

        var track = new TrackChunk();

        // Set tempo (optional but useful for DAWs).
        var tempo = Tempo.FromBeatsPerMinute(options.Bpm);

        // A MIDI Set Tempo meta event stores microseconds-per-quarter in 24 bits, so only
        // [1, 16_777_215] is representable. Guard the whole positive range (not just <= 0):
        // a very small Bpm (~1-3) overflows that limit and a huge Bpm rounds it to 0, either
        // of which would surface as a raw DryWetMidi failure or a degenerate tempo.
        const long maxMicrosecondsPerQuarter = 0xFF_FF_FF; // 16_777_215
        if (tempo.MicrosecondsPerQuarterNote is < 1 or > maxMicrosecondsPerQuarter)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Bpm,
                $"Bpm {options.Bpm} maps to {tempo.MicrosecondsPerQuarterNote} µs/quarter, outside the MIDI-encodable " +
                $"range [1, {maxMicrosecondsPerQuarter}] (roughly 4-60000000 BPM).");
        }

        track.Events.Add(new SetTempoEvent(tempo.MicrosecondsPerQuarterNote));

        using var notesManager = track.ManageNotes();
        var channel = (FourBitNumber)options.Channel;

        for (var i = 0; i < buffer.Count; i++)
        {
            var e = buffer.Get(i);

            // A rest is silence, and MIDI writes silence as the absence of a note. Without this
            // ClampToMidiNote turned RestPitch (-1) into 0 and the file gained an audible C-1
            // wherever the music was quiet.
            if (Rests.IsRest(e.Pitch)) continue;

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
        return midiFile;
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
