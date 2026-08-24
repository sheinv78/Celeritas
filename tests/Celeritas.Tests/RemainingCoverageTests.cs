// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Closes the last of the thinly-covered types: interval naming, the circle of fifths,
/// directive rendering, chord character and the progression report. Each was between 44% and
/// 66% covered, mostly on paths that only render or enumerate — the kind that fail quietly.
/// </summary>
public class RemainingCoverageTests
{
    // ---------- ChromaticInterval ----------

    [Theory]
    [InlineData(0, "Unison")]
    [InlineData(1, "m2")]
    [InlineData(2, "M2")]
    [InlineData(3, "m3")]
    [InlineData(4, "M3")]
    [InlineData(5, "P4")]
    [InlineData(6, "TT")]
    [InlineData(7, "P5")]
    [InlineData(8, "m6")]
    [InlineData(9, "M6")]
    [InlineData(10, "m7")]
    [InlineData(11, "M7")]
    [InlineData(12, "P8")]
    public void ChromaticInterval_NamesEverySimpleInterval(int semitones, string expected)
    {
        Assert.Equal(expected, new ChromaticInterval(semitones).SimpleName);
    }

    [Theory]
    [InlineData(12, 12)]    // an octave stays an octave; only larger intervals reduce
    [InlineData(14, 2)]     // a ninth to a second
    [InlineData(19, 7)]     // a twelfth to a fifth
    [InlineData(24, 12)]    // two octaves reduce to one, not to a unison
    [InlineData(-5, 5)]     // size ignores direction (ClassSemitones is the folding one)
    public void ChromaticInterval_ReducesCompoundIntervals(int semitones, int expectedSimple)
    {
        Assert.Equal(expectedSimple, new ChromaticInterval(semitones).SimpleSemitones);
    }

    [Theory]
    [InlineData(7, 1)]
    [InlineData(-7, -1)]
    [InlineData(0, 0)]
    public void ChromaticInterval_ReportsDirection(int semitones, int expected)
    {
        Assert.Equal(expected, new ChromaticInterval(semitones).Direction);
    }

    [Theory]
    [InlineData(-7, 7)]
    [InlineData(3, 3)]
    public void ChromaticInterval_AbsSemitones_IgnoresDirection(int semitones, int expected)
    {
        Assert.Equal(expected, new ChromaticInterval(semitones).AbsSemitones);
    }

    [Theory]
    [InlineData(-1, 11)]
    [InlineData(-13, 11)]
    [InlineData(13, 1)]
    public void ChromaticInterval_ClassSemitones_FoldsIntoZeroToEleven(int semitones, int expected)
    {
        Assert.Equal(expected, new ChromaticInterval(semitones).ClassSemitones);
    }

    [Fact]
    public void ChromaticInterval_Negation_FlipsDirectionButNotSize()
    {
        var fifth = ChromaticInterval.PerfectFifth;

        var down = -fifth;

        Assert.Equal(-fifth.Semitones, down.Semitones);
        Assert.Equal(fifth.AbsSemitones, down.AbsSemitones);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 3)]
    [InlineData(6, 4)]      // the tritone is reported as its closest generic class
    [InlineData(7, 5)]
    [InlineData(11, 7)]
    [InlineData(12, 8)]
    public void ChromaticInterval_GenericNumber_IsTheDiatonicOrdinal(int semitones, int expected)
    {
        Assert.Equal(expected, new ChromaticInterval(semitones).GenericNumber);
    }

    [Fact]
    public void ChromaticInterval_ToString_IsItsSimpleName()
    {
        Assert.Equal("P5", ChromaticInterval.PerfectFifth.ToString());
    }

    // ---------- CircleOfFifths ----------

    [Fact]
    public void Circle_Clockwise_WalksUpInFifths()
    {
        var circle = CircleOfFifths.PitchClasses(new PitchClass(0));

        Assert.Equal(12, circle.Length);
        Assert.Equal(new PitchClass(0), circle[0]);
        Assert.Equal(new PitchClass(7), circle[1]);   // C -> G
        Assert.Equal(new PitchClass(2), circle[2]);   // G -> D
    }

    [Fact]
    public void Circle_CounterClockwise_WalksDownInFifths()
    {
        var circle = CircleOfFifths.PitchClasses(new PitchClass(0), CircleDirection.CounterClockwise);

        Assert.Equal(new PitchClass(0), circle[0]);
        Assert.Equal(new PitchClass(5), circle[1]);   // C -> F
    }

    [Fact]
    public void Circle_ReturnsToItsStart_AfterTwelveSteps()
    {
        var pc = new PitchClass(3);
        var walked = pc;
        for (var i = 0; i < 12; i++) walked = CircleOfFifths.NextFifth(walked);

        Assert.Equal(pc, walked);
    }

    [Fact]
    public void Circle_NextAndPrev_AreInverses()
    {
        var pc = new PitchClass(4);

        Assert.Equal(pc, CircleOfFifths.PrevFifth(CircleOfFifths.NextFifth(pc)));
        Assert.Equal(pc, CircleOfFifths.PrevFourth(CircleOfFifths.NextFourth(pc)));
    }

    [Fact]
    public void Circle_FourthIsTheInverseOfFifth()
    {
        var pc = new PitchClass(9);

        Assert.Equal(CircleOfFifths.NextFourth(pc), CircleOfFifths.PrevFifth(pc));
    }

    [Fact]
    public void Circle_MajorKeys_AreTwelveDistinctMajorKeys()
    {
        var keys = CircleOfFifths.MajorKeys(new PitchClass(0));

        Assert.Equal(12, keys.Length);
        Assert.All(keys, k => Assert.True(k.IsMajor));
        Assert.Equal(12, keys.Select(k => k.Root).Distinct().Count());
    }

    [Fact]
    public void Circle_MinorKeys_AreTwelveDistinctMinorKeys()
    {
        var keys = CircleOfFifths.MinorKeys(new PitchClass(9));

        Assert.Equal(12, keys.Length);
        Assert.All(keys, k => Assert.False(k.IsMajor));
    }

    [Fact]
    public void Circle_ChordSymbols_HonourTheAccidentalPreference()
    {
        var sharps = CircleOfFifths.MajorChordSymbols(new PitchClass(0), preferSharps: true);
        var flats = CircleOfFifths.MajorChordSymbols(new PitchClass(0), preferSharps: false);

        Assert.Equal(12, sharps.Length);
        Assert.Contains(sharps, s => s.Contains('#', StringComparison.Ordinal));
        Assert.Contains(flats, s => s.Contains('b', StringComparison.Ordinal));
    }

    [Fact]
    public void Circle_MinorChordSymbols_AreAllMinor()
    {
        var symbols = CircleOfFifths.MinorChordSymbols(new PitchClass(9));

        Assert.Equal(12, symbols.Length);
        Assert.All(symbols, s => Assert.EndsWith("m", s, StringComparison.Ordinal));
    }

    [Fact]
    public void Circle_MajorWithRelativeMinors_PairsEachKeyWithItsRelative()
    {
        var pairs = CircleOfFifths.MajorWithRelativeMinors(new PitchClass(0));

        Assert.Equal(12, pairs.Length);
        // C major's relative minor is A minor.
        Assert.Equal("C", pairs[0].Major);
        Assert.Equal("Am", pairs[0].RelativeMinor);
    }

    // ---------- NotationDirective rendering ----------

    [Fact]
    public void TempoBpmDirective_RendersItsBpm()
    {
        var text = new TempoBpmDirective { Time = Rational.Zero, Bpm = 120 }.ToString();

        Assert.Contains("120", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TempoBpmDirective_WithARamp_RendersTheTarget()
    {
        var text = new TempoBpmDirective
        {
            Time = Rational.Zero,
            Bpm = 120,
            TargetBpm = 140,
            RampDuration = Rational.Half
        }.ToString();

        Assert.Contains("120", text, StringComparison.Ordinal);
        Assert.Contains("140", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TempoCharacterDirective_RendersItsCharacter()
    {
        var text = new TempoCharacterDirective { Time = Rational.Zero, Character = "allegro" }.ToString();

        Assert.Contains("allegro", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionDirective_RendersItsLabel()
    {
        var text = new SectionDirective { Time = Rational.Quarter, Label = "chorus" }.ToString();

        Assert.Contains("chorus", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PartDirective_RendersItsName()
    {
        var text = new PartDirective { Time = Rational.Zero, Name = "piano" }.ToString();

        Assert.Contains("piano", text, StringComparison.Ordinal);
    }

    // ---------- ChordCharacterClassifier ----------

    [Theory]
    [InlineData("C", ChordCharacter.Bright)]
    [InlineData("Cm", ChordCharacter.Melancholic)]
    [InlineData("Cm7", ChordCharacter.Warm)]
    [InlineData("C7", ChordCharacter.Tense)]
    [InlineData("Cmaj7", ChordCharacter.Dreamy)]
    [InlineData("Cdim", ChordCharacter.Dark)]
    [InlineData("Caug", ChordCharacter.Mysterious)]
    [InlineData("Csus4", ChordCharacter.Suspended)]
    [InlineData("C5", ChordCharacter.Powerful)]
    public void Classify_MapsEachQualityToItsCharacter(string symbol, ChordCharacter expected)
    {
        Assert.Equal(expected, ChordCharacterClassifier.Classify(symbol).Character);
    }

    [Fact]
    public void Classify_StabilityAndStatedRangesAreCoherent()
    {
        foreach (var symbol in new[] { "C", "Cm", "C7", "Cdim", "Caug", "Csus4", "Cmaj7" })
        {
            var c = ChordCharacterClassifier.Classify(symbol);
            Assert.InRange(c.Stability, 0f, 1f);
            Assert.InRange(c.Brightness, 0f, 1f);
        }
    }

    [Fact]
    public void Classify_ConsonantChordIsMoreStableThanADiminishedOne()
    {
        var major = ChordCharacterClassifier.Classify("C");
        var diminished = ChordCharacterClassifier.Classify("Cdim");

        Assert.True(major.Stability > diminished.Stability);
    }

    // ---------- ProgressionReport ----------

    [Fact]
    public void ProgressionReport_Generate_FillsTheReport()
    {
        var report = ProgressionReport.Generate(["C", "Am", "F", "G"]);

        Assert.Equal(4, report.Chords.Count);
        Assert.False(string.IsNullOrWhiteSpace(report.Pattern));
        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        Assert.NotEmpty(report.TensionCurve ?? []);
    }

    [Fact]
    public void ProgressionReport_Generate_RecordsSymbolsItCouldNotParse()
    {
        var report = ProgressionReport.Generate(["C", "Zzz", "G"]);

        Assert.Contains(report.SkippedSymbols, s => s.Symbol == "Zzz");
        // Positions refer to the parsed sequence, and the skipped entry keeps the input index.
        Assert.Equal(1, report.SkippedSymbols.Single().Index);
    }

    [Fact]
    public void ProgressionReport_Generate_EmptyProgression_IsAnEmptyReport()
    {
        var report = ProgressionReport.Generate([]);

        Assert.Empty(report.Chords);
    }
}
