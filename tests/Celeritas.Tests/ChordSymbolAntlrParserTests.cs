using Xunit;

namespace Celeritas.Tests;

public class ChordSymbolAntlrParserTests
{
    [Theory]
    [InlineData("C", new[] { 60, 64, 67 })]
    [InlineData("Am", new[] { 69, 72, 76 })]
    [InlineData("Am(maj7)", new[] { 69, 72, 76, 80 })]
    [InlineData("Dmaj7", new[] { 62, 66, 69, 73 })]
    [InlineData("C/E", new[] { 52, 60, 67 })]
    [InlineData("C69", new[] { 60, 64, 67, 69, 74 })]
    [InlineData("C7(b9,#11)", new[] { 60, 64, 67, 70, 73, 78 })]
    [InlineData("C7+5", new[] { 60, 64, 68, 70 })]
    [InlineData("C7+9", new[] { 60, 64, 67, 70, 75 })]
    [InlineData("C+7", new[] { 60, 64, 68, 70 })]
    [InlineData("C+9", new[] { 60, 64, 68, 70, 74 })]
    public void ParsePitches_ParsesCommonSymbols(string symbol, int[] expected)
    {
        var actual = Celeritas.Core.Analysis.ProgressionAdvisor.ParseChordSymbol(symbol);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    [Fact]
    public void ParsePitches_ParsesPolychords_WithOctaveStacking()
    {
        // C major at octave 4 plus G major stacked one octave above (octave 5 root base).
        var actual = Celeritas.Core.Analysis.ProgressionAdvisor.ParseChordSymbol("C|G");

        var expected = new[]
        {
            60, 64, 67, // C
            79, 83, 86  // G at root base 72: 72+7, +11, +14
        };

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }
}
