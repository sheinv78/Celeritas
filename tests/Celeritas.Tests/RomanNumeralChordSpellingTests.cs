using Celeritas.Core;
using Xunit;

namespace Celeritas.Tests;

public sealed class RomanNumeralChordSpellingTests
{
    [Fact]
    public void GetPitchClasses_CMajor_V7_IsSpelledCorrectly()
    {
        var key = new KeySignature("C", isMajor: true);
        var roman = new RomanNumeralChord(ScaleDegree.V, ChordQuality.Dominant7, HarmonicFunction.Dominant);

        var pcs = roman.GetPitchClasses(key);

        Assert.Equal(new byte[] { 7, 11, 2, 5 }, pcs); // G B D F
        Assert.Equal((ushort)((1 << 7) | (1 << 11) | (1 << 2) | (1 << 5)), roman.GetPitchClassMask(key));
    }

    [Fact]
    public void GetRootPitchClass_AMinor_V_IsE()
    {
        var key = new KeySignature("A", isMajor: false);
        var roman = new RomanNumeralChord(ScaleDegree.V, ChordQuality.Major, HarmonicFunction.Dominant);

        Assert.Equal((byte)4, roman.GetRootPitchClass(key)); // E
        Assert.Equal(new byte[] { 4, 8, 11 }, roman.GetPitchClasses(key)); // E G# B (spelled by quality)
    }

    [Fact]
    public void KeySignature_GetScaleMask_CMajor_MatchesAnalyzerConstant()
    {
        var key = new KeySignature("C", isMajor: true);
        Assert.Equal(KeyAnalyzer.MajorScaleMask, key.GetScaleMask());
    }
}
