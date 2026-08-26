// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Midi;
using Celeritas.Core.Notation;

namespace Celeritas.Tests;

/// <summary>
/// A rest is silence, so writing one down must not change what the music is.
/// <see cref="MusicNotation.Parse(string, bool)"/> reports a rest as a note event at
/// <see cref="MusicNotation.RestPitch"/> (-1), and every reading that folds a pitch into a pitch
/// class turned that into a B nobody played: a C major triad identified as Cmaj7, a phrase in C
/// major detected as E minor, half a bar of silence counted as an onset, and an exported file
/// gained a note at the bottom of the keyboard.
/// <para>
/// The sweep below asks every public entry point that takes notes the same question twice — once
/// with the rests, once with them removed — and requires the same answer. It is written as one
/// table rather than one test per analyzer so that an entry point added later is a line here, and
/// so that the family cannot be half-fixed again: the first pass at this left thirteen of the
/// seventeen wrong readings in place.
/// </para>
/// </summary>
public class RestsAreSilenceTests
{
    private static readonly KeySignature CMajor = new(0, true);

    /// <summary>Rests in the middle, at the end, at the start, and back to back.</summary>
    public static TheoryData<string> Passages =>
    [
        "4/4: C4/4 E4/4 R/2 G4/4 C5/4 R/2 E4/4 G4/4",
        "4/4: C4/4 R/4 D4/4 R/4 E4/4 R/4 F4/4 R/4",
        "4/4: R/4 R/4 C4/4 E4/4 G4/2 R/2",
        "3/4: G4/4 A4/4 B4/4 R/4 C5/4 D5/4",
        "4/4: C4/1 R/1",
    ];

    private static NoteBuffer BufferOf(IEnumerable<NoteEvent> notes)
    {
        var array = notes.ToArray();
        var buffer = new NoteBuffer(Math.Max(1, array.Length));
        buffer.AddRange(array);
        return buffer;
    }

    /// <summary>Every reading of a passage, named, as text that can be compared.</summary>
    private static IEnumerable<(string Name, Func<NoteEvent[], string> Read)> Readings()
    {
        yield return ("ChordAnalyzer.GetMask(buffer)",
            n => { using var b = BufferOf(n); return $"0x{ChordAnalyzer.GetMask(b):X3}"; }
        );
        yield return ("ChordAnalyzer.Identify(buffer)",
            n => { using var b = BufferOf(n); return ChordAnalyzer.Identify(b).ToString(); }
        );
        yield return ("ChordAnalyzer.Identify(notes)",
            n => ChordAnalyzer.Identify(n.AsSpan()).ToString());
        yield return ("NoteBuffer.GetChords",
            n => { using var b = BufferOf(n); return string.Join(" ", b.GetChords().Select(c => $"{c.Time}:{c.Mask:X3}")); }
        );
        yield return ("KeyAnalyzer.DetectKey(buffer)",
            n => { using var b = BufferOf(n); return KeyAnalyzer.DetectKey(b).ToString(); }
        );
        yield return ("KeyAnalyzer.DetectKey(notes)",
            n => KeyAnalyzer.DetectKey(n.AsSpan()).ToString());
        yield return ("KeyAnalyzer.Analyze(notes, key)",
            n => KeyAnalyzer.Analyze(n, CMajor).ToRomanNumeral());
        yield return ("KeyProfiler.DetectFromBuffer",
            n => { using var b = BufferOf(n); var r = KeyProfiler.DetectFromBuffer(b); return $"{r.Key} {r.DistinctPitchClasses}"; }
        );
        yield return ("KeyProfiler.DetectFromPitches",
            n => KeyProfiler.DetectFromPitches(n.AsSpan()).Key.ToString());
        yield return ("KeyProfiler.AnalyzeModulations",
            n => { using var b = BufferOf(n); return $"{KeyProfiler.AnalyzeModulations(b, Rational.Whole, Rational.Half).Points.Count}"; }
        );
        yield return ("MelodyAnalyzer.Analyze(buffer)",
            n => { using var b = BufferOf(n); var m = MelodyAnalyzer.Analyze(b); return $"{m.Contour} {m.Ambitus} {m.LowestPitch}"; }
        );
        yield return ("ModulationDetector.Analyze(buffer)",
            n => { using var b = BufferOf(n); var r = ModulationDetector.Analyze(b, CMajor); return $"{r.Modulations.Count} {r.EndKey}"; }
        );
        yield return ("ModulationDetector.Analyze(notes)",
            n => { var r = ModulationDetector.Analyze(n.AsSpan(), CMajor); return $"{r.Modulations.Count} {r.EndKey}"; }
        );
        yield return ("PitchClassSetAnalyzer.Analyze",
            n => { using var b = BufferOf(n); var p = PitchClassSetAnalyzer.Analyze(b); return $"{p.PitchClassesText} {string.Join(",", p.PrimeForm)}"; }
        );
        yield return ("PolyphonyAnalyzer.Analyze",
            n => { using var b = BufferOf(n); var p = PolyphonyAnalyzer.Analyze(b); return $"{p.Voices.Voices.Count} {p.QualityScore:F3}"; }
        );
        yield return ("PolyphonyAnalyzer.CheckCounterpointRules",
            n => { using var b = BufferOf(n); var p = PolyphonyAnalyzer.CheckCounterpointRules(b); return $"{p.Violations.Count} {p.VoiceCrossing} {p.SpacingViolations}"; }
        );
        yield return ("PolyphonyAnalyzer.DetectImitation",
            n => { using var b = BufferOf(n); return PolyphonyAnalyzer.DetectImitation(b).HasImitation.ToString(); }
        );
        yield return ("RhythmAnalyzer.Analyze",
            n => { using var b = BufferOf(n); var r = RhythmAnalyzer.Analyze(b); return $"{r.Meter.TimeSignature} {r.Events.Count} {r.Density:F3}"; }
        );
        yield return ("RhythmAnalyzer.DetectMeter",
            n => { using var b = BufferOf(n); return RhythmAnalyzer.DetectMeter(b).TimeSignature.ToString(); }
        );
        yield return ("RhythmAnalyzer.IdentifyPattern",
            n => { using var b = BufferOf(n); return RhythmAnalyzer.IdentifyPattern(b)?.Pattern.Name ?? "none"; }
        );
        yield return ("VoiceSeparator.Separate",
            n => { using var b = BufferOf(n); var v = VoiceSeparator.Separate(b); return $"{v.Voices.Count} {v.Voices.Sum(x => x.Notes.Count)}"; }
        );
        yield return ("VoiceSeparator.SeparateIntoSatb",
            n => { using var b = BufferOf(n); var v = VoiceSeparator.SeparateIntoSatb(b); return $"{v.Soprano.Notes.Count} {v.Alto.Notes.Count} {v.Tenor.Notes.Count} {v.Bass.Notes.Count}"; }
        );
        yield return ("FormAnalyzer.Analyze",
            n => { using var b = BufferOf(n); var f = FormAnalyzer.Analyze(b); return $"{f.Phrases.Count} {f.FormLabel}"; }
        );
        yield return ("MelodyHarmonizer.Harmonize(notes)",
            n => { var h = new MelodyHarmonizer().Harmonize(n, CMajor); return $"{h.Chords.Count} {h.TotalCost:F3}"; }
        );
        yield return ("MelodyHarmonizer.Harmonize(buffer)",
            n => { using var b = BufferOf(n); var h = new MelodyHarmonizer().Harmonize(b, CMajor); return $"{h.Chords.Count} {h.TotalCost:F3}"; }
        );
        yield return ("HarmonicColorAnalyzer.Analyze",
            n => { var r = HarmonicColorAnalyzer.Analyze(n, [("C", Rational.Zero)], CMajor); return $"{r.MelodicHarmony.Count} {r.ColorfulnessRating:F3}"; }
        );
        yield return ("RhythmPredictor.Train(buffer)",
            n => { using var b = BufferOf(n); var p = new RhythmPredictor(2, 1); p.Train(b); var s = p.GetStats(); return $"{s.UniqueContexts} {s.TotalTransitions}"; }
        );
    }

    [Theory]
    [MemberData(nameof(Passages))]
    public void EveryReadingOfAPassage_IgnoresItsRests(string notation)
    {
        var withRests = MusicNotation.Parse(notation);
        var sounding = withRests.Where(n => n.Pitch != MusicNotation.RestPitch).ToArray();

        Assert.NotEqual(withRests.Length, sounding.Length);      // the passage must actually hold rests

        var disagreed = new List<string>();
        foreach (var (name, read) in Readings())
        {
            var a = read(withRests);
            var b = read(sounding);
            if (a != b)
                disagreed.Add($"{name}: with rests \"{a}\", without \"{b}\"");
        }

        Assert.True(disagreed.Count == 0, string.Join(Environment.NewLine, disagreed));
    }

    // ---------- writing the music out ----------

    [Theory]
    [MemberData(nameof(Passages))]
    public void ExportingToMidi_WritesNoNoteWhereThereIsSilence(string notation)
    {
        var withRests = MusicNotation.Parse(notation);
        using var buffer = BufferOf(withRests);

        var path = Path.Combine(Path.GetTempPath(), $"celeritas-rest-{Guid.NewGuid():N}.mid");
        try
        {
            MidiIo.Export(buffer, path);
            using var reread = MidiIo.Import(path);

            var written = Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch).Order();
            var played = withRests.Where(n => n.Pitch != MusicNotation.RestPitch).Select(n => n.Pitch).Order();

            // ClampToMidiNote used to turn RestPitch (-1) into 0, so the file gained an audible C-1.
            Assert.Equal(played, written);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(Passages))]
    public void ExportingToMusicXml_WritesNoNoteWhereThereIsSilence(string notation)
    {
        var withRests = MusicNotation.Parse(notation);
        using var buffer = BufferOf(withRests);

        using var reread = MusicXmlIo.Parse(MusicXmlIo.ToXml(buffer));

        var written = Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Pitch).Order();
        var played = withRests.Where(n => n.Pitch != MusicNotation.RestPitch).Select(n => n.Pitch).Order();

        // A rest written as a note produced an octave of -2 and read back as pitch -1.
        Assert.Equal(played, written);
    }

    // ---------- transposing moves the notes and leaves the silence ----------

    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    [InlineData(12)]
    public void TransposingAPassage_MovesItsNotesAndLeavesItsRests(int semitones)
    {
        // Transpose used to add to every pitch, so a rest at -1 came out as a sounding note a
        // fourth up and the silence disappeared from the music.
        var parsed = MusicNotation.Parse("4/4: C4/4 E4/4 R/2 G4/4");
        using var buffer = BufferOf(parsed);

        MusicMath.Transpose(buffer, semitones);

        Assert.Equal(parsed.Length, buffer.Count);
        for (var i = 0; i < parsed.Length; i++)
        {
            var expected = parsed[i].Pitch == MusicNotation.RestPitch
                ? MusicNotation.RestPitch
                : parsed[i].Pitch + semitones;
            Assert.Equal(expected, buffer.Get(i).Pitch);
        }
    }

    [Fact]
    public void TransposingALongPassage_TakesTheSamePathThroughTheVectorLoopAndItsTail()
    {
        // The vector kernel handles rests with a masked add and the tail with a branch; a
        // passage longer than one vector width exercises both, whatever width the CPU has.
        var notes = new List<NoteEvent>();
        for (var i = 0; i < 37; i++)
        {
            var pitch = i % 4 == 3 ? MusicNotation.RestPitch : 60 + (i % 12);
            notes.Add(new NoteEvent(pitch, new Rational(i, 4), Rational.Quarter));
        }

        using var buffer = BufferOf(notes);
        MusicMath.Transpose(buffer, 7);

        for (var i = 0; i < notes.Count; i++)
        {
            var expected = notes[i].Pitch == MusicNotation.RestPitch
                ? MusicNotation.RestPitch
                : notes[i].Pitch + 7;
            Assert.Equal(expected, buffer.Get(i).Pitch);
        }
    }

    // ---------- a pitch below zero is not automatically a rest ----------

    [Fact]
    public void OnlyTheReservedValueReadsAsSilence()
    {
        // The filters test for RestPitch exactly rather than for "< 0", because Transpose
        // documents that it does not clamp and a pitch below zero is otherwise a note. -2 folds
        // to A# and must still be in the mask.
        NoteEvent[] belowTheKeyboard =
        [
            new(-2, Rational.Zero, Rational.Quarter),
            new(3, Rational.Zero, Rational.Quarter),
        ];
        using var buffer = BufferOf(belowTheKeyboard);

        var mask = ChordAnalyzer.GetMask(buffer);

        Assert.NotEqual(0, mask & (1 << 10));    // -2 -> A#
        Assert.NotEqual(0, mask & (1 << 3));     // 3  -> D#
    }
}
