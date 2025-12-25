using Celeritas.Core;

namespace Celeritas.Tests;

public class MusicMathTests
{
    [Fact]
    public void Transpose_ShouldTransposeAllNotesCorrectly()
    {
        // Arrange
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);    // C4
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter); // E4
        buffer.AddNote(67, Rational.Half, Rational.Quarter);    // G4

        // Act
        MusicMath.Transpose(buffer, 2); // Transpose up 2 semitones

        // Assert
        Assert.Equal(62, buffer.PitchAt(0)); // D4
        Assert.Equal(66, buffer.PitchAt(1)); // F#4
        Assert.Equal(69, buffer.PitchAt(2)); // A4
    }

    [Fact]
    public void Transpose_NegativeSemitones_ShouldTransposeDown()
    {
        // Arrange
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);    // C4
        buffer.AddNote(67, Rational.Quarter, Rational.Quarter); // G4

        // Act
        MusicMath.Transpose(buffer, -5); // Transpose down 5 semitones

        // Assert
        Assert.Equal(55, buffer.PitchAt(0)); // G3
        Assert.Equal(62, buffer.PitchAt(1)); // D4
    }

    [Fact]
    public void Transpose_LargeBuffer_ShouldHandleCorrectly()
    {
        // Arrange
        using var buffer = new NoteBuffer(100);
        for (var i = 0; i < 100; i++)
        {
            buffer.AddNote(60, new Rational(i, 4), Rational.Quarter);
        }

        // Act
        MusicMath.Transpose(buffer, 12); // Transpose up one octave

        // Assert
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(72, buffer.PitchAt(i));
        }
    }

    [Fact]
    public void ScaleVelocity_ShouldMultiplyAllVelocities()
    {
        // Arrange
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.5f);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter, 0.8f);
        buffer.AddNote(67, Rational.Half, Rational.Quarter, 1.0f);

        // Act
        MusicMath.ScaleVelocity(buffer, 0.5f); // Halve all velocities

        // Assert
        Assert.Equal(0.25f, buffer.GetVelocity(0), precision: 5);
        Assert.Equal(0.4f, buffer.GetVelocity(1), precision: 5);
        Assert.Equal(0.5f, buffer.GetVelocity(2), precision: 5);
    }

    [Fact]
    public void ScaleVelocity_WithZeroFactor_ShouldSetAllToZero()
    {
        // Arrange
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.8f);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter, 0.9f);

        // Act
        MusicMath.ScaleVelocity(buffer, 0.0f);

        // Assert
        Assert.Equal(0.0f, buffer.GetVelocity(0));
        Assert.Equal(0.0f, buffer.GetVelocity(1));
    }

    [Fact]
    public void ScaleVelocity_LargeBuffer_ShouldHandleCorrectly()
    {
        // Arrange
        using var buffer = new NoteBuffer(100);
        for (var i = 0; i < 100; i++)
        {
            buffer.AddNote(60, new Rational(i, 4), Rational.Quarter, 1.0f);
        }

        // Act
        MusicMath.ScaleVelocity(buffer, 0.75f);

        // Assert
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(0.75f, buffer.GetVelocity(i), precision: 5);
        }
    }

    [Fact]
    public void Quantize_ShouldSnapToGrid()
    {
        // Arrange
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, new Rational(1, 17), Rational.Quarter); // Slightly off
        buffer.AddNote(64, new Rational(3, 7), Rational.Quarter);  // Off beat
        buffer.AddNote(67, new Rational(1, 2), Rational.Quarter);  // On beat

        // Act
        MusicMath.Quantize(buffer, Rational.Eighth); // Quantize to 8th note grid

        // Assert
        // After quantization, offsets should be multiples of 1/8
        var offset0 = buffer.GetOffset(0);
        var offset2 = buffer.GetOffset(2);

        // Check that offsets are on the grid (denominator should be 8 or simplify to valid value)
        Assert.True(offset0.Denominator == 8 || offset0.Numerator == 0);
        Assert.True(offset2.Denominator == 8 || offset2.Denominator == 2);
    }

    [Fact]
    public void Quantize_ToQuarterNote_ShouldAlignToQuarters()
    {
        // Arrange
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, new Rational(1, 16), Rational.Quarter); // Close to 0
        buffer.AddNote(64, new Rational(3, 16), Rational.Quarter); // Close to 1/4
        buffer.AddNote(67, new Rational(9, 16), Rational.Quarter); // Close to 1/2
        buffer.AddNote(71, new Rational(13, 16), Rational.Quarter); // Close to 3/4

        // Act
        MusicMath.Quantize(buffer, Rational.Quarter);

        // Assert
        // Check that all offsets are quantized to quarter note grid (multiples of 1/4)
        var offset0 = buffer.GetOffset(0);
        var offset1 = buffer.GetOffset(1);
        var offset2 = buffer.GetOffset(2);
        var offset3 = buffer.GetOffset(3);

        // Verify offsets are correct (auto-normalized)
        Assert.Equal(new Rational(0, 1), offset0); // 0/4 -> 0/1
        Assert.Equal(new Rational(1, 4), offset1); // 1/4
        Assert.Equal(new Rational(1, 2), offset2); // 2/4 -> 1/2
        Assert.Equal(new Rational(3, 4), offset3); // 3/4
    }
}
