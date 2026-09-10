// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// I-vi-ii-V is the most played progression in popular music, and the advisor reported it in
/// the key of its own dominant — in all twelve keys — then annotated a "modulation" at its first
/// chord whose target rotated through four different keys as the progression was transposed.
/// <para>
/// Two defects, one on the other. The key scorer's "a fourth up is V going to I" bonus fired on
/// ii going to V, because it never asked whether the chord doing the resolving could be a
/// dominant; Dm rising to G handed G the tonic. And with the key wrong, the modulation finder
/// looked for a better one over the first three chords, found four keys that fit them equally,
/// and kept whichever came first in root order — which is not a property of the music.
/// </para>
/// </summary>
public class TheCommonestProgressionsAreInTheirOwnKeyTests
{
    private static readonly string[] Names =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static readonly (int Step, string Suffix)[] MajorDegrees =
        [(0, ""), (2, "m"), (4, "m"), (5, ""), (7, ""), (9, "m"), (11, "dim")];

    private static readonly (int Step, string Suffix)[] MinorDegrees =
        [(0, "m"), (2, "dim"), (3, ""), (5, "m"), (7, "m"), (8, ""), (10, "")];

    /// <summary>
    /// Shapes whose key a musician would not argue about, as scale degrees. Each starts on the
    /// tonic or on the classic ii-V-I, so the key is not a matter of reading.
    /// </summary>
    public static TheoryData<string> Shapes =>
    [
        "0 5 1 4",      // I-vi-ii-V, the pop turnaround
        "0 3 4 0",      // I-IV-V-I
        "1 4 0",        // ii-V-I
        "0 4 5 3",      // I-V-vi-IV
        "0 5 3 4",      // I-vi-IV-V, the doo-wop changes
        "0 3 0 4 0",    // a plagal figure and a cadence
        "0 1 4 0",      // I-ii-V-I
    ];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AProgressionBuiltOnItsTonicIsReportedInThatKeyInEveryKey(string shape)
    {
        var degrees = shape.Split(' ').Select(int.Parse).ToArray();

        foreach (var isMajor in new[] { true })
        {
            var table = isMajor ? MajorDegrees : MinorDegrees;
            for (var tonic = 0; tonic < 12; tonic++)
            {
                var progression = degrees
                    .Select(d => Names[(tonic + table[d].Step) % 12] + table[d].Suffix)
                    .ToArray();

                var report = ProgressionAdvisor.Analyze(progression);

                Assert.Equal(new KeySignature((byte)tonic, isMajor), report.Key);
                Assert.Empty(report.Modulations);
            }
        }
    }

    [Fact]
    public void TheDominantOfAMinorKeyMayBeMinorAndStillCadence()
    {
        // The rule that fixed I-vi-ii-V is "a minor chord rising a fourth into a MAJOR one is
        // ii-V, not V-I". It must not take the cadence away from natural minor, whose dominant
        // is minor: Gm -> Cm is how C minor closes, and without the bonus "Dsus4 Gm Cm" came out
        // in D minor.
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Ddim", "Gm", "Cm"]).Key);
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Dsus4", "Gm", "Cm"]).Key);
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Fm", "Gm", "Cm"]).Key);
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Fm", "G", "Cm"]).Key);
    }

    [Fact]
    public void ATiedWindowDoesNotPickAKeyByItsRootNumber()
    {
        // Three diatonic chords fit several keys equally. Whatever the finder chooses for them,
        // it must choose the same degree in every transposition: the answer rotated through
        // four keys before.
        var seen = new HashSet<string>();
        for (var tonic = 0; tonic < 12; tonic++)
        {
            string[] progression =
            [
                Names[tonic], Names[(tonic + 9) % 12] + "m", Names[(tonic + 2) % 12] + "m",
                Names[(tonic + 7) % 12], Names[(tonic + 1) % 12], Names[(tonic + 6) % 12] + "m",
                Names[(tonic + 8) % 12], Names[(tonic + 1) % 12],
            ];

            var report = ProgressionAdvisor.Analyze(progression);
            seen.Add(string.Join(" | ", report.Modulations.Select(m =>
                $"{((m.ToKey.Root - tonic) % 12 + 12) % 12}{(m.ToKey.IsMajor ? "M" : "m")}@{m.Position}")));
        }

        Assert.Single(seen);
    }
}
