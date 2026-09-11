// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core.FiguredBass;

namespace Celeritas.Tests;

/// <summary>
/// The figured-bass text readers were lenient and lossy: whatever they could not read they
/// silently dropped, and the bass then realized as a chord the figures did not ask for, with no
/// error to say so. "6#" — the postfix form older editions use — lost its sharp; "b10" put its
/// flat on a figure 1 that does not exist; a bare "#", which every reader of figured bass knows
/// as a raised third, altered nothing; and "6 4", written with a space as it is on the page,
/// became the single figure 64 and realized a note five octaves above the bass.
/// </summary>
public class FiguredBassTextMeansWhatAMusicianWritesTests
{
    [Theory]
    [InlineData("6", new[] { 6 })]
    [InlineData("6/4", new[] { 6, 4 })]
    [InlineData("6 4", new[] { 6, 4 })]
    [InlineData("6,4", new[] { 6, 4 })]
    [InlineData("6-4", new[] { 6, 4 })]
    [InlineData(" 6 / 4 ", new[] { 6, 4 })]
    [InlineData("7", new[] { 7 })]
    [InlineData("6/5", new[] { 6, 5 })]
    [InlineData("4/3", new[] { 4, 3 })]
    [InlineData("4/2", new[] { 4, 2 })]
    [InlineData("7/5/3", new[] { 7, 5, 3 })]
    [InlineData("10", new[] { 10 })]
    [InlineData("b13", new[] { 13 })]
    [InlineData("", new int[0])]
    [InlineData("   ", new int[0])]
    public void TheFiguresAreTheOnesWritten(string text, int[] expected)
    {
        Assert.Equal(expected, FiguredBassRealizer.ParseFigures(text));
    }

    [Theory]
    [InlineData("#6", 6, '#')]
    [InlineData("6#", 6, '#')]
    [InlineData("b6", 6, 'b')]
    [InlineData("6b", 6, 'b')]
    [InlineData("n6", 6, 'n')]
    [InlineData("6n", 6, 'n')]
    [InlineData("#4", 4, '#')]
    [InlineData("4#", 4, '#')]
    [InlineData("+6", 6, '#')]
    [InlineData("6+", 6, '#')]
    [InlineData("4+", 4, '#')]
    [InlineData("b7", 7, 'b')]
    [InlineData("7b", 7, 'b')]
    [InlineData("b10", 10, 'b')]
    [InlineData("#9", 9, '#')]
    public void AnAccidentalBelongsToItsFigureWhicheverSideItIsWrittenOn(string text, int figure, char accidental)
    {
        var accidentals = FiguredBassRealizer.ParseAccidentals(text);

        Assert.Equal(accidental, Assert.Single(accidentals).Value);
        Assert.Equal(figure, Assert.Single(accidentals).Key);
    }

    [Theory]
    [InlineData("#", '#')]
    [InlineData("b", 'b')]
    [InlineData("n", 'n')]
    public void ABareAccidentalAltersTheThird(string text, char accidental)
    {
        Assert.Equal([3], FiguredBassRealizer.ParseFigures(text));
        Assert.Equal(accidental, FiguredBassRealizer.ParseAccidentals(text)[3]);
    }

    [Fact]
    public void TheTwoReadersNeverDisagreeAboutWhichFigureAnAccidentalIsOn()
    {
        // ParseFigures and ParseAccidentals read the same tokens now. Every figure an accidental
        // is reported on must be a figure the other reader returns.
        foreach (var text in new[]
                 {
                     "#6", "6#", "b10", "#", "6/#5", "#6/5", "4/#3", "#4/3", "6+/4", "b13/#9",
                     "n3", "7b/5#/3n", "6 #4 2",
                 })
        {
            var figures = FiguredBassRealizer.ParseFigures(text);
            foreach (var (figure, _) in FiguredBassRealizer.ParseAccidentals(text))
            {
                Assert.Contains(figure, figures);
            }
        }
    }

    [Theory]
    [InlineData("", new[] { 7, 10, 2 })]        // G Bb D: the unfigured dominant of C minor
    [InlineData("#", new[] { 7, 11, 2 })]       // G B D: the bare sharp raises the third
    [InlineData("#3", new[] { 7, 11, 2 })]
    [InlineData("3", new[] { 7, 10, 2 })]       // one figure of the triad names the whole 5/3
    [InlineData("5", new[] { 7, 10, 2 })]
    [InlineData("5/3", new[] { 7, 10, 2 })]
    [InlineData("3/5", new[] { 7, 10, 2 })]
    public void OneFigureOfATriadRealizesTheWholeTriad(string figures, int[] expectedPitchClasses)
    {
        // The reader learned that a bare accidental means "#3"; the realizer had no entry for a
        // lone 3 and built the one interval it was handed, so the dominant of every minor-key
        // cadence written the historical way came out as a two-note chord with no fifth.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new Celeritas.Core.KeySignature(0, false) });
        var realized = realizer.Realize(
        [
            new FiguredBassSymbol
            {
                BassPitch = 43,
                Figures = FiguredBassRealizer.ParseFigures(figures),
                Accidentals = FiguredBassRealizer.ParseAccidentals(figures),
                Time = Celeritas.Core.Rational.Zero,
                Duration = Celeritas.Core.Rational.Quarter,
            },
        ]);

        Assert.Equal(expectedPitchClasses.Order(), realized.Select(n => n.Pitch % 12).Distinct().Order());
    }

    [Fact]
    public void ARaisedSixthRealizesRaisedWhicheverWayItIsWritten()
    {
        // The whole point: the accidental reaches the notes. Over C, a raised sixth is A sharp.
        var realizer = new FiguredBassRealizer();

        int[] Realize(string figures) =>
            [.. realizer.Realize(
                [
                    new FiguredBassSymbol
                    {
                        BassPitch = 48,
                        Figures = FiguredBassRealizer.ParseFigures(figures),
                        Accidentals = FiguredBassRealizer.ParseAccidentals(figures),
                        Time = Celeritas.Core.Rational.Zero,
                        Duration = Celeritas.Core.Rational.Quarter,
                    },
                ]).Select(n => n.Pitch % 12).Order()];

        var prefix = Realize("#6");
        var postfix = Realize("6#");
        var plus = Realize("6+");

        Assert.Equal(prefix, postfix);
        Assert.Equal(prefix, plus);
        Assert.Contains(10, prefix);                 // A sharp
        Assert.DoesNotContain(9, prefix);            // and no A natural
    }
}
