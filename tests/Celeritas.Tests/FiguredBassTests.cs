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
            Figures = [6],
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
            Figures = [6, 4],
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
            Figures = [7],
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
    public void FiguredBassRealizer_NonCKey_VoicesDiatonicIntervalsWithoutOctaveError()
    {
        // Regression: in G major the scale pitch-class array wraps mid-array
        // ([7,9,11,0,2,4,6]); a "6" above the bass D must be B a diatonic sixth
        // (9 semitones) up, not an octave higher.
        var options = new FiguredBassOptions
        {
            Key = new KeySignature(7, true), // G major
            Style = VoiceLeadingStyle.Free,
            MinPitch = 48,
            MaxPitch = 84
        };
        var realizer = new FiguredBassRealizer(options);

        var symbol = new FiguredBassSymbol
        {
            BassPitch = 50, // D3
            Figures = [6],
            Duration = new Rational(1, 4),
            Time = Rational.Zero
        };

        var notes = realizer.RealizeSymbol(symbol);

        // Upper voices: diatonic third (F#) and sixth (B) above D.
        Assert.Equal(50, notes[0].Pitch); // D3 bass
        Assert.Contains(notes, n => n.Pitch % 12 == 6); // F# (diatonic 3rd)
        Assert.Contains(notes, n => n.Pitch % 12 == 11); // B (diatonic 6th)
        // The sixth is B3 (59), a diatonic sixth above D3 (50) — not B4 (71).
        var sixth = notes.First(n => n.Pitch % 12 == 11);
        Assert.Equal(59, sixth.Pitch);
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
        Assert.Equal([6], figures1);
        Assert.Equal([6, 4], figures2);
        Assert.Equal([7], figures3);
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
                Figures = [7],
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

    [Fact]
    public void FiguredBassRealizerOptions_DisallowVoiceCrossing_OrdersUpperVoices()
    {
        var options = new FiguredBassRealizerOptions
        {
            MinPitch = 48,
            MaxPitch = 84,
            AllowVoiceCrossing = false,
            Style = VoiceLeadingStyle.Smooth
        };

        var realizer = new FiguredBassRealizer(options);

        var symbols = new[]
        {
            new FiguredBassSymbol
            {
                BassPitch = 48, // C3
                Figures = [7],
                Duration = new Rational(1, 4),
                Time = Rational.Zero
            },
            new FiguredBassSymbol
            {
                BassPitch = 43, // G2
                Figures = [6, 4],
                Duration = new Rational(1, 4),
                Time = new Rational(1, 4)
            }
        };

        var notes = realizer.Realize(symbols);

        foreach (var t in symbols.Select(s => s.Time))
        {
            var chord = notes.Where(n => n.Offset == t).OrderBy(n => n.Pitch).ToArray();
            Assert.True(chord.Length >= 3);

            var upper = chord.Skip(1).Select(n => n.Pitch).ToArray();
            var sorted = upper.OrderBy(x => x).ToArray();
            Assert.Equal(sorted, upper);
        }
    }

    [Fact]
    public void FiguredBassRealizerOptions_MaxVoiceMovement_FallsBackToClosestWhenImpossible()
    {
        // MaxVoiceMovement is a soft constraint: an unreachable limit (0 semitones across
        // a chord change) must not abort the realization mid-progression; each voice
        // falls back to the octave placement closest to its previous pitch.
        var options = new FiguredBassRealizerOptions
        {
            MinPitch = 60,
            MaxPitch = 72,
            AllowVoiceCrossing = false,
            MaxVoiceMovement = 0,
            Style = VoiceLeadingStyle.Strict
        };

        var realizer = new FiguredBassRealizer(options);

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
                BassPitch = 50, // D3 (forces upper voices to change pitch class)
                Figures = [],
                Duration = new Rational(1, 4),
                Time = new Rational(1, 4)
            }
        };

        var notes = realizer.Realize(symbols);

        // Both chords realize fully: bass + 2 upper voices each.
        var second = notes.Where(n => n.Offset == new Rational(1, 4)).ToArray();
        Assert.Equal(3, second.Length);
        Assert.Equal(50, second[0].Pitch);

        // Upper voices stay above the bass, carry the D-minor pitch classes (F, A),
        // and sit at the octave closest to the previous chord's voices (E4/G4 -> F4/A4).
        Assert.Equal([65, 69], second.Skip(1).Select(n => n.Pitch).ToArray());
    }
}
