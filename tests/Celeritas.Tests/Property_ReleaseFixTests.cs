using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Ornamentation;
using Celeritas.Core.VoiceLeading;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for the logic 0.10.0 changed. The example-based tests written
/// alongside those fixes assert what I expected; these assert what the code must be true of for
/// any input CsCheck can find, which is a different question and the one that catches a test
/// that is wrong but passing.
/// </summary>
public class PropertyReleaseFixTests
{
    private static readonly Gen<int> MidiPitch = Gen.Int[0, 127];
    private static readonly Gen<int> AnyPitch = Gen.Int[-64, 200];
    private static readonly Gen<int> Semitones = Gen.Int[-36, 36];

    // ---------- key detection ----------

    [Fact]
    public void DetectKey_IsInvariantUnderOctaveTransposition()
    {
        // A key is a question about pitch classes. Moving the whole passage by an octave cannot
        // change the answer; if any path folds a pitch with `%` instead of a true modulo, a
        // negative or high octave is where it shows.
        (from pitches in MidiPitch.Array[1, 16]
         from octaves in Gen.Int[-3, 3]
         select (pitches, octaves)).Sample(t =>
        {
            var shifted = t.pitches.Select(p => p + (t.octaves * 12)).ToArray();

            var original = KeyAnalyzer.IdentifyKey(t.pitches);
            var moved = KeyAnalyzer.IdentifyKey(shifted);

            Assert.Equal(original.Root, moved.Root);
            Assert.Equal(original.IsMajor, moved.IsMajor);
        });
    }

    [Fact]
    public void DetectKey_IsInvariantUnderReordering()
    {
        // Detection reads a multiset. Shuffling the notes changes nothing about which pitch
        // classes sound how often, so it must not change the key.
        (from pitches in MidiPitch.Array[1, 16]
         from seed in Gen.Int[0, int.MaxValue]
         select (pitches, seed)).Sample(t =>
        {
            // Deterministic shuffle from the generated seed: CsCheck replays by seed, and a
            // Random of our own would make a failure unreproducible.
            var shuffled = t.pitches.OrderBy(p => HashCode.Combine(p, t.seed)).ToArray();

            var original = KeyAnalyzer.IdentifyKey(t.pitches);
            var reordered = KeyAnalyzer.IdentifyKey(shuffled);

            Assert.Equal(original.Root, reordered.Root);
            Assert.Equal(original.IsMajor, reordered.IsMajor);
        });
    }

    [Fact]
    public void DetectKey_TransposingThePassage_TransposesTheKey()
    {
        // Transposing every note by n semitones must move the detected tonic by exactly n.
        // This is the property the relative-key fix exists to preserve: emphasis decides the
        // key, and emphasis moves with the music.
        (from pitches in Gen.Int[24, 96].Array[3, 16]
         from n in Gen.Int[-11, 11]
         select (pitches, n)).Sample(t =>
        {
            var moved = t.pitches.Select(p => p + t.n).ToArray();

            var original = KeyAnalyzer.IdentifyKey(t.pitches);
            var transposed = KeyAnalyzer.IdentifyKey(moved);

            Assert.Equal(PitchMath.Fold(original.Root + t.n), transposed.Root);
            Assert.Equal(original.IsMajor, transposed.IsMajor);
        });
    }

    [Fact]
    public void DetectKey_NeverNamesAKeyMissingASoundedPitchClass_WhenSomeScaleContainsThemAll()
    {
        // The overlap prefilter's guarantee: where a diatonic scale contains every pitch class
        // sounded, the answer is one of those scales.
        MidiPitch.Array[1, 12].Sample(pitches =>
        {
            var sounded = pitches.Select(PitchMath.Fold).Distinct().ToArray();

            var anyScaleContainsAll = Enumerable.Range(0, 12).Any(root =>
                new[] { true, false }.Any(major =>
                    sounded.All(pc => new KeySignature((byte)root, major).GetScale().Contains((byte)pc))));

            if (!anyScaleContainsAll)
                return;

            var scale = KeyAnalyzer.IdentifyKey(pitches).GetScale();
            Assert.All(sounded, pc => Assert.Contains((byte)pc, scale));
        });
    }

    [Fact]
    public void DetectKey_NeverThrows_ForAnyPitchWhatsoever()
    {
        // Including negatives and values past 127: the crash this release fixed was exactly a
        // pitch below zero indexing backwards out of a distribution array.
        AnyPitch.Array[0, 24].Sample(pitches =>
        {
            var key = KeyAnalyzer.IdentifyKey(pitches);
            Assert.InRange(key.Root, (byte)0, (byte)11);
        });
    }

    // ---------- scale degrees ----------

    [Fact]
    public void ScaleDegreePitchClass_IsTheKeyRootPlusTheDegreeOffset()
    {
        // The defect this replaces read the enum's value as an ordinal. The invariant that
        // catches that class: every degree of every key lands inside that key's own scale.
        (from root in Gen.Int[0, 11]
         from major in Gen.Bool
         select (root, major)).Sample(t =>
        {
            var key = new KeySignature((byte)t.root, t.major);
            var scale = key.GetScale();

            foreach (var degree in Enum.GetValues<ScaleDegree>())
            {
                var pc = key.GetScaleDegreePitchClass(degree);
                Assert.Contains(pc, scale);
            }
        });
    }

    [Fact]
    public void ScaleDegrees_AreDistinctWithinAKey()
    {
        // Seven degrees, seven distinct pitch classes. Two degrees colliding would mean the
        // degree-to-offset mapping lost one, which is what an ordinal misread does.
        (from root in Gen.Int[0, 11] from major in Gen.Bool select (root, major)).Sample(t =>
        {
            var key = new KeySignature((byte)t.root, t.major);

            var pitchClasses = Enum.GetValues<ScaleDegree>()
                .Select(key.GetScaleDegreePitchClass)
                .ToArray();

            Assert.Equal(pitchClasses.Length, pitchClasses.Distinct().Count());
        });
    }

    [Fact]
    public void SuggestNext_OnlyEverSuggestsChordsDiatonicToTheKeyItDetected()
    {
        // Every suggestion must be spellable in the key the advisor itself reports; a wrong
        // degree lands outside it. Generated over every key rather than the few I picked.
        (from root in Gen.Int[0, 11] from major in Gen.Bool select (root, major)).Sample(t =>
        {
            var tonic = new KeySignature((byte)t.root, t.major);
            var seed = new FunctionalChord(tonic,
                new RomanNumeralChord(ScaleDegree.I, t.major ? ChordQuality.Major : ChordQuality.Minor,
                    HarmonicFunction.Tonic)).Symbol();

            var report = ProgressionAdvisor.Analyze([seed]);
            var scale = report.Key.GetScale();

            foreach (var suggestion in ProgressionAdvisor.SuggestNext([seed], 8))
            {
                // The leading-tone diminished chord of a minor key uses the raised 7th, which
                // is deliberately outside the natural-minor scale.
                if (suggestion.Reason.Contains("Leading tone", StringComparison.Ordinal))
                    continue;

                Assert.True(ProgressionAdvisor.TryParseChordSymbol(suggestion.Chord, out var pitches),
                    $"suggested an unparsable symbol: {suggestion.Chord}");

                var root = PitchMath.Fold(pitches.Min());
                Assert.Contains((byte)root, scale);
            }
        });
    }

    // ---------- voice leading ----------

    [Fact]
    public void Solve_AlwaysVoicesExactlyThePitchClassesItWasGiven()
    {
        // Whatever path the DP takes, a voicing is of the chord it was asked for -- never a
        // note added, never one dropped.
        Gen.Int[0, 11].Array[3, 4].Array[1, 4].Sample(progression =>
        {
            var chords = progression.Select(c => c.Distinct().ToArray()).ToList();
            if (chords.Any(c => c.Length < 3))
                return;

            var solution = new VoiceLeadingSolver().Solve(chords);
            if (!solution.IsValid)
                return;

            for (var i = 0; i < chords.Count; i++)
            {
                var sounded = solution.Voicings[i].ToPitches().Select(PitchMath.Fold).ToHashSet();
                Assert.Equal(chords[i].Select(PitchMath.Fold).ToHashSet(), sounded);
            }
        });
    }

    [Fact]
    public void Solve_AlwaysOrdersVoicesUpwardsAndInsideMidiRange()
    {
        Gen.Int[0, 11].Array[3, 4].Array[1, 4].Sample(progression =>
        {
            var chords = progression.Select(c => c.Distinct().ToArray()).ToList();
            if (chords.Any(c => c.Length < 3))
                return;

            var solution = new VoiceLeadingSolver().Solve(chords);
            if (!solution.IsValid)
                return;

            foreach (var v in solution.Voicings)
            {
                Assert.True(v.Bass <= v.Tenor && v.Tenor <= v.Alto && v.Alto <= v.Soprano);
                Assert.All(v.ToPitches(), p => Assert.InRange(p, 0, 127));
            }
        });
    }

    // ---------- meter and rhythm ----------

    [Fact]
    public void DetectMeter_NeverThrows_AndAlwaysNamesAPositiveMeter()
    {
        (from count in Gen.Int[1, 24]
         from unit in Gen.Int[1, 16]
         select (count, unit)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.count);
            for (var i = 0; i < t.count; i++)
                buffer.AddNote(60, new Rational(i, t.unit), new Rational(1, t.unit));

            var meter = RhythmAnalyzer.DetectMeter(buffer);

            Assert.True(meter.TimeSignature.BeatsPerMeasure > 0);
            Assert.True(meter.TimeSignature.BeatUnit > 0);
            Assert.InRange(meter.Confidence, 0f, 1f);
        });
    }

    [Fact]
    public void DetectMeter_IsInvariantUnderWholesaleTimeShift()
    {
        // Sliding the same rhythm later in the piece must not change the meter it implies.
        (from count in Gen.Int[4, 16] from bars in Gen.Int[0, 4] select (count, bars)).Sample(t =>
        {
            using var atZero = new NoteBuffer(t.count);
            using var shifted = new NoteBuffer(t.count);
            for (var i = 0; i < t.count; i++)
            {
                atZero.AddNote(60, new Rational(i, 4), Rational.Quarter);
                shifted.AddNote(60, new Rational(i, 4) + new Rational(t.bars, 1), Rational.Quarter);
            }

            Assert.Equal(
                RhythmAnalyzer.DetectMeter(atZero).TimeSignature,
                RhythmAnalyzer.DetectMeter(shifted).TimeSignature);
        });
    }

    // ---------- modulation confidence ----------

    [Fact]
    public void ModulationConfidence_NeverLeavesItsDocumentedRange()
    {
        // The defect this guards: a distant key correlates negatively with the window, which
        // pushed the ratio past 1 and carried Confidence with it.
        (from roots in Gen.Int[0, 11].Array[2, 4]
         from major in Gen.Bool
         select (roots, major)).Sample(t =>
        {
            using var buffer = new NoteBuffer(t.roots.Length * 16);
            var pos = Rational.Zero;
            foreach (var root in t.roots)
            {
                var key = new KeySignature((byte)root, t.major);
                foreach (var pc in key.GetScale())
                {
                    for (var rep = 0; rep < 2; rep++)
                    {
                        buffer.AddNote(60 + pc, pos, Rational.Eighth);
                        pos += Rational.Eighth;
                    }
                }
            }

            var result = ModulationDetector.Analyze(buffer, new KeySignature((byte)t.roots[0], t.major));

            Assert.All(result.Modulations, m => Assert.InRange(m.Confidence, 0f, 1f));

            // And events must run forwards in time.
            var offsets = result.Modulations.Select(m => m.Offset).ToArray();
            for (var i = 1; i < offsets.Length; i++)
                Assert.True(offsets[i] >= offsets[i - 1], "modulation events ran backwards");
        });
    }

    // ---------- ornaments ----------

    [Fact]
    public void Articulation_ScalesDurationByExactlyItsMultiplier()
    {
        // The truncation defect: 0.7f stored as 0.69999998 became 69/100 rather than 7/10.
        (from hundredths in Gen.Int[1, 300]
         from den in Gen.Int[1, 16]
         select (hundredths, den)).Sample(t =>
        {
            var multiplier = t.hundredths / 100f;
            var note = new NoteEvent(60, Rational.Zero, new Rational(1, t.den));

            var expanded = new Articulation
            {
                BaseNote = note,
                DurationMultiplier = multiplier,
            }.Expand();

            Assert.Equal(note.Duration * new Rational(t.hundredths, 100), expanded[0].Duration);
        });
    }

    [Fact]
    public void Transpose_MovesEveryPitchByExactlyTheInterval()
    {
        // The SIMD path and the scalar path must agree; this exercises whichever the host
        // selects, over arbitrary buffer lengths including those shorter than a vector.
        (from count in Gen.Int[0, 40]
         from n in Semitones
         select (count, n)).Sample(t =>
        {
            using var buffer = new NoteBuffer(Math.Max(1, t.count));
            for (var i = 0; i < t.count; i++)
                buffer.AddNote(60 + (i % 12), new Rational(i, 4), Rational.Quarter);

            var before = new int[t.count];
            for (var i = 0; i < t.count; i++)
                before[i] = buffer.Get(i).Pitch;

            MusicMath.Transpose(buffer, t.n);

            for (var i = 0; i < t.count; i++)
                Assert.Equal(before[i] + t.n, buffer.Get(i).Pitch);
        });
    }
}

file static class ScaleExtensions
{
    /// <summary>True when the scale contains the pitch class.</summary>
    internal static bool Contains(this byte[] scale, byte pitchClass) =>
        Array.IndexOf(scale, pitchClass) >= 0;
}
