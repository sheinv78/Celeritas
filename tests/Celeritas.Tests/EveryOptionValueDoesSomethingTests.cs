// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Accompaniment;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Harmonization;
using Celeritas.Core.Midi;
using Celeritas.Core.Ornamentation;
using Melanchall.DryWetMidi.Core;
using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// The enums a caller passes in rather than reads out. "Is this value ever returned" is the wrong
/// question for them; the right one is whether choosing it changes anything, or whether it is
/// silently the same as its neighbour — which is worse than an unreachable result, because the
/// caller picked it on purpose and got the other thing.
/// </summary>
public class EveryOptionValueDoesSomethingTests
{
    private static string[] Answers<T>(Func<T, string> answer) where T : struct, Enum
        => [.. Enum.GetValues<T>().Select(answer)];

    [Fact]
    public void EachDirectionRoundTheCircleGoesTheOtherWay()
    {
        Assert.Equal(2, Answers<CircleDirection>(
            d => string.Join(",", CircleOfFifths.PitchClasses(new PitchClass(0), d).Select(p => p.Value)))
            .Distinct().Count());
    }

    [Fact]
    public void EachChordTypeAndMinorDominantStyleBuildsSomethingDifferent()
    {
        Assert.Equal(2, Answers<DiatonicChordType>(
            t => string.Join(" ", FunctionalProgressions
                .TwoFiveOne(new KeySignature(0, true), t).Select(c => c.Symbol())))
            .Distinct().Count());

        Assert.Equal(2, Answers<MinorDominantStyle>(
            s => string.Join(" ", FunctionalProgressions
                .TwoFiveOne(new KeySignature(0, false), DiatonicChordType.Seventh, s)
                .Select(c => c.Symbol())))
            .Distinct().Count());
    }

    [Fact]
    public void EachRhythmStyleModelPredictsDifferently()
    {
        // Comparing the models' context counts is too coarse — Classical and Jazz happen to hold
        // the same number. What they are for is the prediction.
        var answers = Answers<RhythmStyle>(style =>
        {
            var model = RhythmModels.GetStyleModel(style);
            var prediction = model.Predict([Rational.Quarter, Rational.Quarter]);
            return $"{prediction.MostLikely} {prediction.Confidence:F4} "
                   + string.Join(",", model.Generate([Rational.Quarter], 8));
        });

        Assert.Equal(Enum.GetValues<RhythmStyle>().Length, answers.Distinct().Count());
    }

    [Fact]
    public void EachOrnamentTypeShapesTheNoteDifferently()
    {
        var baseNote = new NoteEvent(60, Rational.Zero, Rational.Half, 0.6f);

        static string Shape(NoteEvent[] notes) =>
            string.Join(" ", notes.Select(n => $"{n.Pitch}@{n.Offset}+{n.Duration}"));

        Assert.Equal(2, Answers<TurnType>(
            t => Shape(new Turn { BaseNote = baseNote, Type = t }.Expand())).Distinct().Count());

        Assert.Equal(2, Answers<MordentType>(
            t => Shape(new Mordent { BaseNote = baseNote, Type = t }.Expand())).Distinct().Count());

        Assert.Equal(2, Answers<AppogiaturaType>(
            t => Shape(new Appoggiatura { BaseNote = baseNote, Type = t }.Expand())).Distinct().Count());

        // Ten articulation marks, ten different notes — the loud ones used to collapse into one
        // at any dynamic above mezzo-forte.
        Assert.Equal(10, Answers<ArticulationType>(
            t => Shape(Articulation.FromType(t, baseNote).Expand())
                 + Articulation.FromType(t, baseNote).Expand()[0].Velocity.ToString("F4"))
            .Distinct().Count());
    }

    [Fact]
    public void TheTwoOptionValuesThatShareABehaviourSayThatTheyDo()
    {
        // GraceNoteType.Multiple and VoiceLeadingStyle.Strict each produce exactly what their
        // neighbour does. Neither is a bug in the arithmetic — how many grace notes there are
        // comes from Intervals, and the figured-bass realizer does not enforce common-practice
        // rules — but a caller choosing one and getting the other silently is, so both values now
        // say so. This holds them to it: if either ever starts doing something of its own, the
        // remark beside it is out of date and this is what says so.
        var baseNote = new NoteEvent(60, Rational.Zero, Rational.Half, 0.6f);

        static string Shape(NoteEvent[] notes) =>
            string.Join(" ", notes.Select(n => $"{n.Pitch}@{n.Offset}+{n.Duration}"));

        foreach (var intervals in new[] { new[] { 2 }, new[] { 2, -1 }, new[] { 2, -1, 3 } })
        {
            Assert.Equal(
                Shape(new GraceNote
                {
                    BaseNote = baseNote,
                    Type = GraceNoteType.Appoggiatura,
                    Intervals = intervals,
                }.Expand()),
                Shape(new GraceNote
                {
                    BaseNote = baseNote,
                    Type = GraceNoteType.Multiple,
                    Intervals = intervals,
                }.Expand()));
        }

        // ...and Acciaccatura really is different, so the comparison above means something.
        Assert.NotEqual(
            Shape(new GraceNote { BaseNote = baseNote, Type = GraceNoteType.Appoggiatura }.Expand()),
            Shape(new GraceNote { BaseNote = baseNote, Type = GraceNoteType.Acciaccatura }.Expand()));

        FiguredBassSymbol[] symbols =
        [
            new() { BassPitch = 48, Figures = [], Time = Rational.Zero, Duration = Rational.Quarter },
            new() { BassPitch = 53, Figures = [6], Time = Rational.Quarter, Duration = Rational.Quarter },
            new() { BassPitch = 55, Figures = [], Time = Rational.Half, Duration = Rational.Quarter },
            new() { BassPitch = 48, Figures = [], Time = new Rational(3, 4), Duration = Rational.Quarter },
        ];

        string Realize(VoiceLeadingStyle style) => Shape(
            new FiguredBassRealizer(new FiguredBassRealizerOptions { Style = style }).Realize(symbols));

        Assert.Equal(Realize(VoiceLeadingStyle.Smooth), Realize(VoiceLeadingStyle.Strict));

        // Free is the one that takes its own path, so the comparison above means something.
        Assert.NotEqual(Realize(VoiceLeadingStyle.Smooth), Realize(VoiceLeadingStyle.Free));
    }

    [Fact]
    public void AMergeModeCanBePassedSomewhere()
    {
        // MidiMergeMode named two behaviours that existed only as two separate methods, so
        // nothing anywhere took one as a parameter: the type could be constructed and not used.
        var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/2");

        MidiFile Build(int transpose)
        {
            var file = new MidiFile();
            file.AddTrack(
                [.. melody.Select(n => new NoteEvent(n.Pitch + transpose, n.Offset, n.Duration, n.Velocity))],
                "part");
            return file;
        }

        var appended = Build(0).Merge(Build(7), MidiMergeMode.AppendTracks).GetStatistics();
        var single = Build(0).Merge(Build(7), MidiMergeMode.SingleTrack).GetStatistics();

        Assert.Equal(melody.Length * 2, appended.NoteCount);
        Assert.Equal(melody.Length * 2, single.NoteCount);
        Assert.NotEqual(appended.TrackCount, single.TrackCount);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(0).Merge(Build(7), (MidiMergeMode)99));
    }

    [Fact]
    public void EachSplitModeAndAccompanimentPatternProducesSomethingOfItsOwn()
    {
        var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/2");

        Assert.Equal(2, Answers<MidiSplitMode>(mode =>
        {
            var file = new MidiFile();
            file.AddTrack(melody, "one");
            file.AddTrack(
                [.. melody.Select(n => new NoteEvent(n.Pitch + 7, n.Offset, n.Duration, n.Velocity))],
                "two");
            return string.Join(" | ", file.Split(mode).Select(p =>
            {
                var s = p.GetStatistics();
                return $"{s.TrackCount}t/{s.NoteCount}n";
            }));
        }).Distinct().Count());

        ChordAssignment[] chords =
        [
            new(Rational.Zero, Rational.Whole, ChordAnalyzer.Identify([60, 64, 67]), [60, 64, 67]),
            new(Rational.Whole, Rational.Whole * 2, ChordAnalyzer.Identify([65, 69, 72]), [65, 69, 72]),
        ];

        Assert.Equal(2, Answers<AccompanimentPattern>(pattern => string.Join(" ",
            AccompanimentGenerator
                .Generate(chords, AccompanimentOptions.Default with { Pattern = pattern })
                .Select(n => $"{n.Pitch}@{n.Offset}")))
            .Distinct().Count());
    }
}
