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
        Assert.Equal(ScaleDegree.Iv, result.Degree);
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
        Assert.Equal(ScaleDegree.Ii, result.Degree);
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
        Assert.Equal(ScaleDegree.Vi, result.Degree);
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

    // A bare pitch-class set cannot distinguish a key from its relative major/minor
    // (identical pitch content), so either answer is correct. What these tests pin down
    // is the rotation direction: the detected key's scale must contain exactly the input
    // pitch classes — the historical RotateRight bug returned keys with a different set
    // (e.g. "D minor" for a D-major scale).
    [Theory]
    [InlineData(new[] { 62, 64, 66, 67, 69, 71, 73 }, 2, 11)]  // D major scale -> D major or B minor
    [InlineData(new[] { 67, 69, 71, 72, 74, 76, 78 }, 7, 4)]   // G major scale -> G major or E minor
    [InlineData(new[] { 63, 65, 67, 68, 70, 72, 74 }, 3, 0)]   // Eb major scale -> Eb major or C minor
    [InlineData(new[] { 66, 68, 70, 71, 73, 75, 77 }, 6, 3)]   // F# major scale -> F# major or Eb minor
    public void IdentifyKey_NonCMajorScales_ShouldReturnKeyWithMatchingScale(
        int[] pitches, int majorRoot, int relativeMinorRoot)
    {
        var key = KeyAnalyzer.IdentifyKey(pitches);

        Assert.True((key.Root == majorRoot && key.IsMajor) || (key.Root == relativeMinorRoot && !key.IsMajor),
            $"Expected {majorRoot} major or its relative {relativeMinorRoot} minor, got {key}");

        var inputMask = ChordAnalyzer.GetMask(pitches);
        Assert.Equal(inputMask, KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor));
    }

    [Theory]
    [InlineData(new[] { 64, 66, 67, 69, 71, 72, 74 }, 4, 7)]   // E natural minor scale -> E minor or G major
    [InlineData(new[] { 62, 64, 65, 67, 69, 70, 72 }, 2, 5)]   // D natural minor scale -> D minor or F major
    public void IdentifyKey_NonAMinorScales_ShouldReturnKeyWithMatchingScale(
        int[] pitches, int minorRoot, int relativeMajorRoot)
    {
        var key = KeyAnalyzer.IdentifyKey(pitches);

        Assert.True((key.Root == minorRoot && !key.IsMajor) || (key.Root == relativeMajorRoot && key.IsMajor),
            $"Expected {minorRoot} minor or its relative {relativeMajorRoot} major, got {key}");

        var inputMask = ChordAnalyzer.GetMask(pitches);
        Assert.Equal(inputMask, KeyAnalyzer.GetScaleMask(key.Root, key.IsMajor));
    }

    [Fact]
    public void GetScaleMask_AgreesWithKeySignature_ForAll24Keys()
    {
        for (var root = 0; root < 12; root++)
        {
            foreach (var isMajor in new[] { true, false })
            {
                var expected = new KeySignature((byte)root, isMajor).GetScaleMask();
                var actual = KeyAnalyzer.GetScaleMask(root, isMajor);
                Assert.True(expected == actual,
                    $"Scale mask mismatch for root={root}, isMajor={isMajor}: KeySignature={expected:B12}, KeyAnalyzer={actual:B12}");
            }
        }
    }
}
