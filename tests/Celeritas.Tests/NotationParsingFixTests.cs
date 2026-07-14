using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// Regression tests for parser bugs found in the July 2026 review:
/// jazz-minor "-" quality, mmaj7 without parentheses, enharmonic octave carry,
/// and the FormatWithDirectives non-termination.
/// </summary>
public class NotationParsingFixTests
{
    [Theory]
    [InlineData("C-7", new[] { 60, 63, 67, 70 })]    // C minor 7, NOT a B7 on a Cb root
    [InlineData("C-", new[] { 60, 63, 67 })]         // C minor triad
    [InlineData("D-9", new[] { 62, 65, 69, 72, 76 })] // D minor 9
    public void ParsePitches_MinusMeansMinor(string symbol, int[] expected)
    {
        var actual = ChordSymbolAntlrParser.ParsePitches(symbol);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Theory]
    [InlineData("Cmmaj7", new[] { 60, 63, 67, 71 })]  // minor triad + major 7th
    [InlineData("C-maj7", new[] { 60, 63, 67, 71 })]
    [InlineData("Cm(maj7)", new[] { 60, 63, 67, 71 })] // parenthesized form must agree
    public void ParsePitches_MinorMajorSeventh_KeepsMinorThird(string symbol, int[] expected)
    {
        var actual = ChordSymbolAntlrParser.ParsePitches(symbol);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Fact]
    public void ParsePitches_FlatRoot_StillWorks()
    {
        // Root flats use 'b': Bb major
        var actual = ChordSymbolAntlrParser.ParsePitches("Bb");

        Assert.Equal(new[] { 70, 74, 77 }.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Theory]
    [InlineData("Cb4", 59)]  // C-flat 4 = B3, not B4
    [InlineData("B#4", 72)]  // B-sharp 4 = C5, not C4
    [InlineData("Cb0", 11)]
    [InlineData("C#4", 61)]
    [InlineData("Bb3", 58)]
    public void ParseNote_EnharmonicSpellings_LandInCorrectOctave(string notation, int expectedMidi)
    {
        Assert.Equal(expectedMidi, MusicNotation.ParseNote(notation));
    }

    [Theory]
    [InlineData("Cb4/4", 59)]
    [InlineData("B#4/4", 72)]
    public void AntlrParser_EnharmonicSpellings_LandInCorrectOctave(string notation, int expectedMidi)
    {
        var notes = MusicNotation.Parse(notation);

        Assert.Single(notes);
        Assert.Equal(expectedMidi, notes[0].Pitch);
    }

    [Fact]
    public void FormatWithDirectives_DirectiveAfterLastNote_Terminates()
    {
        // Historically looped forever: the directive's time is after the final note ends
        // and nothing advanced the clock.
        var notes = new[] { new NoteEvent(60, Rational.Zero, Rational.Quarter) };
        NotationDirective[] directives =
        [
            new SectionDirective { Label = "coda", Time = new Rational(1, 2) }
        ];

        var result = MusicNotation.FormatWithDirectives(notes, directives);

        Assert.Contains("C4", result);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
