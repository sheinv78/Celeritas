// Copyright (c) 2025 Vladimir V. Shein

using System.Text.RegularExpressions;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The roman numeral is the whole content of a modal pattern label, and the tables were written
/// by hand: Lydian's seventh-degree triad (B-D-F# in C Lydian) is minor and was labelled "vii°",
/// the Dorian ii (E-G-B in D Dorian) is minor and was described as "Dorian ii chord (major)", the
/// Lydian dominant's "bVII7" would need a note the scale does not have. A student checking the
/// library's answer against the scale found the label wrong. This test does the checking.
/// </summary>
public partial class APatternLabelNamesTheChordsOfItsModeTests
{
    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];

    /// <summary>
    /// The chords a named progression borrows from a neighbouring mode, and says so by its name.
    /// </summary>
    private static readonly HashSet<(Mode Mode, string Name, string Token)> NamedBorrowings =
    [
        (Mode.Aeolian, "i - bVII - bVI - V", "V"),                 // the Andalusian cadence's harmonic-minor V
        (Mode.HarmonicMinor, "i - bVII - bVI - V7", "bVII"),       // the descent passes through natural minor
        (Mode.PhrygianDominant, "I - bII - bIII - bII", "bIII"),   // the flamenco vamp's Phrygian bIII
        (Mode.PhrygianDominant, "I - bVII - bVI - bII - I", "bVII"),
        (Mode.PhrygianDominant, "I - bVII - bVI - bII - I", "bVI"), // the Phrygian descent's major chords
        (Mode.LydianDominant, "I7 - bVII7 - I7", "bVII7"),          // the fusion vamp's Mixolydian bVII7
    ];

    [GeneratedRegex(@"^(?<acc>[b#]?)(?<numeral>VII|VI|IV|V|III|II|I|vii|vi|iv|v|iii|ii|i)(?<suffix>.*)$")]
    private static partial Regex Token();

    private static string Quality(int root, int third, int fifth) => ((third - root + 12) % 12, (fifth - root + 12) % 12) switch
    {
        (4, 7) => "major",
        (3, 7) => "minor",
        (3, 6) => "diminished",
        (4, 8) => "augmented",
        var other => $"other {other}",
    };

    public static TheoryData<Mode> ModesWithTables =>
        [.. Enum.GetValues<Mode>().Where(m => ModalProgressions.GetProgressionsForMode(m).Count > 0)];

    [Fact]
    public void OnlyHeptatonicModesHaveATable()
    {
        // A mode with no catalogue used to be handed the major one, so a whole-tone progression
        // could be reported as an "Authentic cadence" on a fifth the scale does not contain.
        foreach (var mode in Enum.GetValues<Mode>())
        {
            var table = ModalProgressions.GetProgressionsForMode(mode);
            if (ModeLibrary.GetIntervals(mode).Length != 7)
            {
                Assert.Empty(table);
            }
        }

        Assert.Empty(ModalProgressions.GetProgressionsForMode(Mode.WholeTone));
        Assert.Empty(ModalProgressions.GetProgressionsForMode(Mode.Blues));
        Assert.Empty(ModalProgressions.GetProgressionsForMode(Mode.Altered));
        Assert.Empty(ModalProgressions.GetProgressionsForMode(Mode.LocrianNatural2));
        Assert.NotEmpty(ModalProgressions.GetProgressionsForMode(Mode.Ionian));
    }

    [Theory]
    [MemberData(nameof(ModesWithTables))]
    public void EveryLabelWritesTheQualityAndAccidentalItsDegreeHasInTheMode(Mode mode)
    {
        var intervals = ModeLibrary.GetIntervals(mode);
        Assert.Equal(7, intervals.Length);

        foreach (var progression in ModalProgressions.GetProgressionsForMode(mode))
        {
            var tokens = progression.Name.Split(" - ");
            Assert.Equal(progression.Degrees.Count, tokens.Length);

            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (NamedBorrowings.Contains((mode, progression.Name, token)))
                {
                    continue;
                }

                var match = Token().Match(token);
                Assert.True(match.Success, $"{mode} '{progression.Name}': '{token}' is not a roman numeral");

                var degree = progression.Degrees[i];
                var root = intervals[degree - 1];
                var third = intervals[(degree + 1) % 7];
                var fifth = intervals[(degree + 3) % 7];
                var diatonic = Quality(root, third, fifth);

                var suffix = match.Groups["suffix"].Value;
                var numeral = match.Groups["numeral"].Value;
                var claimed = suffix.Contains('°') ? "diminished"
                    : suffix.Contains('+') ? "augmented"
                    : char.IsUpper(numeral[0]) ? "major"
                    : "minor";

                Assert.True(
                    claimed == diatonic,
                    $"{mode} '{progression.Name}': '{token}' claims {claimed}, the degree-{degree} triad is {diatonic}");

                var expectedAccidental = (root - MajorScale[degree - 1]) switch
                {
                    -1 => "b",
                    1 => "#",
                    0 => "",
                    var d => $"?{d}",
                };
                Assert.True(
                    match.Groups["acc"].Value == expectedAccidental,
                    $"{mode} '{progression.Name}': '{token}' should carry '{expectedAccidental}' on degree {degree}");
            }
        }
    }

    [Fact]
    public void TheLabelsThatWereWrongReadRightNow()
    {
        var lydian = ModalProgressions.Analyze(["C", "D", "Bm"]);
        Assert.Equal(Mode.Lydian, lydian.DetectedKey.Mode);
        Assert.Equal("I - II - vii", lydian.MatchedProgression?.Name);

        var dorian = ModalProgressions.Analyze(["Dm", "Em", "Dm"]);
        Assert.Equal(Mode.Dorian, dorian.DetectedKey.Mode);
        Assert.DoesNotContain("major", dorian.MatchedProgression?.Description ?? "major");
    }
}
