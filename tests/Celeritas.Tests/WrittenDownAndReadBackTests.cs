// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Notation;
using Celeritas.Core.Ornamentation;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Notation is a way of writing music down, so what the writer produces has to read back as the
/// music it was given. It did not: a gap between two notes vanished, notes struck together but
/// held for different lengths came out as a succession, and a duration outside the plain note
/// values was written as text that is not notation at all. None of that threw — the passage
/// simply became a different passage.
/// </summary>
public class WrittenDownAndReadBackTests
{
    private static string Describe(IEnumerable<NoteEvent> notes) =>
        string.Join(" ", notes
            .Where(n => n.Pitch != MusicNotation.RestPitch)
            .Select(n => $"{n.Pitch}@{n.Offset}+{n.Duration}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private static void AssertRoundTrips(params NoteEvent[] notes)
    {
        var text = MusicNotation.FormatNoteSequence(notes);
        var reread = MusicNotation.Parse(text);

        Assert.Equal(Describe(notes), Describe(reread));
    }

    [Fact]
    public void ASilenceBetweenTwoNotesIsWrittenDown()
    {
        // The writer emitted notes back to back whatever their offsets, so the gap disappeared
        // and the second note moved to where the first one ended.
        AssertRoundTrips(
            new NoteEvent(60, Rational.Zero, Rational.Quarter),
            new NoteEvent(64, Rational.Half, Rational.Quarter));
    }

    [Fact]
    public void ASilenceBeforeTheFirstNoteIsWrittenDown()
    {
        AssertRoundTrips(new NoteEvent(60, Rational.Quarter, Rational.Quarter));
    }

    [Fact]
    public void NotesStruckTogetherButHeldDifferently_StayTogether()
    {
        // "C4/4 E4/2" is a C followed by an E. The two notes begin together, so the writer now
        // uses the polyphonic form the notation already had.
        AssertRoundTrips(
            new NoteEvent(60, Rational.Zero, Rational.Quarter),
            new NoteEvent(64, Rational.Zero, Rational.Half));
    }

    [Fact]
    public void OverlappingNotesStayOverlapped()
    {
        AssertRoundTrips(
            new NoteEvent(60, Rational.Zero, Rational.Half),
            new NoteEvent(64, Rational.Quarter, Rational.Half));
    }

    [Fact]
    public void AChordWhoseNotesShareALengthIsStillWrittenAsAChord()
    {
        // The polyphonic form is for what the melodic one cannot hold; an ordinary chord must
        // still read as "[C4 E4 G4]/4".
        NoteEvent[] triad =
        [
            new(60, Rational.Zero, Rational.Quarter),
            new(64, Rational.Zero, Rational.Quarter),
            new(67, Rational.Zero, Rational.Quarter),
        ];

        Assert.Equal("[C4 E4 G4]/4", MusicNotation.FormatNoteSequence(triad));
    }

    [Theory]
    [InlineData(2, 1)]      // two whole notes
    [InlineData(5, 4)]      // five quarters
    [InlineData(1, 5)]      // a quintuplet
    [InlineData(1, 12)]     // a triplet eighth
    [InlineData(3, 2)]      // a dotted whole note
    public void ADurationOutsideThePlainNoteValues_IsWrittenSoItCanBeReadBack(int numerator, int denominator)
    {
        // A duration the notation has no single form for used to be written as the rational —
        // "C4/5/4" — which does not parse. It is now tied pieces, which is how it is engraved.
        AssertRoundTrips(new NoteEvent(60, Rational.Zero, new Rational(numerator, denominator)));
    }

    [Fact]
    public void AnyPassageOfSimpleDurations_ReadsBackAsItself()
    {
        (from pitches in Gen.Int[48, 72].Array[1, 6]
         from starts in Gen.Int[0, 8].Array[1, 6]
         from lengths in Gen.Int[1, 5].Array[1, 6]
         from denominators in Gen.Int[0, 3].Array[1, 3]
         select (pitches, starts, lengths, denominators)).Sample(t =>
        {
            int[] denominators = [1, 2, 4, 8];
            var notes = new List<NoteEvent>();
            for (var i = 0; i < t.pitches.Length; i++)
            {
                notes.Add(new NoteEvent(
                    t.pitches[i],
                    new Rational(t.starts[i % t.starts.Length], 4),
                    new Rational(t.lengths[i % t.lengths.Length], denominators[t.denominators[i % t.denominators.Length]])));
            }

            // Two notes of the same pitch at the same instant are one note, not two.
            var distinct = notes.GroupBy(x => (x.Pitch, x.Offset)).Select(g => g.First()).ToArray();

            return Describe(distinct) == Describe(MusicNotation.Parse(MusicNotation.FormatNoteSequence(distinct)));
        }, iter: 1000);
    }

    // ---------- a passage says which meter it opens in ----------

    [Fact]
    public void ParseFullReportsTheMeterThePassageOpensIn()
    {
        // The visitor keeps TimeSignature current so measure validation uses the meter in force,
        // and ParseFull handed that back — so a passage that changed meter reported the one it
        // ended in, although the doc promises the leading one.
        var parsed = MusicNotation.ParseFull("4/4: C4/4 C4/4 C4/4 C4/4 | 3/4: D4/4 D4/4 D4/4");

        Assert.Equal(new TimeSignature(4, 4), parsed.TimeSignature);
    }

    // ---------- notation reports its own failures ----------

    [Theory]
    [InlineData("C4/99999999999999999999")]
    [InlineData("99999999999999999999/4: C4/4")]
    [InlineData("@bpm=99999999999999999999 C4/4")]
    public void ANumberTooLargeToHold_IsReportedAsBadNotation(string notation)
    {
        // int.Parse threw OverflowException, which Parse does not document — so a caller that
        // handled every documented failure still crashed on it.
        Assert.Throws<ArgumentException>(() => MusicNotation.Parse(notation));
    }

    // ---------- what is read in has to be playable ----------

    [Fact]
    public void ANegativeDurationInMusicXml_IsReportedRatherThanImported()
    {
        // It came through untouched and became a note of negative length: one that ends before
        // it starts and sorts before its own onset.
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        var xml = MusicXmlIo.ToXml(buffer).Replace("<duration>", "<duration>-", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => MusicXmlIo.Parse(xml));
    }

    [Fact]
    public void ADiatonicGlissandoStaysOnTheKeyboard()
    {
        // The chromatic path holds every step on the keyboard and this one did not: a glissando
        // aimed below MIDI 0 emitted pitches down to -28.
        var glissando = new Glissando
        {
            BaseNote = new NoteEvent(2, Rational.Zero, Rational.Whole),
            TargetPitch = -30,
            IsAbsolute = false,
            Chromatic = false,
        };

        Assert.All(glissando.Expand(), n => Assert.InRange(n.Pitch, 0, 127));
    }

    [Fact]
    public void AGlissandoWithRoomToRun_StillRunsThroughTheScale()
    {
        var glissando = new Glissando
        {
            BaseNote = new NoteEvent(60, Rational.Zero, Rational.Whole),
            TargetPitch = 72,
            IsAbsolute = true,
            Chromatic = false,
        };

        var notes = glissando.Expand();

        Assert.True(notes.Length > 2, "a diatonic glissando across an octave should pass through the scale");
        Assert.All(notes, n => Assert.InRange(n.Pitch, 60, 72));
    }

    [Fact]
    public void AFigureWithNoRoomAboveItsBass_IsRefusedRatherThanRealisedOffTheKeyboard()
    {
        // Upper voices stack above the bass, so a bass near the top pushed them past MIDI 127:
        // a bass of 125 realized as 125, 129, 132, 136.
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new KeySignature(0, true) });

        var thrown = Assert.Throws<ArgumentException>(() => realizer.Realize(
        [
            new FiguredBassSymbol
            {
                BassPitch = 125,
                Figures = [3, 5, 7],
                Duration = Rational.Quarter,
                Time = Rational.Zero,
            },
        ]));

        Assert.Contains("keyboard", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFigureWithRoomAboveItsBass_IsStillRealised()
    {
        var realizer = new FiguredBassRealizer(new FiguredBassOptions { Key = new KeySignature(0, true) });

        var notes = realizer.Realize(
        [
            new FiguredBassSymbol
            {
                BassPitch = 48,
                Figures = [3, 5],
                Duration = Rational.Quarter,
                Time = Rational.Zero,
            },
        ]);

        Assert.NotEmpty(notes);
        Assert.All(notes, n => Assert.InRange(n.Pitch, 0, 127));
    }
}
