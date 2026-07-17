using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class NashvilleNotationTests
{
    [Theory]
    [InlineData(ScaleDegree.I, ChordQuality.Major, "1")]
    [InlineData(ScaleDegree.Ii, ChordQuality.Minor, "2m")]
    [InlineData(ScaleDegree.Iii, ChordQuality.Minor, "3m")]
    [InlineData(ScaleDegree.Iv, ChordQuality.Major, "4")]
    [InlineData(ScaleDegree.V, ChordQuality.Dominant7, "57")]
    [InlineData(ScaleDegree.Vi, ChordQuality.Minor, "6m")]
    [InlineData(ScaleDegree.Vii, ChordQuality.Diminished, "7°")]
    [InlineData(ScaleDegree.I, ChordQuality.Major7, "1maj7")]
    [InlineData(ScaleDegree.Ii, ChordQuality.Minor7, "2m7")]
    [InlineData(ScaleDegree.Vii, ChordQuality.HalfDim7, "7m7b5")]
    public void ToNashville_FormatsDegreeAndQuality(ScaleDegree degree, ChordQuality quality, string expected)
    {
        var chord = new RomanNumeralChord(degree, quality, HarmonicFunction.Tonic);
        Assert.Equal(expected, chord.ToNashville());
    }

    [Fact]
    public void ToNashville_Invalid_ReturnsQuestionMark()
    {
        Assert.Equal("?", RomanNumeralChord.Invalid.ToNashville());
    }

    [Fact]
    public void Progression_ExposesNashvilleNumbers()
    {
        // A I-IV-V-I in C major reads as 1 4 5 1.
        var report = ProgressionAdvisor.Analyze(["C", "F", "G", "C"]);

        var nashville = report.Chords.Select(c => c.Nashville).ToArray();
        Assert.Equal(["1", "4", "5", "1"], nashville);
    }
}
