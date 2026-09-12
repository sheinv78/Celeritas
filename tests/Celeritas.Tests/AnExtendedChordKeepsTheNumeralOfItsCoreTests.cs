// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The progression report names a chord by its root and a <see cref="ChordQuality"/>, and the
/// library has no quality for a ninth, eleventh or thirteenth chord, natural or altered. So
/// every extended dominant — G7b9 in C, the commonest dominant there is in a minor key and an
/// ordinary one in major — had no quality, no roman numeral ("?"), and the function "Chromatic
/// (outside the key)", while the plain G7 beside it was V7. A musician reads G7b9 as V7 with a
/// flat ninth: the numeral of the seventh chord at the chord's core, the alteration figured after
/// it, the function dominant, the chord diatonic. <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int}, int)"/>
/// now names a symbol's chord by that core when the whole set has no template, and the report
/// writes what the symbol said above the core after the numeral.
/// </summary>
/// <remarks>
/// Every row of the first theory read "?" / "Chromatic (outside the key)" before the fix, except
/// the half-diminished and plain dominant rows, which pin what had to stay.
/// </remarks>
public class AnExtendedChordKeepsTheNumeralOfItsCoreTests
{
    private const string Dominant = "Dominant (tension/pull to resolve)";
    private const string Subdominant = "Subdominant (motion/tension building)";
    private const string Tonic = "Tonic (home/stable)";
    private const string Chromatic = "Chromatic (outside the key)";

    private static ChordAnalysisDetail InCMajor(string symbol) =>
        ProgressionAdvisor.Analyze(["C", symbol, "C"]).Chords[1];

    [Theory]
    [InlineData("G7b9", "V7(b9)", "57(b9)", Dominant, false)]
    [InlineData("G7(b9,#9)", "V7(b9,#9)", "57(b9,#9)", Dominant, false)]
    [InlineData("G7(#11)", "V7(#11)", "57(#11)", Dominant, false)]
    [InlineData("G13(b9)", "V13(b9)", "513(b9)", Dominant, false)]
    [InlineData("G7(b13)", "V7(b13)", "57(b13)", Dominant, false)]
    [InlineData("G9", "V9", "59", Dominant, false)]
    [InlineData("G11", "V11", "511", Dominant, false)]
    [InlineData("G13", "V13", "513", Dominant, false)]
    [InlineData("G7sus4", "V7sus4", "57sus4", Dominant, false)]
    [InlineData("G9sus4", "V9sus4", "59sus4", Dominant, false)]
    [InlineData("G7(b9)add9", "V9(b9)", "59(b9)", Dominant, false)]
    // "alt" is #5 and b9: an augmented seventh with a flat ninth, and the report says so.
    [InlineData("G7alt", "V+7(b9)", "5+7(b9)", Dominant, true)]
    [InlineData("Dm9", "ii9", "2m9", Subdominant, false)]
    [InlineData("Dm11", "ii11", "2m11", Subdominant, false)]
    [InlineData("Cmaj9", "Imaj9", "1maj9", Tonic, false)]
    [InlineData("Cmaj13", "Imaj13", "1maj13", Tonic, false)]
    [InlineData("Am9", "vi9", "6m9", Tonic, false)]
    [InlineData("C6/9", "I6(add9)", "16(add9)", Tonic, false)]
    [InlineData("Fmaj7(#11)", "IVmaj7(#11)", "4maj7(#11)", Subdominant, false)]
    // the half-diminished supertonic has a quality of its own: iiø7, borrowed in major
    [InlineData("Dm7b5", "iiø7", "2m7b5", Subdominant, true)]
    // the plain dominant is untouched
    [InlineData("G7", "V7", "57", Dominant, false)]
    public void InCMajorTheNumeralIsTheCoresWithTheColourFigured(string symbol, string roman, string nashville, string function, bool borrowed)
    {
        var detail = InCMajor(symbol);

        Assert.Equal(roman, detail.RomanNumeral);
        Assert.Equal(nashville, detail.Nashville);
        Assert.Equal(function, detail.Function);
        Assert.Equal(borrowed, detail.IsBorrowed);
    }

    [Fact]
    public void AHalfDiminishedNinthIsFiguredOnItsHalfDiminishedSeventh()
    {
        var detail = InCMajor("Dø9");

        Assert.Equal("iiø9", detail.RomanNumeral);
        Assert.Equal("2m9b5", detail.Nashville);
        Assert.Equal(Subdominant, detail.Function);
    }

    [Fact]
    public void TheDominantOfAMinorKeyKeepsItsFunctionUnderAFlatNinth()
    {
        var report = ProgressionAdvisor.Analyze(["Cm", "Dm7b5", "G7b9", "Cm"]);

        Assert.Equal(new KeySignature(0, false), report.Key);
        Assert.Equal("i - iiø7 - V7(b9) - i", report.Pattern);
        Assert.All(report.Chords, c => Assert.False(c.IsBorrowed, $"{c.Symbol} reported as borrowed"));
        Assert.Equal(Dominant, report.Chords[2].Function);
        Assert.Equal(ChordCharacter.Tense, report.Chords[2].Character);
        Assert.Contains(report.Cadences, c => c.Type == CadenceType.Authentic);
    }

    [Fact]
    public void TheTritoneSubstituteIsStillChromatic()
    {
        // Db7 in C is a dominant seventh on a root the key does not own: no numeral, chromatic.
        var detail = InCMajor("Db7");

        Assert.Equal("?", detail.RomanNumeral);
        Assert.Equal("?", detail.Nashville);
        Assert.Equal(Chromatic, detail.Function);
        Assert.True(detail.IsBorrowed);

        // And so is an extended chord on such a root: the core gives a quality, not a degree.
        Assert.Equal("?", InCMajor("Db9").RomanNumeral);
        Assert.Equal(Chromatic, InCMajor("Db7b9").Function);
    }

    [Fact]
    public void AChordWithNoCoreTheLibraryNamesIsStillUnknown()
    {
        // A dominant seventh with no third has no triad to root a quality on. It was Unknown and
        // still is; the report says so rather than guessing.
        var parsed = ParsedChord.FromSymbol("G7no3");

        Assert.NotNull(parsed);
        Assert.Equal(ChordQuality.Unknown, parsed.Value.Info.Quality);
        Assert.Equal("?", InCMajor("G7no3").RomanNumeral);
    }

    [Fact]
    public void TheCoreIsTheSeventhChordASymbolExtends()
    {
        Assert.Equal(ChordQuality.Dominant7, ParsedChord.FromSymbol("G7b9")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Dominant7, ParsedChord.FromSymbol("G13(b9,#11)")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Minor7, ParsedChord.FromSymbol("Dm9")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Major7, ParsedChord.FromSymbol("Cmaj9")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.HalfDim7, ParsedChord.FromSymbol("Bø9")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Augmented7, ParsedChord.FromSymbol("G7alt")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Sus4, ParsedChord.FromSymbol("G7sus4")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Major6, ParsedChord.FromSymbol("C6/9")!.Value.Info.Quality);
        Assert.Equal(ChordQuality.Dominant7Flat5, ParsedChord.FromSymbol("G7(b5,#5)")!.Value.Info.Quality);

        // The root is the symbol's, whatever the bass or the layers above.
        Assert.Equal(7, ParsedChord.FromSymbol("G9/B")!.Value.Info.RootPitchClass);
        Assert.Equal(ChordQuality.Dominant7, ParsedChord.FromSymbol("G9/B")!.Value.Info.Quality);
    }

    [Fact]
    public void TheCoreIsAlwaysPartOfTheChordAndNeverOverridesAWholeMatch()
    {
        // Over the parity table's grid of roots and suffixes: a named quality's template lies
        // inside the chord, rooted where the symbol says; a chord the templates name whole keeps
        // exactly that quality; and a chord with a third and a fifth is never Unknown.
        string[] roots = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
        string[] suffixes =
        [
            "", "m", "dim", "aug", "sus2", "sus4", "5", "maj7", "m7", "7", "7b5", "m7b5", "dim7", "aug7", "m(maj7)",
            "add9", "add11", "6", "m6", "9", "m9", "maj9", "11", "13", "6/9", "7#9", "7b9", "7#11", "7(b9,#11)",
            "7alt", "7sus4", "9sus4", "maj7#11", "7b13", "13#11", "7(b9,#9)", "7(b5,#5)", "9(b9,#9)", "7(b9,#9,#11,b13)",
            "5(b9)", "dim7(#5)", "aug7(b5)", "7(b9)add9", "13(b9)", "m9(maj7)", "maj13#11", "dim7add9", "aug(maj7)",
        ];

        foreach (var root in roots)
        {
            foreach (var suffix in suffixes)
            {
                var symbol = root + suffix;
                var parsed = ParsedChord.FromSymbol(symbol);
                Assert.True(parsed.HasValue, $"{symbol} did not parse");

                var (_, pitches, info) = parsed.Value;
                var mask = ChordAnalyzer.GetMask(pitches);
                var intervals = pitches.Select(p => PitchMath.Fold(p - info.RootPitchClass)).ToHashSet();

                if (ChordLibrary.TryGetQuality(mask, info.RootPitchClass, out var whole))
                {
                    Assert.Equal(whole, info.Quality);
                }

                var hasThirdAndPerfectFifth = intervals.Overlaps([3, 4]) && intervals.Contains(7);
                if (hasThirdAndPerfectFifth)
                {
                    Assert.NotEqual(ChordQuality.Unknown, info.Quality);
                }

                if (info.Quality != ChordQuality.Unknown)
                {
                    var template = new RomanNumeralChord(ScaleDegree.I, info.Quality, HarmonicFunction.Tonic)
                        .GetPitchClasses(new KeySignature(0, true))
                        .Select(pc => (int)pc);
                    Assert.True(template.All(intervals.Contains), $"{symbol}: {info.Quality} is not inside [{string.Join(", ", intervals.Order())}]");
                }
            }
        }
    }

    [Fact]
    public void AChordTheTemplatesNameWholeIsFiguredExactlyAsBefore()
    {
        // No figures on a known quality: the roman numeral and Nashville label of every quality
        // the library defines are what RomanNumeralChord writes for it.
        foreach (var quality in Enum.GetValues<ChordQuality>().Where(q => q != ChordQuality.Unknown))
        {
            // The stack of fourths has no conventional symbol; G-C-F is G7sus4 without its fifth.
            var symbol = quality == ChordQuality.Quartal ? "G7sus4omit5" : new ChordInfo(7, quality).ToSymbol(preferSharps: true);
            var report = ProgressionAdvisor.Analyze(["C", symbol, "C"]);
            var detail = report.Chords[1];
            var roman = KeyAnalyzer.Analyze(new ChordInfo(7, quality), report.Key);

            Assert.Equal(quality, ParsedChord.FromSymbol(symbol)!.Value.Info.Quality);
            Assert.Equal(roman.ToRomanNumeral(), detail.RomanNumeral);
            Assert.Equal(roman.ToNashville(), detail.Nashville);
        }
    }

    [Fact]
    public void TheSameJazzProgressionReadsTheSameInEveryKey()
    {
        string[] names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
        var readings = new HashSet<string>();

        for (var tonic = 0; tonic < 12; tonic++)
        {
            string[] progression =
            [
                names[tonic] + "maj9",
                names[(tonic + 9) % 12] + "m9",
                names[(tonic + 2) % 12] + "m11",
                names[(tonic + 7) % 12] + "13(b9)",
                names[tonic] + "6/9",
            ];
            var report = ProgressionAdvisor.Analyze(progression);

            Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
            readings.Add(report.Pattern);
        }

        Assert.Equal(["Imaj9 - vi9 - ii11 - V13(b9) - I6(add9)"], readings);
    }

    [Fact]
    public void TheBassRootedIdentifyStillHasNoNameForANinthChord()
    {
        // Without a symbol there is no root to build a core on: five pitch classes could be a
        // ninth on one root or a sixth-nine on another. The public Identify keeps its answer.
        Assert.Equal(ChordQuality.Unknown, ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol("G9")).Quality);
        Assert.Equal(ChordQuality.Unknown, ChordAnalyzer.Identify(ProgressionAdvisor.ParseChordSymbol("G7b9")).Quality);
    }

    [Fact]
    public void TheSingleChordClassifierHearsTheCore()
    {
        // The other public road that reads a symbol's quality: an altered dominant is tense, a
        // ninth chord on a minor seventh warm — not Unknown.
        Assert.Equal(ChordCharacter.Tense, ChordCharacterClassifier.Classify("G7b9").Character);
        Assert.Equal(ChordCharacter.Tense, ChordCharacterClassifier.Classify("G13").Character);
        Assert.Equal(ChordCharacter.Warm, ChordCharacterClassifier.Classify("Dm9").Character);
        Assert.Equal(ChordCharacter.Dreamy, ChordCharacterClassifier.Classify("Cmaj9").Character);
        Assert.Equal(ChordCharacter.Mysterious, ChordCharacterClassifier.Classify("G7alt").Character);
    }
}
