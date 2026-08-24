// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// Articulation was 11.9% covered — only its argument guard was tested — while this release
/// changed how it converts its multiplier to a duration. Changed and untested is the worst
/// pairing, so these cover what it produces, not just what it rejects.
/// </summary>
public class ArticulationTests
{
    private static NoteEvent Quarter(float velocity = 0.8f) =>
        new(60, Rational.Zero, Rational.Quarter, velocity);

    // ---------- the rounding fix this release made ----------

    [Theory]
    [InlineData(0.5f, 1, 2)]
    [InlineData(0.25f, 1, 4)]
    [InlineData(0.7f, 7, 10)]     // 0.7f is stored as 0.69999998; truncation gave 69/100
    [InlineData(0.9f, 9, 10)]
    [InlineData(1.0f, 1, 1)]
    [InlineData(1.1f, 11, 10)]
    public void Expand_ScalesDurationByTheExactRatio_NotATruncatedOne(
        float multiplier, int expectedNum, int expectedDen)
    {
        var note = Quarter();
        var articulation = new Articulation { BaseNote = note, DurationMultiplier = multiplier };

        var expanded = articulation.Expand();

        Assert.Single(expanded);
        Assert.Equal(note.Duration * new Rational(expectedNum, expectedDen), expanded[0].Duration);
    }

    [Fact]
    public void Expand_KeepsPitchAndOffset()
    {
        var note = new NoteEvent(67, Rational.Half, Rational.Quarter, 0.8f);

        var expanded = new Articulation { BaseNote = note, DurationMultiplier = 0.5f }.Expand();

        Assert.Equal(67, expanded[0].Pitch);
        Assert.Equal(Rational.Half, expanded[0].Offset);
    }

    // ---------- velocity ----------

    [Fact]
    public void Expand_ScalesVelocity()
    {
        var expanded = new Articulation
        {
            BaseNote = Quarter(0.5f),
            VelocityMultiplier = 1.5f
        }.Expand();

        Assert.Equal(0.75f, expanded[0].Velocity, 5);
    }

    [Theory]
    [InlineData(0.9f, 2.0f)]    // would overshoot 1.0
    [InlineData(0.5f, 10f)]
    public void Expand_ClampsVelocityToTheValidRange(float baseVelocity, float multiplier)
    {
        var expanded = new Articulation
        {
            BaseNote = Quarter(baseVelocity),
            VelocityMultiplier = multiplier
        }.Expand();

        Assert.InRange(expanded[0].Velocity, 0f, 1f);
    }

    // ---------- the guard, and that it is the only rejection ----------

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    public void Expand_NonPositiveDurationMultiplier_Throws(float multiplier)
    {
        var articulation = new Articulation { BaseNote = Quarter(), DurationMultiplier = multiplier };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => articulation.Expand());
        Assert.Equal("DurationMultiplier", ex.ParamName);
    }

    [Fact]
    public void Expand_TinyButPositiveMultiplier_IsAccepted_AndKeepsThePitchAudible()
    {
        // A very short note is still a note; only a non-positive one is a caller error.
        var expanded = new Articulation { BaseNote = Quarter(), DurationMultiplier = 0.01f }.Expand();

        Assert.Single(expanded);
        Assert.True(expanded[0].Duration > Rational.Zero);
    }

    // ---------- FromType: every defined type produces a sane articulation ----------

    [Theory]
    [InlineData(ArticulationType.Normal)]
    [InlineData(ArticulationType.Staccato)]
    [InlineData(ArticulationType.Staccatissimo)]
    [InlineData(ArticulationType.Tenuto)]
    [InlineData(ArticulationType.Accent)]
    [InlineData(ArticulationType.Marcato)]
    [InlineData(ArticulationType.Legato)]
    [InlineData(ArticulationType.Portato)]
    [InlineData(ArticulationType.Sforzando)]
    [InlineData(ArticulationType.Fermata)]
    public void FromType_EveryDefinedType_ExpandsToOnePlayableNote(ArticulationType type)
    {
        var articulation = Articulation.FromType(type, Quarter());

        Assert.Equal(type, articulation.Type);
        Assert.True(articulation.DurationMultiplier > 0f,
            $"{type} would make Expand throw on its own preset");

        var expanded = articulation.Expand();

        Assert.Single(expanded);
        Assert.Equal(60, expanded[0].Pitch);
        Assert.True(expanded[0].Duration > Rational.Zero);
        Assert.InRange(expanded[0].Velocity, 0f, 1f);
    }

    [Fact]
    public void FromType_Staccato_ShortensTheNote()
    {
        var expanded = Articulation.FromType(ArticulationType.Staccato, Quarter()).Expand();

        Assert.True(expanded[0].Duration < Rational.Quarter);
    }

    [Fact]
    public void FromType_Staccatissimo_IsShorterThanStaccato()
    {
        var staccato = Articulation.FromType(ArticulationType.Staccato, Quarter()).Expand()[0];
        var staccatissimo = Articulation.FromType(ArticulationType.Staccatissimo, Quarter()).Expand()[0];

        Assert.True(staccatissimo.Duration < staccato.Duration);
    }

    [Fact]
    public void FromType_Accent_IsLouderThanTenuto_WhichIsLouderThanNormal()
    {
        var normal = Articulation.FromType(ArticulationType.Normal, Quarter(0.5f)).Expand()[0];
        var tenuto = Articulation.FromType(ArticulationType.Tenuto, Quarter(0.5f)).Expand()[0];
        var accent = Articulation.FromType(ArticulationType.Accent, Quarter(0.5f)).Expand()[0];

        Assert.True(tenuto.Velocity > normal.Velocity);
        Assert.True(accent.Velocity > tenuto.Velocity);
    }

    [Fact]
    public void FromType_Normal_LeavesTheNoteAlone()
    {
        var note = Quarter(0.6f);

        var expanded = Articulation.FromType(ArticulationType.Normal, note).Expand();

        Assert.Equal(note.Duration, expanded[0].Duration);
        Assert.Equal(note.Velocity, expanded[0].Velocity, 5);
    }

    [Fact]
    public void FromType_UndefinedType_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => Articulation.FromType((ArticulationType)99, Quarter()));

        Assert.Equal("type", ex.ParamName);
    }

    [Fact]
    public void FromType_CoversEveryMemberOfTheEnum()
    {
        // A member added to ArticulationType without a switch arm of its own would fall to the
        // default and silently behave as Normal. Enumerating the enum catches that; a
        // hand-listed theory would not -- Sforzando and Fermata were missing from mine.
        foreach (var type in Enum.GetValues<ArticulationType>())
        {
            var articulation = Articulation.FromType(type, Quarter());

            Assert.Equal(type, articulation.Type);
            Assert.True(articulation.DurationMultiplier > 0f, $"{type} would make Expand throw");
            Assert.Single(articulation.Expand());
        }
    }

    [Fact]
    public void FromType_Sforzando_IsTheLoudest_AndFermataTheLongest()
    {
        var byType = Enum.GetValues<ArticulationType>()
            .ToDictionary(t => t, t => Articulation.FromType(t, Quarter(0.5f)).Expand()[0]);

        Assert.Equal(byType.Values.Max(n => n.Velocity), byType[ArticulationType.Sforzando].Velocity);
        Assert.True(byType[ArticulationType.Fermata].Duration > Rational.Quarter,
            "a fermata should hold longer than written");
    }
}
