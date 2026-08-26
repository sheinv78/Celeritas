// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.FiguredBass;
using Celeritas.Core.Midi;
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

    // ---------- a tie belongs to one voice ----------

    [Fact]
    public void ATieAtTheEndOfAVoice_DoesNotSwallowTheNextVoicesNote()
    {
        // Pending ties were not cleared between the voices of a polyphonic block, so a tie left
        // dangling at the end of one voice merged the next voice's first same-pitch note into
        // it: three notes came back as two, and the one that vanished belonged to another line.
        var notes = MusicNotation.Parse("<< C4/4~ | C4/4 D4/4 >>");

        Assert.Equal(3, notes.Length);
        Assert.Equal(2, notes.Count(n => n.Pitch == 60));
    }

    [Theory]
    [InlineData("<< C4/4~ C4/4 | G3/2 >>", 2)]
    [InlineData("C4/4~ C4/4 D4/4", 2)]
    public void ATieWithinOneLine_StillJoinsItsNotes(string notation, int expected)
    {
        // The fix must not have stopped ties from working where they do belong.
        var notes = MusicNotation.Parse(notation);

        Assert.Equal(expected, notes.Length);
        Assert.Contains(notes, n => n.Pitch == 60 && n.Duration == Rational.Half);
    }

    // ---------- an ornament survives the tie before it ----------

    [Fact]
    public void AnOrnamentOnANoteTiedIntoIsStillPlayed()
    {
        // The tie branch returned before the ornament was ever looked at, so "C4/4~ C4/4{tr}"
        // came back as one held half note with the trill silently gone. An ornament is an
        // articulation of the note it is written on, so that note is struck: it ends the tie.
        var ornamented = MusicNotation.Parse("C4/4~ C4/4{tr}");
        var plain = MusicNotation.Parse("C4/4 C4/4{tr}");

        Assert.Equal(plain.Length, ornamented.Length);
        Assert.True(ornamented.Length > 2, "the trill did not expand");
    }

    [Fact]
    public void ATieBetweenPlainNotesStillJoinsThem()
    {
        var notes = MusicNotation.Parse("C4/4~ C4/4");

        Assert.Single(notes);
        Assert.Equal(Rational.Half, notes[0].Duration);
    }

    // ---------- a dynamics mark may be quoted, as its grammar says ----------

    [Theory]
    [InlineData("@dynamics=\"mf\" C4/4")]
    [InlineData("@dynamics=mf C4/4")]
    public void ADynamicsMarkIsReadInEitherFormItsGrammarAccepts(string notation)
    {
        // The grammar lists STRING among the forms, and the visitor threw "Invalid dynamics
        // value" for the quoted one — notation its own grammar accepts.
        var directives = MusicNotation.ParseFull(notation).Directives.ToArray();

        var dynamics = Assert.Single(directives.OfType<DynamicsDirective>());
        Assert.Equal("mf", dynamics.StartLevel);
    }

    // ---------- a note MIDI cannot hold is refused, not clipped ----------

    [Fact]
    public void ANoteLongerThanMidiCanExpress_IsRefusedRatherThanClipped()
    {
        // The length was clipped to int.MaxValue ticks and written anyway, so the file held a
        // different note from the one exported and nothing said so.
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, new Rational(2_000_000, 1));

        var path = Path.Combine(Path.GetTempPath(), $"celeritas-long-{Guid.NewGuid():N}.mid");
        try
        {
            var thrown = Assert.Throws<ArgumentException>(() => MidiIo.Export(buffer, path));
            Assert.Contains("MIDI can express", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---------- a missing argument is named, not dereferenced ----------

    [Fact]
    public void MidiIoNamesTheArgumentItWasNotGiven()
    {
        // A sweep of every public static entry point, called with nulls, found these four
        // reaching a dereference instead: the caller got "Object reference not set" and no clue
        // which argument was the problem.
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        using var stream = new MemoryStream();
        var path = Path.Combine(Path.GetTempPath(), $"celeritas-null-{Guid.NewGuid():N}.mid");

        Assert.Throws<ArgumentNullException>(() => MidiIo.Export(null!, path));
        Assert.Throws<ArgumentNullException>(() => MidiIo.Export(buffer, (string)null!));
        Assert.Throws<ArgumentNullException>(() => MidiIo.Export(null!, stream));
        Assert.Throws<ArgumentNullException>(() => MidiIo.Export(buffer, (Stream)null!));
        Assert.Throws<ArgumentNullException>(() => MidiIo.Import((string)null!));
        Assert.Throws<ArgumentNullException>(() => MidiIo.Import((Stream)null!));

        Assert.False(File.Exists(path), "a rejected export must not have created the file");
    }

    [Fact]
    public void ACatalogThatIsNotJson_IsReportedAsBadData()
    {
        // JsonException is not a type a caller of a music library would think to catch.
        Assert.Throws<ArgumentNullException>(() => PitchClassSetCatalog.LoadJson(null!));
        Assert.Throws<InvalidDataException>(() => PitchClassSetCatalog.LoadJson(""));
        Assert.Throws<InvalidDataException>(() => PitchClassSetCatalog.LoadJson("{ not json"));
    }

    [Fact]
    public void ACatalogThatIsJson_StillLoads()
    {
        var catalog = PitchClassSetCatalog.LoadJson("""
            [ { "forte": "3-11A", "primeForm": [0, 3, 7], "name": "minor triad" } ]
            """);

        Assert.True(catalog.TryGetByPrimeForm([0, 3, 7], out var entry));
        Assert.Equal("3-11A", entry!.Forte);
    }

    // ---------- an absurd argument is answered, not run forever on ----------

    [Fact]
    public void AskingForMoreVoicesThanThereAreNotes_Returns()
    {
        // Every voice table is sized by maxVoices, so int.MaxValue seeded two billion of them
        // and the call never came back. A voice needs a note, so there cannot be more voices
        // than notes — and the empty ones were dropped from the result anyway.
        using var buffer = new NoteBuffer(4);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

        var many = VoiceSeparator.Separate(buffer, int.MaxValue);
        var few = VoiceSeparator.Separate(buffer, 4);

        Assert.Equal(few.Voices.Count, many.Voices.Count);
        Assert.Equal(buffer.Count, many.Voices.Sum(v => v.Notes.Count));
    }

    [Fact]
    public void AStepFarSmallerThanTheMusic_IsRefusedRatherThanRunForever()
    {
        // A step of 1/long.MaxValue across a couple of bars asks for about 2^62 windows.
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.AddNote(64, Rational.Quarter, Rational.Quarter);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KeyProfiler.AnalyzeModulations(buffer, Rational.Quarter, new Rational(1, long.MaxValue)));
    }

    [Fact]
    public void AnOrdinaryWindowAndStep_StillWalkTheMusic()
    {
        using var buffer = new NoteBuffer(8);
        for (var i = 0; i < 8; i++)
            buffer.AddNote(60 + i, new Rational(i, 4), Rational.Quarter);

        var trajectory = KeyProfiler.AnalyzeModulations(buffer, Rational.Whole, Rational.Half);

        Assert.NotEmpty(trajectory.Points);
    }

    [Fact]
    public void ADefaultAnalysisResultCanStillBePrinted()
    {
        // A value type is always constructible as default, and its arrays are null there — every
        // formatted member, and so the compiler-generated ToString, threw ArgumentNullException.
        var empty = default(PitchClassSetAnalysisResult);

        Assert.Equal("{}", empty.PitchClassesText);
        Assert.Equal("{}", empty.NormalOrderText);
        Assert.Equal("{}", empty.PrimeFormText);
        Assert.Equal("<>", empty.IntervalVectorText);
        Assert.NotNull(empty.ToString());
    }

    // ---------- a tempo ramp keeps the tempo it ramps to ----------

    [Fact]
    public void ATempoRampWithNoStatedLength_KeepsItsTarget()
    {
        // The ramp duration is optional in the notation, and both the writer and ToString
        // required it before they would mention the target — so a passage that ramps to 180
        // was written back as "@bpm 120" and read as a steady tempo.
        var parsed = MusicNotation.ParseFull("@bpm=120->180 C4/4");
        var directives = parsed.Directives.ToArray();

        var written = MusicNotation.FormatWithDirectives(parsed.Notes.AsSpan(), directives.AsSpan());
        var reread = MusicNotation.ParseFull(written).Directives.OfType<TempoBpmDirective>().Single();

        Assert.Equal(120, reread.Bpm);
        Assert.Equal(180, reread.TargetBpm);
        Assert.Contains("180", directives.OfType<TempoBpmDirective>().Single().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ATempoRampWithALength_KeepsBoth()
    {
        var parsed = MusicNotation.ParseFull("@bpm=120->180/2 C4/4");
        var directives = parsed.Directives.ToArray();

        var reread = MusicNotation
            .ParseFull(MusicNotation.FormatWithDirectives(parsed.Notes.AsSpan(), directives.AsSpan()))
            .Directives.OfType<TempoBpmDirective>().Single();

        Assert.Equal(180, reread.TargetBpm);
        Assert.Equal(Rational.Half, reread.RampDuration);
    }

    // ---------- statistics describe the music, not the silence before it ----------

    [Fact]
    public void RhythmStatisticsMeasureTheMusicRatherThanTheTimeBeforeIt()
    {
        // Counting measures from time zero billed a passage its leading silence: four quarters
        // all inside bar 5 were reported as five measures at 0.80 notes per measure.
        using var buffer = new NoteBuffer(4);
        for (var i = 0; i < 4; i++)
            buffer.AddNote(60 + i, new Rational(16 + i, 4), Rational.Quarter);

        var statistics = RhythmAnalyzer.Analyze(buffer, new TimeSignature(4, 4)).Statistics;

        Assert.Equal(1, statistics.MeasureCount);
        Assert.Equal(4f, statistics.NotesPerMeasure);
    }

    [Fact]
    public void RhythmStatisticsStillCountEveryMeasureTheMusicSpans()
    {
        using var buffer = new NoteBuffer(8);
        for (var i = 0; i < 8; i++)
            buffer.AddNote(60 + i, new Rational(i, 4), Rational.Quarter);

        var statistics = RhythmAnalyzer.Analyze(buffer, new TimeSignature(4, 4)).Statistics;

        Assert.Equal(2, statistics.MeasureCount);
        Assert.Equal(4f, statistics.NotesPerMeasure);
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
