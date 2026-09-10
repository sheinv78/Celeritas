// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The key of a progression is scored from its chords, and every chord the library can name is
/// evidence about it.
/// <para>
/// The scorer held two hand-written lists — three qualities counted as major, four as minor — and
/// the other twelve of the nineteen scored nothing whatever. A progression coloured with one of
/// them fell through to the tie-break and came out in a key its own chords contradicted: adding a
/// ninth to the tonic turned "Dm G C" from C major into D minor, and suspending it turned
/// "Csus4 Am F G" into G major. Over a corpus of 1668 colourings the detected key moved in 252.
/// </para>
/// </summary>
public class EveryChordIsEvidenceOfAKeyTests
{
    private static readonly string[] Names =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static readonly (int Step, string Suffix)[] MajorDegrees =
        [(0, ""), (2, "m"), (4, "m"), (5, ""), (7, ""), (9, "m"), (11, "dim")];

    private static readonly (int Step, string Suffix)[] MinorDegrees =
        [(0, "m"), (2, "dim"), (3, ""), (5, "m"), (7, "m"), (8, ""), (10, "")];

    /// <summary>Progression shapes as scale degrees: I-IV-V-I, I-vi-IV-V, ii-V-I, vi-IV-I-V.</summary>
    private static readonly int[][] Shapes =
        [[0, 3, 4, 0], [0, 5, 3, 4], [1, 4, 0], [5, 3, 0, 4], [0, 3, 0, 4, 0]];

    /// <summary>
    /// Colourings that keep a chord where it is: suspending it or reducing it to root and fifth
    /// drops the third without replacing it, and adding a ninth to a major triad leaves a major
    /// triad. (<c>add9</c> is itself major, so it replaces a minor chord rather than colouring
    /// one, and is only applied to the major degrees.)
    /// </summary>
    public static TheoryData<string> Colourings => ["sus4", "sus2", "5", "add9"];

    [Theory]
    [MemberData(nameof(Colourings))]
    public void ColouringAChordLeavesTheProgressionInItsKey(string colouring)
    {
        var moved = new List<string>();

        foreach (var isMajor in new[] { true, false })
        {
            var degrees = isMajor ? MajorDegrees : MinorDegrees;
            for (var tonic = 0; tonic < 12; tonic++)
            {
                foreach (var shape in Shapes)
                {
                    var plain = shape
                        .Select(d => Names[(tonic + degrees[d].Step) % 12] + degrees[d].Suffix)
                        .ToArray();
                    var plainKey = ProgressionAdvisor.Analyze(plain).Key;

                    for (var position = 0; position < shape.Length; position++)
                    {
                        if (colouring == "add9" && degrees[shape[position]].Suffix.Length != 0)
                        {
                            continue;
                        }

                        var coloured = (string[])plain.Clone();
                        coloured[position] =
                            Names[(tonic + degrees[shape[position]].Step) % 12] + colouring;
                        if (ProgressionAdvisor.ParseChordSymbol(coloured[position]).Length == 0)
                        {
                            continue;
                        }

                        var key = ProgressionAdvisor.Analyze(coloured).Key;
                        if (key != plainKey)
                        {
                            moved.Add($"{string.Join(" ", plain)} is {plainKey}, "
                                      + $"{string.Join(" ", coloured)} is {key}");
                        }
                    }
                }
            }
        }

        Assert.True(moved.Count == 0, string.Join(Environment.NewLine, moved.Take(10)));
    }

    [Fact]
    public void TheQualitiesLeftOutOfTheScoringAreLeftOutOnPurpose()
    {
        // Diminished and dim7 stay out of both arms, and this records why rather than leaving it
        // to be "fixed" later: a diminished triad is a leading-tone or supertonic chord that sits
        // on no tonic, and a dim7's four rotations are the same four pitch classes, so it belongs
        // to four keys equally. Counting Ddim as C major's supertonic — which a D MINOR triad
        // really is — made "Ddim Gm Cm" evidence for C major.
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Ddim", "Gm", "Cm"]).Key);
        Assert.Equal(new KeySignature(0, false), ProgressionAdvisor.Analyze(["Ddim", "Gm", "Csus4"]).Key);
    }

    [Fact]
    public void EveryQualityIsClassifiedByTheThirdItIsBuiltOn()
    {
        // The scorer asks the chord library which third a quality has, and the library reads that
        // off its own interval templates. If a quality is ever added to the enum without a
        // template, it reports "no third" and would be scored as a suspended chord — evidence for
        // both modes of a root it may not even have. This is the one place that can catch it.
        var unclassified = Enum.GetValues<ChordQuality>()
            .Where(q => q != ChordQuality.Unknown && ChordLibrary.ThirdOf(q) == ChordThird.None)
            .Order()
            .ToArray();

        // Exactly the chords that have no third: the two suspensions, the bare fifth, and the
        // stack of fourths.
        Assert.Equal(
            [ChordQuality.Sus2, ChordQuality.Sus4, ChordQuality.Power, ChordQuality.Quartal],
            unclassified);

        Assert.Equal(ChordThird.Major, ChordLibrary.ThirdOf(ChordQuality.Add9));
        Assert.Equal(ChordThird.Major, ChordLibrary.ThirdOf(ChordQuality.Dominant7Flat5));
        Assert.Equal(ChordThird.Minor, ChordLibrary.ThirdOf(ChordQuality.HalfDim7));
    }

    [Fact]
    public void AChordTheLibraryCannotNameIsStillEvidenceForAKey()
    {
        // There is no ChordQuality for a ninth, eleventh or thirteenth, so Identify answers
        // Unknown for them — but the symbol was parsed, its root is the lowest note and its third
        // is a semitone count away. Reading only the named quality made every one of them
        // evidence for nothing, and SuggestNext(["C#9"]) then advised in C major.
        foreach (var suffix in new[] { "9", "m9", "maj9", "11", "13" })
        {
            var pitches = ProgressionAdvisor.ParseChordSymbol("C" + suffix);
            Assert.NotEmpty(pitches);
            Assert.Equal(ChordQuality.Unknown, ChordAnalyzer.Identify(pitches).Quality);

            // The advice after it is the advice after the same chord in C, transposed — not the
            // advice for whatever key the tie-break happened to fall into.
            var onC = ProgressionAdvisor.SuggestNext(["C" + suffix], 5);
            var onFSharp = ProgressionAdvisor.SuggestNext(["F#" + suffix], 5);

            Assert.Equal(onC.Select(s => s.Reason), onFSharp.Select(s => s.Reason));
            Assert.Equal(
                onC.Select(s => ShapeOf(s.Chord, 0)),
                onFSharp.Select(s => ShapeOf(s.Chord, 6)));
        }
    }

    /// <summary>A chord symbol as the pitch classes it names, relative to a root.</summary>
    private static string ShapeOf(string symbol, int relativeTo)
    {
        var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
        return pitches.Length == 0
            ? "?"
            : string.Join(",", pitches.Select(p => ((p - relativeTo) % 12 + 12) % 12).Distinct().Order());
    }
}
