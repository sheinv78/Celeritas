// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The key, modulation and secondary-dominant heuristics of <see cref="ProgressionAdvisor"/> were
/// tuned on the library's own random symbol corpus, and random progressions have no key, so a
/// model wrong about real music passed. Judged on the progressions below — the textbook and the
/// bandstand, each with the key, the modulations and the applied dominants a musician would write
/// — the advisor read a twelve-bar blues as modulating to the supertonic minor at its first chord,
/// I - V7/V - V - I as a direct modulation to the dominant with no secondary dominant, C7 - F - G7
/// - C7 in F, the Neapolitan as a modulation, and the dominant of every minor key as borrowed from
/// the parallel major. Each case is asked in all twelve keys.
/// </summary>
public class RealProgressionsAreReadAsAMusicianReadsThemTests
{
    /// <summary>
    /// A shape is chord tokens in C: the root as semitones above the tonic, an optional ":suffix",
    /// and "/bass" as semitones above the tonic. Expected modulations are (earliest position,
    /// tonic as semitones above the key's, is major). Secondary dominants: a count, -1 for "any",
    /// -2 for "either the listed modulation with none, or no modulation and one".
    /// </summary>
    public static TheoryData<string, bool, string, string, int> Cases => new()
    {
        { "12-bar blues", true, "0:7 0:7 0:7 0:7 5:7 5:7 0:7 0:7 7:7 5:7 0:7 7:7", "", -1 },
        { "rhythm changes A", true, "0 9:7 2:m7 7:7 4:m7 9:7 2:m7 7:7 0 0:7 5 6:dim 0 7:7 0", "", 3 },
        { "Pachelbel", true, "0 7 9:m 4:m 5 0 5 7", "", 0 },
        { "Andalusian", false, "0:m 10 8 7", "", 0 },
        { "I V7/V V7 I", true, "0 2:7 7:7 0", "", 1 },
        { "minor circle", false, "0:m 5:m 10 3 8 2:m7b5 7:7 0:m", "", 0 },
        { "doo-wop", true, "0 9:m 5 7", "", 0 },
        { "borrowed iv", true, "0 5 5:m 0", "", 0 },
        { "I iii IV V", true, "0 4:m 5 7", "", 0 },
        { "tritone substitution", true, "2:m7 1:7 0:maj7", "", 0 },
        { "Picardy third", false, "0:m 5:m 7:7 0", "", 0 },
        { "pivot to the relative minor", true, "0 5 7 0 9:m 2:m 4:7 9:m", "4,9,m", -2 },
        { "gear change up a semitone", true, "0 5 7 0 1 6 8 1", "4,1,M", 0 },
        { "gospel vii/V", true, "0 0/4 5 6:dim 7:7 0", "", 0 },
        { "two secondary dominants", true, "0 4:7 9:m 2:7 7 0", "", 2 },
        { "ii V loop", true, "0:maj7 2:m7 7:7 0:maj7 2:m7 7:7", "", 0 },
        { "Neapolitan", false, "0:m 5:m/8 1 7:7 0:m", "", 0 },
        { "plagal rock", true, "0 5 0 7 0 5 7 0", "", 0 },
        { "Aeolian rock", false, "0:m 10 8 10 0:m", "", 0 },
        { "deceptive", true, "0 5 7 9:m", "", 0 },
        { "minor blues", false, "0:m7 5:m7 0:m7 0:m7 5:m7 5:m7 0:m7 0:m7 7:7 5:m7 0:m7 7:7", "", 0 },
        { "dominant-seventh tonic", true, "0:7 5 7:7 0:7", "", 0 },
        { "sus colouring", true, "0:sus4 0 7:sus4 7 9:m 5", "", 0 },
        { "V7/ii chain", true, "0 9:7 2:m7 7:7 0", "", 1 },
        { "I vi ii V", true, "0 9:m 2:m 7", "", 0 },
        { "modulation to the dominant through a pivot", true, "0 5 7 0 9:m 2 7 2 7 0 7", "5,7,M", 0 },
    };

    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static string[] Spell(string shape, int tonic) =>
        [.. shape.Split(' ').Select(token =>
        {
            var bass = "";
            var slash = token.IndexOf('/');
            if (slash >= 0)
            {
                bass = "/" + Names[(tonic + int.Parse(token[(slash + 1)..])) % 12];
                token = token[..slash];
            }

            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            var suffix = colon >= 0 ? token[(colon + 1)..] : "";
            return Names[(tonic + root) % 12] + suffix + bass;
        })];

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheKeyTheModulationsAndTheSecondaryDominantsAreTheMusiciansInEveryKey(
        string name, bool isMajor, string shape, string modulation, int secondaryDominants)
    {
        var expectedModulations = modulation.Length == 0
            ? []
            : new[] { (Position: int.Parse(modulation.Split(',')[0]), ToRoot: int.Parse(modulation.Split(',')[1]), ToMajor: modulation.Split(',')[2] == "M") };

        var readings = new HashSet<string>();
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var report = ProgressionAdvisor.Analyze(Spell(shape, tonic));

            Assert.True(
                report.Key == new KeySignature((byte)tonic, isMajor),
                $"{name} in {Names[tonic]}: key {report.Key}, pattern {report.Pattern}");

            var modulations = report.Modulations.Where(m => m.Type != ModulationType.Tonicization).ToList();
            var modulationsMatch = modulations.Count == expectedModulations.Length
                && expectedModulations.All(e => modulations.Any(m =>
                    m.Position >= e.Position
                    && m.ToKey.Root == (tonic + e.ToRoot) % 12
                    && m.ToKey.IsMajor == e.ToMajor));
            var secondaryMatch = secondaryDominants == -1 || report.SecondaryDominants.Count == secondaryDominants;

            if (secondaryDominants == -2)
            {
                var tonicizationInstead = modulations.Count == 0 && report.SecondaryDominants.Count == 1;
                secondaryMatch = tonicizationInstead || (modulationsMatch && report.SecondaryDominants.Count == 0);
                modulationsMatch = modulationsMatch || tonicizationInstead;
            }

            var described = string.Join(", ", modulations.Select(m => $"{m.Type}@{m.Position}->{m.ToKey}"));
            Assert.True(modulationsMatch, $"{name} in {Names[tonic]}: modulations [{described}], pattern {report.Pattern}");
            Assert.True(secondaryMatch, $"{name} in {Names[tonic]}: {report.SecondaryDominants.Count} secondary dominants, pattern {report.Pattern}");

            var relative = string.Join(", ", modulations.Select(m =>
                $"{m.Type}@{m.Position}->{PitchMath.Fold(m.ToKey.Root - tonic)}{(m.ToKey.IsMajor ? "M" : "m")}"));
            readings.Add($"{PitchMath.Fold(report.Key.Root - tonic)}{(report.Key.IsMajor ? "M" : "m")}|{report.Pattern}|{relative}|{report.SecondaryDominants.Count}");
        }

        // The same music in twelve keys is one reading.
        Assert.Single(readings);
    }

    [Fact]
    public void AModulationThroughAnAppliedDominantPivotsOnTheChordBeforeIt()
    {
        // vi of C is ii of G, and D → G takes the music there. The applied dominant itself
        // belongs to neither key, and used to be named the pivot.
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "Am", "D", "G", "D", "G", "C", "G"]);

        var modulation = Assert.Single(report.Modulations, m => m.Type != ModulationType.Tonicization);
        Assert.Equal(ModulationType.PivotChord, modulation.Type);
        Assert.Equal("Am", modulation.PivotChord);
        Assert.Equal("vi in C Major = ii in G Major", modulation.PivotAnalysis);
        Assert.Equal(new KeySignature(7, true), modulation.ToKey);
    }

    [Fact]
    public void TheDominantOfAMinorKeyIsItsOwn()
    {
        foreach (var progression in new[] { new[] { "Am", "Dm", "E", "Am" }, new[] { "Cm", "Fm", "G7", "Cm" }, new[] { "Fm", "Bbm", "C7", "Fm" } })
        {
            var report = ProgressionAdvisor.Analyze(progression);

            Assert.False(report.Key.IsMajor);
            Assert.False(report.HasModalMixture, string.Join(" ", progression));
            Assert.Empty(report.BorrowedChords);
            Assert.All(report.Chords, c => Assert.False(c.IsBorrowed, $"{c.Symbol} in {report.Key}"));
            Assert.DoesNotContain(report.Highlights, h => h.Contains("borrowed", StringComparison.OrdinalIgnoreCase));
        }

        // ...and the Picardy tonic is what the minor key borrows from its parallel major.
        var picardy = ProgressionAdvisor.Analyze(["Am", "Dm", "E", "A"]);
        Assert.Equal(new KeySignature(9, false), picardy.Key);
        Assert.Equal("A", Assert.Single(picardy.BorrowedChords).Chord);
    }

    [Fact]
    public void TheVoiceLeadingOfATextbookProgressionIsNotRough()
    {
        // The parallel fifths were counted between root-position stacks planed in parallel, on
        // which every change of root is a fifth: I - IV - V - I had three and was "Rough", and
        // "Excellent" was reachable only by a chord that never changes. Measured on the voicing
        // a musician writes — each tone to the nearest tone of the next chord — it has none.
        var textbook = ProgressionAdvisor.Analyze(["C", "F", "G", "C"]);
        Assert.Equal(0, textbook.ParallelFifths);
        Assert.Equal("Excellent", textbook.QualityRating);

        Assert.Equal(0, ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]).ParallelFifths);
        Assert.Equal(0, ProgressionAdvisor.Analyze(["C", "Am"]).ParallelFifths);

        // Triads planed by semitone cannot avoid them, and a power-chord riff is made of them.
        Assert.Equal(3, ProgressionAdvisor.Analyze(["C", "Db", "D", "Eb"]).ParallelFifths);
        Assert.Equal(2, ProgressionAdvisor.Analyze(["C5", "G5", "C5"]).ParallelFifths);
    }

    [Fact]
    public void ADirectModulationIsPlacedOnTheFirstChordOfTheNewKey()
    {
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C", "Db", "Gb", "Ab", "Db"]);

        var modulation = Assert.Single(report.Modulations, m => m.Type != ModulationType.Tonicization);
        Assert.Equal(ModulationType.Direct, modulation.Type);
        Assert.Equal(4, modulation.Position);
        Assert.Equal(new KeySignature(1, true), modulation.ToKey);
        Assert.Empty(report.SecondaryDominants);
    }
}
