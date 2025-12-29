using Celeritas.Core;

namespace Celeritas.Tests;

public class FunctionalProgressionsTests
{
    [Fact]
    public void TwoFiveOne_CMajor_Sevenths_ShouldMatchCommonSymbols()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var prog = FunctionalProgressions.TwoFiveOne(key, DiatonicChordType.Seventh);

        Assert.Equal(["Dm7", "G7", "Cmaj7"], prog.Select(c => c.Symbol(preferSharps: true)).ToArray());
        Assert.Equal(["ii7", "V7", "Imaj7"], prog.Select(c => c.RomanNumeral).ToArray());
    }

    [Fact]
    public void TwoFiveOne_AMinor_Sevenths_HarmonicDominant_ShouldUseE7()
    {
        var key = new KeySignature(PitchClass.A.Value, isMajor: false);
        var prog = FunctionalProgressions.TwoFiveOne(key, DiatonicChordType.Seventh, minorDominant: MinorDominantStyle.Harmonic);

        Assert.Equal(["Bm7b5", "E7", "Am7"], prog.Select(c => c.Symbol(preferSharps: true)).ToArray());
        Assert.Equal(["iiø7", "V7", "i7"], prog.Select(c => c.RomanNumeral).ToArray());
    }

    [Fact]
    public void Circle_CMajor_Triads_ShouldStartOnIAndEndOnI()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var prog = FunctionalProgressions.Circle(key, DiatonicChordType.Triad);

        Assert.Equal("C", prog[0].Symbol(preferSharps: true));
        Assert.Equal("C", prog[^1].Symbol(preferSharps: true));
        Assert.Equal(8, prog.Length);
    }

    [Fact]
    public void Turnaround_CMajor_Sevenths_ShouldMatchIviIIVI()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var prog = FunctionalProgressions.Turnaround(key, DiatonicChordType.Seventh);

        Assert.Equal(["Cmaj7", "Am7", "Dm7", "G7", "Cmaj7"], prog.Select(c => c.Symbol(preferSharps: true)).ToArray());
    }

    [Fact]
    public void ThreeSixTwoFiveOne_CMajor_Sevenths_ShouldMatchCommonJazzChain()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var prog = FunctionalProgressions.ThreeSixTwoFiveOne(key, DiatonicChordType.Seventh);

        Assert.Equal(["Em7", "Am7", "Dm7", "G7", "Cmaj7"], prog.Select(c => c.Symbol(preferSharps: true)).ToArray());
        Assert.Equal(["iii7", "vi7", "ii7", "V7", "Imaj7"], prog.Select(c => c.RomanNumeral).ToArray());
    }

    [Fact]
    public void SecondaryDominantTo_II_InCMajor_ShouldBeA7()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var sd = FunctionalProgressions.SecondaryDominantTo(key, ScaleDegree.II, DiatonicChordType.Seventh);

        Assert.Equal("A7", sd.Symbol(preferSharps: true));
        Assert.Equal("V7/ii", sd.RomanNumeral);
    }

    [Fact]
    public void SecondaryDominantTo_V_InCMajor_ShouldBeD7()
    {
        var key = new KeySignature(PitchClass.C.Value, isMajor: true);
        var sd = FunctionalProgressions.SecondaryDominantTo(key, ScaleDegree.V, DiatonicChordType.Seventh);

        Assert.Equal("D7", sd.Symbol(preferSharps: true));
        Assert.Equal("V7/v", sd.RomanNumeral);
    }
}
