// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// Two analyzers at their limits: the modulation detector given too little music to judge, and
/// the voice-leading solver on a search space small enough to take its sequential path or tight
/// enough to have no path at all. Both answer with a well-formed result either way, so the
/// difference between "nothing to report" and "reported nothing" only shows in an assertion.
/// </summary>
public class ModulationAndVoiceLeadingEdgeTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static NoteEvent[] Chord(int quarter, params int[] pitches) =>
        [.. pitches.Select(p => new NoteEvent(p, new Rational(quarter, 4), Rational.Quarter))];

    // ---------- not enough music to judge ----------

    [Fact]
    public void NoNotesAtAll_StaysInTheKeyItStartedIn()
    {
        var result = ModulationDetector.Analyze(ReadOnlySpan<NoteEvent>.Empty, CMajor);

        Assert.Empty(result.Modulations);
        Assert.Equal(CMajor, result.StartKey);
        Assert.Equal(CMajor, result.EndKey);
    }

    [Fact]
    public void ASingleChord_IsNotEnoughToModulate()
    {
        var result = ModulationDetector.Analyze(Chord(0, 60, 64, 67), CMajor);

        Assert.Empty(result.Modulations);
        Assert.Equal(CMajor, result.EndKey);
    }

    [Fact]
    public void AnEmptyBuffer_IsHandledLikeAnEmptySpan()
    {
        using var buffer = new NoteBuffer(4);

        var result = ModulationDetector.Analyze(buffer, CMajor);

        Assert.Empty(result.Modulations);
        Assert.Equal(CMajor, result.EndKey);
    }

    [Fact]
    public void TheCountsAddUpToTheModulationList()
    {
        // A long stretch in C followed by a long stretch in E flat.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67), .. Chord(1, 65, 69, 72), .. Chord(2, 67, 71, 74), .. Chord(3, 60, 64, 67),
            .. Chord(4, 63, 67, 70), .. Chord(5, 68, 72, 75), .. Chord(6, 70, 74, 77), .. Chord(7, 63, 67, 70),
            .. Chord(8, 63, 67, 70), .. Chord(9, 68, 72, 75), .. Chord(10, 70, 74, 77), .. Chord(11, 63, 67, 70),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.Equal(
            result.Modulations.Count(m => m.Type == ModulationType.Tonicization),
            result.TonicizationCount);
        Assert.Equal(
            result.Modulations.Count(m => m.Type != ModulationType.Tonicization),
            result.TrueModulationCount);
        Assert.Equal(result.TonicizationCount + result.TrueModulationCount, result.Modulations.Count);
        Assert.True(result.KeyCount >= 1, "even an unmodulating piece is in one key");
    }

    [Fact]
    public void APieceThatNeverLeavesItsKey_CountsOneKey()
    {
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67), .. Chord(1, 65, 69, 72),
            .. Chord(2, 67, 71, 74), .. Chord(3, 60, 64, 67),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.Equal(1, result.KeyCount);
        Assert.Equal(0, result.TonicizationCount);
        Assert.Equal(0, result.TrueModulationCount);
    }

    [Fact]
    public void NotesThatSpellNoRecognisableChord_DoNotDerailTheAnalysis()
    {
        // Clusters the chord library has no name for: the detector must pass over them rather
        // than reading a key out of noise.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 61, 62), .. Chord(1, 66, 67, 68),
            .. Chord(2, 71, 72, 73), .. Chord(3, 60, 61, 62),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.NotNull(result.Modulations);
        Assert.Equal(CMajor, result.StartKey);
    }

    // ---------- the solver's small and impossible cases ----------

    [Fact]
    public void ASmallSearchSpace_TakesTheSequentialPath_AndStillSolves()
    {
        // Two-note chords give few voicings each, well under the threshold where the solver
        // fans the forward pass out across threads.
        var solution = new VoiceLeadingSolver().Solve([[0, 7], [5, 0], [7, 2], [0, 7]]);

        Assert.True(solution.IsValid);
        Assert.Equal(4, solution.Voicings.Count);
        Assert.True(solution.TotalCost < float.MaxValue);
    }

    [Fact]
    public void ASmallSearchSpaceAgreesWithItself_AcrossRuns()
    {
        var a = new VoiceLeadingSolver().Solve([[0, 7], [5, 0], [0, 7]]);
        var b = new VoiceLeadingSolver().Solve([[0, 7], [5, 0], [0, 7]]);

        Assert.Equal(a.TotalCost, b.TotalCost);
        Assert.Equal(
            a.Voicings.Select(v => string.Join(",", v.ToPitches())),
            b.Voicings.Select(v => string.Join(",", v.ToPitches())));
    }

    [Fact]
    public void WithNoAffordableTransition_TheSolverSaysItFoundNoPath()
    {
        // Every transition costs something, so a ceiling of nearly zero rules them all out.
        var solver = new VoiceLeadingSolver(new VoiceLeadingSolverOptions { MaxTransitionCost = 0.0001f });

        var solution = solver.Solve([[0, 4, 7], [5, 9, 0], [7, 11, 2]]);

        Assert.False(solution.IsValid);
        Assert.Empty(solution.Voicings);
        Assert.Equal(float.MaxValue, solution.TotalCost);
        Assert.Contains(solution.Warnings, w => w.Contains("No valid voice leading path", StringComparison.Ordinal));
    }

    [Fact]
    public void ASolvedProgressionStillPrintsWhatIsWrongWithIt()
    {
        // C-G to Db-Ab: the best available path is a solution, but it moves by an augmented
        // interval. The score has to say so rather than presenting it as clean.
        var solution = new VoiceLeadingSolver().Solve([[0, 7], [1, 8]]);

        Assert.True(solution.IsValid);
        var warning = Assert.Single(solution.Warnings);
        Assert.Contains("AugmentedInterval", warning, StringComparison.Ordinal);

        var score = solution.ToScore();
        Assert.Contains("Warnings:", score, StringComparison.Ordinal);
        Assert.Contains("AugmentedInterval", score, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsolvableProgressionPrintsNoScoreAtAll()
    {
        var solver = new VoiceLeadingSolver(new VoiceLeadingSolverOptions { MaxTransitionCost = 0.0001f });

        var score = solver.Solve([[0, 4, 7], [5, 9, 0], [7, 11, 2]]).ToScore();

        Assert.Equal("No valid solution found.", score);
    }

    [Fact]
    public void ACleanSolutionPrintsNoWarningSection()
    {
        var score = new VoiceLeadingSolver().Solve([[0, 4, 7], [7, 11, 2], [0, 4, 7]]).ToScore();

        Assert.DoesNotContain("Warnings:", score, StringComparison.Ordinal);
        Assert.Contains("Total voice leading cost:", score, StringComparison.Ordinal);
    }
    // ---------- every diatonic degree the detector can meet ----------

    [Fact]
    public void ADiatonicTourOfTheKey_IsReadWithoutModulating()
    {
        // One chord on each degree of C major: the detector has to map every scale index to
        // its roman numeral, and none of them is a key change.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67),    // I
            .. Chord(1, 62, 65, 69),    // ii
            .. Chord(2, 64, 67, 71),    // iii
            .. Chord(3, 65, 69, 72),    // IV
            .. Chord(4, 67, 71, 74),    // V
            .. Chord(5, 69, 72, 76),    // vi
            .. Chord(6, 71, 74, 77),    // vii
            .. Chord(7, 60, 64, 67),    // I
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.Equal(CMajor, result.StartKey);
        Assert.Equal(CMajor, result.EndKey);
        Assert.Equal(1, result.KeyCount);
    }

    [Fact]
    public void SingleNotesAreNotChords()
    {
        // One pitch class per beat: nothing to identify, so nothing to modulate to.
        NoteEvent[] notes = [.. Chord(0, 60), .. Chord(1, 62), .. Chord(2, 64), .. Chord(3, 65)];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.Empty(result.Modulations);
    }

    [Fact]
    public void AChordOnANoteOutsideTheKey_IsPassedOver()
    {
        // F sharp major has no place in C major's scale, so the detector cannot give it a
        // degree — and must not invent one.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67),
            .. Chord(1, 66, 70, 73),
            .. Chord(2, 60, 64, 67),
            .. Chord(3, 67, 71, 74),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.Equal(CMajor, result.StartKey);
        Assert.All(result.Modulations, m => Assert.NotEqual(m.FromKey, m.ToKey));
    }

    [Fact]
    public void EveryModulationIsPlacedInTimeWithAMeasuredConfidence()
    {
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67), .. Chord(1, 65, 69, 72), .. Chord(2, 67, 71, 74), .. Chord(3, 60, 64, 67),
            .. Chord(4, 62, 66, 69), .. Chord(5, 67, 71, 74), .. Chord(6, 69, 73, 76), .. Chord(7, 62, 66, 69),
            .. Chord(8, 62, 66, 69), .. Chord(9, 69, 73, 76), .. Chord(10, 62, 66, 69),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.All(result.Modulations, m =>
        {
            Assert.True(m.Offset >= Rational.Zero, "a modulation was placed before the music started");
            Assert.InRange(m.Confidence, 0f, 1f);
            Assert.NotEqual(m.FromKey, m.ToKey);
        });
    }
    // ---------- what the pivot search reads on its way ----------

    [Fact]
    public void APivotSearchReadsTheChordsBeforeTheModulation()
    {
        // C major on several degrees, then a settled stretch of A major: the detector walks
        // back over the earlier chords looking for one that belongs to both keys.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67),     // I
            .. Chord(1, 64, 67, 71),     // iii
            .. Chord(2, 65, 69, 72),     // IV
            .. Chord(3, 69, 72, 76),     // vi
            .. Chord(4, 69, 73, 76),     // A major
            .. Chord(5, 66, 69, 73),     // D... F# minor
            .. Chord(6, 64, 68, 71),     // E major
            .. Chord(7, 69, 73, 76),
            .. Chord(8, 66, 69, 73),
            .. Chord(9, 64, 68, 71),
            .. Chord(10, 69, 73, 76),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.All(result.Modulations, m =>
        {
            Assert.NotEqual(m.FromKey, m.ToKey);
            Assert.InRange(m.Confidence, 0f, 1f);
        });
    }

    [Fact]
    public void AClusterBeforeAModulation_IsNotReadAsAPivot()
    {
        // The chord before the key change spells nothing the library can name; the pivot
        // search has to pass over it rather than assigning it a roman numeral.
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67),
            .. Chord(1, 65, 69, 72),
            .. Chord(2, 60, 61, 62),          // a cluster
            .. Chord(3, 63, 67, 70),
            .. Chord(4, 68, 72, 75),
            .. Chord(5, 70, 74, 77),
            .. Chord(6, 63, 67, 70),
            .. Chord(7, 68, 72, 75),
            .. Chord(8, 63, 67, 70),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.All(result.Modulations, m => Assert.NotEqual(m.FromKey, m.ToKey));
    }

    [Fact]
    public void ATurnToTheParallelMinor_IsNoticed()
    {
        NoteEvent[] notes =
        [
            .. Chord(0, 60, 64, 67), .. Chord(1, 65, 69, 72), .. Chord(2, 67, 71, 74), .. Chord(3, 60, 64, 67),
            .. Chord(4, 60, 63, 67), .. Chord(5, 65, 68, 72), .. Chord(6, 67, 70, 74), .. Chord(7, 60, 63, 67),
            .. Chord(8, 60, 63, 67), .. Chord(9, 65, 68, 72), .. Chord(10, 60, 63, 67),
        ];

        var result = ModulationDetector.Analyze(notes, CMajor);

        Assert.NotEmpty(result.Modulations);
        Assert.Contains(result.Modulations, m => !m.ToKey.IsMajor);
        Assert.All(result.Modulations, m =>
        {
            Assert.NotEqual(m.FromKey, m.ToKey);
            Assert.True(Enum.IsDefined(m.Type), $"{m.Type} is not a defined modulation type");
        });
    }
}
