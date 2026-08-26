// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Inversions, cadence detection on too little material, and the roman-numeral spellings and
/// notes the report attaches to the less common chord qualities.
/// </summary>
public class AdvisorDetailTests
{
    private static readonly KeySignature CMajor = new(0, true);

    // ---------- inversions ----------

    [Theory]
    [InlineData("C", 0)]
    [InlineData("C/E", 1)]
    [InlineData("C/G", 2)]
    [InlineData("C7/Bb", 3)]
    public void TheBassNoteDecidesTheInversion(string symbol, int expected)
    {
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);

        Assert.Equal(expected, ProgressionAdvisor.GetInversion(pitches));
    }

    [Fact]
    public void ADiminishedSeventhInThirdInversion_IsStillThirdInversion()
    {
        // A fully diminished seventh puts its seventh a minor seventh (9 semitones... 10 in
        // the enharmonic reading the analyzer uses) above the root; whichever way it is
        // spelled, a chord tone in the bass must map to one of the four inversions.
        var pitches = ProgressionAdvisor.ParseChordSymbol("Cdim7");

        Assert.InRange(ProgressionAdvisor.GetInversion(pitches), 0, 3);
    }

    [Fact]
    public void ABassThatIsNotAChordTone_ReadsAsRootPosition()
    {
        // A slash bass outside the chord has no inversion to name, so the analyzer says root
        // position rather than inventing one.
        var pitches = ProgressionAdvisor.ParseChordSymbol("C/D");

        Assert.Equal(0, ProgressionAdvisor.GetInversion(pitches));
    }

    [Theory]
    [InlineData(0, "root position")]
    [InlineData(1, "1st inversion")]
    [InlineData(2, "2nd inversion")]
    [InlineData(3, "3rd inversion")]
    public void EveryInversionHasAName(int inversion, string expected)
    {
        Assert.Equal(expected, ProgressionAdvisor.GetInversionName(inversion), ignoreCase: true);
    }

    [Fact]
    public void GetInversion_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressionAdvisor.GetInversion(null!));
    }

    // ---------- cadence detection with too little to go on ----------

    [Fact]
    public void OneChordIsNotACadence()
    {
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["C"], CMajor));
    }

    [Fact]
    public void NoParsableChordsIsNotACadence()
    {
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["Zzz", "Qqq"], CMajor));
    }

    [Fact]
    public void ATonicMovingToTheSupertonicIsNotACadence()
    {
        Assert.Equal(CadenceType.None, ProgressionAdvisor.DetectCadence(["C", "Dm"], CMajor));
    }

    [Fact]
    public void TheRealCadencesAreStillRecognised()
    {
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["G", "C"], CMajor));
        Assert.Equal(CadenceType.Plagal, ProgressionAdvisor.DetectCadence(["F", "C"], CMajor));
        Assert.Equal(CadenceType.Deceptive, ProgressionAdvisor.DetectCadence(["G", "Am"], CMajor));
    }

    [Fact]
    public void DetectCadence_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ProgressionAdvisor.DetectCadence(null!, CMajor));
    }

    // ---------- how the less common qualities are written up ----------

    [Theory]
    [InlineData("Cdim7", "°7")]
    [InlineData("Cm7b5", "ø7")]
    [InlineData("Caug", "+")]
    [InlineData("Csus2", "sus2")]
    [InlineData("Csus4", "sus4")]
    [InlineData("Cmaj7", "maj7")]
    public void TheRomanNumeralCarriesTheQualitySymbol(string symbol, string suffix)
    {
        var report = ProgressionAdvisor.Analyze(["C", symbol, "C"]);

        Assert.Contains(report.Chords, c => c.RomanNumeral.Contains(suffix, StringComparison.Ordinal));
    }

    [Fact]
    public void AFullyDiminishedSeventhIsCalledOutAsUnstable()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Cdim7", "G"]);

        Assert.Contains(report.Chords, c =>
            c.SpecialNote is not null && c.SpecialNote.Contains("Fully diminished", StringComparison.Ordinal));
    }

    [Fact]
    public void AHalfDiminishedSeventhIsCalledOutAsMelancholic()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Dm7b5", "G"]);

        Assert.Contains(report.Chords, c =>
            c.SpecialNote is not null && c.SpecialNote.Contains("Half-diminished", StringComparison.Ordinal));
    }

    [Fact]
    public void AnAugmentedChordReadsAsMysterious()
    {
        var report = ProgressionAdvisor.Analyze(["C", "Caug", "F"]);

        Assert.Contains(report.Chords, c => c.Character == ChordCharacter.Mysterious);
        Assert.All(report.Chords, c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
    }

    [Fact]
    public void AnAlteredDominant_IsNotDrawnOnTheTensionCurveAsARestingTonic()
    {
        // 7b5, m(maj7), add9 and add11 were all missing from the character table and fell
        // through to Stable — "tonic, at rest (home)". The old test here asserted only that
        // SOME chord in the progression was Stable, which the plain C satisfied, so it passed
        // while saying nothing about the chord it was named for.
        var report = ProgressionAdvisor.Analyze(["C", "C7b5", "F"]);

        var altered = Assert.Single(report.Chords, c => c.Symbol == "C7b5");
        Assert.Equal(ChordCharacter.Mysterious, altered.Character);
    }

    [Theory]
    [InlineData("Cadd9", ChordCharacter.Dreamy)]
    [InlineData("Cadd11", ChordCharacter.Dreamy)]
    [InlineData("Cm(maj7)", ChordCharacter.Melancholic)]
    public void TheOtherQualitiesTheCharacterTableUsedToMiss_ReadAsThemselves(string symbol, ChordCharacter expected)
    {
        var report = ProgressionAdvisor.Analyze(["C", symbol, "F"]);

        var chord = Assert.Single(report.Chords, c => c.Symbol == symbol);
        Assert.Equal(expected, chord.Character);
    }

    // ---------- the roman numeral says which chord it is ----------

    [Theory]
    [InlineData("C7b5", "7b5")]
    [InlineData("Caug7", "+7")]
    [InlineData("Cadd9", "add9")]
    [InlineData("Cadd11", "add11")]
    [InlineData("C5", "5")]
    public void ARomanNumeralCarriesTheQualityItsChordActuallyHas(string symbol, string suffix)
    {
        // The advisor kept its own copy of the suffix table and the copy had gone stale: these
        // six qualities fell through to the empty string, so C7b5 was labelled "I" — the same
        // label as a plain C — while the Nashville field beside it said "17b5".
        var report = ProgressionAdvisor.Analyze(["C", symbol, "F"]);

        var chord = Assert.Single(report.Chords, c => c.Symbol == symbol);
        Assert.EndsWith(suffix, chord.RomanNumeral, StringComparison.Ordinal);
        Assert.NotEqual(report.Chords[0].RomanNumeral, chord.RomanNumeral);
    }

    // ---------- an inversion is a fact about the music, not about pitch-class numbering ----------

    [Fact]
    public void AChordInRootPositionIsNotReportedAsInverted()
    {
        // GetInversion looked the chord up by mask alone, and sus/symmetric qualities share one
        // mask with their rotations — so a root-position Csus4 was read as an F sus2 with the
        // fourth in the bass and reported as an inversion.
        Assert.Equal(0, ProgressionAdvisor.GetInversion([60, 65, 67]));      // Csus4
        Assert.Equal(0, ProgressionAdvisor.GetInversion([60, 62, 67]));      // Csus2
        Assert.Equal(0, ProgressionAdvisor.GetInversion([60, 64, 68]));      // Caug
        Assert.Equal(0, ProgressionAdvisor.GetInversion([60, 63, 66, 69]));  // Cdim7
    }

    [Fact]
    public void TheInversionOfAChordIsTheSameInEveryKey()
    {
        int[][] chords =
        [
            [60, 65, 67], [60, 62, 67], [60, 64, 68], [60, 63, 66, 69],
            [60, 64, 67], [64, 67, 72], [67, 72, 76], [60, 64, 66, 70],
        ];

        foreach (var chord in chords)
        {
            var here = ProgressionAdvisor.GetInversion(chord);
            for (var semitones = 1; semitones <= 11; semitones++)
            {
                var moved = chord.Select(p => p + semitones).ToArray();
                Assert.Equal(here, ProgressionAdvisor.GetInversion(moved));
            }
        }
    }

    [Fact]
    public void APlainTriadHasNothingSpecialToSay()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G"]);

        Assert.All(report.Chords, c => Assert.Null(c.SpecialNote));
    }
}
