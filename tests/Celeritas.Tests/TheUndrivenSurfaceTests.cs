// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Orchestration;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// The public members no sweep had driven, held to the properties they must have: an operation
/// undone by its own inverse, a relation walked twice, a measure that is symmetric and knows a
/// thing is identical to itself, and everything that answers about pitch answering the same way
/// when the music is moved.
/// <para>
/// Thirteen of the fifteen came back clean the first time, which is the point of writing them
/// down — the fourteenth found that a progression handed in complete was reported as a fragment
/// of itself, and the fifteenth was a wrong assumption of mine about a documented return value.
/// </para>
/// </summary>
public class TheUndrivenSurfaceTests
{
    private static IEnumerable<int[]> EverySet()
    {
        for (ushort mask = 1; mask < 4096; mask++)
        {
            yield return PitchClassSetAnalyzer.MaskToPitchClasses(mask);
        }
    }

    [Fact]
    public void EveryMaskRebuildsFromThePitchClassesItNames()
    {
        for (ushort mask = 0; mask < 4096; mask++)
        {
            var pitchClasses = PitchClassSetAnalyzer.MaskToPitchClasses(mask);

            ushort rebuilt = 0;
            foreach (var pitchClass in pitchClasses)
            {
                rebuilt |= (ushort)(1 << pitchClass);
            }

            Assert.Equal(mask, rebuilt);
            Assert.Equal(System.Numerics.BitOperations.PopCount(mask), pitchClasses.Length);
        }
    }

    [Fact]
    public void InvertingAndComplementingAreTheirOwnInverses()
    {
        foreach (var set in EverySet())
        {
            Assert.Equal(
                set.Order(),
                PitchClassSetAnalyzer.Invert(PitchClassSetAnalyzer.Invert(set)).Order());
            Assert.Equal(
                set.Order(),
                PitchClassSetAnalyzer.Complement(PitchClassSetAnalyzer.Complement(set)).Order());
        }
    }

    [Fact]
    public void TransposingASetAndBackLeavesItWhereItWas()
    {
        foreach (var set in EverySet())
        {
            foreach (var semitones in new[] { -13, -8, -1, 1, 5, 12, 13 })
            {
                Assert.Equal(
                    set.Order(),
                    PitchClassSetAnalyzer.Transpose(
                        PitchClassSetAnalyzer.Transpose(set, semitones), -semitones).Order());
            }
        }
    }

    [Fact]
    public void PrimeFormAndIntervalVectorDoNotMoveWhenTheSetDoes()
    {
        // Both are what a set IS rather than where it sits, so transposing must not touch them.
        foreach (var set in EverySet())
        {
            var prime = PitchClassSetAnalyzer.GetPrimeForm(set);
            var vector = PitchClassSetAnalyzer.GetIntervalVector(set);

            for (var semitones = 1; semitones < 12; semitones++)
            {
                var moved = PitchClassSetAnalyzer.Transpose(set, semitones);
                Assert.Equal(prime, PitchClassSetAnalyzer.GetPrimeForm(moved));
                Assert.Equal(vector, PitchClassSetAnalyzer.GetIntervalVector(moved));
            }
        }
    }

    [Fact]
    public void AnIntervalVectorCountsEachPairOfNotesExactlyOnce()
    {
        foreach (var set in EverySet())
        {
            var vector = PitchClassSetAnalyzer.GetIntervalVector(set);

            Assert.Equal(6, vector.Length);
            Assert.Equal(set.Length * (set.Length - 1) / 2, vector.Sum());
        }
    }

    [Fact]
    public void SimilarityIsSymmetricAndKnowsASetIsIdenticalToItself()
    {
        var sets = EverySet().Where(s => s.Length is >= 3 and <= 6).Take(200).ToArray();

        foreach (var a in sets)
        {
            Assert.Equal(1.0, PitchClassSetAnalyzer.Similarity(a, a), 9);

            foreach (var b in sets.Take(30))
            {
                var forward = PitchClassSetAnalyzer.Similarity(a, b);
                Assert.Equal(forward, PitchClassSetAnalyzer.Similarity(b, a), 9);
                Assert.InRange(forward, 0.0, 1.0);
            }
        }
    }

    [Fact]
    public void KeyRelationsUndoEachOtherAndTheCircleComesHome()
    {
        foreach (var isMajor in new[] { true, false })
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new KeySignature((byte)root, isMajor);

                Assert.Equal(key, key.GetParallelKey().GetParallelKey());
                Assert.Equal(key, key.GetRelativeKey().GetRelativeKey());
                Assert.Equal(key, key.GetDominantKey().GetSubdominantKey());

                // A relative key has the same notes; that is what makes it relative.
                Assert.Equal(key.GetScaleMask(), key.GetRelativeKey().GetScaleMask());

                var walked = key;
                for (var step = 0; step < 12; step++)
                {
                    walked = walked.GetDominantKey();
                }

                Assert.Equal(key, walked);
            }
        }
    }

    [Fact]
    public void AModalKeyConvertsToAKeySignatureAndBack()
    {
        foreach (var isMajor in new[] { true, false })
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new KeySignature((byte)root, isMajor);
                var asModal = ModalKey.FromKeySignature(key);

                Assert.Equal(key, asModal.ToKeySignature());
                Assert.Equal(key.GetScaleMask(), ModeLibrary.GetScaleMask(asModal));
            }
        }

        foreach (var mode in Enum.GetValues<Mode>())
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new ModalKey((byte)root, mode);

                Assert.Equal(new ModalKey((byte)root, Mode.Ionian), key.ParallelMajor);
                Assert.Equal(new ModalKey((byte)root, Mode.Aeolian), key.ParallelMinor);
            }
        }
    }

    [Fact]
    public void RotatingAMaskOneWayAndBackLeavesIt()
    {
        for (ushort value = 0; value < 4096; value += 7)
        {
            for (var shift = 0; shift <= 12; shift++)
            {
                var left = KeyAnalyzer.RotateLeft(value, shift);

                Assert.Equal(value, KeyAnalyzer.RotateRight(left, shift));
                Assert.Equal(
                    System.Numerics.BitOperations.PopCount(value),
                    System.Numerics.BitOperations.PopCount(left));
            }
        }
    }

    [Fact]
    public void TheTwoScaleMaskImplementationsAgreeWithEachOtherAndWithTheScale()
    {
        foreach (var isMajor in new[] { true, false })
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new KeySignature((byte)root, isMajor);

                Assert.Equal(key.GetScaleMask(), KeyAnalyzer.GetScaleMask(root, isMajor));

                ushort fromScale = 0;
                foreach (var pitchClass in key.GetScale())
                {
                    fromScale |= (ushort)(1 << pitchClass);
                }

                Assert.Equal(key.GetScaleMask(), fromScale);
            }
        }
    }

    [Fact]
    public void ContainsPitchAgreesWithTheScaleMaskBelowZeroToo()
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            for (var root = 0; root < 12; root++)
            {
                var key = new ModalKey((byte)root, mode);
                var mask = ModeLibrary.GetScaleMask(key);

                for (var pitch = -24; pitch < 36; pitch++)
                {
                    var expected = (mask & (1 << (((pitch % 12) + 12) % 12))) != 0;
                    Assert.Equal(expected, ModeLibrary.ContainsPitch(key, pitch));
                }
            }
        }
    }

    [Fact]
    public void EveryProgressionBuilderBuildsTheSameProgressionInEveryKey()
    {
        var builders = new (string Name, Func<KeySignature, DiatonicChordType, MinorDominantStyle, FunctionalChord[]> Build)[]
        {
            ("Circle", FunctionalProgressions.Circle),
            ("TwoFiveOne", FunctionalProgressions.TwoFiveOne),
            ("Turnaround", FunctionalProgressions.Turnaround),
            ("ThreeSixTwoFiveOne", FunctionalProgressions.ThreeSixTwoFiveOne),
        };

        foreach (var (name, build) in builders)
        {
            foreach (var type in Enum.GetValues<DiatonicChordType>())
            {
                foreach (var style in Enum.GetValues<MinorDominantStyle>())
                {
                    foreach (var isMajor in new[] { true, false })
                    {
                        string? reference = null;
                        for (var root = 0; root < 12; root++)
                        {
                            var chords = build(new KeySignature((byte)root, isMajor), type, style);
                            var shape = string.Join(" | ", chords.Select(c =>
                                $"{c.Roman.Degree}/{c.Roman.Quality}/"
                                + string.Join(",", RelativeTo(c.PitchClassMask, root))));

                            reference ??= shape;
                            Assert.Equal(reference, shape);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void WithTheNaturalMinorDominantNoBuiltChordLeavesTheKey()
    {
        var builders = new Func<KeySignature, DiatonicChordType, MinorDominantStyle, FunctionalChord[]>[]
        {
            FunctionalProgressions.Circle,
            FunctionalProgressions.TwoFiveOne,
            FunctionalProgressions.Turnaround,
            FunctionalProgressions.ThreeSixTwoFiveOne,
        };

        foreach (var build in builders)
        {
            foreach (var type in Enum.GetValues<DiatonicChordType>())
            {
                foreach (var isMajor in new[] { true, false })
                {
                    for (var root = 0; root < 12; root++)
                    {
                        var key = new KeySignature((byte)root, isMajor);
                        foreach (var chord in build(key, type, MinorDominantStyle.Natural))
                        {
                            Assert.Equal(0, chord.PitchClassMask & ~key.GetScaleMask());
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void AProgressionHandedInWholeIsNotReportedAsAFragmentOfItself()
    {
        // Confidence is the fraction of the PATTERN found in the input, so a short pattern is
        // easier to match whole: "ii - V - I" scores 1.0 on "I - iii - vi - ii - V - I", which
        // also scores 1.0 against itself, and keeping the first found reported the fragment.
        foreach (var mode in Enum.GetValues<Mode>())
        {
            foreach (var progression in ModalProgressions.GetProgressionsForMode(mode))
            {
                var (detectedMode, match, confidence) =
                    ModalProgressions.DetectModalProgression(progression.Degrees.ToArray());

                Assert.True(Enum.IsDefined(detectedMode));
                Assert.InRange(confidence, 0f, 1f);

                if (detectedMode == mode)
                {
                    Assert.NotNull(match);
                    Assert.Equal(progression.Name, match.Value.Name);
                }
            }
        }
    }

    [Fact]
    public void AnalyzeAlsoReportsTheWholeProgressionNotAFragment()
    {
        // The rule above reached DetectModalProgression first and Analyze only later: the same
        // strict "better than" kept catalogue order on a tie, so "I - iii - vi - ii - V - I"
        // handed to Analyze came back as "ii - V - I".
        var whole = ModalProgressions.Analyze(["C", "Em", "Am", "Dm", "G", "C"]);
        Assert.Equal("I - iii - vi - ii - V - I", whole.MatchedProgression?.Name);

        var lydian = ModalProgressions.Analyze(["C", "D", "Bm", "C"]);
        Assert.Equal("I - II - vii - I", lydian.MatchedProgression?.Name);
    }

    [Fact]
    public void EveryRhythmPatternPlayedExactlyIsNamedAsItself()
    {
        // At equal match quality the pattern accounting for more onsets wins. Habanera,
        // Charleston and Clave 3-2 begin with a shorter catalogue entry, and a quality tie went
        // to catalogue order, so played exactly they were named as their own first half — and
        // named correctly only when played slightly off. Waltz and Backbeat carry metric and
        // velocity requirements a bare duration list cannot meet, so they are not asked here.
        foreach (var pattern in RhythmAnalyzer.CommonPatterns)
        {
            if (pattern.Name is "Waltz" or "Backbeat")
            {
                continue;
            }

            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var repeat = 0; repeat < 2; repeat++)
            {
                foreach (var duration in pattern.Durations)
                {
                    notes.Add(new NoteEvent(60, time, duration, 0.8f));
                    time += duration;
                }
            }

            var match = RhythmAnalyzer.IdentifyPattern(notes);

            Assert.NotNull(match);
            Assert.Equal(pattern.Name, match.Pattern.Name);
        }
    }

    [Fact]
    public void ChordKeyFitMovesWithTheMusic()
    {
        // The doc calls it a dot product with the key profile, higher being a better fit — so it
        // is not bounded, but it must answer the same for the same chord in the same place.
        for (ushort mask = 1; mask < 4096; mask += 3)
        {
            for (var root = 0; root < 12; root++)
            {
                foreach (var isMajor in new[] { true, false })
                {
                    ushort rotated = 0;
                    for (var i = 0; i < 12; i++)
                    {
                        if ((mask & (1 << i)) != 0)
                        {
                            rotated |= (ushort)(1 << ((i + 1) % 12));
                        }
                    }

                    Assert.Equal(
                        KeyProfiler.ChordKeyFit(mask, new KeySignature((byte)root, isMajor)),
                        KeyProfiler.ChordKeyFit(rotated, new KeySignature((byte)((root + 1) % 12), isMajor)),
                        4);
                }
            }
        }
    }

    [Fact]
    public void EveryKeyProfileIsTheCProfileRotatedOntoItsOwnRoot()
    {
        foreach (var isMajor in new[] { true, false })
        {
            var reference = KeyProfiler.GetKeyProfile(0, isMajor).ToArray();

            for (var root = 0; root < 12; root++)
            {
                var profile = KeyProfiler.GetKeyProfile(root, isMajor);

                Assert.Equal(12, profile.Length);
                for (var degree = 0; degree < 12; degree++)
                {
                    Assert.Equal(reference[degree], profile[(degree + root) % 12], 4);
                }
            }
        }
    }

    [Fact]
    public void VoiceLeadingScoresMoveWithTheMusic()
    {
        var random = new Random(20260910);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var from = RandomVoicing(random);
            var to = RandomVoicing(random);
            var shift = random.Next(1, 12);

            Assert.Equal(
                VoiceLeadingRules.ScoreSmoothness(from, to),
                VoiceLeadingRules.ScoreSmoothness(Shift(from, shift), Shift(to, shift)),
                3);

            Assert.Equal(
                VoiceLeadingRules.Score(from, to, 0),
                VoiceLeadingRules.Score(Shift(from, shift), Shift(to, shift), shift % 12),
                3);
        }
    }

    [Fact]
    public void OrchestrationKeepsEveryNoteInsideItsPartAndKeepsItsPitchClass()
    {
        var random = new Random(20260910);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var notes = Enumerable.Range(0, random.Next(1, 12))
                .Select(i => new NoteEvent(random.Next(0, 128), new Rational(i, 4), Rational.Quarter, 0.8f))
                .ToArray();

            var result = OrchestrationMapper.Map(notes);

            Assert.Equal(notes.Length, result.Parts.Sum(p => p.Notes.Length));

            foreach (var part in result.Parts)
            {
                foreach (var note in part.Notes)
                {
                    Assert.True(
                        part.Definition.Range.Contains(note.Pitch),
                        $"{note.Pitch} outside {part.Definition.Name}");
                }
            }

            Assert.Equal(
                notes.Select(n => ((n.Pitch % 12) + 12) % 12).Order(),
                result.Parts.SelectMany(p => p.Notes)
                    .Select(n => ((n.Pitch % 12) + 12) % 12).Order());
        }
    }

    [Fact]
    public void ACatalogueKeyIsTheSameWhicheverWayThePrimeFormIsGiven()
    {
        // PrimeFormKey folded without sorting, so a caller indexing with it from [7,3,0] got
        // "7,3,0" — a key the catalogue never stores anything under, since it sorts on load —
        // and every lookup through that key missed.
        Assert.Equal("0,3,7", PitchClassSetCatalog.PrimeFormKey([0, 3, 7]));
        Assert.Equal("0,3,7", PitchClassSetCatalog.PrimeFormKey([7, 3, 0]));
        Assert.Equal("0,3,7", PitchClassSetCatalog.PrimeFormKey([19, 15, 12]));
        Assert.Equal("0,3,7", PitchClassSetCatalog.PrimeFormKey([-12, 3, 7]));

        for (ushort mask = 1; mask < 4096; mask += 7)
        {
            var set = PitchClassSetAnalyzer.MaskToPitchClasses(mask);
            var shuffled = set.Reverse().Select(p => p + 12).ToArray();

            Assert.Equal(
                PitchClassSetCatalog.PrimeFormKey(set),
                PitchClassSetCatalog.PrimeFormKey(shuffled));
            Assert.Equal(
                PitchClassSetCatalog.PrimeFormKey(set),
                string.Join(",", PitchClassSetCatalog.NormalizePrimeForm(shuffled)));
        }
    }

    private static int[] RelativeTo(ushort mask, int root)
    {
        var classes = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                classes.Add(((i - root) % 12 + 12) % 12);
            }
        }

        classes.Sort();
        return [.. classes];
    }

    private static Voicing RandomVoicing(Random random)
    {
        var bass = random.Next(40, 56);
        var tenor = random.Next(bass, 68);
        var alto = random.Next(tenor, 75);
        return new Voicing(bass, tenor, alto, random.Next(alto, 82));
    }

    private static Voicing Shift(Voicing voicing, int semitones) =>
        new(voicing.Bass + semitones, voicing.Tenor + semitones,
            voicing.Alto + semitones, voicing.Soprano + semitones);
}
