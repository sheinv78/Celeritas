// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Midi;
using Celeritas.Core.Notation;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests for the fixes made on 2026-08-25: rests must not be heard as notes, and
/// an export that rejects its arguments must not touch the file at the path. The example tests
/// written beside those fixes assert the cases I thought of; these assert the rule itself for
/// any input CsCheck can find.
/// </summary>
public class PropertyRestAndExportTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-prop-export").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static readonly Gen<int> MidiPitch = Gen.Int[36, 96];

    /// <summary>A melody of quarter notes at consecutive beats, with rests at the given slots.</summary>
    private static NoteEvent[] WithRestsAt(int[] pitches, bool[] restFlags)
    {
        var notes = new List<NoteEvent>(pitches.Length + restFlags.Length);
        var beat = 0;

        for (var i = 0; i < pitches.Length; i++)
        {
            if (i < restFlags.Length && restFlags[i])
            {
                notes.Add(new NoteEvent(MusicNotation.RestPitch, new Rational(beat, 4), Rational.Quarter));
                beat++;
            }

            notes.Add(new NoteEvent(pitches[i], new Rational(beat, 4), Rational.Quarter));
            beat++;
        }

        return [.. notes];
    }

    private static NoteEvent[] WithoutRests(NoteEvent[] notes) =>
        [.. notes.Where(n => n.Pitch != MusicNotation.RestPitch)];

    // ---------- rests are not notes ----------

    [Fact]
    public void KeyDetection_IsUnchangedByAnyArrangementOfRests()
    {
        // A rest carries no pitch. Whatever the melody, and wherever the rests fall in it, the
        // detected key must be the key of the notes alone.
        (from pitches in MidiPitch.Array[1, 12]
         from flags in Gen.Bool.Array[0, 12]
         select (pitches, flags)).Sample(t =>
        {
            var withRests = WithRestsAt(t.pitches, t.flags);
            var sounding = WithoutRests(withRests);

            var a = KeyProfiler.DetectFromPitches(withRests.AsSpan());
            var b = KeyProfiler.DetectFromPitches(sounding.AsSpan());

            return a.Key.Root == b.Key.Root
                && a.Key.IsMajor == b.Key.IsMajor
                && a.DistinctPitchClasses == b.DistinctPitchClasses;
        }, iter: 1000);
    }

    [Fact]
    public void Harmonization_IsUnchangedByAnyArrangementOfRests()
    {
        var key = new KeySignature(0, true);

        (from pitches in MidiPitch.Array[1, 8]
         from flags in Gen.Bool.Array[0, 8]
         select (pitches, flags)).Sample(t =>
        {
            var harmonizer = new MelodyHarmonizer();
            var withRests = harmonizer.Harmonize(WithRestsAt(t.pitches, t.flags), key);
            var sounding = harmonizer.Harmonize(WithoutRests(WithRestsAt(t.pitches, t.flags)), key);

            return withRests.Chords.Count == sounding.Chords.Count
                && Math.Abs(withRests.TotalCost - sounding.TotalCost) < 1e-4f;
        }, iter: 500);
    }

    [Fact]
    public void VoiceSeparation_NeverPutsARestInAVoice()
    {
        (from pitches in MidiPitch.Array[1, 12]
         from flags in Gen.Bool.Array[0, 12]
         select (pitches, flags)).Sample(t =>
        {
            var notes = WithRestsAt(t.pitches, t.flags);
            using var buffer = new NoteBuffer(notes.Length);
            buffer.AddRange(notes);

            var result = VoiceSeparator.Separate(buffer);

            return result.Voices.All(v => v.Notes.All(n => n.Pitch >= 0));
        }, iter: 500);
    }

    [Fact]
    public void HarmonicColour_ReportsOnlySoundingNotes()
    {
        var key = new KeySignature(0, true);
        (string Chord, Rational Start)[] chords = [("C", Rational.Zero)];

        (from pitches in MidiPitch.Array[1, 10]
         from flags in Gen.Bool.Array[0, 10]
         select (pitches, flags)).Sample(t =>
        {
            var notes = WithRestsAt(t.pitches, t.flags);

            var result = HarmonicColorAnalyzer.Analyze(notes, chords, key);

            return result.MelodicHarmony.Count == WithoutRests(notes).Length
                && result.MelodicHarmony.All(e => e.Pitch >= 0);
        }, iter: 500);
    }

    // ---------- a refused export leaves the destination alone ----------

    [Fact]
    public void ARefusedMidiExport_NeverTouchesTheFileAtThePath()
    {
        (from ticks in Gen.Int[-1000, 100000]
         from channel in Gen.Int[-4, 32]
         from bpm in Gen.Int[-10, 2000]
         select (ticks, channel, bpm)).Sample(t =>
        {
            using var buffer = new NoteBuffer(2);
            buffer.AddNote(60, Rational.Zero, Rational.Quarter);
            buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

            // CsCheck runs samples in parallel, so each iteration needs a path of its own.
            var path = Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
            MidiIo.Export(buffer, path);
            var before = File.ReadAllBytes(path);

            var options = new MidiExportOptions(
                TicksPerQuarterNote: t.ticks, Bpm: t.bpm, Channel: t.channel);

            try
            {
                MidiIo.Export(buffer, path, options);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Refused: the previous export must still be there, byte for byte.
                return File.ReadAllBytes(path).SequenceEqual(before);
            }

            // Accepted: the file was rewritten, and it must still be readable.
            using var reread = MidiIo.Import(path);
            return reread.Count == 2;
        }, iter: 300);
    }

    [Fact]
    public void ARefusedMusicXmlExport_NeverTouchesTheFileAtThePath()
    {
        (from pitches in MidiPitch.Array[1, 6]
         from offset in Gen.Int[-4, 4]
         select (pitches, offset)).Sample(t =>
        {
            var path = Path.Combine(_work, $"{Guid.NewGuid():N}.musicxml");

            using var good = new NoteBuffer(2);
            good.AddNote(60, Rational.Zero, Rational.Whole);
            MusicXmlIo.Export(good, path);
            var before = File.ReadAllText(path);

            using var candidate = new NoteBuffer(t.pitches.Length);
            for (var i = 0; i < t.pitches.Length; i++)
                candidate.AddNote(t.pitches[i], new Rational(t.offset + i, 4), Rational.Quarter);

            try
            {
                MusicXmlIo.Export(candidate, path);
            }
            catch (ArgumentException)
            {
                return File.ReadAllText(path) == before;
            }

            using var reread = MusicXmlIo.Parse(File.ReadAllText(path));
            return reread.Count == t.pitches.Length;
        }, iter: 200);
    }

    // ---------- the guards themselves ----------

    [Fact]
    public void MidiExportOptions_AreAcceptedExactlyWhenTheyAreRepresentable()
    {
        (from ticks in Gen.Int[-100, 40000]
         from channel in Gen.Int[-2, 20]
         from bpm in Gen.Int[-5, 300]
         select (ticks, channel, bpm)).Sample(t =>
        {
            using var buffer = new NoteBuffer(1);
            buffer.AddNote(60, Rational.Zero, Rational.Quarter);

            // A set-tempo event stores microseconds-per-quarter in 24 bits, so the writable
            // tempo range starts at about 4 BPM — below that the value does not fit.
            var representable = t.ticks is > 0 and <= short.MaxValue
                && t.channel is >= 0 and <= 15
                && t.bpm > 0
                && 60_000_000L / t.bpm <= 0xFF_FF_FF;

            using var stream = new MemoryStream();
            try
            {
                MidiIo.Export(buffer, stream,
                    new MidiExportOptions(TicksPerQuarterNote: t.ticks, Bpm: t.bpm, Channel: t.channel));
                return representable;
            }
            catch (ArgumentOutOfRangeException)
            {
                return !representable;
            }
        }, iter: 500);
    }
}
