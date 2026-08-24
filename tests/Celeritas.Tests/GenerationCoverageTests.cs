// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Simd;

namespace Celeritas.Tests;

/// <summary>
/// The last uncovered generation paths: the arpeggio walk, the Free figured-bass style, and
/// the chromatic-bass fallback. Generators fail quietly — a wrong voicing is still a chord and
/// a truncated arpeggio is still a phrase — so these assert musical shape, not just non-null.
/// </summary>
public class GenerationCoverageTests
{
    private static readonly KeySignature CMajor = new(0, true);

    private static List<ChordAssignment> TwoChords() =>
    [
        new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify("C4 E4 G4"), [60, 64, 67]),
        new(Rational.Half, Rational.Whole, ChordAnalyzer.Identify("F4 A4 C5"), [65, 69, 72]),
    ];

    // ---------- the arpeggio walk ----------

    [Fact]
    public void Arpeggio_StartsEachSegmentOnTheBass_ThenCyclesTheChordTones()
    {
        var notes = AccompanimentGenerator.Generate(TwoChords(),
            AccompanimentOptions.Default with { Pattern = AccompanimentPattern.Arpeggio });

        var firstSegment = notes.Where(n => n.Offset < Rational.Half).OrderBy(n => n.Offset.ToDouble()).ToArray();

        Assert.NotEmpty(firstSegment);
        Assert.Equal(firstSegment.Min(n => n.Pitch), firstSegment[0].Pitch);   // bass leads
        Assert.All(firstSegment.Skip(1), n => Assert.True(n.Pitch > firstSegment[0].Pitch));
    }

    [Fact]
    public void Arpeggio_TheBassIsLouderThanTheChordTones()
    {
        var notes = AccompanimentGenerator.Generate(TwoChords(),
            AccompanimentOptions.Default with { Pattern = AccompanimentPattern.Arpeggio });

        var bass = notes.OrderBy(n => n.Offset.ToDouble()).First();
        Assert.All(notes.Where(n => n.Pitch > bass.Pitch),
            n => Assert.True(n.Velocity <= bass.Velocity));
    }

    [Fact]
    public void Arpeggio_ClipsTheLastNoteToTheSegmentRatherThanOverrunning()
    {
        // A subdivision that does not divide the segment evenly: the final note must be
        // shortened, not allowed to sound past the chord it belongs to.
        var notes = AccompanimentGenerator.Generate(TwoChords(), AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = new Rational(3, 16),
        });

        Assert.All(notes, n => Assert.True(n.Offset + n.Duration <= Rational.Whole));

        var firstSegment = notes.Where(n => n.Offset < Rational.Half).ToArray();
        Assert.All(firstSegment, n => Assert.True(n.Offset + n.Duration <= Rational.Half,
            $"a note at {n.Offset} for {n.Duration} overran its segment"));
    }

    [Fact]
    public void Arpeggio_NonPositiveSubdivision_FallsBackRatherThanLoopingForever()
    {
        // A zero step would never advance the walk; the generator substitutes a sane default.
        var notes = AccompanimentGenerator.Generate(TwoChords(), AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = Rational.Zero,
        });

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.True(n.Duration > Rational.Zero));
    }

    [Fact]
    public void Arpeggio_LongSegment_CyclesThroughTheChordToneRepeatedly()
    {
        List<ChordAssignment> oneLongChord =
            [new(Rational.Zero, new Rational(2, 1), ChordAnalyzer.Identify("C4 E4 G4"), [60, 64, 67])];

        var notes = AccompanimentGenerator.Generate(oneLongChord, AccompanimentOptions.Default with
        {
            Pattern = AccompanimentPattern.Arpeggio,
            Subdivision = Rational.Quarter,
        });

        Assert.Equal(8, notes.Length);                       // two whole notes at a quarter each
        Assert.True(notes.Select(n => n.Pitch).Distinct().Count() > 1, "the walk never moved");
    }

    [Fact]
    public void Accompaniment_ChordWithNoTones_IsSkippedRatherThanCrashing()
    {
        List<ChordAssignment> withEmpty =
        [
            new(Rational.Zero, Rational.Half, ChordAnalyzer.Identify("C4 E4 G4"), [60, 64, 67]),
            new(Rational.Half, Rational.Whole, default, []),
        ];

        var notes = AccompanimentGenerator.Generate(withEmpty, AccompanimentOptions.Default);

        Assert.All(notes, n => Assert.True(n.Offset < Rational.Half));
    }

    // ---------- figured bass: the Free style ----------

    [Fact]
    public void FreeStyle_RealizesEachSymbolIndependently()
    {
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Style = VoiceLeadingStyle.Free });

        var notes = realizer.Realize(
        [
            new FiguredBassSymbol { BassPitch = 48, Figures = [], Duration = Rational.Quarter, Time = Rational.Zero },
            new FiguredBassSymbol { BassPitch = 53, Figures = [6], Duration = Rational.Quarter, Time = Rational.Quarter },
        ]);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.InRange(n.Pitch, 0, 127));
    }

    [Fact]
    public void FreeStyle_UpperVoicesStillSoundAboveTheBass()
    {
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Style = VoiceLeadingStyle.Free });

        var notes = realizer.Realize(
            [new FiguredBassSymbol { BassPitch = 52, Figures = [6], Duration = Rational.Quarter, Time = Rational.Zero }]);

        var bass = notes[0].Pitch;
        Assert.All(notes.Skip(1), n => Assert.True(n.Pitch > bass,
            $"upper voice {n.Pitch} sounded below the bass {bass}"));
    }

    [Fact]
    public void FreeStyle_WithCrossingAllowed_StillProducesEveryVoice()
    {
        var realizer = new FiguredBassRealizer(new FiguredBassRealizerOptions
        {
            Style = VoiceLeadingStyle.Free,
            AllowVoiceCrossing = true,
        });

        var notes = realizer.Realize(
            [new FiguredBassSymbol { BassPitch = 48, Figures = [6, 4], Duration = Rational.Quarter, Time = Rational.Zero }]);

        Assert.Equal(3, notes.Length);
    }

    // ---------- figured bass: a bass outside the key ----------

    [Fact]
    public void ChromaticBass_IsRealizedByTheGenericIntervalFallback()
    {
        // F# is not in C major, so the diatonic degree lookup fails and the realizer falls
        // back to generic interval sizes. It must still produce a chord, not silence.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor });

        var notes = realizer.Realize(
            [new FiguredBassSymbol { BassPitch = 54, Figures = [3, 5], Duration = Rational.Quarter, Time = Rational.Zero }]);

        Assert.True(notes.Length >= 2, "a chromatic bass produced no upper voices");
        Assert.Equal(54, notes[0].Pitch);
        Assert.All(notes.Skip(1), n => Assert.True(n.Pitch > 54));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    public void ChromaticBass_EveryFigureSizeMapsToSomethingAboveTheBass(int figure)
    {
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = CMajor });

        var notes = realizer.Realize(
            [new FiguredBassSymbol { BassPitch = 54, Figures = [figure], Duration = Rational.Quarter, Time = Rational.Zero }]);

        Assert.All(notes.Skip(1), n => Assert.True(n.Pitch > 54, $"figure {figure} landed at or below the bass"));
    }

    [Fact]
    public void EmptySymbolList_RealizesToNothing()
    {
        Assert.Empty(new FiguredBassRealizer().Realize([]));
    }

    // ---------- SIMD reporting ----------

    [Fact]
    public void GetBest_PicksTheWidestAvailableSet()
    {
        var detected = SimdInfo.Detect();
        var best = SimdInfo.GetBest();

        // Whatever the host offers, GetBest must name something the host actually has, and it
        // must be the widest of them -- never a narrower set while a wider one is available.
        if (detected.HasFlag(SimdInstructionSet.Avx512F))
            Assert.Equal(SimdInstructionSet.Avx512F, best);
        else if (detected.HasFlag(SimdInstructionSet.Avx2))
            Assert.Equal(SimdInstructionSet.Avx2, best);
        else if (detected.HasFlag(SimdInstructionSet.Sse2))
            Assert.Equal(SimdInstructionSet.Sse2, best);
        else if (detected.HasFlag(SimdInstructionSet.Neon))
            Assert.Equal(SimdInstructionSet.Neon, best);
        else if (detected.HasFlag(SimdInstructionSet.WasmSimd))
            Assert.Equal(SimdInstructionSet.WasmSimd, best);
        else
            Assert.Equal(SimdInstructionSet.None, best);
    }

    [Fact]
    public void GetDescription_NamesEverySetTheHostReports()
    {
        var detected = SimdInfo.Detect();
        var text = SimdInfo.GetDescription();

        Assert.False(string.IsNullOrWhiteSpace(text));

        if (detected == SimdInstructionSet.None)
        {
            Assert.Contains("scalar", text, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Every detected set should appear somewhere in the description.
            foreach (var set in Enum.GetValues<SimdInstructionSet>())
            {
                if (set == SimdInstructionSet.None || !detected.HasFlag(set))
                    continue;

                Assert.Contains(set.ToString().Replace("F", "", StringComparison.Ordinal)[..3],
                    text.Replace("-", "", StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
