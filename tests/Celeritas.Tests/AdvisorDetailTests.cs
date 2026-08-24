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
    public void AQualityWithNoCharacterOfItsOwn_ReadsAsStable()
    {
        var report = ProgressionAdvisor.Analyze(["C", "C7b5", "F"]);

        Assert.Contains(report.Chords, c => c.Character == ChordCharacter.Stable);
    }

    [Fact]
    public void APlainTriadHasNothingSpecialToSay()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G"]);

        Assert.All(report.Chords, c => Assert.Null(c.SpecialNote));
    }
}
