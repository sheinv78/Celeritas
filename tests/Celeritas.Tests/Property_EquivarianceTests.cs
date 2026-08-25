// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Music does not change when it is transposed, so neither should an analysis of it. Every
/// property here moves the whole passage (and the key it is read in) by the same interval and
/// requires the answer to move with it — nothing else. This is the family that found the
/// tie-break defects in <see cref="KeyAnalyzer"/> and <see cref="ProgressionAdvisor"/>: an
/// answer that depends on absolute pitch class looks entirely reasonable until it is asked the
/// same question in a different key.
/// </summary>
public class PropertyEquivarianceTests
{
    /// <summary>A middle register, so transposing by up to an octave cannot leave MIDI range.</summary>
    private static readonly Gen<int> MiddlePitch = Gen.Int[48, 72];
    private static readonly Gen<int> Shift = Gen.Int[1, 11];

    private static NoteEvent[] Melody(int[] pitches, int shift = 0) =>
        [.. pitches.Select((p, i) => new NoteEvent(p + shift, new Rational(i, 4), Rational.Quarter))];

    // ---------- chords ----------

    [Fact]
    public void ChordQuality_IsTheSameInEveryKey_AndAKnownRootMovesWithIt()
    {
        (from pitches in MiddlePitch.Array[2, 5]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            var original = ChordAnalyzer.Identify(t.pitches);
            var shifted = ChordAnalyzer.Identify([.. t.pitches.Select(p => p + t.n)]);

            if (original.Quality != shifted.Quality)
                return false;

            // An unrecognized set has no root to move: Unknown reports 0 as a placeholder,
            // and reading that as a pitch class would be reading a value that is not there.
            return original.Quality == ChordQuality.Unknown
                || PitchMath.Fold(original.RootPitchClass + t.n) == shifted.RootPitchClass;
        }, iter: 1000);
    }

    [Fact]
    public void AnUnrecognizedSet_ReportsNoRootInAnyKey()
    {
        (from pitches in MiddlePitch.Array[2, 5]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            var shifted = ChordAnalyzer.Identify([.. t.pitches.Select(p => p + t.n)]);

            return shifted.Quality != ChordQuality.Unknown || shifted.RootPitchClass == 0;
        }, iter: 500);
    }

    // ---------- melody ----------

    [Fact]
    public void MelodyShape_IsTheSameInEveryKey()
    {
        (from pitches in MiddlePitch.Array[2, 12]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            var original = MelodyAnalyzer.Analyze(t.pitches);
            var shifted = MelodyAnalyzer.Analyze([.. t.pitches.Select(p => p + t.n)]);

            return original.Contour == shifted.Contour
                && original.Ambitus == shifted.Ambitus
                && original.AmbitusDescription.Split(':')[0] == shifted.AmbitusDescription.Split(':')[0]
                && original.Intervals.SequenceEqual(shifted.Intervals)
                && Math.Abs(original.Complexity - shifted.Complexity) < 1e-9
                && original.CharacterDescription == shifted.CharacterDescription;
        }, iter: 500);
    }

    // ---------- counterpoint ----------

    [Fact]
    public void CounterpointVerdicts_AreTheSameInEveryKey()
    {
        (from pitches in Gen.Int[48, 66].Array[2, 10]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            var original = PolyphonyAnalyzer.CheckCounterpointRules(Melody(t.pitches));
            var shifted = PolyphonyAnalyzer.CheckCounterpointRules(Melody(t.pitches, t.n));

            return original.ParallelFifths == shifted.ParallelFifths
                && original.ParallelOctaves == shifted.ParallelOctaves
                && original.HiddenParallels == shifted.HiddenParallels
                && original.VoiceCrossing == shifted.VoiceCrossing
                && original.SpacingViolations == shifted.SpacingViolations
                && original.Violations.Count == shifted.Violations.Count;
        }, iter: 500);
    }

    [Fact]
    public void VoiceSeparation_KeepsEveryNoteInEveryKey()
    {
        // Voice IDENTITY is deliberately register-based — the separator seeds its voices at
        // SATB centres, so the same line an octave up may be called Alto rather than Tenor.
        // What cannot change with the key is that every note is placed, exactly once.
        (from pitches in Gen.Int[48, 66].Array[2, 12]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.pitches.Length);
            foreach (var note in Melody(t.pitches, t.n)) buffer.Add(note);

            var result = VoiceSeparator.Separate(buffer);
            var assigned = result.Voices.SelectMany(v => v.Notes.Select(x => x.OriginalIndex)).ToArray();

            return assigned.Length == buffer.Count && assigned.Distinct().Count() == buffer.Count;
        }, iter: 500);
    }

    // ---------- harmonic colour ----------

    [Fact]
    public void HarmonicColour_ReadsTheSameInEveryKey()
    {
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        (from pitches in MiddlePitch.Array[1, 8]
         from chordRoot in Gen.Int[0, 11]
         from n in Shift
         select (pitches, chordRoot, n)).Sample(t =>
        {
            (string Chord, Rational Start)[] chordsA = [(names[t.chordRoot], Rational.Zero)];
            (string Chord, Rational Start)[] chordsB = [(names[(t.chordRoot + t.n) % 12], Rational.Zero)];

            var a = HarmonicColorAnalyzer.Analyze(
                Melody(t.pitches), chordsA, new KeySignature((byte)t.chordRoot, true));
            var b = HarmonicColorAnalyzer.Analyze(
                Melody(t.pitches, t.n), chordsB, new KeySignature((byte)((t.chordRoot + t.n) % 12), true));

            return a.MelodicHarmony.Count == b.MelodicHarmony.Count
                && a.MelodicHarmony.Zip(b.MelodicHarmony, (x, y) => x.Type == y.Type && x.IsChordTone == y.IsChordTone).All(same => same)
                && a.ChromaticNotes.Count == b.ChromaticNotes.Count
                && Math.Abs(a.ColorfulnessRating - b.ColorfulnessRating) < 1e-9;
        }, iter: 500);
    }

    // ---------- voice leading ----------

    [Fact]
    public void AThreeNoteChordSolvesInEveryKey()
    {
        // The solver stacks four voices inside fixed SATB compasses, so solvability is not
        // transposition-invariant in general: four voices on ONE pitch class need C3-C6-and-a-
        // bit, which the soprano ceiling refuses for some pitch classes and allows for others.
        // A chord with three distinct pitch classes always has room, in every key.
        (from chord in Gen.Int[0, 11].Array[3, 3]
         from n in Shift
         select (chord, n)).Sample(t =>
        {
            if (t.chord.Distinct().Count() != 3)
                return true;

            var solver = new VoiceLeadingSolver();

            var original = solver.Solve([t.chord]);
            var shifted = solver.Solve([[.. t.chord.Select(pc => PitchMath.Fold(pc + t.n))]]);

            return original.IsValid && shifted.IsValid
                && original.Voicings.Count == shifted.Voicings.Count;
        }, iter: 300);
    }

    // ---------- modulation ----------

    [Fact]
    public void ModulationDetection_MovesWithTheMusic()
    {
        (from roots in Gen.Int[0, 11].Array[4, 10]
         from n in Shift
         select (roots, n)).Sample(t =>
        {
            NoteEvent[] Build(int shift) =>
            [
                .. t.roots.SelectMany((root, i) => new[]
                {
                    new NoteEvent(60 + PitchMath.Fold(root + shift), new Rational(i, 4), Rational.Quarter),
                    new NoteEvent(64 + PitchMath.Fold(root + shift), new Rational(i, 4), Rational.Quarter),
                    new NoteEvent(67 + PitchMath.Fold(root + shift), new Rational(i, 4), Rational.Quarter),
                }),
            ];

            var a = ModulationDetector.Analyze(Build(0), new KeySignature(0, true));
            var b = ModulationDetector.Analyze(Build(t.n), new KeySignature((byte)t.n, true));

            if (a.Modulations.Count != b.Modulations.Count)
                return false;

            return a.Modulations
                .Zip(b.Modulations, (x, y) =>
                    x.Type == y.Type
                    && x.Offset == y.Offset
                    && PitchMath.Fold(x.ToKey.Root + t.n) == y.ToKey.Root
                    && x.ToKey.IsMajor == y.ToKey.IsMajor)
                .All(same => same);
        }, iter: 300);
    }

    // ---------- rhythm does not care about pitch at all ----------

    [Fact]
    public void RhythmAnalysis_IgnoresPitchEntirely()
    {
        (from pitches in MiddlePitch.Array[2, 12]
         from n in Shift
         select (pitches, n)).Sample(t =>
        {
            using var a = new NoteBuffer(t.pitches.Length);
            using var b = new NoteBuffer(t.pitches.Length);
            foreach (var note in Melody(t.pitches)) a.Add(note);
            foreach (var note in Melody(t.pitches, t.n)) b.Add(note);

            var original = RhythmAnalyzer.Analyze(a);
            var shifted = RhythmAnalyzer.Analyze(b);

            return original.Meter.TimeSignature == shifted.Meter.TimeSignature
                && Math.Abs(original.Syncopation - shifted.Syncopation) < 1e-6f
                && Math.Abs(original.Density - shifted.Density) < 1e-6f
                && original.GrooveFeel == shifted.GrooveFeel
                && original.TextureDescription == shifted.TextureDescription;
        }, iter: 500);
    }
}
