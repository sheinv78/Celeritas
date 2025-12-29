using Celeritas.Core;

namespace Celeritas.Tests;

public class CircleOfFifthsTests
{
    [Fact]
    public void PitchClasses_ClockwiseFromC_ShouldMatchStandardOrder_WithSharps()
    {
        var pcs = CircleOfFifths.PitchClasses(PitchClass.C, CircleDirection.Clockwise);
        var names = pcs.Select(p => p.ToName(preferSharps: true)).ToArray();

        Assert.Equal(
            ["C", "G", "D", "A", "E", "B", "F#", "C#", "G#", "D#", "A#", "F"],
            names);
    }

    [Fact]
    public void PitchClasses_CounterClockwiseFromC_ShouldMatchFourthSteps_WithFlats()
    {
        var pcs = CircleOfFifths.PitchClasses(PitchClass.C, CircleDirection.CounterClockwise);
        var names = pcs.Select(p => p.ToName(preferSharps: false)).ToArray();

        Assert.Equal(
            ["C", "F", "Bb", "Eb", "Ab", "Db", "Gb", "B", "E", "A", "D", "G"],
            names);
    }

    [Fact]
    public void MajorChordSymbols_ShouldReturnChordNames()
    {
        var chords = CircleOfFifths.MajorChordSymbols(PitchClass.C, preferSharps: true);
        Assert.Equal("C", chords[0]);
        Assert.Equal("G", chords[1]);
    }

    [Fact]
    public void MinorChordSymbols_ShouldReturnChordNamesWithM()
    {
        var chords = CircleOfFifths.MinorChordSymbols(PitchClass.A, preferSharps: true);
        Assert.Equal("Am", chords[0]);
        Assert.Equal("Em", chords[1]);
    }

    [Fact]
    public void MajorWithRelativeMinors_ShouldPairCorrectly()
    {
        var pairs = CircleOfFifths.MajorWithRelativeMinors(PitchClass.C, preferSharps: true);
        Assert.Equal(("C", "Am"), pairs[0]);
        Assert.Equal(("G", "Em"), pairs[1]);
    }
}
