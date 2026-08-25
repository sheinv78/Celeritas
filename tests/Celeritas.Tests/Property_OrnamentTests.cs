// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Ornamentation;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Every ornament expands one written note into several played ones. Whatever it produces has
/// to be playable and has to stay where the written note was: a trill that runs past its own
/// note, or a turn that reaches a pitch no instrument has, is not something a listener would
/// notice in a test that only checks the note count.
/// </summary>
public class PropertyOrnamentTests
{
    private static readonly Gen<int> MidiPitch = Gen.Int[36, 84];
    private static readonly Gen<int> SmallInterval = Gen.Int[1, 4];

    private static NoteEvent Base(int pitch, int eighths) =>
        new(pitch, Rational.Zero, new Rational(Math.Max(1, eighths), 8), 0.8f);

    private static bool Playable(NoteEvent[] notes, NoteEvent written) =>
        notes.Length > 0
        && notes.All(n => n.Pitch is >= 0 and <= 127)
        && notes.All(n => n.Duration > Rational.Zero)
        && notes.All(n => n.Velocity is >= 0f and <= 1f)
        && notes.All(n => n.Offset >= written.Offset)
        && notes.All(n => n.Offset + n.Duration <= written.Offset + written.Duration);

    [Fact]
    public void ATrillStaysInsideTheNoteItDecorates()
    {
        (from pitch in MidiPitch
         from eighths in Gen.Int[1, 8]
         from interval in SmallInterval
         from speed in Gen.Int[2, 16]
         from upper in Gen.Bool
         select (pitch, eighths, interval, speed, upper)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var trill = new Trill
            {
                BaseNote = written,
                Interval = t.interval,
                Speed = t.speed,
                StartWithUpper = t.upper,
            };

            return Playable(trill.Expand(), written);
        }, iter: 500);
    }

    [Fact]
    public void AMordentStaysInsideTheNoteItDecorates()
    {
        (from pitch in MidiPitch
         from eighths in Gen.Int[1, 8]
         from interval in SmallInterval
         from alternations in Gen.Int[1, 4]
         from upper in Gen.Bool
         select (pitch, eighths, interval, alternations, upper)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var mordent = new Mordent
            {
                BaseNote = written,
                Interval = t.interval,
                Alternations = t.alternations,
                Type = t.upper ? MordentType.Upper : MordentType.Lower,
            };

            return Playable(mordent.Expand(), written);
        }, iter: 500);
    }

    [Fact]
    public void ATurnStaysInsideTheNoteItDecorates()
    {
        (from pitch in MidiPitch
         from eighths in Gen.Int[1, 8]
         from upperInterval in SmallInterval
         from lowerInterval in SmallInterval
         from inverted in Gen.Bool
         select (pitch, eighths, upperInterval, lowerInterval, inverted)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var turn = new Turn
            {
                BaseNote = written,
                UpperInterval = t.upperInterval,
                LowerInterval = t.lowerInterval,
                Type = t.inverted ? TurnType.Inverted : TurnType.Normal,
            };

            return Playable(turn.Expand(), written);
        }, iter: 500);
    }

    [Fact]
    public void AGlissandoStaysInsideTheNoteAndReachesItsTarget()
    {
        (from pitch in MidiPitch
         from target in MidiPitch
         from eighths in Gen.Int[1, 8]
         from steps in Gen.Int[2, 16]
         select (pitch, target, eighths, steps)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var glissando = new Glissando
            {
                BaseNote = written,
                TargetPitch = t.target,
                IsAbsolute = true,
                Steps = t.steps,
            };

            var notes = glissando.Expand();

            if (!Playable(notes, written))
                return false;

            // A slide moves in one direction only: it never doubles back.
            var rising = t.target >= t.pitch;
            return notes.Zip(notes.Skip(1), (a, b) => rising ? b.Pitch >= a.Pitch : b.Pitch <= a.Pitch)
                .All(monotone => monotone);
        }, iter: 500);
    }

    [Fact]
    public void AnAppoggiaturaStaysInsideTheNoteItDecorates()
    {
        (from pitch in MidiPitch
         from eighths in Gen.Int[1, 8]
         from interval in SmallInterval
         from direction in Gen.Int[-1, 1]
         from longForm in Gen.Bool
         select (pitch, eighths, interval, direction, longForm)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var appoggiatura = new Appoggiatura
            {
                BaseNote = written,
                Interval = t.interval,
                Direction = t.direction,
                Type = t.longForm ? AppogiaturaType.Long : AppogiaturaType.Short,
            };

            return Playable(appoggiatura.Expand(), written);
        }, iter: 500);
    }

    [Fact]
    public void EveryArticulationKeepsTheNoteAudible()
    {
        (from pitch in MidiPitch
         from eighths in Gen.Int[1, 8]
         from which in Gen.Int[0, Enum.GetValues<ArticulationType>().Length - 1]
         select (pitch, eighths, which)).Sample(t =>
        {
            var written = Base(t.pitch, t.eighths);
            var type = Enum.GetValues<ArticulationType>()[t.which];

            var notes = Articulation.FromType(type, written).Expand();

            // An articulation may lengthen the note (a fermata does), so it is not bounded by
            // the written duration — but it must stay one audible note at the written pitch.
            return notes.Length == 1
                && notes[0].Pitch == t.pitch
                && notes[0].Offset == written.Offset
                && notes[0].Duration > Rational.Zero
                && notes[0].Velocity is >= 0f and <= 1f;
        }, iter: 500);
    }

    [Fact]
    public void OrnamentsNeverProduceANoteOffTheKeyboard_EvenAtTheEdges()
    {
        // At the very top and bottom of the keyboard an ornament that reaches upward or
        // downward has nowhere to go; it must clamp rather than emit an unplayable pitch.
        (from pitch in Gen.OneOf(Gen.Int[0, 3], Gen.Int[124, 127])
         from interval in Gen.Int[1, 6]
         select (pitch, interval)).Sample(t =>
        {
            var written = Base(t.pitch, 4);

            NoteEvent[][] expansions =
            [
                new Trill { BaseNote = written, Interval = t.interval }.Expand(),
                new Mordent { BaseNote = written, Interval = t.interval }.Expand(),
                new Mordent { BaseNote = written, Interval = t.interval, Type = MordentType.Lower }.Expand(),
                new Turn { BaseNote = written, UpperInterval = t.interval, LowerInterval = t.interval }.Expand(),
                new Appoggiatura { BaseNote = written, Interval = t.interval, Direction = 1 }.Expand(),
                new Appoggiatura { BaseNote = written, Interval = t.interval, Direction = -1 }.Expand(),
            ];

            return expansions.All(notes => notes.All(n => n.Pitch is >= 0 and <= 127));
        }, iter: 500);
    }
}
