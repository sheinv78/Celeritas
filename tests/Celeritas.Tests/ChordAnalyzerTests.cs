using Celeritas.Core;

namespace Celeritas.Tests;

public class ChordAnalyzerTests
{
    [Fact]
    public void GetMask_CMajor_ShouldReturnCorrectMask()
    {
        // Arrange
        int[] pitches = [60, 64, 67]; // C, E, G

        // Act
        var mask = ChordAnalyzer.GetMask(pitches);

        // Assert
        // C=bit0, E=bit4, G=bit7
        ushort expected = (1 << 0) | (1 << 4) | (1 << 7);
        Assert.Equal(expected, mask);
    }

    [Fact]
    public void Identify_CMajor_ShouldReturnCorrectChord()
    {
        // Arrange
        int[] pitches = [60, 64, 67]; // C, E, G

        // Act
        var chord = ChordAnalyzer.Identify(pitches);

        // Assert
        Assert.Equal(ChordQuality.Major, chord.Quality);
        Assert.Equal("C", chord.Root);
    }

    [Fact]
    public void Identify_AMinor_ShouldReturnCorrectChord()
    {
        // Arrange
        int[] pitches = [57, 60, 64]; // A, C, E

        // Act
        var chord = ChordAnalyzer.Identify(pitches);

        // Assert
        Assert.Equal(ChordQuality.Minor, chord.Quality);
        Assert.Equal("A", chord.Root);
    }

    [Fact]
    public void Identify_G7_ShouldReturnDominant7()
    {
        // Arrange
        int[] pitches = [55, 59, 62, 65]; // G, B, D, F

        // Act
        var chord = ChordAnalyzer.Identify(pitches);

        // Assert
        Assert.Equal(ChordQuality.Dominant7, chord.Quality);
        Assert.Equal("G", chord.Root);
    }

    [Fact]
    public void Identify_C7b5_ShouldReturnDominant7Flat5()
    {
        // C7♭5: C E Gb Bb
        int[] pitches = [60, 64, 66, 70];

        var chord = ChordAnalyzer.Identify(pitches);

        Assert.Equal(ChordQuality.Dominant7Flat5, chord.Quality);
        Assert.Equal("C", chord.Root);
    }

    [Fact]
    public void Identify_UnknownChord_ShouldReturnUnknown()
    {
        // Arrange
        int[] pitches = [60, 61]; // C, C# - not a standard chord

        // Act
        var chord = ChordAnalyzer.Identify(pitches);

        // Assert
        Assert.Equal(ChordQuality.Unknown, chord.Quality);
    }

    [Fact]
    public void GetMask_WithOctaveDuplication_ShouldHaveSameMask()
    {
        // Arrange
        int[] pitches1 = [60, 64, 67];       // C4, E4, G4
        int[] pitches2 = [60, 64, 67, 72];   // C4, E4, G4, C5

        // Act
        var mask1 = ChordAnalyzer.GetMask(pitches1);
        var mask2 = ChordAnalyzer.GetMask(pitches2);

        // Assert
        Assert.Equal(mask1, mask2); // Octaves should not change the mask
    }
}

