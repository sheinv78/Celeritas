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

    /// <summary>
    /// Given the exact notes of a mode and told where its root is, detection must name that mode.
    /// It could not name eight of them: the candidate list held only the diatonic modes plus
    /// harmonic and melodic minor, so C Phrygian Dominant came back as C Phrygian — a mode without
    /// the major third that defines the scale — at a confidence this detector treats as certain.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryModeAndRoot))]
    public void AModeIsFoundFromItsOwnScale(Mode mode, int root)
    {
        var scale = ModeLibrary.GetScaleNotes(new ModalKey((byte)root, mode));

        var (key, _) = ModeLibrary.DetectModeWithRoot(scale, root);

        Assert.Equal(root, key.Root);
        Assert.Equal(mode, key.Mode);
    }

    /// <summary>
    /// The two pentatonics are the exception, and deliberately so: each is contained in modes the
    /// detector already offers, and the score rewards containing the notes played, so a contained
    /// scale can only tie with its container. They come back as that container with a confidence
    /// of zero, which says "this fits several modes equally" rather than picking one and meaning it.
    /// </summary>
    [Theory]
    [InlineData(Mode.MajorPentatonic)]
    [InlineData(Mode.MinorPentatonic)]
    public void APentatonicIsReportedAsAModeThatContainsItWithNoConfidence(Mode pentatonic)
    {
        for (var root = 0; root < 12; root++)
        {
            var scale = ModeLibrary.GetScaleNotes(new ModalKey((byte)root, pentatonic));

            var (key, confidence) = ModeLibrary.DetectModeWithRoot(scale, root);

            Assert.Equal(root, key.Root);
            Assert.Equal(0f, confidence);
            Assert.All(scale, pitchClass =>
                Assert.True(
                    ModeLibrary.ContainsPitch(key, ((pitchClass % 12) + 12) % 12),
                    $"{pentatonic} on {root} was answered {key}, which lacks {pitchClass}"));
        }
    }

    /// <summary>
    /// The exact answer the documentation of <see cref="Mode.MajorPentatonic"/>,
    /// <see cref="Mode.MinorPentatonic"/>, <see cref="ModeLibrary.DetectMode"/> and
    /// <see cref="ModeLibrary.DetectModeWithRoot(float[], int)"/> promises for a pentatonic, on
    /// every root. Told the root, the detector answers Ionian for a major pentatonic and Dorian
    /// for a minor one, on that root. Left to find the root, it answers Ionian on the
    /// pentatonic's own root when that root is the most prominent note — Aeolian there for a
    /// minor pentatonic — and, when no note stands out, Ionian on the lowest-numbered pitch
    /// class of the three major keys that contain the five notes. All at confidence 0.
    /// </summary>
    [Theory]
    [InlineData(Mode.MajorPentatonic, Mode.Ionian, Mode.Ionian, 0)]
    [InlineData(Mode.MinorPentatonic, Mode.Dorian, Mode.Aeolian, 3)]
    public void APentatonicIsAnsweredAsItsDocumentationSays(
        Mode pentatonic, Mode withRootHint, Mode withProminentRoot, int semitonesToRelativeMajor)
    {
        for (var root = 0; root < 12; root++)
        {
            var scale = ModeLibrary.GetScaleNotes(new ModalKey((byte)root, pentatonic));
            var even = new float[12];
            foreach (var pitchClass in scale)
            {
                even[pitchClass] = 1f;
            }

            // Told the root, through either overload.
            var expectedHinted = new ModalKey((byte)root, withRootHint);
            Assert.Equal((expectedHinted, 0f), ModeLibrary.DetectModeWithRoot(even, root));
            Assert.Equal((expectedHinted, 0f), ModeLibrary.DetectModeWithRoot(scale, root));

            // The root is the most prominent note.
            var rootHeavy = (float[])even.Clone();
            rootHeavy[root] = 3f;
            Assert.Equal((new ModalKey((byte)root, withProminentRoot), 0f), ModeLibrary.DetectMode(rootHeavy));

            // No note stands out: the lowest-numbered of the three major keys holding the five
            // notes — the pentatonic's relative major and that key's subdominant and dominant.
            var relativeMajor = (root + semitonesToRelativeMajor) % 12;
            var lowestMajorKey = Math.Min(relativeMajor, Math.Min((relativeMajor + 5) % 12, (relativeMajor + 7) % 12));
            Assert.Equal((new ModalKey((byte)lowestMajorKey, Mode.Ionian), 0f), ModeLibrary.DetectMode(even));
        }
    }

    /// <summary>
    /// Every diatonic mode is a rotation of a major scale, so its relative major must be built on
    /// the same notes. Lydian and Mixolydian were missing from the table and fell through to a
    /// default that returns the mode's own root, so C Lydian claimed C major — which differs by
    /// the F sharp that makes it Lydian — and the answer was silently the parallel major.
    /// </summary>
    [Theory]
    [InlineData(Mode.Ionian)]
    [InlineData(Mode.Dorian)]
    [InlineData(Mode.Phrygian)]
    [InlineData(Mode.Lydian)]
    [InlineData(Mode.Mixolydian)]
    [InlineData(Mode.Aeolian)]
    [InlineData(Mode.Locrian)]
    public void ADiatonicModeAndItsRelativeMajorAreBuiltOnTheSameNotes(Mode mode)
    {
        for (var root = 0; root < 12; root++)
        {
            var key = new ModalKey((byte)root, mode);

            var relative = key.RelativeMajor;

            Assert.Equal(Mode.Ionian, relative.Mode);
            Assert.Equal(
                ModeLibrary.GetScaleMask(key),
                ModeLibrary.GetScaleMask(relative));
        }
    }

    public static TheoryData<Mode, int> EveryModeAndRoot()
    {
        var data = new TheoryData<Mode, int>();
        foreach (var mode in Enum.GetValues<Mode>())
        {
            if (mode is Mode.MajorPentatonic or Mode.MinorPentatonic)
            {
                continue;
            }

            for (var root = 0; root < 12; root++)
            {
                data.Add(mode, root);
            }
        }

        return data;
    }
}
