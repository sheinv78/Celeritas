// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Notation;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Properties the generators and the notation writer must hold for any input, not just the
/// examples someone thought to write down. A generator that quietly places a note outside its
/// chord, or a writer that loses a duration, still produces music — these are the assertions
/// that notice.
/// </summary>
public class PropertyGenerationInvariantTests
{
    private static readonly KeySignature CMajor = new(0, true);
    private static readonly Gen<int> MidiPitch = Gen.Int[36, 84];
    private static readonly Gen<int> Figure = Gen.Int[2, 9];

    // ---------- MusicXML round-trips what it was given ----------

    [Fact]
    public void MusicXml_ExportImport_PreservesEveryNote()
    {
        (from pitches in MidiPitch.Array[1, 8]
         from denominator in Gen.Const(4)
         select pitches).Sample(pitches =>
        {
            using var original = new NoteBuffer(pitches.Length);
            for (var i = 0; i < pitches.Length; i++)
                original.AddNote(pitches[i], new Rational(i, 4), Rational.Quarter);

            using var reread = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));

            if (reread.Count != original.Count)
                return false;

            var wanted = Enumerable.Range(0, original.Count)
                .Select(i => (original.Get(i).Pitch, original.Get(i).Offset, original.Get(i).Duration))
                .OrderBy(n => n.Item2.ToDouble()).ThenBy(n => n.Item1)
                .ToArray();
            var got = Enumerable.Range(0, reread.Count)
                .Select(i => (reread.Get(i).Pitch, reread.Get(i).Offset, reread.Get(i).Duration))
                .OrderBy(n => n.Item2.ToDouble()).ThenBy(n => n.Item1)
                .ToArray();

            return wanted.SequenceEqual(got);
        }, iter: 300);
    }

    [Fact]
    public void MusicXml_ExportImport_PreservesChords()
    {
        // Simultaneous notes are written as a chord; they must come back as simultaneous notes.
        (from pitches in MidiPitch.Array[2, 5]
         select pitches.Distinct().Order().ToArray()).Sample(pitches =>
        {
            using var original = new NoteBuffer(pitches.Length);
            foreach (var pitch in pitches)
                original.AddNote(pitch, Rational.Zero, Rational.Half);

            using var reread = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));

            return reread.Count == pitches.Length
                && Enumerable.Range(0, reread.Count).All(i => reread.Get(i).Offset == Rational.Zero);
        }, iter: 200);
    }

    // ---------- figured bass always sounds above its bass ----------

    [Fact]
    public void FiguredBass_UpperVoicesAlwaysSoundAboveTheBass()
    {
        (from bass in Gen.Int[36, 72]
         from figures in Figure.Array[0, 3]
         select (bass, figures)).Sample(t =>
        {
            var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor });

            var notes = realizer.Realize(
            [
                new FiguredBassSymbol
                {
                    BassPitch = t.bass,
                    Figures = t.figures.Distinct().Order().ToArray(),
                    Duration = Rational.Quarter,
                    Time = Rational.Zero,
                },
            ]);

            return notes.Length >= 1
                && notes[0].Pitch == t.bass
                && notes.Skip(1).All(n => n.Pitch > t.bass);
        }, iter: 500);
    }

    [Fact]
    public void FiguredBass_EveryPitchIsPlayable()
    {
        (from bass in Gen.Int[24, 84]
         from figures in Figure.Array[0, 4]
         select (bass, figures)).Sample(t =>
        {
            var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor });

            var notes = realizer.Realize(
            [
                new FiguredBassSymbol
                {
                    BassPitch = t.bass,
                    Figures = t.figures.Distinct().Order().ToArray(),
                    Duration = Rational.Quarter,
                    Time = Rational.Zero,
                },
            ]);

            return notes.All(n => n.Pitch is >= 0 and <= 127 && n.Duration > Rational.Zero);
        }, iter: 500);
    }

    // ---------- accompaniment stays inside its chord ----------

    [Fact]
    public void Accompaniment_NeverSoundsOutsideTheChordItBelongsTo()
    {
        (from roots in Gen.Int[0, 11].Array[1, 4]
         from arpeggio in Gen.Bool
         select (roots, arpeggio)).Sample(t =>
        {
            var chords = new List<ChordAssignment>();
            for (var i = 0; i < t.roots.Length; i++)
            {
                int[] pitches = [60 + t.roots[i], 64 + t.roots[i], 67 + t.roots[i]];
                chords.Add(new ChordAssignment(
                    new Rational(i, 2), new Rational(i + 1, 2), ChordAnalyzer.Identify(pitches), pitches));
            }

            var options = AccompanimentOptions.Default with
            {
                Pattern = t.arpeggio ? AccompanimentPattern.Arpeggio : AccompanimentPattern.Block,
            };

            var notes = AccompanimentGenerator.Generate(chords, options);

            return notes.All(n =>
            {
                var chord = chords.FirstOrDefault(c => n.Offset >= c.Start && n.Offset < c.End);
                return chord.End > chord.Start
                    && n.Offset + n.Duration <= chord.End
                    && n.Pitch is >= 0 and <= 127;
            });
        }, iter: 300);
    }

    [Fact]
    public void Accompaniment_OnlyEverSoundsChordTones()
    {
        Gen.Int[0, 11].Array[1, 4].Sample(roots =>
        {
            var chords = new List<ChordAssignment>();
            for (var i = 0; i < roots.Length; i++)
            {
                int[] pitches = [60 + roots[i], 64 + roots[i], 67 + roots[i]];
                chords.Add(new ChordAssignment(
                    new Rational(i, 2), new Rational(i + 1, 2), ChordAnalyzer.Identify(pitches), pitches));
            }

            var notes = AccompanimentGenerator.Generate(chords, AccompanimentOptions.Default);

            return notes.All(n =>
            {
                var index = Math.Clamp((int)(n.Offset.ToDouble() * 2), 0, roots.Length - 1);
                var wanted = new[] { roots[index], (roots[index] + 4) % 12, (roots[index] + 7) % 12 };
                return wanted.Contains(PitchMath.Fold(n.Pitch));
            });
        }, iter: 300);
    }

    // ---------- harmonizing transposed music transposes the harmony ----------

    [Fact]
    public void Harmonization_TransposingTheMelodyAndTheKey_TransposesTheChords()
    {
        (from pitches in Gen.Int[48, 72].Array[2, 6]
         from n in Gen.Int[1, 11]
         select (pitches, n)).Sample(t =>
        {
            var harmonizer = new MelodyHarmonizer();

            NoteEvent[] original = [.. t.pitches.Select((p, i) => new NoteEvent(p, new Rational(i, 4), Rational.Quarter))];
            NoteEvent[] shifted = [.. original.Select(e => new NoteEvent(e.Pitch + t.n, e.Offset, e.Duration))];

            var a = harmonizer.Harmonize(original, new KeySignature(0, true));
            var b = harmonizer.Harmonize(shifted, new KeySignature((byte)t.n, true));

            if (a.Chords.Count != b.Chords.Count)
                return false;

            return a.Chords
                .Zip(b.Chords, (x, y) => (PitchMath.Fold(x.Chord.RootPitchClass + t.n) == y.Chord.RootPitchClass)
                                          && x.Chord.Quality == y.Chord.Quality)
                .All(equal => equal);
        }, iter: 300);
    }

    // ---------- form analysis partitions its phrases ----------

    [Fact]
    public void FormAnalysis_SectionsCoverThePhrasesWithoutOverlapping()
    {
        (from lengths in Gen.Int[2, 5].Array[1, 5]
         select lengths).Sample(lengths =>
        {
            using var buffer = new NoteBuffer(lengths.Sum() + 4);
            var beat = 0;
            foreach (var length in lengths)
            {
                for (var i = 0; i < length; i++)
                {
                    buffer.AddNote(60 + (i % 5), new Rational(beat, 4), Rational.Quarter);
                    beat++;
                }

                beat += 4;      // a rest long enough to end the phrase
            }

            var result = FormAnalyzer.Analyze(buffer);

            if (result.Sections.Count == 0)
                return result.Phrases.Count <= 1 || result.FormLabel.Length == 0;

            var ordered = result.Sections.OrderBy(s => s.StartPhraseIndex).ToArray();

            for (var i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].EndPhraseIndex < ordered[i].StartPhraseIndex)
                    return false;
                if (i > 0 && ordered[i].StartPhraseIndex <= ordered[i - 1].EndPhraseIndex)
                    return false;
            }

            return ordered[0].StartPhraseIndex == 0
                && ordered[^1].EndPhraseIndex == result.Phrases.Count - 1;
        }, iter: 300);
    }
    // ---------- voice separation partitions the notes it was given ----------

    [Fact]
    public void VoiceSeparation_PlacesEveryNoteInExactlyOneVoice()
    {
        // Dropping a note or voicing it twice would change every downstream count — the
        // texture, the crossings, the counterpoint verdict — while still looking like music.
        (from pitches in MidiPitch.Array[1, 16]
         from spread in Gen.Int[1, 4]
         select (pitches, spread)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.pitches.Length);
            for (var i = 0; i < t.pitches.Length; i++)
                buffer.AddNote(t.pitches[i], new Rational(i / t.spread, 4), Rational.Quarter);

            var result = VoiceSeparator.Separate(buffer);

            var assigned = result.Voices.SelectMany(v => v.Notes.Select(n => n.OriginalIndex)).ToArray();

            return assigned.Length == buffer.Count
                && assigned.Distinct().Count() == buffer.Count
                && assigned.All(i => i >= 0 && i < buffer.Count)
                && result.TotalNotes == buffer.Count;
        }, iter: 500);
    }

    [Fact]
    public void VoiceSeparation_KeepsEachVoiceInTimeOrder()
    {
        (from pitches in MidiPitch.Array[2, 16]
         from spread in Gen.Int[1, 4]
         select (pitches, spread)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.pitches.Length);
            for (var i = 0; i < t.pitches.Length; i++)
                buffer.AddNote(t.pitches[i], new Rational(i / t.spread, 4), Rational.Quarter);

            var result = VoiceSeparator.Separate(buffer);

            return result.Voices.All(v =>
                v.Notes.Zip(v.Notes.Skip(1), (a, b) => a.Offset <= b.Offset).All(ordered => ordered));
        }, iter: 500);
    }

    // ---------- the progression report moves with the music ----------

    [Fact]
    public void ProgressionAnalysis_TransposingTheChords_TransposesTheKey()
    {
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        (from degrees in Gen.Int[0, 11].Array[3, 6]
         from n in Gen.Int[1, 11]
         select (degrees, n)).Sample(t =>
        {
            var original = t.degrees.Select(d => names[d]).ToArray();
            var shifted = t.degrees.Select(d => names[(d + t.n) % 12]).ToArray();

            var a = ProgressionAdvisor.Analyze(original);
            var b = ProgressionAdvisor.Analyze(shifted);

            return PitchMath.Fold(a.Key.Root + t.n) == b.Key.Root
                && a.Key.IsMajor == b.Key.IsMajor;
        }, iter: 500);
    }

    [Fact]
    public void CadenceDetection_IsTheSameInEveryKey()
    {
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        (from from_ in Gen.Int[0, 11]
         from to in Gen.Int[0, 11]
         from n in Gen.Int[1, 11]
         select (from_, to, n)).Sample(t =>
        {
            var inC = ProgressionAdvisor.DetectCadence(
                [names[t.from_], names[t.to]], new KeySignature(0, true));
            var shifted = ProgressionAdvisor.DetectCadence(
                [names[(t.from_ + t.n) % 12], names[(t.to + t.n) % 12]], new KeySignature((byte)t.n, true));

            return inC == shifted;
        }, iter: 500);
    }

    // ---------- rhythm analysis keeps every onset ----------

    [Fact]
    public void RhythmAnalysis_ReportsAnEventForEveryOnset()
    {
        (from count in Gen.Int[1, 16]
         from unit in Gen.Int[1, 8]
         select (count, unit)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.count);
            for (var i = 0; i < t.count; i++)
                buffer.AddNote(60, new Rational(i * t.unit, 8), new Rational(t.unit, 8));

            var result = RhythmAnalyzer.Analyze(buffer);

            return result.Events.Count == t.count
                && result.Events.All(e => e.Offset >= Rational.Zero)
                && result.Syncopation is >= 0f and <= 1f
                && result.Density >= 0f;
        }, iter: 500);
    }
}
