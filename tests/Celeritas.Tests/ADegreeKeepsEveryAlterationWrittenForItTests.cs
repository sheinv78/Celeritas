// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A chord symbol may alter one degree twice: "C7(b9,#9)" is the stock altered-dominant sound
/// with both ninths, "C7(b5,#5)" has both fifths. The builder kept one slot per degree, so the
/// alteration written last overwrote the one written first: "C7(b9,#9)" came back without its
/// b9, "C7(#9,b9)" without its #9, "C7(b5,#5)" without its b5 — and the pitch set of a symbol
/// depended on the order its alterations were written in. Every road to the pitches of a symbol
/// — <see cref="ProgressionAdvisor.ParseChordSymbol"/>, <c>TryParseChordSymbol</c>, the
/// progression report, the native export the Python package calls — goes through that one
/// builder, so all of them lost the same note.
/// </summary>
public class ADegreeKeepsEveryAlterationWrittenForItTests
{
    private static readonly string[] Alterations = ["b5", "#5", "b9", "#9", "#11", "b13"];

    /// <summary>The semitones above the root each alteration names.</summary>
    private static readonly Dictionary<string, int> AlterationSemitones = new()
    {
        ["b5"] = 6,
        ["#5"] = 8,
        ["b9"] = 13,
        ["#9"] = 15,
        ["#11"] = 18,
        ["b13"] = 20,
    };

    /// <summary>The natural pitch of each alterable degree, which an alteration displaces.</summary>
    private static readonly Dictionary<string, int> NaturalSemitones = new()
    {
        ["b5"] = 7,
        ["#5"] = 7,
        ["b9"] = 14,
        ["#9"] = 14,
        ["#11"] = 17,
        ["b13"] = 21,
    };

    private static int[] Parse(string symbol) => [.. ProgressionAdvisor.ParseChordSymbol(symbol).Order()];

    [Theory]
    [InlineData("C7(b9,#9)", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C7(#9,b9)", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C7b9#9", new[] { 60, 64, 67, 70, 73, 75 })]
    [InlineData("C7(b5,#5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("C7(#5,b5)", new[] { 60, 64, 66, 68, 70 })]
    [InlineData("C7(b9,#9,#11,b13)", new[] { 60, 64, 67, 70, 73, 75, 78, 80 })]
    [InlineData("C7(b13,#11,#9,b9)", new[] { 60, 64, 67, 70, 73, 75, 78, 80 })]
    public void BothAlterationsOfADegreeAreHeard(string symbol, int[] expected)
    {
        Assert.Equal(expected, Parse(symbol));
    }

    [Fact]
    public void ANinthChordWithBothAlteredNinthsHasNoNaturalNinth()
    {
        var parsed = Parse("C9(b9,#9)");

        Assert.Equal([60, 64, 67, 70, 73, 75], parsed);
        Assert.DoesNotContain(74, parsed);
        Assert.Equal(Parse("C7(b9,#9)"), parsed);
    }

    [Fact]
    public void ARepeatedAlterationIsOneNote()
    {
        Assert.Equal([60, 64, 67, 70, 73], Parse("C7(b9,b9)"));
        Assert.Equal(Parse("C7b9"), Parse("C7(b9,b9)"));
    }

    [Fact]
    public void TheSameAlterationsInAnyOrderAreTheSameChord()
    {
        // Every subset of two to four alterations, in every order it can be written, on a
        // seventh and on a ninth chord, on roots at three places in the octave. Each answer has
        // every alteration that was named, no natural of a degree that was altered, and is the
        // same whichever order the alterations were written in.
        foreach (var root in new[] { "C", "F#", "Bb" })
        {
            var rootPitch = Parse(root)[0];
            foreach (var extension in new[] { "7", "9" })
            {
                foreach (var subset in Subsets(Alterations, 2, 4))
                {
                    int[]? first = null;
                    foreach (var order in Permutations(subset))
                    {
                        var symbol = $"{root}{extension}({string.Join(',', order)})";
                        var parsed = Parse(symbol);

                        Assert.True(parsed.Length > 0, $"{symbol} did not parse");
                        foreach (var alteration in order)
                        {
                            Assert.Contains(rootPitch + AlterationSemitones[alteration], parsed);
                            Assert.DoesNotContain(rootPitch + NaturalSemitones[alteration], parsed);
                        }

                        first ??= parsed;
                        Assert.True(first.SequenceEqual(parsed), $"{symbol} = [{string.Join(", ", parsed)}] but the same alterations written as [{string.Join(", ", subset)}] gave [{string.Join(", ", first)}]");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheReportHearsBothNinthsToo()
    {
        // The second public road to the same pitches: the report spells the notes of each chord.
        var report = ProgressionAdvisor.Analyze(["C7(b9,#9)"]);
        var notes = report.Chords[0].Notes;

        Assert.Contains(notes, n => n is "Db" or "C#");
        Assert.Contains(notes, n => n is "D#" or "Eb");
        Assert.DoesNotContain("D", notes);
    }

    [Fact]
    public void TheShorthandsThatAlterTheFifthStillRead()
    {
        // "alt" is the documented minimal reading, #5 and b9; "ø" is a b5 under a minor seventh.
        // Both write into the same sets the explicit alterations do.
        Assert.Equal([60, 64, 68, 70, 73], Parse("C7alt"));
        Assert.Equal([60, 63, 66, 70], Parse("Cø7"));
        Assert.Equal([60, 63, 66, 70], Parse("Cm7b5"));
        Assert.Equal([60, 63, 66, 70], Parse("Cm7(b5)"));
        Assert.Equal([60, 63, 66, 69], Parse("Cdim7"));
    }

    private static IEnumerable<string[]> Subsets(string[] items, int minSize, int maxSize)
    {
        for (var mask = 1; mask < (1 << items.Length); mask++)
        {
            var subset = Enumerable.Range(0, items.Length).Where(i => (mask & (1 << i)) != 0).Select(i => items[i]).ToArray();
            if (subset.Length >= minSize && subset.Length <= maxSize)
            {
                yield return subset;
            }
        }
    }

    private static IEnumerable<string[]> Permutations(string[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, j) => j != i).ToArray();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }
}
