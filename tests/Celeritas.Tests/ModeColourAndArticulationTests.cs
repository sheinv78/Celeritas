// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Tests;

/// <summary>
/// Two results that could not say what they were for: half of a public tuple that came back
/// empty whatever it was asked, and a set of articulation marks that stopped being distinguishable
/// at the dynamics where they matter most.
/// </summary>
public class ModeColourAndArticulationTests
{
    // ---------- avoid notes ----------

    [Theory]
    [InlineData(Mode.Ionian, new[] { 5 })]            // the 4th against the major 3rd
    [InlineData(Mode.Mixolydian, new[] { 5 })]        // the same 4th, against a dominant 7th
    [InlineData(Mode.Aeolian, new[] { 8 })]           // the b6 against the 5th
    [InlineData(Mode.Locrian, new[] { 1 })]           // the b2 against the root
    [InlineData(Mode.HarmonicMinor, new[] { 8 })]     // the b6 again
    public void AModeReportsTheDegreeThatFightsItsOwnTonicChord(Mode mode, int[] expected)
    {
        var (_, avoid) = ModeLibrary.GetCharacteristicNotes(mode);
        Assert.Equal(expected, avoid);
    }

    [Theory]
    [InlineData(Mode.Dorian)]
    [InlineData(Mode.Lydian)]
    [InlineData(Mode.MelodicMinor)]
    [InlineData(Mode.LydianDominant)]
    [InlineData(Mode.LocrianNatural2)]
    public void TheModesWithNothingToAvoidReportNothing(Mode mode)
    {
        // These are the modes a melody can move through freely, and having no avoid note is
        // exactly what makes them so — Lydian over a major chord, Dorian over a minor one,
        // melodic minor and its Lydian-dominant and Locrian-natural-2 rotations over the rest.
        var (_, avoid) = ModeLibrary.GetCharacteristicNotes(mode);
        Assert.Empty(avoid);
    }

    [Fact]
    public void AnAvoidNoteIsAlwaysASemitoneAboveAChordToneAndNeverAChordToneItself()
    {
        // The property the answer is derived from, checked against every mode rather than the
        // handful named above: whatever comes back must be in the scale, must not be part of the
        // tonic seventh, and must sit a semitone above something that is.
        foreach (var mode in Enum.GetValues<Mode>())
        {
            var (_, avoid) = ModeLibrary.GetCharacteristicNotes(mode);
            var scale = ModeLibrary.GetScaleNotes(new ModalKey(0, mode));

            if (scale.Length != 7)
            {
                Assert.Empty(avoid);
                continue;
            }

            var chord = new[] { scale[0], scale[2], scale[4], scale[6] };

            foreach (var degree in avoid)
            {
                Assert.Contains(degree, scale);
                Assert.DoesNotContain(degree, chord);
                Assert.Contains((degree + 11) % 12, chord);
            }

            // ...and nothing that satisfies all three was left out.
            foreach (var degree in scale)
            {
                if (!chord.Contains(degree) && chord.Contains((degree + 11) % 12))
                {
                    Assert.Contains(degree, avoid);
                }
            }
        }
    }

    [Fact]
    public void TheAvoidHalfIsNotEmptyForEveryModeInTheLibrary()
    {
        // It used to be: all nineteen modes returned an empty second element, so a caller asking
        // the question got nothing back for it whatever they asked about.
        var withAvoid = Enum.GetValues<Mode>()
            .Count(m => ModeLibrary.GetCharacteristicNotes(m).avoid.Length > 0);

        Assert.True(withAvoid > 0, "no mode reports an avoid note");
    }

    // ---------- articulation ----------

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    [InlineData(0.8f)]
    [InlineData(0.9f)]
    [InlineData(0.99f)]
    public void TheLoudMarksStayInOrderAtEveryDynamic(float baseVelocity)
    {
        // Multiplying and clamping ran out of room: at 0.8, accent (x1.3), marcato (x1.5) and
        // sforzando (x1.6) all came out at 1.000 and three distinct marks were the same note.
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter, baseVelocity);

        float Velocity(ArticulationType type) =>
            Articulation.FromType(type, note).Expand()[0].Velocity;

        var normal = Velocity(ArticulationType.Normal);
        var tenuto = Velocity(ArticulationType.Tenuto);
        var accent = Velocity(ArticulationType.Accent);
        var marcato = Velocity(ArticulationType.Marcato);
        var sforzando = Velocity(ArticulationType.Sforzando);

        Assert.True(normal < tenuto, $"normal {normal} < tenuto {tenuto}");
        Assert.True(tenuto < accent, $"tenuto {tenuto} < accent {accent}");
        Assert.True(accent < marcato, $"accent {accent} < marcato {marcato}");
        Assert.True(marcato < sforzando, $"marcato {marcato} < sforzando {sforzando}");
        Assert.True(sforzando <= 1f, $"sforzando {sforzando} <= 1");
    }

    [Fact]
    public void AMarkNeverPushesANoteOffTheTopOfTheScale()
    {
        foreach (var type in Enum.GetValues<ArticulationType>())
        {
            for (var step = 0; step <= 20; step++)
            {
                var velocity = step / 20f;
                var note = new NoteEvent(60, Rational.Zero, Rational.Quarter, velocity);
                var shaped = Articulation.FromType(type, note).Expand()[0].Velocity;

                Assert.InRange(shaped, 0f, 1f);
            }
        }
    }

    [Fact]
    public void TheMarksAreUnchangedInTheMiddleOfTheRange()
    {
        // The new reading and the old multiplication meet exactly at half velocity, so nothing
        // moves for music written there; only where the old formula had already saturated.
        var note = new NoteEvent(60, Rational.Zero, Rational.Quarter, 0.5f);

        Assert.Equal(0.55f, Articulation.FromType(ArticulationType.Tenuto, note).Expand()[0].Velocity, 5);
        Assert.Equal(0.65f, Articulation.FromType(ArticulationType.Accent, note).Expand()[0].Velocity, 5);
        Assert.Equal(0.75f, Articulation.FromType(ArticulationType.Marcato, note).Expand()[0].Velocity, 5);
        Assert.Equal(0.80f, Articulation.FromType(ArticulationType.Sforzando, note).Expand()[0].Velocity, 5);
    }
}
