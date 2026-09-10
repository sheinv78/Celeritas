// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The notation writer and the notation parser are a pair: text this library produces, this
/// library must read. Three places broke that.
/// <para>
/// <see cref="MusicNotation.ToNotation"/> writes MIDI pitch 0 as "C-1", which is correct
/// scientific pitch notation and what <see cref="MusicNotation.ParseNote"/> and
/// <see cref="SpnNote.Parse"/> both read — but the grammar had no negative octave, so the whole
/// bottom octave of the keyboard could be imported from a MIDI file and then not written down.
/// <see cref="MusicNotation.FormatNoteSequence"/> with <c>useDot: false</c> emitted "C4/3/8" for
/// a dotted eighth, because the splitter counted a dotted value as writable in one go whether or
/// not the dot was allowed. And <see cref="MusicNotation.FormatDuration"/> with
/// <c>useLetters: true</c> wrote "1/64" for anything finer than a 32nd, where the numeric arm
/// beside it already fell back to "64", which parses.
/// </para>
/// </summary>
public class EverythingWrittenCanBeReadBackTests
{
    public static TheoryData<int, int> DurationsToWrite
    {
        get
        {
            TheoryData<int, int> data = [];
            foreach (var (n, d) in new[]
                     {
                         (1, 1), (1, 2), (1, 4), (1, 8), (1, 16), (1, 32), (1, 64), (1, 128),
                         (3, 8), (3, 4), (3, 16), (3, 32), (3, 2), (7, 16), (1, 3), (2, 3),
                         (1, 6), (1, 12), (5, 8), (1, 5), (2, 1), (4, 1), (5, 4), (1, 24),
                     })
            {
                data.Add(n, d);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DurationsToWrite))]
    public void ASequenceWrittenWithAnyFlagsReadsBackAsTheSameMusic(int numerator, int denominator)
    {
        var duration = new Rational(numerator, denominator);
        NoteEvent[] one = [new(60, Rational.Zero, duration, 0.8f)];

        foreach (var useDot in new[] { true, false })
        {
            foreach (var useLetters in new[] { true, false })
            {
                var text = MusicNotation.FormatNoteSequence(one, useDot, useLetters);
                var back = MusicNotation.Parse(text);

                Assert.All(back, note => Assert.Equal(60, note.Pitch));
                Assert.Equal(
                    duration,
                    back.Aggregate(Rational.Zero, (total, note) => total + note.Duration));
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryMidiPitchCanBeWrittenDownAndReadBack(bool preferSharps)
    {
        for (var pitch = 0; pitch <= 127; pitch++)
        {
            var name = MusicNotation.ToNotation(pitch, preferSharps);

            // All three readers of a note name, on the same text.
            Assert.True(MusicNotation.TryParseNote(name.AsSpan(), out var byParseNote), name);
            Assert.Equal(pitch, byParseNote);

            Assert.True(SpnNote.TryParse(name, out var bySpn), name);
            Assert.Equal(pitch, bySpn.MidiPitch);

            var byGrammar = MusicNotation.Parse(name + "/4");
            Assert.Single(byGrammar);
            Assert.Equal(pitch, byGrammar[0].Pitch);
        }
    }

    [Fact]
    public void TheBottomOctaveSurvivesBeingWrittenAsAPassage()
    {
        // MidiIo can hand back a bass note down here; "C-1" then had to be writable.
        NoteEvent[] low =
        [
            new(0, Rational.Zero, Rational.Quarter, 0.8f),
            new(11, Rational.Quarter, Rational.Quarter, 0.8f),
            new(5, Rational.Half, Rational.Half, 0.8f),
        ];

        var text = MusicNotation.FormatNoteSequence(low);
        var back = MusicNotation.Parse(text);

        Assert.Equal(low.Select(n => n.Pitch), back.Select(n => n.Pitch));
        Assert.Equal(low.Select(n => n.Offset), back.Select(n => n.Offset));
        Assert.Equal(low.Select(n => n.Duration), back.Select(n => n.Duration));
    }

    [Theory]
    [MemberData(nameof(DurationsToWrite))]
    public void ADurationOfTheFormOneOverNReadsBackWhicheverWayItIsWritten(int numerator, int denominator)
    {
        // FormatDuration's documented fallback for a duration outside the plain note values is
        // the denominator alone, which is how the grammar spells a tuplet — and ParseDuration
        // stopped at 32 and refused the rest, so "64" came back rejected by the very method that
        // is FormatDuration's own reader.
        if (numerator != 1)
        {
            return;
        }

        var duration = new Rational(numerator, denominator);
        foreach (var useDot in new[] { true, false })
        {
            foreach (var useLetters in new[] { true, false })
            {
                var text = MusicNotation.FormatDuration(duration, useDot, useLetters);
                Assert.Equal(duration, MusicNotation.ParseDuration(text));
            }
        }
    }

    [Fact]
    public void ADurationFinerThanAThirtySecondFallsBackToTheFormThatParses()
    {
        // There is no letter for a 64th. The numeric arm already answered "64"; the letter arm
        // beside it answered "1/64", which nothing in the library reads.
        Assert.Equal("64", MusicNotation.FormatDuration(new Rational(1, 64), useLetters: true));
        Assert.Equal("64", MusicNotation.FormatDuration(new Rational(1, 64), useLetters: false));
        Assert.Equal("12", MusicNotation.FormatDuration(new Rational(1, 12), useLetters: true));

        Assert.Equal(new Rational(1, 64), MusicNotation.ParseDuration("64"));
        Assert.Equal(new Rational(1, 12), MusicNotation.ParseDuration("12"));
    }

    [Fact]
    public void ADottedValueIsSplitWhenTheDotIsNotAllowed()
    {
        // "C4/3/8" is not notation: the grammar wants a note value after the slash. With dots
        // turned off a dotted eighth is three sixteenths tied, which is.
        NoteEvent[] dotted = [new(60, Rational.Zero, new Rational(3, 8), 0.8f)];

        var withDot = MusicNotation.FormatNoteSequence(dotted, useDot: true);
        var withoutDot = MusicNotation.FormatNoteSequence(dotted, useDot: false);

        Assert.DoesNotContain("3/8", withoutDot, StringComparison.Ordinal);
        Assert.Equal(
            new Rational(3, 8),
            MusicNotation.Parse(withDot).Aggregate(Rational.Zero, (t, n) => t + n.Duration));
        Assert.Equal(
            new Rational(3, 8),
            MusicNotation.Parse(withoutDot).Aggregate(Rational.Zero, (t, n) => t + n.Duration));
    }

    [Fact]
    public void APolyphonicPassageOfAwkwardDurationsSurvivesEveryFlagCombination()
    {
        var random = new Random(20260910);
        Rational[] durations =
        [
            new(1, 1), new(1, 2), new(1, 4), new(1, 8), new(1, 16), new(1, 32), new(1, 64),
            new(3, 8), new(3, 4), new(3, 16), new(1, 3), new(1, 6), new(1, 12), new(5, 8),
        ];

        for (var iteration = 0; iteration < 200; iteration++)
        {
            var notes = new List<NoteEvent>();
            var time = Rational.Zero;
            for (var i = 0; i < random.Next(2, 8); i++)
            {
                var duration = durations[random.Next(durations.Length)];
                notes.Add(new NoteEvent(random.Next(0, 128), time, duration, 0.8f));
                time += duration;
            }

            var written = notes.ToArray();
            var soundingTotal = written.Aggregate(Rational.Zero, (t, n) => t + n.Duration);

            foreach (var useDot in new[] { true, false })
            {
                foreach (var useLetters in new[] { true, false })
                {
                    foreach (var groupChords in new[] { true, false })
                    {
                        var text = MusicNotation.FormatNoteSequence(
                            written, useDot, useLetters, groupChords);
                        var back = MusicNotation.Parse(text);

                        Assert.Equal(
                            soundingTotal,
                            back.Aggregate(Rational.Zero, (t, n) => t + n.Duration));
                        Assert.Equal(
                            written.Select(n => n.Pitch).Order(),
                            back.Select(n => n.Pitch).Distinct().Order()
                                .Where(p => written.Any(w => w.Pitch == p))
                                .SelectMany(p => Enumerable.Repeat(p, written.Count(w => w.Pitch == p)))
                                .Order());
                    }
                }
            }
        }
    }
}
