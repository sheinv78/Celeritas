using Celeritas.Core;

namespace Celeritas.Tests;

public class NoteBufferTests
{
    [Fact]
    public void AddNote_ShouldStoreNoteCorrectly()
    {
        // Arrange
        using var buffer = new NoteBuffer(10);

        // Act
        buffer.AddNote(60, Rational.Zero, Rational.Quarter, 0.8f);

        // Assert
        Assert.Equal(1, buffer.Count);
        Assert.Equal(60, buffer.PitchAt(0));
        Assert.Equal(Rational.Zero, buffer.GetOffset(0));
        Assert.Equal(Rational.Quarter, buffer.GetDuration(0));
        Assert.Equal(0.8f, buffer.GetVelocity(0));
    }

    [Fact]
    public void AddNote_ShouldThrowWhenBufferFull()
    {
        // Arrange
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            buffer.AddNote(61, Rational.Zero, Rational.Quarter));
    }

    [Fact]
    public void Clear_ShouldResetCount()
    {
        // Arrange
        using var buffer = new NoteBuffer(10);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(61, Rational.Zero, Rational.Quarter);

        // Act
        buffer.Clear();

        // Assert
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Sort_ShouldOrderNotesByOffset()
    {
        // Arrange
        using var buffer = new NoteBuffer(3);
        buffer.AddNote(60, new Rational(2, 1), Rational.Quarter);
        buffer.AddNote(61, new Rational(0, 1), Rational.Quarter);
        buffer.AddNote(62, new Rational(1, 1), Rational.Quarter);

        // Act
        buffer.Sort();

        // Assert
        Assert.Equal(61, buffer.PitchAt(0)); // offset 0
        Assert.Equal(62, buffer.PitchAt(1)); // offset 1
        Assert.Equal(60, buffer.PitchAt(2)); // offset 2
    }
}

