// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The advisor's untested arms: the suggestion tables for iii, vi and vii°, the chromatic
/// fallback, melodic-minor detection, and the modulation finder. Every one of them returns
/// something plausible for any input, so a wrong table or a wrong key comparison reads as a
/// confident answer — which is how the "modulation to G Major (same key)" entry survived.
/// </summary>
public class ProgressionAdvisorCoverageTests
{
    private static string[] Chords(params string[] symbols) => symbols;

    // ---------- what to play next ----------

    [Fact]
    public void AfterTheMediant_TheAdviceIsToFall()
    {
        var suggestions = ProgressionAdvisor.SuggestNext(Chords("C", "F", "G", "Em"));

        Assert.Equal("Am", suggestions[0].Chord);
        Assert.Equal("Descending to relative minor", suggestions[0].Reason);
        Assert.Contains(suggestions, s => s.Chord == "F");
        Assert.Contains(suggestions, s => s.Chord == "Dm");
    }

    [Fact]
    public void AfterTheSubmediant_TheCircleIsOffered()
    {
        var suggestions = ProgressionAdvisor.SuggestNext(Chords("C", "F", "G", "Am"));

        Assert.Equal("F", suggestions[0].Chord);
        Assert.Contains(suggestions, s => s.Chord == "Dm" && s.Reason == "Circle progression");
        Assert.Contains(suggestions, s => s.Chord == "G");
    }

    [Fact]
    public void AfterTheLeadingToneChord_TheTonicIsCertain()
    {
        var suggestions = ProgressionAdvisor.SuggestNext(Chords("C", "F", "G", "Bdim"));

        Assert.Equal("C", suggestions[0].Chord);
        Assert.Equal(1.0f, suggestions[0].Score);
        Assert.Equal("Leading tone resolution", suggestions[0].Reason);
    }

    [Fact]
    public void AfterAChromaticChord_TheGenericAdviceIsGiven()
    {
        // Db is not a degree of C major, so the roman numeral is invalid and the advisor
        // must fall through to its generic arm rather than treating it as the tonic.
        var suggestions = ProgressionAdvisor.SuggestNext(Chords("C", "F", "C", "Db"));

        Assert.Equal("C", suggestions[0].Chord);
        Assert.Equal("Resolve to tonic", suggestions[0].Reason);
        Assert.Contains(suggestions, s => s.Reason == "Build tension with dominant");
    }

    [Fact]
    public void SuggestionsAreOrderedByScore_AndCapped()
    {
        var suggestions = ProgressionAdvisor.SuggestNext(Chords("C", "F", "G", "Am"), maxSuggestions: 3);

        Assert.True(suggestions.Count <= 3);
        Assert.Equal(suggestions.OrderByDescending(s => s.Score).Select(s => s.Chord), suggestions.Select(s => s.Chord));
    }

    [Fact]
    public void NothingParsable_YieldsNoAdviceAtAll()
    {
        Assert.Empty(ProgressionAdvisor.SuggestNext(Chords("Zzz")));
    }

    // ---------- minor colour ----------

    [Fact]
    public void ARaisedSixthAndSeventhTogether_ReadAsMelodicMinor()
    {
        // G#m7b5 over an A-minor progression carries both F# and G#.
        var report = ProgressionAdvisor.Analyze(Chords("Am", "Dm", "G#m7b5", "Am"));

        Assert.True(report.UsesMelodicMinor);
        Assert.False(report.UsesHarmonicMinor);
        Assert.Contains("Uses melodic minor color (raised 6th/7th)", report.Highlights);
    }

    [Fact]
    public void ARaisedSeventhAlone_ReadsAsHarmonicMinor()
    {
        var report = ProgressionAdvisor.Analyze(Chords("Am", "B7", "E", "Am"));

        Assert.True(report.UsesHarmonicMinor);
        Assert.False(report.UsesMelodicMinor);
        Assert.Contains("Uses harmonic minor color (raised 7th)", report.Highlights);
    }

    [Fact]
    public void AMajorKey_NeverReportsMinorColour()
    {
        var report = ProgressionAdvisor.Analyze(Chords("C", "F", "G", "C"));

        Assert.False(report.UsesHarmonicMinor);
        Assert.False(report.UsesMelodicMinor);
    }

    // ---------- chord character ----------

    [Theory]
    [InlineData("C5", ChordCharacter.Powerful)]
    [InlineData("Gsus4/D", ChordCharacter.Modal)]
    [InlineData("C", ChordCharacter.Stable)]
    [InlineData("Cm", ChordCharacter.Melancholic)]
    public void ChordQualityDecidesCharacter(string symbol, ChordCharacter expected)
    {
        // Analysed as its own one-chord progression so nothing else can colour the reading.
        var report = ProgressionAdvisor.Analyze(Chords(symbol, symbol));

        Assert.Equal(expected, report.Chords[0].Character);
    }

    // ---------- modulation ----------

    [Fact]
    public void APivotChordModulation_NamesTheChordAndReadsItInBothKeys()
    {
        var report = ProgressionAdvisor.Analyze(
            Chords("C", "F", "G", "C", "Am", "D", "G", "D", "G", "C", "G"));

        var pivot = report.Modulations.First(m => m.Type == ModulationType.PivotChord && m.PivotAnalysis is not null);

        Assert.Equal("G", pivot.PivotChord);
        Assert.Equal("V in C Major = III in E Minor", pivot.PivotAnalysis);
    }

    [Fact]
    public void NoModulationEverGoesFromAKeyToItself()
    {
        // Regression: after modulating to G major, the D-G cadence inside G was read as
        // another secondary dominant of the MAIN key and reported as
        // "Modulation to G Major (same key)".
        var report = ProgressionAdvisor.Analyze(
            Chords("C", "F", "G", "C", "Am", "D", "G", "D", "G", "C", "G"));

        Assert.All(report.Modulations, m => Assert.False(
            m.FromKey.Root == m.ToKey.Root && m.FromKey.IsMajor == m.ToKey.IsMajor,
            $"modulation at {m.Position} goes from {m.FromKey} to itself: {m.Description}"));
    }

    [Fact]
    public void ADirectModulation_IsReportedWithoutAPivot()
    {
        var report = ProgressionAdvisor.Analyze(
            Chords("C", "F", "C", "Am", "D", "G", "Em", "D", "G"));

        var direct = report.Modulations.Where(m => m.Type == ModulationType.Direct).ToArray();

        Assert.NotEmpty(direct);
        Assert.All(direct, m => Assert.Null(m.PivotChord));
        Assert.All(direct, m => Assert.Contains("Direct modulation", m.Description, StringComparison.Ordinal));
    }

    [Fact]
    public void AProgressionThatStaysPut_ModulatesNowhere()
    {
        var report = ProgressionAdvisor.Analyze(Chords("C", "Am", "F", "G", "C", "Am", "F", "G"));

        Assert.Empty(report.Modulations);
    }

    // ---------- parallel motion ----------

    [Fact]
    public void ParallelFifthsBetweenRootPositionTriads_AreCounted()
    {
        var report = ProgressionAdvisor.Analyze(Chords("C5", "G5", "C5"));

        Assert.True(report.ParallelFifths > 0);
    }

    [Fact]
    public void ChordSymbolsVoiceEachPitchClassOnce_SoNoOctavesCanBeParallel()
    {
        // ParallelOctaves counts octaves between aligned voices, which needs a chord to
        // sound one pitch class twice. No symbol the parser accepts does that — a slash
        // bass moves the note down rather than doubling it — so the count stays 0. If a
        // future voicing doubles anything, this test is the notice that the counter goes live.
        string[] symbols =
        [
            "C", "Cm", "C7", "Cmaj7", "Cm7b5", "Cdim7", "C6", "C9", "C11", "C13",
            "Cadd9", "Cadd11", "Csus2", "Csus4", "C5", "Caug", "C/E", "C/G", "C/C", "Cm11",
        ];

        foreach (var symbol in symbols)
        {
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
                continue;

            var classes = pitches.Select(PitchMath.Fold).ToArray();
            Assert.Equal(classes.Length, classes.Distinct().Count());
        }

        Assert.Equal(0, ProgressionAdvisor.Analyze(Chords("C/C", "D/D")).ParallelOctaves);
    }
}
