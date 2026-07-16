// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

// Confidence here is a same-root mode margin (see ModeLibrary.ModeMargin / issue #30), not the old
// "how well does it fit" score that reported ~1.0 for anything — a single note included. Modes are
// close neighbours, so an honest margin for a clean full scale is modest (~0.2, in the same band as
// KeyProfiler's own key confidences). These tests assert a positive floor that a full scale clears
// but a single note or bare triad (which sit at 0.0) does not.
public class ModalSystemTests
{
    [Fact]
    public void DetectModeWithRoot_FromNotes_AutomaticRootDetection()
    {
        // Arrange: C Dorian scale (C D Eb F G A Bb)
        var scale = MusicNotation.Parse("C4 D4 Eb4 F4 G4 A4 Bb4");

        // Act: Detect mode without explicit root hint (uses first note)
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale);

        // Assert
        Assert.Equal(0, key.Root);  // C
        Assert.Equal(Mode.Dorian, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_FromNotes_ExplicitRoot()
    {
        // Arrange: D Dorian scale starting from different note
        var scale = MusicNotation.Parse("D4 E4 F4 G4 A4 B4 C5 D5");

        // Act: Detect mode with explicit root
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale, rootHint: 2);  // D = 2

        // Assert
        Assert.Equal(2, key.Root);  // D
        Assert.Equal(Mode.Dorian, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_FromNotes_MixolydianScale()
    {
        // Arrange: G Mixolydian (G A B C D E F)
        var scale = MusicNotation.Parse("G4 A4 B4 C5 D5 E5 F5");

        // Act
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale, rootHint: 7);  // G = 7

        // Assert
        Assert.Equal(7, key.Root);  // G
        Assert.Equal(Mode.Mixolydian, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_FromPitchClasses_Works()
    {
        // Arrange: C Phrygian (C Db Eb F G Ab Bb)
        int[] pitchClasses = [0, 1, 3, 5, 7, 8, 10];

        // Act
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(pitchClasses, rootHint: 0);

        // Assert
        Assert.Equal(0, key.Root);  // C
        Assert.Equal(Mode.Phrygian, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_FromNotes_HarmonicMinor()
    {
        // Arrange: A harmonic minor (A B C D E F G#)
        var scale = MusicNotation.Parse("A4 B4 C5 D5 E5 F5 G#5");

        // Act
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale, rootHint: 9);  // A = 9

        // Assert
        Assert.Equal(9, key.Root);  // A
        Assert.Equal(Mode.HarmonicMinor, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }

    [Fact]
    public void DetectModeWithRoot_EmptyNotes_ThrowsException()
    {
        // Arrange
        var emptyNotes = Array.Empty<NoteEvent>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            ModeLibrary.DetectModeWithRoot(emptyNotes));
    }

    [Fact]
    public void DetectModeWithRoot_WithOctaves_ExtractsPitchClassesCorrectly()
    {
        // Arrange: C Dorian across multiple octaves
        var scale = MusicNotation.Parse("C3 D3 Eb4 F4 G5 A5 Bb6");

        // Act
        var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale);

        // Assert: Should ignore octaves and identify mode correctly
        Assert.Equal(0, key.Root);  // C
        Assert.Equal(Mode.Dorian, key.Mode);
        Assert.True(confidence > 0.1f, $"a full scale should clear the modest margin floor, got {confidence}");
    }
}
