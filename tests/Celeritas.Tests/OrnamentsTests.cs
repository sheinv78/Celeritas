// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

public class OrnamentsTests
{
    [Fact]
    public void Trill_Expand_CreatesAlternatingNotes()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 2), 0.8f);

        var trill = new Trill
        {
            BaseNote = baseNote,
            Interval = 2, // Whole tone
            Speed = 8, // 8 notes per quarter
            StartWithUpper = false
        };

        // Act
        var expanded = trill.Expand();

        // Assert
        Assert.NotEmpty(expanded);
        Assert.True(expanded.Length >= 4);

        // Check alternation
        for (int i = 0; i < expanded.Length - 1; i++)
        {
            var current = expanded[i].Pitch;
            var next = expanded[i + 1].Pitch;
            Assert.True(current == 60 || current == 62); // C4 or D4
            Assert.NotEqual(current, next); // Should alternate
        }
    }

    [Fact]
    public void Trill_WithTurn_EndsWithTurnPattern()
    {
        // Arrange
        var baseNote = new NoteEvent(64, Rational.Zero, new Rational(1, 2), 0.8f);

        var trill = new Trill
        {
            BaseNote = baseNote,
            Interval = 2,
            Speed = 8,
            EndWithTurn = true
        };

        // Act
        var expanded = trill.Expand();

        // Assert
        Assert.NotEmpty(expanded);
        // Last notes should be lower neighbor + main note
        var lastTwo = expanded.TakeLast(2).ToArray();
        Assert.Equal(62, lastTwo[0].Pitch); // Lower neighbor (D4)
        Assert.Equal(64, lastTwo[1].Pitch); // Main note (E4)
    }

    [Fact]
    public void Mordent_Upper_CreatesThreeNotes()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f);

        var mordent = new Mordent
        {
            BaseNote = baseNote,
            Type = MordentType.Upper,
            Interval = 2,
            Alternations = 1
        };

        // Act
        var expanded = mordent.Expand();

        // Assert
        Assert.Equal(3, expanded.Length);
        Assert.Equal(60, expanded[0].Pitch); // C4
        Assert.Equal(62, expanded[1].Pitch); // D4 (upper neighbor)
        Assert.Equal(60, expanded[2].Pitch); // C4
    }

    [Fact]
    public void Mordent_Lower_CreatesThreeNotes()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f);

        var mordent = new Mordent
        {
            BaseNote = baseNote,
            Type = MordentType.Lower,
            Interval = 2,
            Alternations = 1
        };

        // Act
        var expanded = mordent.Expand();

        // Assert
        Assert.Equal(3, expanded.Length);
        Assert.Equal(60, expanded[0].Pitch); // C4
        Assert.Equal(58, expanded[1].Pitch); // Bb3 (lower neighbor)
        Assert.Equal(60, expanded[2].Pitch); // C4
    }

    [Fact]
    public void Turn_Normal_CreatesFourNotes()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f);

        var turn = new Turn
        {
            BaseNote = baseNote,
            Type = TurnType.Normal,
            UpperInterval = 2,
            LowerInterval = 2
        };

        // Act
        var expanded = turn.Expand();

        // Assert
        Assert.Equal(4, expanded.Length);
        Assert.Equal(62, expanded[0].Pitch); // D4 (upper)
        Assert.Equal(60, expanded[1].Pitch); // C4 (main)
        Assert.Equal(58, expanded[2].Pitch); // Bb3 (lower)
        Assert.Equal(60, expanded[3].Pitch); // C4 (main)
    }

    [Fact]
    public void Turn_Inverted_ReversesPattern()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f);

        var turn = new Turn
        {
            BaseNote = baseNote,
            Type = TurnType.Inverted,
            UpperInterval = 2,
            LowerInterval = 2
        };

        // Act
        var expanded = turn.Expand();

        // Assert
        Assert.Equal(4, expanded.Length);
        Assert.Equal(58, expanded[0].Pitch); // Bb3 (lower)
        Assert.Equal(60, expanded[1].Pitch); // C4 (main)
        Assert.Equal(62, expanded[2].Pitch); // D4 (upper)
        Assert.Equal(60, expanded[3].Pitch); // C4 (main)
    }

    [Fact]
    public void Appoggiatura_Long_TakesHalfDuration()
    {
        // Arrange
        var baseNote = new NoteEvent(60, Rational.Zero, new Rational(1, 2), 0.8f);

        var appoggiatura = new Appoggiatura
        {
            BaseNote = baseNote,
            Type = AppogiaturaType.Long,
            Interval = 2
        };

        // Act
        var expanded = appoggiatura.Expand();

        // Assert
        Assert.Equal(2, expanded.Length);
        Assert.Equal(62, expanded[0].Pitch); // D4 (appoggiatura)
        Assert.Equal(60, expanded[1].Pitch); // C4 (main)
        Assert.Equal(new Rational(1, 4), expanded[0].Duration); // Half of base duration
        Assert.Equal(new Rational(1, 4), expanded[1].Duration);
    }

    [Fact]
    public void OrnamentApplier_AppliesOrnaments()
    {
        // Arrange
        var notes = new[]
        {
            new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f),
            new NoteEvent(64, new Rational(1, 4), new Rational(1, 4), 0.8f),
        };

        var mordent = new Mordent
        {
            BaseNote = notes[0],
            Type = MordentType.Upper,
            Interval = 2,
            Alternations = 1
        };

        var ornaments = new Ornament[] { mordent };

        // Act
        var result = OrnamentApplier.ApplyOrnaments(notes, ornaments);

        // Assert
        Assert.True(result.Length > notes.Length); // Should have more notes after expansion
    }
}
