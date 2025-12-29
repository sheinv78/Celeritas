using Celeritas.Core;

namespace Celeritas.Tests;

public class PitchClassArithmeticTests
{
    [Fact]
    public void PitchClass_ShouldWrapModulo12_OnAddition()
    {
        var b = new PitchClass(11);
        Assert.Equal((byte)0, (b + 1).Value);
    }

    [Fact]
    public void PitchClass_ShouldWrapModulo12_OnSubtraction()
    {
        var c = new PitchClass(0);
        Assert.Equal((byte)11, (c - 1).Value);
    }

    [Fact]
    public void PitchClass_Difference_ShouldReturnAscendingIntervalClass()
    {
        var c = new PitchClass(0);
        var d = new PitchClass(2);

        var interval = d - c;

        Assert.Equal(2, interval.Semitones);
        Assert.Equal("M2", interval.SimpleName);
        Assert.Equal(2, interval.GenericNumber);
    }

    [Fact]
    public void PitchClass_Difference_ShouldWrapAcrossOctave()
    {
        var b = new PitchClass(11);
        var c = new PitchClass(0);

        var interval = c - b;

        Assert.Equal(1, interval.Semitones);
        Assert.Equal("m2", interval.SimpleName);
        Assert.Equal(2, interval.GenericNumber);
    }

    [Fact]
    public void PitchClass_SignedIntervalTo_ShouldPreferShortestDirection()
    {
        var c = new PitchClass(0);
        var b = new PitchClass(11);

        var interval = c.SignedIntervalTo(b);

        Assert.Equal(-1, interval.Semitones);
        Assert.Equal("m2", interval.SimpleName);
    }

    [Fact]
    public void PitchClass_SignedIntervalTo_Tritone_ShouldBePositiveSix()
    {
        var c = new PitchClass(0);
        var fs = new PitchClass(6);

        var interval = c.SignedIntervalTo(fs);

        Assert.Equal(6, interval.Semitones);
        Assert.Equal("TT", interval.SimpleName);
    }

    [Fact]
    public void PitchClass_AddInterval_ShouldTransposeToNewPitchClass()
    {
        var c = new PitchClass(0);
        var e = c + ChromaticInterval.MajorThird;

        Assert.Equal((byte)4, e.Value);
        Assert.Equal("E", e.Name);
    }

    [Fact]
    public void MidiPitch_Transpose_ShouldWorkWithIntervals()
    {
        var c4 = MusicNotation.ParseNote("C4");
        var e4 = c4.Transpose(ChromaticInterval.MajorThird);

        Assert.Equal(MusicNotation.ParseNote("E4"), e4);
    }

    [Fact]
    public void MidiPitch_IntervalTo_ShouldReturnSignedSemitones()
    {
        var c4 = MusicNotation.ParseNote("C4");
        var a3 = MusicNotation.ParseNote("A3");

        var interval = c4.IntervalTo(a3);

        Assert.Equal(-3, interval.Semitones); // C4 -> A3 is down a minor 3rd
        Assert.Equal("m3", interval.SimpleName);
    }
}
