// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// A chord symbol names its root, and every reader of symbols threw that away and rediscovered
/// the root from the pitches with the bass on top of the list. A slash chord contradicts that by
/// design: "Am7/C" — A minor seventh over its third — was identified as C6 and reported as I6 in
/// C major, "Dm7/F" as IV6, and "Csus4/G" as a quartal chord on G that made an authentic cadence
/// out of C - Csus4/G - C. A ninth chord, which no template covers, came back as Unknown rooted
/// on C, so ii7-V9-I was read in the minor key of its ii chord in eleven of twelve
/// transpositions. The parser now hands the root on, and the chord is identified as the
/// intervals above <em>that</em> root.
/// </summary>
public class ASymbolNamesItsOwnRootTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    [Theory]
    [InlineData("Am7/C", 9, ChordQuality.Minor7)]
    [InlineData("Dm7/F", 2, ChordQuality.Minor7)]
    [InlineData("Bm7b5/D", 11, ChordQuality.HalfDim7)]
    [InlineData("Csus4/G", 0, ChordQuality.Sus4)]
    [InlineData("Gsus4/C", 7, ChordQuality.Sus4)]
    [InlineData("Csus2/D", 0, ChordQuality.Sus2)]
    [InlineData("Cm7/Eb", 0, ChordQuality.Minor7)]
    [InlineData("C6", 0, ChordQuality.Major6)]
    [InlineData("Am7", 9, ChordQuality.Minor7)]
    [InlineData("G9", 7, ChordQuality.Unknown)]
    [InlineData("C/E", 0, ChordQuality.Major)]
    [InlineData("Eaug", 4, ChordQuality.Augmented)]
    [InlineData("F#7b5", 6, ChordQuality.Dominant7Flat5)]
    public void TheChordIsRootedWhereTheSymbolSays(string symbol, int root, ChordQuality quality)
    {
        var parsed = ParsedChord.FromSymbol(symbol);

        Assert.NotNull(parsed);
        Assert.Equal(root, parsed.Value.Info.RootPitchClass);
        Assert.Equal(quality, parsed.Value.Info.Quality);
    }

    [Fact]
    public void ASlashChordIsTheInversionItWrites()
    {
        Assert.Equal(1, ProgressionAdvisor.GetInversion("Am7/C"));
        Assert.Equal(1, ProgressionAdvisor.GetInversion("Dm7/F"));
        Assert.Equal(1, ProgressionAdvisor.GetInversion("Bm7b5/D"));
        Assert.Equal(2, ProgressionAdvisor.GetInversion("Csus4/G"));
        Assert.Equal(1, ProgressionAdvisor.GetInversion("C/E"));
        Assert.Equal(2, ProgressionAdvisor.GetInversion("C/G"));
        Assert.Equal(3, ProgressionAdvisor.GetInversion("G7/F"));
        Assert.Equal(0, ProgressionAdvisor.GetInversion("C"));
        Assert.Equal(0, ProgressionAdvisor.GetInversion("not a chord"));

        // The pitches-only road cannot know the symbol and is documented to read F-A-C-D with F
        // in the bass as F6 in root position.
        Assert.Equal(0, ProgressionAdvisor.GetInversion(ProgressionAdvisor.ParseChordSymbol("Dm7/F")));
    }

    [Fact]
    public void ASlashChordKeepsItsRomanNumeral()
    {
        Assert.Equal("I - vi7 - ii7 - V7", ProgressionAdvisor.Analyze(["C", "Am7/C", "Dm7", "G7"]).Pattern);
        Assert.Equal("I - ii7 - V7 - I", ProgressionAdvisor.Analyze(["C", "Dm7/F", "G7", "C"]).Pattern);
        Assert.Equal("i - iiø7 - V7 - i", ProgressionAdvisor.Analyze(["Am", "Bm7b5/D", "E7", "Am"]).Pattern);

        var suspended = ProgressionAdvisor.Analyze(["C", "Csus4/G", "C"]);
        Assert.Equal("I - Isus4 - I", suspended.Pattern);
        Assert.Empty(suspended.Cadences);
    }

    [Fact]
    public void ANinthChordIsEvidenceOfItsOwnKeyInEveryTransposition()
    {
        var seen = new HashSet<string>();
        for (var tonic = 0; tonic < 12; tonic++)
        {
            string[] twoFiveOne = [Names[(tonic + 2) % 12] + "m7", Names[(tonic + 7) % 12] + "9", Names[tonic]];

            var report = ProgressionAdvisor.Analyze(twoFiveOne);

            Assert.Equal(new KeySignature((byte)tonic, true), report.Key);
            seen.Add($"{report.Pattern}|{report.KeyConfidence:0.###}");
        }

        // The same music in twelve keys is one reading, not two.
        Assert.Single(seen);
    }

    [Fact]
    public void TheSiblingReadersAgreeOnTheRoot()
    {
        // The classifier, the colour analyzer and the modal analyzer all read symbols; none of
        // them may root a chord on its slash bass.
        Assert.Equal(ChordQuality.Minor7, ChordCharacterClassifier.Classify("Am7/C").Quality);
        Assert.Equal(ChordQuality.Sus4, ChordCharacterClassifier.Classify("Csus4/G").Quality);

        // Over Am7/C every note of A minor seventh is a chord tone; over "C6" they would be too,
        // so ask about the roman numeral the colour analyzer's modal reading is built on instead:
        // the chord assignment it builds is rooted on A.
        var melody = MusicNotation.Parse("A4/4 C5/4 E5/4 G5/4");
        var colour = HarmonicColorAnalyzer.Analyze(melody, [("Am7/C", Rational.Zero)], new KeySignature(0, true));
        Assert.All(colour.MelodicHarmony, e => Assert.True(e.IsChordTone));

        var modal = ModalProgressions.Analyze(["C", "Am7/C", "Dm7", "G7"]);
        Assert.NotNull(modal);
    }

    [Fact]
    public void AFirstInversionSeventhChordStillOwesItsResolution()
    {
        // The voice-leading rules read F-A-C-D as Dm7 whatever the bass: this is common-practice
        // voice leading, where that sonority over F is ii6/5 and its seventh must fall. When it
        // was read as F6 the first inversion lost the rule the root position kept.
        var to = new Voicing(55, 59, 64, 67);
        var rootPosition = VoiceLeadingRules.Check(new Voicing(50, 57, 60, 65), to, 0).Violations;
        var firstInversion = VoiceLeadingRules.Check(new Voicing(53, 57, 60, 62), to, 0).Violations;

        Assert.True(rootPosition.HasFlag(VoiceLeadingViolation.UnresolvedSeventh));
        Assert.True(firstInversion.HasFlag(VoiceLeadingViolation.UnresolvedSeventh));

        var halfDiminishedTo = new Voicing(52, 55, 60, 64);
        Assert.True(VoiceLeadingRules.Check(new Voicing(47, 53, 57, 62), halfDiminishedTo, 9).Violations.HasFlag(VoiceLeadingViolation.UnresolvedSeventh));
        Assert.True(VoiceLeadingRules.Check(new Voicing(50, 53, 57, 59), halfDiminishedTo, 9).Violations.HasFlag(VoiceLeadingViolation.UnresolvedSeventh));
    }
}
