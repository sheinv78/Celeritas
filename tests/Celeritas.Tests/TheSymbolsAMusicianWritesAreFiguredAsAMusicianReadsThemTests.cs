// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Chord symbols that <see cref="NothingWrittenInASymbolIsDroppedTests"/> and
/// <see cref="AnExtendedChordKeepsTheNumeralOfItsCoreTests"/> do not name, on other roots,
/// with Unicode accidentals, in slash and polychords, and through the progression report in
/// major and minor keys. Every expected value is what a musician spells or names; where the
/// library's convention decides (an alteration displaces only the perfect fifth; the extension
/// chain's natural gives way but an explicit add is kept; a polychord names a pitch once and a
/// pitch class as often as its layers do), the row says so.
/// </summary>
/// <remarks>
/// The reviewer's held-out symbols for that fix, folded into the suite. Its
/// <see cref="ASecondaryDominantsTargetDegreeIsFiguredLikeItsPatternEntry"/> found that
/// <c>SecondaryDominantInfo.TargetDegree</c> and <c>ModulationInfo.PivotAnalysis</c> still wrote
/// the core's bare numeral ("ii7" for Dm9) while <c>Pattern</c> said "ii9" — two fields of one
/// report disagreeing about one chord — and those two writers now go through the same figures.
/// </remarks>
public class TheSymbolsAMusicianWritesAreFiguredAsAMusicianReadsThemTests
{
    private const string Dominant = "Dominant (tension/pull to resolve)";
    private const string Subdominant = "Subdominant (motion/tension building)";
    private const string Tonic = "Tonic (home/stable)";
    private const string Chromatic = "Chromatic (outside the key)";

    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    private static ChordAnalysisDetail In(string tonic, string symbol) =>
        ProgressionAdvisor.Analyze([tonic, symbol, tonic]).Chords[1];

    // ---------------------------------------------------------------- (a) power chords

    [Theory]
    [InlineData("D5(b9)", new[] { 62, 69, 75 })]              // D A Eb
    [InlineData("E♭5(♯9)", new[] { 63, 70, 78 })]             // Eb Bb F#
    [InlineData("F#5(b13)", new[] { 66, 73, 86 })]            // F# C# D
    [InlineData("B5(b9,#9)", new[] { 71, 78, 84, 86 })]       // B F# C D
    [InlineData("Bb5add9(#11)", new[] { 70, 77, 84, 88 })]    // Bb F C E
    [InlineData("A♭5(♭9)", new[] { 68, 75, 81 })]             // Ab Eb Bbb
    [InlineData("C5(#5)(b9)", new[] { 60, 68, 73 })]          // the fifth gives way, the b9 is heard
    [InlineData("G5add2", new[] { 67, 69, 74 })]
    [InlineData("Db5(b5)add9", new[] { 61, 67, 75 })]
    [InlineData("Ab5(b9)/C", new[] { 48, 68, 75, 81 })]       // slash bass below, nothing dropped
    public void APowerChordOnAnyRootTakesItsAlterationAndItsAdd(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    // ---------------------------------------------------------------- (b) fifths

    [Theory]
    // a redundant b5 on a diminished chord is the chord itself
    [InlineData("Ebdim7(b5)", new[] { 63, 66, 69, 72 })]
    [InlineData("Dhalfdim7(b5)", new[] { 62, 65, 68, 72 })]
    [InlineData("Gbø7(b5)", new[] { 66, 69, 72, 76 })]
    [InlineData("Cdim7b5", new[] { 60, 63, 66, 69 })]         // unparenthesised
    [InlineData("Dm(b5)", new[] { 62, 65, 68 })]               // m(b5) is dim
    [InlineData("Bdim7(b5)/D", new[] { 50, 71, 77, 80 })]     // slash keeps the diminished seventh
    // a second fifth on a diminished or augmented chord is a second fifth, as on C7(b5,#5)
    [InlineData("D°7(#5)", new[] { 62, 65, 68, 70, 71 })]
    [InlineData("B♭dim7(♯5)", new[] { 70, 73, 76, 78, 79 })]
    [InlineData("Gaug7(♭5)", new[] { 67, 71, 73, 75, 77 })]
    [InlineData("A+7(♭5)", new[] { 69, 73, 75, 77, 79 })]
    [InlineData("Bbdim(#5)", new[] { 70, 73, 76, 78 })]
    [InlineData("F#ø7(#5)", new[] { 66, 69, 72, 74, 76 })]
    [InlineData("Cdim7(b5,#5)", new[] { 60, 63, 66, 68, 69 })]
    public void OnlyThePerfectFifthGivesWayOnAnyRoot(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void TheSpellingsOfOneChordAgree()
    {
        Assert.Equal(Parse("Ebdim7"), Parse("Ebdim7(b5)"));
        Assert.Equal(Parse("Dø7"), Parse("Dhalfdim7(b5)"));
        Assert.Equal(Parse("Dø7"), Parse("Dm7(♭5)"));
        Assert.Equal(Parse("G7(b5,#5)"), Parse("Gaug7(♭5)"));
        Assert.Equal(Parse("G7(b5,#5)"), Parse("G+7(b5)"));
        Assert.NotEqual(Parse("Ebdim7"), Parse("Ebø7"));
    }

    // ---------------------------------------------------------------- (c) adds beside alterations

    [Theory]
    [InlineData("Dm7(b9)add9", new[] { 62, 65, 69, 72, 75, 76 })]
    [InlineData("E♭7(♭9)add9", new[] { 63, 67, 70, 73, 76, 77 })]
    [InlineData("G7(♯11)add11", new[] { 67, 71, 74, 77, 84, 85 })]
    [InlineData("F13(♭13)add13", new[] { 65, 69, 72, 75, 79, 82, 85, 86 })]
    [InlineData("Cmaj7(#11)add11", new[] { 60, 64, 67, 71, 77, 78 })]
    [InlineData("C7(#9)add9", new[] { 60, 64, 67, 70, 74, 75 })]
    [InlineData("Cadd11(#11)", new[] { 60, 64, 67, 77, 78 })]
    [InlineData("C7(b9,#9)add9", new[] { 60, 64, 67, 70, 73, 74, 75 })]
    [InlineData("C13(b13)add13", new[] { 60, 64, 67, 70, 74, 77, 80, 81 })]
    [InlineData("Cadd9(#9)", new[] { 60, 64, 67, 74, 75 })]
    // the extension chain's natural gives way, the explicit add puts it back
    [InlineData("C9(b9)add9", new[] { 60, 64, 67, 70, 73, 74 })]
    // the kept convention on other roots
    [InlineData("F9(b9)", new[] { 65, 69, 72, 75, 78 })]
    [InlineData("Bb13(b13)", new[] { 70, 74, 77, 80, 84, 87, 90 })]
    public void AnAddSurvivesAnAlterationOfItsDegreeOnAnyRoot(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    // ---------------------------------------------------------------- (d) polychords and slashes

    [Theory]
    [InlineData("C13|A", new[] { 60, 64, 67, 70, 74, 77, 81, 85, 88 })]          // A5 once
    [InlineData("D7(b9)|Eb", new[] { 62, 66, 69, 72, 75, 79, 82 })]              // Eb5 once
    [InlineData("Cmaj9|D", new[] { 60, 64, 67, 71, 74, 78, 81 })]
    [InlineData("C9|D|E", new[] { 60, 64, 67, 70, 74, 78, 81, 88, 92, 95 })]
    [InlineData("Eb9|F", new[] { 63, 67, 70, 73, 77, 81, 84 })]
    [InlineData("A9|B", new[] { 69, 73, 76, 79, 83, 87, 90 })]
    [InlineData("C7(b9,#9)|D♭", new[] { 60, 64, 67, 70, 73, 75, 77, 80 })]       // Unicode flat in the upper layer
    // a pitch class two layers share in different octaves is two pitches
    [InlineData("C|C", new[] { 60, 64, 67, 72, 76, 79 })]
    [InlineData("Cmaj7|Em", new[] { 60, 64, 67, 71, 76, 79, 83 })]
    // a slash chord inside a polychord
    [InlineData("C/E|G", new[] { 52, 60, 67, 79, 83, 86 })]
    [InlineData("C9|D/F#", new[] { 60, 64, 67, 70, 74, 54, 81 })]
    public void APolychordNamesEachPitchOnceAndEachOctaveSeparately(string symbol, int[] expected)
    {
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);

        Assert.Equal(expected, pitches);
        Assert.Equal(pitches.Length, pitches.Distinct().Count());
    }

    [Fact]
    public void AFiveLayerStackDropsExactlyItsSharedPitches()
    {
        // C G D A E, each a triad an octave above the last: D5 is the fifth of G and the root
        // of D, E7 the fifth of A and the root of E. Thirteen pitches, not fifteen.
        var pitches = ProgressionAdvisor.ParseChordSymbol("C|G|D|A|E");

        Assert.Equal([60, 64, 67, 79, 83, 86, 90, 93, 105, 109, 112, 116, 119], pitches);
    }

    // ---------------------------------------------------------------- (f) lowercase roots

    [Theory]
    [InlineData("d7")]
    [InlineData("bb")]
    [InlineData("h7")]
    [InlineData("B/c")]
    [InlineData("Cm/e")]
    [InlineData("Cmaj7/b")]
    [InlineData("C|g")]
    public void ALowercaseRootOrBassIsRefusedInEveryPosition(string symbol)
    {
        Assert.False(ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches));
        Assert.Empty(pitches);
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol(symbol));
    }

    // ---------------------------------------------------------------- (e) the report, major keys

    [Theory]
    [InlineData("F", "C7b9", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("F", "C7(b9,b13)", "V7(b9,b13)", "57(b9,b13)", Dominant, false)]
    [InlineData("F", "C7(#9)", "V7(#9)", "57(#9)", Dominant, false)]
    [InlineData("F", "C9sus4", "V9sus4", "59sus4", Dominant, false)]
    [InlineData("F", "Gm9", "ii9", "2m9", Subdominant, false)]
    [InlineData("F", "Gm7b5", "iiø7", "2m7b5", Subdominant, true)]
    [InlineData("F", "Eø9", "viiø9", "7m9b5", Dominant, false)]
    [InlineData("F", "Bbm9", "iv9", "4m9", Subdominant, true)]
    [InlineData("F", "Bbmaj7(#11)", "IVmaj7(#11)", "4maj7(#11)", Subdominant, false)]
    [InlineData("F", "F6/9", "I6(add9)", "16(add9)", Tonic, false)]
    [InlineData("Bb", "F13(b9)", "V13(b9)", "513(b9)", Dominant, false)]
    [InlineData("Bb", "F7(#9,b13)", "V7(#9,b13)", "57(#9,b13)", Dominant, false)]
    [InlineData("Bb", "F9(#11)", "V9(#11)", "59(#11)", Dominant, false)]
    [InlineData("Bb", "Cm11", "ii11", "2m11", Subdominant, false)]
    [InlineData("Eb", "Bb7(b9,#9,#11,b13)", "V7(b9,#9,#11,b13)", "57(b9,#9,#11,b13)", Dominant, false)]
    [InlineData("Eb", "Bb13#11", "V13(#11)", "513(#11)", Dominant, false)]
    [InlineData("D", "A7♭9", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("D", "A9(♯11)", "V9(#11)", "59(#11)", Dominant, false)]
    [InlineData("D", "C#ø9", "viiø9", "7m9b5", Dominant, false)]
    [InlineData("D", "Dmaj13", "Imaj13", "1maj13", Tonic, false)]
    [InlineData("D", "Bm11", "vi11", "6m11", Tonic, false)]
    [InlineData("G", "D7b9", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("G", "Dm9", "v9", "5m9", Dominant, true)]
    [InlineData("G", "D7(b5,#5)", "V7b5(#5)", "57b5(#5)", Dominant, false)]
    [InlineData("G", "Dsus4add9", "Vsus4(add9)", "5sus4(add9)", Dominant, false)]
    public void InAMajorKeyTheNumeralIsTheCoresWithItsFigures(string tonic, string symbol, string roman, string nashville, string function, bool borrowed)
    {
        var detail = In(tonic, symbol);

        Assert.Equal(roman, detail.RomanNumeral);
        Assert.Equal(nashville, detail.Nashville);
        Assert.Equal(function, detail.Function);
        Assert.Equal(borrowed, detail.IsBorrowed);
    }

    [Theory]
    // the tritone substitute and the other chromatic dominants stay chromatic, extended or not
    [InlineData("F", "Gb7")]
    [InlineData("F", "Gb7b9")]
    [InlineData("F", "Db7")]
    [InlineData("Bb", "E7b9")]
    [InlineData("Eb", "A7")]
    [InlineData("D", "Eb9")]
    [InlineData("G", "Ab7")]
    [InlineData("Am", "Bb7")]
    [InlineData("Am", "Bb9")]
    [InlineData("Dm", "Eb7")]
    [InlineData("Gm", "Ab7")]
    [InlineData("Cm", "Db7b9")]
    public void ADominantOnARootTheKeyDoesNotOwnIsStillChromatic(string tonic, string symbol)
    {
        var detail = In(tonic, symbol);

        Assert.Equal("?", detail.RomanNumeral);
        Assert.Equal("?", detail.Nashville);
        Assert.Equal(Chromatic, detail.Function);
        Assert.True(detail.IsBorrowed);
    }

    // ---------------------------------------------------------------- (e) the report, minor keys

    [Theory]
    [InlineData("Am", "E7b9", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("Am", "E7(b9,b13)", "V7(b9,b13)", "57(b9,b13)", Dominant, false)]
    [InlineData("Am", "E7alt", "V+7(b9)", "5+7(b9)", Dominant, false)]
    [InlineData("Am", "E7(#9)", "V7(#9)", "57(#9)", Dominant, false)]
    [InlineData("Am", "E9", "V9", "59", Dominant, false)]
    [InlineData("Am", "E9sus4", "V9sus4", "59sus4", Dominant, false)]
    [InlineData("Am", "E7(b9)/G#", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("Am", "E7b9/D", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("Am", "Bø7", "iiø7", "2m7b5", Subdominant, false)]
    [InlineData("Am", "Bø9", "iiø9", "2m9b5", Subdominant, false)]
    [InlineData("Am", "Dm7(b5)", "ivø7", "4m7b5", Subdominant, false)]
    [InlineData("Am", "Am9", "i9", "1m9", Tonic, false)]
    [InlineData("Am", "Am6/9", "i6(add9)", "1m6(add9)", Tonic, false)]
    [InlineData("Am", "Am(maj9)", "imaj9", "1m(maj9)", Tonic, false)]
    [InlineData("Am", "Fmaj9", "VImaj9", "6maj9", Tonic, false)]
    [InlineData("Am", "G9", "VII9", "79", Dominant, false)]
    [InlineData("Am", "Em9", "v9", "5m9", Dominant, false)]
    [InlineData("Dm", "A7(♭9,♭13)", "V7(b9,b13)", "57(b9,b13)", Dominant, false)]
    [InlineData("Dm", "A+7(b9)", "V+7(b9)", "5+7(b9)", Dominant, false)]
    [InlineData("Dm", "Eø9", "iiø9", "2m9b5", Subdominant, false)]
    [InlineData("Dm", "Gm9", "iv9", "4m9", Subdominant, false)]
    [InlineData("Gm", "D7(b9,#9)", "V7(b9,#9)", "57(b9,#9)", Dominant, false)]
    [InlineData("Gm", "Aø7", "iiø7", "2m7b5", Subdominant, false)]
    [InlineData("Gm", "D5(b9)", "V5(b9)", "55(b9)", Dominant, false)]
    [InlineData("Cm", "G7#9", "V7(#9)", "57(#9)", Dominant, false)]
    [InlineData("Cm", "Dø9", "iiø9", "2m9b5", Subdominant, false)]
    [InlineData("Cm", "Fm9", "iv9", "4m9", Subdominant, false)]
    [InlineData("Cm", "Fm6/9", "iv6(add9)", "4m6(add9)", Subdominant, false)]
    [InlineData("Cm", "Cm11", "i11", "1m11", Tonic, false)]
    [InlineData("Cm", "Ab7", "VI7", "67", Tonic, false)]
    public void InAMinorKeyTheDominantAndItsNeighboursKeepTheirFunction(string tonic, string symbol, string roman, string nashville, string function, bool borrowed)
    {
        var detail = In(tonic, symbol);

        Assert.Equal(roman, detail.RomanNumeral);
        Assert.Equal(nashville, detail.Nashville);
        Assert.Equal(function, detail.Function);
        Assert.Equal(borrowed, detail.IsBorrowed);
    }

    // ---------------------------------------------------------------- whole progressions

    [Theory]
    [InlineData(new[] { "Am", "Dm9", "E7b9", "Am" }, 9, false, "i - iv9 - V7(b9) - i")]
    [InlineData(new[] { "Am", "Bø7", "E7(b9,b13)", "Am" }, 9, false, "i - iiø7 - V7(b9,b13) - i")]
    [InlineData(new[] { "Am", "Bø9", "E7alt", "Am" }, 9, false, "i - iiø9 - V+7(b9) - i")]
    [InlineData(new[] { "Em", "Am9", "B7b9", "Em" }, 4, false, "i - iv9 - V7(b9) - i")]
    [InlineData(new[] { "Cm", "Ab7", "Dø7", "G7b9", "Cm" }, 0, false, "i - VI7 - iiø7 - V7(b9) - i")]
    [InlineData(new[] { "F", "Gm9", "C7b9", "Fmaj9" }, 5, true, "I - ii9 - V7(b9) - Imaj9")]
    [InlineData(new[] { "Am9", "D7b9", "Gmaj9" }, 7, true, "ii9 - V7(b9) - Imaj9")]
    [InlineData(new[] { "Dm7", "G7b9", "Cmaj7" }, 0, true, "ii7 - V7(b9) - Imaj7")]
    [InlineData(new[] { "Dm7", "Db7b9", "Cmaj7" }, 0, true, "ii7 - ? - Imaj7")]
    [InlineData(new[] { "Dm7", "Db9", "Cmaj7" }, 0, true, "ii7 - ? - Imaj7")]
    [InlineData(new[] { "C", "D7b9", "G7b9", "C" }, 0, true, "I - II7(b9) - V7(b9) - I")]
    [InlineData(new[] { "C9", "F9", "C9", "G9" }, 0, true, "I9 - IV9 - I9 - V9")]
    [InlineData(new[] { "G9sus4", "G7b9", "C" }, 0, true, "V9sus4 - V7(b9) - I")]
    [InlineData(new[] { "Ebmaj9", "Cm9", "Fm9", "Bb13(b9)", "Eb6/9" }, 3, true, "Imaj9 - vi9 - ii9 - V13(b9) - I6(add9)")]
    public void AProgressionOfExtendedChordsReadsAsItsPlainTwinDoes(string[] progression, int tonic, bool major, string pattern)
    {
        var report = ProgressionAdvisor.Analyze(progression);

        Assert.Equal(new KeySignature((byte)tonic, major), report.Key);
        Assert.Equal(pattern, report.Pattern);
    }

    [Fact]
    public void AMinorCadenceWithAFlatNinthIsAuthenticAndNotModalMixture()
    {
        foreach (var progression in new[] { new[] { "Am", "Dm9", "E7b9", "Am" }, new[] { "Cm", "Dø7", "G7b9", "Cm" }, new[] { "Dm7", "G7b9", "Cmaj7" }, new[] { "C9", "F9", "C9", "G9" } })
        {
            var report = ProgressionAdvisor.Analyze(progression);

            Assert.False(report.HasModalMixture, string.Join(' ', progression));
            Assert.All(report.Chords, c => Assert.False(c.IsBorrowed, $"{c.Symbol} in {string.Join(' ', progression)}"));
            Assert.Contains(report.Cadences, c => c.Type is CadenceType.Authentic or CadenceType.Half);
        }
    }

    [Fact]
    public void ASecondaryDominantWithAFlatNinthIsHeard()
    {
        var report = ProgressionAdvisor.Analyze(["C", "E7b9", "Am7", "D7", "G7", "C"]);

        Assert.Equal("I - III7(b9) - vi7 - II7 - V7 - I", report.Pattern);
        Assert.Contains(report.SecondaryDominants, s => s.Chord == "E7b9" && s.Target == "Am7");
        Assert.Contains(report.SecondaryDominants, s => s.Chord == "D7" && s.Target == "G7");
    }

    [Fact]
    public void ASecondaryDominantsTargetDegreeIsFiguredLikeItsPatternEntry()
    {
        // The report names Dm9 "ii9" in Pattern; the secondary dominant that aims at it must
        // call its target the same thing, not the bare "ii7" of its core.
        var report = ProgressionAdvisor.Analyze(["C", "A7", "Dm9", "G7", "C"]);
        var applied = Assert.Single(report.SecondaryDominants);

        Assert.Equal("I - VI7 - ii9 - V7 - I", report.Pattern);
        Assert.Equal("Dm9", applied.Target);
        Assert.Equal(report.Chords[2].RomanNumeral, applied.TargetDegree);
        Assert.Equal("ii9", applied.TargetDegree);

        // And a pivot chord's analysis writes the figured numerals it has in both keys.
        var pivot = ProgressionAdvisor.Analyze(["C", "F", "G7", "C", "Am9", "D9", "G", "D9", "G"])
            .Modulations.Single(m => m.Type == ModulationType.PivotChord);

        Assert.Equal("Am9", pivot.PivotChord);
        Assert.Equal("vi9 in C Major = ii9 in G Major", pivot.PivotAnalysis);
    }

    [Fact]
    public void TheMinorTwoFiveOneWithAFlatNinthReadsTheSameInEveryKey()
    {
        string[] names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
        var readings = new HashSet<string>();

        for (var tonic = 0; tonic < 12; tonic++)
        {
            string[] progression =
            [
                names[tonic] + "m",
                names[(tonic + 2) % 12] + "ø7",
                names[(tonic + 7) % 12] + "7b9",
                names[tonic] + "m",
            ];
            var report = ProgressionAdvisor.Analyze(progression);

            Assert.Equal(new KeySignature((byte)tonic, false), report.Key);
            Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic);
            readings.Add(report.Pattern);
        }

        Assert.Equal(["i - iiø7 - V7(b9) - i"], readings);
    }

    [Fact]
    public void TheUpgradingGuidesSnippetPrintsWhatItSays()
    {
        var jazz = ProgressionAdvisor.Analyze(["Dm9", "G13(b9)", "Cmaj9"]);
        var g = jazz.Chords[1];

        Assert.Equal("ii9 - V13(b9) - Imaj9", jazz.Pattern);
        Assert.Equal("G13(b9) V13(b9) 513(b9) Dominant (tension/pull to resolve)", $"{g.Symbol} {g.RomanNumeral} {g.Nashville} {g.Function}");
    }

    [Fact]
    public void TheOtherPublicRoadsHearTheCore()
    {
        Assert.Equal(ChordCharacter.Tense, ChordCharacterClassifier.Classify("F13(b9)").Character);
        Assert.Equal(ChordCharacter.Warm, ChordCharacterClassifier.Classify("Gm9").Character);
        Assert.Equal(ChordCharacter.Dreamy, ChordCharacterClassifier.Classify("Fmaj9").Character);
        Assert.Equal(ChordCharacter.Powerful, ChordCharacterClassifier.Classify("D5(b9)").Character);
        Assert.Equal(ChordCharacterClassifier.Classify("Bm7b5").Character, ChordCharacterClassifier.Classify("Bø9").Character);
        Assert.Equal(ChordCharacter.Dark, ChordCharacterClassifier.Classify("Bø9").Character);

        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["Cm", "G7b9", "Cm"]));
        Assert.Equal(CadenceType.Authentic, ProgressionAdvisor.DetectCadence(["F", "G7alt", "C"]));
        Assert.Equal(CadenceType.Deceptive, ProgressionAdvisor.DetectCadence(["C", "G13", "Am"]));
    }
}
