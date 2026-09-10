// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A chord symbol states its own root, so every reading of one must answer on that root, and
/// advice about a progression must be advice about the music rather than about where on the
/// keyboard its pitch classes happen to be numbered.
/// <para>
/// Four entry points built their chords with <c>ChordLibrary.GetChord(ChordAnalyzer.GetMask(…))</c>,
/// the bare lookup that can only ever answer the lowest registered root of a pitch-class set.
/// <see cref="ProgressionAdvisor.Analyze(string[])"/> and the inversion reader were changed away
/// from it; <see cref="ProgressionAdvisor.DetectCadence"/>, <see cref="ProgressionAdvisor.SuggestNext"/>,
/// <see cref="HarmonicColorAnalyzer"/> and <see cref="ModalProgressions"/> were not, so a Csus4
/// was read as an F sus2 and an F#7b5 as a C7b5 — a tritone from the chord the caller wrote.
/// </para>
/// </summary>
public class ChordSymbolNamesItsOwnRootTests
{
    private static readonly string[] Roots =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    /// <summary>Every suffix the chord-symbol parser accepts on any root.</summary>
    public static TheoryData<string> Suffixes =>
    [
        "", "m", "7", "maj7", "m7", "dim", "dim7", "aug", "sus2", "sus4",
        "9", "m9", "maj9", "7b5", "7#5", "add9", "11", "13", "m(maj7)",
    ];

    [Theory]
    [MemberData(nameof(Suffixes))]
    public void ParseChordSymbolPutsTheRootAtTheBottom(string suffix)
    {
        // The reading below depends on this: Identify takes the lowest pitch for the bass, and
        // the bass is what tells a sus4 from the sus2 a fourth above it.
        foreach (var name in Roots)
        {
            var symbol = name + suffix;
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            var root = MusicNotation.ParseNote(name + "4") % 12;
            Assert.Equal(root, ((pitches.Min() % 12) + 12) % 12);
        }
    }

    [Theory]
    [MemberData(nameof(Suffixes))]
    public void IdentifyingAParsedSymbolAnswersOnTheRootTheSymbolNames(string suffix)
    {
        var wrong = new List<string>();
        foreach (var name in Roots)
        {
            var symbol = name + suffix;
            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                continue;
            }

            var identified = ChordAnalyzer.Identify(pitches);
            if (identified.Quality == ChordQuality.Unknown)
            {
                continue;    // a set the library has no name for says nothing about its root
            }

            var root = MusicNotation.ParseNote(name + "4") % 12;
            if (identified.RootPitchClass != root)
            {
                wrong.Add($"{symbol} identified as {identified}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join(", ", wrong));
    }

    [Theory]
    [MemberData(nameof(Suffixes))]
    public void SuggestingTheNextChordGivesTheSameAdviceInEveryKey(string suffix)
    {
        // The bare mask lookup answers from absolute pitch-class numbering, which is not a
        // property of the music: SuggestNext(["Aaug"]) advised in Db and SuggestNext(["A7b5"])
        // in Eb, while the same chords on C advised in C.
        var reference = string.Empty;
        var disagreed = new List<string>();

        for (var i = 0; i < Roots.Length; i++)
        {
            var symbol = Roots[i] + suffix;
            if (ProgressionAdvisor.ParseChordSymbol(symbol).Length == 0)
            {
                continue;
            }

            var root = MusicNotation.ParseNote(Roots[i] + "4") % 12;
            var advice = string.Join(" | ", ProgressionAdvisor
                .SuggestNext([symbol], 5)
                .Select(s => $"{ShapeOf(s.Chord, root)}:{s.Reason}"));

            if (i == 0)
            {
                reference = advice;
            }
            else if (advice != reference)
            {
                disagreed.Add($"C{suffix} -> {reference}; {symbol} -> {advice}");
            }
        }

        Assert.True(disagreed.Count == 0, string.Join(Environment.NewLine, disagreed));
    }

    /// <summary>A suggested chord as the pitch classes it names, relative to the chord advised on.</summary>
    private static string ShapeOf(string symbol, int relativeTo)
    {
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
        return pitches.Length == 0
            ? "?"
            : string.Join(",", pitches.Select(p => ((p - relativeTo) % 12 + 12) % 12).Distinct().Order());
    }

    [Fact]
    public void AdviceAfterASuspendedTonicResolvesToThatTonic()
    {
        // Csus4 read as an F sus2 made the advisor call F "the perfect authentic cadence" after
        // it, while Analyze read the very same input as Isus4 in C major.
        var report = ProgressionAdvisor.Analyze(["Csus4"]);
        Assert.Equal(new KeySignature(0, true), report.Key);

        var suggested = ProgressionAdvisor.SuggestNext(["Csus4"], 5).Select(s => s.Chord).ToArray();
        Assert.DoesNotContain("Bb", suggested);      // the flat seventh of F, foreign to C major
        Assert.Equal(
            ProgressionAdvisor.SuggestNext(["C"], 5).Select(s => s.Chord),
            suggested);
    }

    [Fact]
    public void AdviceAfterAnAlteredDominantStaysInItsOwnKey()
    {
        // A 7b5 maps onto itself a tritone away, so the bare lookup could only answer the
        // lower-numbered of its two roots: F#7b5 came back rooted on C and every suggestion was
        // a tritone from the music.
        Assert.Equal(new KeySignature(6, true), ProgressionAdvisor.Analyze(["F#7b5"]).Key);

        var suggested = ProgressionAdvisor.SuggestNext(["F#7b5"], 5).Select(s => s.Chord).ToArray();
        Assert.DoesNotContain("C", suggested);
        Assert.DoesNotContain("G", suggested);
        Assert.Contains("B", suggested);            // the subdominant of F# major
    }
}
