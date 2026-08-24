// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// The SATB solver had no functional tests: coverage was 10.8%, and every covered line was a
/// null guard. These assert what the feature promises rather than how it computes — a solution
/// really voices the chords it was given, really obeys the counterpoint rules the solver exists
/// to enforce, and really answers the options it is handed.
/// </summary>
public class VoiceLeadingSolverTests
{
    private static readonly int[] CMajor = [0, 4, 7];
    private static readonly int[] FMajor = [5, 9, 0];
    private static readonly int[] GMajor = [7, 11, 2];
    private static readonly int[] AMinor = [9, 0, 4];

    private static List<int[]> Progression(params int[][] chords) => [.. chords];

    // ---------- it produces a usable solution at all ----------

    [Fact]
    public void Solve_DiatonicProgression_ProducesOneVoicingPerChord()
    {
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, CMajor));

        Assert.True(solution.IsValid);
        Assert.Equal(4, solution.Voicings.Count);
        Assert.Empty(solution.Warnings);
    }

    [Fact]
    public void Solve_EmptyProgression_IsAnEmptySolution_NotAFailure()
    {
        var solution = new VoiceLeadingSolver().Solve([]);

        Assert.Empty(solution.Voicings);
        Assert.Equal(0f, solution.TotalCost);
        Assert.Empty(solution.Warnings);
    }

    [Fact]
    public void Solve_ChordThatCannotBeVoiced_FailsLoudlyWithAWarning()
    {
        // An empty pitch-class set has nothing to voice; the solver must say so rather than
        // return a confident empty answer.
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, []));

        Assert.False(solution.IsValid);
        Assert.NotEmpty(solution.Warnings);
    }

    // ---------- the voicings are actually the chords asked for ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Solve_EveryVoicedPitchBelongsToItsChord(int index)
    {
        var chords = Progression(CMajor, FMajor, GMajor, AMinor);
        var solution = new VoiceLeadingSolver().Solve(chords);

        var expected = chords[index];
        foreach (var pitch in solution.Voicings[index].ToPitches())
        {
            Assert.Contains(((pitch % 12) + 12) % 12, expected);
        }
    }

    [Fact]
    public void Solve_EveryChordSoundsAllOfItsPitchClasses()
    {
        // Four voices over a triad means one is doubled, but none may be dropped: a "voicing"
        // missing the third is a different chord.
        var chords = Progression(CMajor, FMajor, GMajor, AMinor);
        var solution = new VoiceLeadingSolver().Solve(chords);

        for (var i = 0; i < chords.Count; i++)
        {
            var sounded = solution.Voicings[i].ToPitches().Select(p => ((p % 12) + 12) % 12).ToHashSet();
            Assert.Equal(chords[i].ToHashSet(), sounded);
        }
    }

    [Fact]
    public void Solve_VoicesAreOrderedBassUpwards()
    {
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, CMajor));

        foreach (var v in solution.Voicings)
        {
            Assert.True(v.Bass <= v.Tenor, $"bass {v.Bass} above tenor {v.Tenor}");
            Assert.True(v.Tenor <= v.Alto, $"tenor {v.Tenor} above alto {v.Alto}");
            Assert.True(v.Alto <= v.Soprano, $"alto {v.Alto} above soprano {v.Soprano}");
        }
    }

    [Fact]
    public void Solve_AllVoicesLandInMidiRange()
    {
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, AMinor, CMajor));

        foreach (var pitch in solution.Voicings.SelectMany(v => v.ToPitches()))
        {
            Assert.InRange(pitch, 0, 127);
        }
    }

    // ---------- it enforces the rules it exists for ----------

    [Fact]
    public void Solve_StrictMode_ProducesNoParallelFifthsOrOctaves()
    {
        // This is the solver's whole purpose. Checked with the library's own rule checker, so
        // the test cannot drift from the definition the solver is optimizing against.
        var solution = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Strict)
            .Solve(Progression(CMajor, FMajor, GMajor, CMajor, AMinor, FMajor, GMajor, CMajor));

        Assert.True(solution.IsValid);

        for (var i = 1; i < solution.Voicings.Count; i++)
        {
            var check = VoiceLeadingRules.Check(solution.Voicings[i - 1], solution.Voicings[i]);
            Assert.False(check.HasViolation(VoiceLeadingViolation.ParallelFifths),
                $"parallel fifths between chord {i - 1} and {i}");
            Assert.False(check.HasViolation(VoiceLeadingViolation.ParallelOctaves),
                $"parallel octaves between chord {i - 1} and {i}");
        }
    }

    [Fact]
    public void Solve_PrefersSmoothMotion_OverLeaping()
    {
        // A solved progression should move voices by small intervals; the cost function exists
        // to make that happen. Anything above an octave in an inner voice is a leap no
        // four-part writing textbook would accept between diatonic triads.
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, CMajor));

        for (var i = 1; i < solution.Voicings.Count; i++)
        {
            var from = solution.Voicings[i - 1].ToPitches();
            var to = solution.Voicings[i].ToPitches();
            for (var v = 0; v < 4; v++)
            {
                Assert.True(Math.Abs(to[v] - from[v]) <= 12,
                    $"voice {v} leapt {Math.Abs(to[v] - from[v])} semitones between chord {i - 1} and {i}");
            }
        }
    }

    // ---------- the options are honoured ----------

    [Fact]
    public void Solve_StrictAndRelaxed_AreNotTheSameSolver()
    {
        var chords = Progression(CMajor, FMajor, GMajor, AMinor, FMajor, CMajor);

        var strict = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Strict).Solve(chords);
        var relaxed = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Relaxed).Solve(chords);

        Assert.True(strict.IsValid);
        Assert.True(relaxed.IsValid);

        // Strict weights smoothness more heavily and rejects any violating transition, so the
        // two presets must not be interchangeable — either the path or its cost differs.
        var samePath = strict.Voicings.SequenceEqual(relaxed.Voicings);
        Assert.False(samePath && Math.Abs(strict.TotalCost - relaxed.TotalCost) < 0.001f,
            "Strict and Relaxed produced an identical solution at an identical cost");
    }

    [Fact]
    public void Solve_DefaultOptions_MatchAnExplicitDefault()
    {
        var chords = Progression(CMajor, GMajor, CMajor);

        var implicitDefault = new VoiceLeadingSolver().Solve(chords);
        var explicitDefault = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Default).Solve(chords);

        Assert.Equal(explicitDefault.Voicings, implicitDefault.Voicings);
        Assert.Equal(explicitDefault.TotalCost, implicitDefault.TotalCost);
    }

    // ---------- the symbol overload agrees with the pitch-class one ----------

    [Fact]
    public void SolveFromSymbols_AgreesWithSolve_OnTheSameChords()
    {
        var bySymbol = new VoiceLeadingSolver().SolveFromSymbols(["C", "F", "G", "C"]);
        var byPitchClass = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, CMajor));

        Assert.True(bySymbol.IsValid);
        Assert.Equal(byPitchClass.Voicings, bySymbol.Voicings);
    }

    [Fact]
    public void SolveFromSymbols_UnparsableSymbol_FailsWithAWarningNamingIt()
    {
        var solution = new VoiceLeadingSolver().SolveFromSymbols(["C", "Zzz", "G"]);

        Assert.False(solution.IsValid);
        Assert.Contains(solution.Warnings, w => w.Contains("Zzz", StringComparison.Ordinal));
    }

    // ---------- the solution converts to something usable ----------

    [Fact]
    public void ToNoteBuffer_EmitsFourVoicesPerChord_AtTheRequestedDuration()
    {
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor));

        using var buffer = solution.ToNoteBuffer(Rational.Whole);

        Assert.Equal(12, buffer.Count);
        for (var i = 0; i < buffer.Count; i++)
        {
            Assert.Equal(Rational.Whole, buffer.Get(i).Duration);
        }
    }

    [Fact]
    public void ToScore_RendersEveryChord()
    {
        var solution = new VoiceLeadingSolver().Solve(Progression(CMajor, FMajor, GMajor, CMajor));

        var score = solution.ToScore();

        Assert.False(string.IsNullOrWhiteSpace(score));

        // The heading read "SATB VoicePart Leading" until a stray rename was undone; pin the
        // words a reader actually sees so a find-replace cannot mangle them again unnoticed.
        Assert.Contains("SATB Voice Leading:", score, StringComparison.Ordinal);
        Assert.DoesNotContain("VoicePart", score, StringComparison.Ordinal);

        foreach (var column in new[] { "Bass", "Tenor", "Alto", "Soprano" })
            Assert.Contains(column, score, StringComparison.Ordinal);

        // One numbered row per chord.
        foreach (var n in new[] { " 1.", " 2.", " 3.", " 4." })
            Assert.Contains(n, score, StringComparison.Ordinal);
    }

    // ---------- determinism: the search is parallel, the answer must not be ----------

    [Fact]
    public void Solve_IsDeterministic_AcrossRepeatedRuns()
    {
        // FindOptimalPath runs the DP in parallel. A race there would surface as a different
        // path between runs, which no amount of single-run testing would catch.
        var chords = Progression(CMajor, FMajor, GMajor, AMinor, FMajor, GMajor, CMajor);
        var first = new VoiceLeadingSolver().Solve(chords);

        for (var run = 0; run < 12; run++)
        {
            var again = new VoiceLeadingSolver().Solve(chords);
            Assert.Equal(first.Voicings, again.Voicings);
            Assert.Equal(first.TotalCost, again.TotalCost);
        }
    }
}
