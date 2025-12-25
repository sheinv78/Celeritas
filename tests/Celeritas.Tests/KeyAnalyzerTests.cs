using Celeritas.Core;

namespace Celeritas.Tests;

public class KeyAnalyzerTests
{
    [Fact]
    public void Analyze_IChordInCMajor_ShouldReturnTonicFunction()
    {
        // Arrange
        int[] pitches = [60, 64, 67]; // C E G = C major
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.I, result.Degree);
        Assert.Equal(ChordQuality.Major, result.Quality);
        Assert.Equal(HarmonicFunction.Tonic, result.Function);
        Assert.Equal("I", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_VChordInCMajor_ShouldReturnDominantFunction()
    {
        // Arrange
        int[] pitches = [67, 71, 74]; // G B D = G major
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.V, result.Degree);
        Assert.Equal(ChordQuality.Major, result.Quality);
        Assert.Equal(HarmonicFunction.Dominant, result.Function);
        Assert.Equal("V", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_V7ChordInCMajor_ShouldReturnDominant7()
    {
        // Arrange
        int[] pitches = [67, 71, 74, 77]; // G B D F = G7
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.V, result.Degree);
        Assert.Equal(ChordQuality.Dominant7, result.Quality);
        Assert.Equal(HarmonicFunction.Dominant, result.Function);
        Assert.Equal("V7", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_IVChordInCMajor_ShouldReturnSubdominant()
    {
        // Arrange
        int[] pitches = [65, 69, 72]; // F A C = F major
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.IV, result.Degree);
        Assert.Equal(HarmonicFunction.Subdominant, result.Function);
        Assert.Equal("IV", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_iiChordInCMajor_ShouldReturnMinorSubdominant()
    {
        // Arrange
        int[] pitches = [62, 65, 69]; // D F A = D minor
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.II, result.Degree);
        Assert.Equal(ChordQuality.Minor, result.Quality);
        Assert.Equal(HarmonicFunction.Subdominant, result.Function);
        Assert.Equal("ii", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_viChordInCMajor_ShouldReturnMinorTonic()
    {
        // Arrange
        int[] pitches = [57, 60, 64]; // A C E = A minor
        var key = new KeySignature("C", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.VI, result.Degree);
        Assert.Equal(ChordQuality.Minor, result.Quality);
        Assert.Equal(HarmonicFunction.Tonic, result.Function);
        Assert.Equal("vi", result.ToRomanNumeral());
    }

    [Fact]
    public void Analyze_IChordInGMajor_ShouldWorkInDifferentKey()
    {
        // Arrange
        int[] pitches = [67, 71, 74]; // G B D = G major
        var key = new KeySignature("G", true);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.I, result.Degree);
        Assert.Equal(HarmonicFunction.Tonic, result.Function);
    }

    [Fact]
    public void Analyze_iChordInAMinor_ShouldReturnMinorTonic()
    {
        // Arrange
        int[] pitches = [57, 60, 64]; // A C E = A minor
        var key = new KeySignature("A", false);

        // Act
        var result = KeyAnalyzer.Analyze(pitches, key);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ScaleDegree.I, result.Degree);
        Assert.Equal(ChordQuality.Minor, result.Quality);
        Assert.Equal(HarmonicFunction.Tonic, result.Function);
        Assert.Equal("i", result.ToRomanNumeral());
    }

    [Fact]
    public void IdentifyKey_CMajorScale_ShouldReturnCMajor()
    {
        // Arrange - C major scale pitches
        int[] pitches = [60, 62, 64, 65, 67, 69, 71]; // C D E F G A B

        // Act
        var key = KeyAnalyzer.IdentifyKey(pitches);

        // Assert
        Assert.Equal(0, key.Root); // C
        Assert.True(key.IsMajor);
    }

    [Fact]
    public void IdentifyKey_AMinorScale_ShouldReturnAMinorOrCMajor()
    {
        // Arrange - A natural minor scale
        int[] pitches = [57, 59, 60, 62, 64, 65, 67]; // A B C D E F G

        // Act
        var key = KeyAnalyzer.IdentifyKey(pitches);

        // Assert - A minor and C major share the same notes (relative keys)
        // Algorithm may return either depending on implementation
        Assert.True((key.Root == 9 && !key.IsMajor) || (key.Root == 0 && key.IsMajor),
            $"Expected A minor or C major, got {key}");
    }

    [Fact]
    public void IdentifyKey_GMajorTriad_ShouldRecognizeKey()
    {
        // Arrange - G major chord
        int[] pitches = [67, 71, 74]; // G B D

        // Act
        var key = KeyAnalyzer.IdentifyKey(pitches);

        // Assert - Should identify as G major or related key
        Assert.True(key.IsMajor);
    }
}
