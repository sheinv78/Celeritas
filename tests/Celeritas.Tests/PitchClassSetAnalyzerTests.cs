using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class PitchClassSetAnalyzerTests
{
    [Fact]
    public void MajorTriad_PrimeForm_Is037()
    {
        // C major triad: {0,4,7}
        var result = PitchClassSetAnalyzer.Analyze([60, 64, 67]);
        Assert.Equal(new[] { 0, 3, 7 }, result.PrimeForm);
        Assert.Equal(3, result.Cardinality);
        Assert.Equal(new[] { 0, 4, 7 }, result.PitchClasses);
    }

    [Fact]
    public void ChromaticTetrachord_PrimeForm_Is0123()
    {
        var result = PitchClassSetAnalyzer.Analyze([60, 61, 62, 63]);
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.PrimeForm);
        Assert.Equal(4, result.Cardinality);
    }

    [Fact]
    public void WholeToneHexachord_PrimeForm_Is0246810()
    {
        var result = PitchClassSetAnalyzer.Analyze([60, 62, 64, 66, 68, 70]);
        Assert.Equal(new[] { 0, 2, 4, 6, 8, 10 }, result.PrimeForm);
        Assert.Equal(6, result.Cardinality);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(11)]
    public void PrimeForm_IsTranspositionInvariant(int t)
    {
        // Diatonic collection: {0,2,4,5,7,9,11}
        var basePcs = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var transposed = PitchClassSetAnalyzer.Transpose(basePcs, t);

        var a = PitchClassSetAnalyzer.Analyze(basePcs);
        var b = PitchClassSetAnalyzer.Analyze(transposed);

        Assert.Equal(a.PrimeForm, b.PrimeForm);
        Assert.Equal(a.IntervalVector, b.IntervalVector);
    }
}
