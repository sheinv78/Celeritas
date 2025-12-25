// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.FiguredBass;

namespace Celeritas.Tests;

public class FiguredBassTests
{
    [Fact]
    public void FiguredBassRealizer_EmptyFigures_CreatesRootPosition()
    {
        // Arrange
        var realizer = new FiguredBassRealizer();
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 48, // C3
            Figures = [],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Act
        var notes = realizer.RealizeSymbol(symbol);

        // Assert
        Assert.Equal(3, notes.Length); // Bass + 3rd + 5th
        Assert.Equal(48, notes[0].Pitch); // C3 (bass)
        Assert.Contains(notes, n => n.Pitch % 12 == 4); // E (3rd)
        Assert.Contains(notes, n => n.Pitch % 12 == 7); // G (5th)
    }

    [Fact]
    public void FiguredBassRealizer_SixFigure_CreatesFirstInversion()
    {
        // Arrange
        var realizer = new FiguredBassRealizer();
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 52, // E3
            Figures = new[] { 6 },
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Act
        var notes = realizer.RealizeSymbol(symbol);

        // Assert
        Assert.Equal(3, notes.Length); // Bass + 3rd + 6th
        Assert.Equal(52, notes[0].Pitch); // E3 (bass)
    }

    [Fact]
    public void FiguredBassRealizer_SixFourFigure_CreatesSecondInversion()
    {
        // Arrange
        var realizer = new FiguredBassRealizer();
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 55, // G3
            Figures = new[] { 6, 4 },
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Act
        var notes = realizer.RealizeSymbol(symbol);

        // Assert
        Assert.Equal(3, notes.Length); // Bass + 4th + 6th
        Assert.Equal(55, notes[0].Pitch); // G3 (bass)
    }

    [Fact]
    public void FiguredBassRealizer_SevenFigure_CreatesSeventhChord()
    {
        // Arrange
        var realizer = new FiguredBassRealizer();
        var symbol = new FiguredBassSymbol
        {
            BassPitch = 55, // G3
            Figures = new[] { 7 },
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Act
        var notes = realizer.RealizeSymbol(symbol);

        // Assert
        Assert.Equal(4, notes.Length); // Bass + 3rd + 5th + 7th
        Assert.Equal(55, notes[0].Pitch); // G3 (bass)
    }

    [Fact]
    public void FiguredBassRealizer_ParseFigures_ParsesCorrectly()
    {
        // Arrange & Act
        var figures1 = FiguredBassRealizer.ParseFigures("6");
        var figures2 = FiguredBassRealizer.ParseFigures("6/4");
        var figures3 = FiguredBassRealizer.ParseFigures("7");
        var figures4 = FiguredBassRealizer.ParseFigures("");

        // Assert
        Assert.Equal(new[] { 6 }, figures1);
        Assert.Equal(new[] { 6, 4 }, figures2);
        Assert.Equal(new[] { 7 }, figures3);
        Assert.Empty(figures4);
    }

    [Fact]
    public void FiguredBassRealizer_MultipleSymbols_CreatesProgression()
    {
        // Arrange
        var realizer = new FiguredBassRealizer();
        var symbols = new[]
        {
            new FiguredBassSymbol
            {
                BassPitch = 48, // C3
                Figures = [],
                Duration = new Rational(1, 4),
                Time = Rational.Zero
            },
            new FiguredBassSymbol
            {
                BassPitch = 55, // G3
                Figures = new[] { 7 },
                Duration = new Rational(1, 4),
                Time = new Rational(1, 4)
            }
        };

        // Act
        var notes = realizer.Realize(symbols);

        // Assert
        Assert.True(notes.Length >= 6); // At least 3 notes per symbol

        // First chord at time 0
        var firstChord = notes.Where(n => n.Offset == Rational.Zero).ToArray();
        Assert.NotEmpty(firstChord);

        // Second chord at time 1/4
        var secondChord = notes.Where(n => n.Offset == new Rational(1, 4)).ToArray();
        Assert.NotEmpty(secondChord);
    }

    [Fact]
    public void FiguredBassOptions_RespectsPitchRange()
    {
        // Arrange
        var options = new FiguredBassOptions
        {
            MinPitch = 60, // C4
            MaxPitch = 72  // C5
        };
        var realizer = new FiguredBassRealizer(options);

        var symbol = new FiguredBassSymbol
        {
            BassPitch = 36, // C2 (low)
            Figures = [],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        // Act
        var notes = realizer.RealizeSymbol(symbol);

        // Assert
        // Upper voices should be in range [60, 72]
        var upperVoices = notes.Skip(1).ToArray(); // Skip bass
        foreach (var note in upperVoices)
        {
            Assert.InRange(note.Pitch, options.MinPitch, options.MaxPitch);
        }
    }
}
