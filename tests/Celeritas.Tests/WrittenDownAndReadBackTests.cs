// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Harmonization;
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

    [Fact]
    public void ScoringADefaultChordCandidate_DoesNotDereferenceIt()
    {
        // ChordCandidate is a struct, so default is a value any caller can hand over, and its
        // Pitches are null there — the mask builder dereferenced them.
        var scorer = new DefaultTransitionScorer();
        var real = new ChordCandidate(ChordAnalyzer.Identify([60, 64, 67]), [60, 64, 67], 0f);

        scorer.ScoreTransition(default, real, new KeySignature(0, true));
        scorer.ScoreTransition(real, default, new KeySignature(0, true));
        scorer.ScoreFit(default, [60, 64], isStrongBeat: true);
    }

    [Fact]
    public void ScoringAMelodyBelowZero_ReadsTheRightPitchClass()
    {
        // `%` keeps the sign in C#, so a pitch below zero shifted by a negative amount — and the
        // shift count is masked to 5 bits, so the bit it set was not even in the 12-bit mask and
        // the note counted as foreign to every chord. -5 is a G, which a C major triad contains.
        var scorer = new DefaultTransitionScorer();
        var cMajor = new ChordCandidate(ChordAnalyzer.Identify([60, 64, 67]), [60, 64, 67], 0f);

        var below = scorer.ScoreFit(cMajor, [-5], isStrongBeat: true);
        var above = scorer.ScoreFit(cMajor, [67], isStrongBeat: true);

        Assert.Equal(above, below);
    }

    // ---------- what MIDI cannot hold, it is documented as not holding ----------

    [Fact]
    public void TwoSoundingsOfOnePitchThatOverlap_ComeBackWithTheirEndsRepaired()
    {
        // A MIDI note is a note-on and a note-off, so two soundings of the same pitch on one
        // channel cannot be told apart: the first note-off ends whichever is sounding. This
        // pins the documented consequence, so a change to it is a decision rather than a
        // surprise — MusicXmlIo, checked below, keeps the two apart.
        using var original = new NoteBuffer(2);
        original.AddNote(66, new Rational(5, 8), new Rational(3, 2));      // 5/8 .. 17/8
        original.AddNote(66, new Rational(9, 8), new Rational(3, 8));      // 9/8 .. 3/2, inside it

        var path = Path.Combine(Path.GetTempPath(), $"celeritas-overlap-{Guid.NewGuid():N}.mid");
        try
        {
            MidiIo.Export(original, path);
            using var reread = MidiIo.Import(path);

            var notes = Enumerable.Range(0, reread.Count)
                .Select(i => reread.Get(i))
                .OrderBy(n => n.Offset.ToDouble())
                .ToArray();

            Assert.Equal(2, notes.Length);
            Assert.Equal(new Rational(5, 8), notes[0].Offset);
            Assert.Equal(new Rational(7, 8), notes[0].Duration);           // ends at 3/2
            Assert.Equal(new Rational(9, 8), notes[1].Offset);
            Assert.Equal(Rational.Whole, notes[1].Duration);               // ends at 17/8
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MusicXmlKeepsTwoSoundingsOfOnePitchApart()
    {
        using var original = new NoteBuffer(2);
        original.AddNote(66, new Rational(5, 8), new Rational(3, 2));
        original.AddNote(66, new Rational(9, 8), new Rational(3, 8));

        using var reread = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));

        var notes = Enumerable.Range(0, reread.Count)
            .Select(i => (reread.Get(i).Offset, reread.Get(i).Duration))
            .OrderBy(n => n.Offset.ToDouble())
            .ToArray();

        Assert.Equal([(new Rational(5, 8), new Rational(3, 2)), (new Rational(9, 8), new Rational(3, 8))], notes);
    }

    [Fact]
    public void ADurationOffTheTickGrid_IsWrittenToTheNearestTick()
    {
        // A septuplet is 1920/28 = 68.57 ticks at the default 480 per quarter, written as 69.
        // Documented on MidiIo; pinned here so the grid cannot change without a decision.
        using var original = new NoteBuffer(1);
        original.AddNote(60, Rational.Zero, new Rational(1, 28));

        var path = Path.Combine(Path.GetTempPath(), $"celeritas-tick-{Guid.NewGuid():N}.mid");
        try
        {
            MidiIo.Export(original, path);
            using var reread = MidiIo.Import(path);

            Assert.Equal(new Rational(23, 640), reread.Get(0).Duration);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ADurationOnTheTickGrid_SurvivesExactly()
    {
        // Everything ordinary, and every triplet and quintuplet, is a whole number of ticks.
        Rational[] exact = [Rational.Whole, Rational.Half, Rational.Quarter, new(1, 32), new(1, 12), new(1, 20)];

        using var original = new NoteBuffer(exact.Length);
        var offset = Rational.Zero;
        foreach (var duration in exact)
        {
            original.AddNote(60, offset, duration);
            offset += duration;
        }

        var path = Path.Combine(Path.GetTempPath(), $"celeritas-grid-{Guid.NewGuid():N}.mid");
        try
        {
            MidiIo.Export(original, path);
            using var reread = MidiIo.Import(path);

            Assert.Equal(
                Enumerable.Range(0, original.Count).Select(i => original.Get(i).Duration),
                Enumerable.Range(0, reread.Count).Select(i => reread.Get(i).Duration));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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

    public static TheoryData<int, int> RampLengthsTheNotationMustHold => new()
    {
        { 2, 1 }, // two whole notes
        { 5, 4 }, // five quarters
        { 3, 1 }, // three whole notes
        { 7, 8 }, // seven eighths
        { 1, 3 }, // a triplet whole
        { 3, 2 }, // a dotted whole
        { 3, 8 }, // a dotted quarter
    };

    [Theory]
    [MemberData(nameof(RampLengthsTheNotationMustHold))]
    public void ATempoRampLongerThanOneNoteValue_IsWrittenAsTiedNoteValuesAndReadsBack(int numerator, int denominator)
    {
        // A ramp lasting two whole notes was written as "@bpm 120 -> 60 /2/1" — the rational
        // fallback in a position where the grammar wants a note value — and ParseFull refused
        // it. The written length is now tied note values, "/1~/1", as a held note is written.
        var length = new Rational(numerator, denominator);
        NotationDirective[] directives =
        [
            new TempoBpmDirective { Time = Rational.Zero, Bpm = 120, TargetBpm = 60, RampDuration = length },
        ];
        NoteEvent[] notes = [new NoteEvent(60, Rational.Zero, Rational.Quarter)];

        foreach (var useLetters in new[] { false, true })
        {
            var written = MusicNotation.FormatWithDirectives(notes, directives, useLetters: useLetters);
            var reread = MusicNotation.ParseFull(written).Directives.OfType<TempoBpmDirective>().Single();

            Assert.Equal(120, reread.Bpm);
            Assert.Equal(60, reread.TargetBpm);
            Assert.Equal(length, reread.RampDuration);
        }
    }

    [Fact]
    public void ATempoRampWrittenAsTiedNoteValues_IsReadAsTheirSum()
    {
        var reread = MusicNotation.ParseFull("@bpm 120 -> 60 /1~/1~/4 C4/4")
            .Directives.OfType<TempoBpmDirective>().Single();

        Assert.Equal(new Rational(9, 4), reread.RampDuration);

        var withLetters = MusicNotation.ParseFull("@bpm 120 -> 60 :h.~:e C4/4")
            .Directives.OfType<TempoBpmDirective>().Single();

        Assert.Equal(new Rational(7, 8), withLetters.RampDuration);
    }

    [Fact]
    public void ATempoRampWrittenAsTiedNoteValues_DoesNotTieTheNoteAfterIt()
    {
        // The tilde inside the ramp length belongs to the ramp; it must not reach forward and
        // join the notes that follow into one.
        var parsed = MusicNotation.ParseFull("@bpm 120 -> 60 /1~/1 C4/4 C4/4");

        Assert.Equal(2, parsed.Notes.Length);
        Assert.All(parsed.Notes, n => Assert.Equal(Rational.Quarter, n.Duration));
    }

    [Fact]
    public void ATempoRampsToStringWritesTheLengthAsTheNotationDoes()
    {
        // ToString answered the same question with the rational fallback, "/2/1", which is not
        // the notation's spelling of two whole notes.
        var ramp = new TempoBpmDirective
        {
            Time = Rational.Zero,
            Bpm = 120,
            TargetBpm = 60,
            RampDuration = new Rational(2, 1)
        };

        Assert.Equal("@bpm 120 -> 60 /1~/1 at 0", ramp.ToString());
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
    /// <summary>
    /// The two writers hold the same timeline. FormatWithDirectives walked the notes as one
    /// melodic line instead of separating them into voices, so it carried the three faults
    /// FormatNoteSequence was rewritten to lose: a gap between notes vanished, notes struck
    /// together but held for different lengths became a succession, and so did overlapping
    /// notes. Half of these passages were written as different music, and not one has a
    /// directive in it.
    /// </summary>
    [Theory]
    [MemberData(nameof(PassagesTheNotationMustHold))]
    public void BothWritersWriteTheSameMusic(string name, NoteEvent[] passage)
    {
        var asSequence = MusicNotation.FormatNoteSequence(passage);

        var withDirectives = MusicNotation.FormatWithDirectives(passage, []);

        Assert.Equal(asSequence, withDirectives);

        // and the sounding notes come back unchanged; a gap comes back as a written rest, and
        // separating voices reorders the events, so compare the passages and not their order
        static (int Pitch, Rational Offset, Rational Duration)[] Sounding(IEnumerable<NoteEvent> notes) =>
            [.. notes.Where(n => !Rests.IsRest(n.Pitch))
                .Select(n => (n.Pitch, n.Offset, n.Duration))
                .OrderBy(n => n.Offset).ThenBy(n => n.Pitch)];

        Assert.Equal(Sounding(passage), Sounding(MusicNotation.Parse(withDirectives)));

        _ = name;
    }

    public static TheoryData<string, NoteEvent[]> PassagesTheNotationMustHold() => new()
    {
        { "a plain melody", [new(60, Rational.Zero, Rational.Quarter), new(62, Rational.Quarter, Rational.Quarter)] },
        { "a gap between two notes", [new(60, Rational.Zero, Rational.Quarter), new(62, Rational.Half, Rational.Quarter)] },
        { "two voices struck together", [new(60, Rational.Zero, Rational.Quarter), new(64, Rational.Zero, Rational.Quarter), new(62, Rational.Quarter, Rational.Quarter), new(65, Rational.Quarter, Rational.Quarter)] },
        { "one onset, two lengths", [new(60, Rational.Zero, Rational.Half), new(64, Rational.Zero, Rational.Quarter)] },
        { "overlapping notes", [new(60, Rational.Zero, Rational.Half), new(64, Rational.Quarter, Rational.Half)] },
        { "a chord", [new(60, Rational.Zero, Rational.Half), new(64, Rational.Zero, Rational.Half), new(67, Rational.Zero, Rational.Half)] },
        { "music that does not start at zero", [new(60, Rational.Half, Rational.Quarter)] },
    };

    /// <summary>
    /// A directive label is quoted unless it lexes as one whole IDENT. The rule looked only at
    /// the first character, so "verse_1" and "abC" were written bare and read back as an
    /// identifier plus a stray number or pitch name, and single letters the lexer had already
    /// spoken for went bare too: b is a flat, q a quarter, f a dynamic. 93 of these 132 label
    /// and directive pairs came back as notation that will not parse.
    /// </summary>
    [Theory]
    [MemberData(nameof(LabelsAndDirectiveKinds))]
    public void ADirectiveLabelSurvivesWhateverItIsCalled(string label, string kind)
    {
        NotationDirective directive = kind switch
        {
            "section" => new SectionDirective { Label = label, Time = Rational.Zero },
            "part" => new PartDirective { Name = label, Time = Rational.Zero },
            _ => new TempoCharacterDirective { Character = label, Time = Rational.Zero },
        };

        var written = MusicNotation.FormatWithDirectives(
            [new NoteEvent(60, Rational.Zero, Rational.Quarter)], [directive]);

        var readBack = MusicNotation.ParseFull(written);

        var only = Assert.Single(readBack.Directives);
        var name = only switch
        {
            SectionDirective d => d.Label,
            PartDirective d => d.Name,
            TempoCharacterDirective d => d.Character,
            _ => null,
        };

        Assert.Equal(label, name);
    }

    public static TheoryData<string, string> LabelsAndDirectiveKinds()
    {
        string[] labels =
        [
            // words the lexer has already spoken for
            "b", "w", "h", "q", "e", "s", "t", "r", "rest",
            "p", "pp", "mf", "f", "ff", "sfz",
            "bpm", "tempo", "section", "part", "dynamics", "cresc", "dim", "to",
            "tr", "trill", "mord", "turn", "app",
            // shapes an identifier cannot hold
            "verse_1", "abC", "A", "B", "Chorus", "Verse 1", "2", "a-b", "",
            // and ones it can
            "a", "verse", "intro", "coda", "my_part", "tempos", "trills",
        ];

        var data = new TheoryData<string, string>();
        foreach (var label in labels)
        {
            foreach (var kind in new[] { "section", "part", "tempo" })
            {
                data.Add(label, kind);
            }
        }

        return data;
    }

    /// <summary>
    /// Directives keep their times through a passage that needs more than one voice to write.
    /// </summary>
    [Fact]
    public void DirectivesKeepTheirTimesAcrossAPolyphonicPassage()
    {
        NoteEvent[] passage =
        [
            new(60, Rational.Zero, Rational.Half),
            new(64, Rational.Zero, Rational.Quarter),
            new(65, Rational.Quarter, Rational.Quarter),
        ];

        NotationDirective[] directives =
        [
            new TempoBpmDirective { Bpm = 80, Time = Rational.Zero },
            new SectionDirective { Label = "middle", Time = Rational.Quarter },
            new DynamicsDirective { Type = DynamicsType.Static, StartLevel = "ff", Time = Rational.Half },
        ];

        var written = MusicNotation.FormatWithDirectives(passage, directives);
        var readBack = MusicNotation.ParseFull(written);

        Assert.Equal(
            directives.Select(d => d.Time).Order(),
            readBack.Directives.Select(d => d.Time).Order());

        var sounding = readBack.Notes.Where(n => !Rests.IsRest(n.Pitch))
            .Select(n => (n.Pitch, n.Offset, n.Duration)).Order().ToArray();
        Assert.Equal(
            passage.Select(n => (n.Pitch, n.Offset, n.Duration)).Order().ToArray(),
            sounding);
    }

    // ---------- a directive is written at its own time ----------

    private static string DescribeDirectives(IEnumerable<NotationDirective> directives) =>
        string.Join(" | ", directives.Select(d => d.ToString()).Order(StringComparer.Ordinal));

    /// <summary>
    /// A directive is written at its own time, wherever that falls. One whose time fell inside
    /// a note or a rest was written at the next note boundary instead, so it read back later
    /// than it was given: with directive times on a sixteenth grid, 2297 of 3000 random passages
    /// had at least one move. The note is now cut into tied pieces at the directive's time with
    /// the directive between them, a rest into two rests, and a chord the directive falls inside
    /// gives way, since the notation ties notes and not chords.
    /// </summary>
    [Theory]
    [MemberData(nameof(DirectivesInsideNotesAndRests))]
    public void ADirectiveInsideANoteOrARestReadsBackAtItsOwnTime(
        string name, NoteEvent[] passage, NotationDirective[] directives, string expected)
    {
        var written = MusicNotation.FormatWithDirectives(passage, directives);

        Assert.Equal(expected, written);

        var readBack = MusicNotation.ParseFull(written);
        Assert.Equal(DescribeDirectives(directives), DescribeDirectives(readBack.Directives));
        Assert.Equal(Describe(passage), Describe(readBack.Notes));

        _ = name;
    }

    public static TheoryData<string, NoteEvent[], NotationDirective[], string> DirectivesInsideNotesAndRests()
    {
        NoteEvent C4(Rational offset, Rational duration) => new(60, offset, duration);
        NotationDirective Bpm(Rational time) => new TempoBpmDirective { Bpm = 90, Time = time };
        NotationDirective Section(string label, Rational time) => new SectionDirective { Label = label, Time = time };

        return new TheoryData<string, NoteEvent[], NotationDirective[], string>
        {
            {
                "inside a note",
                [C4(Rational.Zero, Rational.Half)],
                [Bpm(Rational.Quarter)],
                "C4/4~ @bpm 90 C4/4"
            },
            {
                "inside the silence between two notes",
                [C4(Rational.Zero, Rational.Quarter), new(64, Rational.Half, Rational.Quarter)],
                [Bpm(new Rational(3, 8))],
                "C4/4 R/8 @bpm 90 R/8 E4/4"
            },
            {
                "inside a written rest",
                [new(MusicNotation.RestPitch, Rational.Zero, Rational.Half), new(64, Rational.Half, Rational.Quarter)],
                [Section("b", Rational.Quarter)],
                "R/4 @section \"b\" R/4 E4/4"
            },
            {
                "inside a chord",
                [C4(Rational.Zero, Rational.Half), new(64, Rational.Zero, Rational.Half), new(67, Rational.Zero, Rational.Half)],
                [new DynamicsDirective { Type = DynamicsType.Static, StartLevel = "ff", Time = Rational.Quarter }],
                "<< C4/4~ @dynamics ff C4/4 | [E4 G4]/2 >>"
            },
            {
                "two inside one note",
                [C4(Rational.Zero, Rational.Whole)],
                [Bpm(Rational.Quarter), Section("coda", new Rational(3, 4))],
                "C4/4~ @bpm 90 C4/2~ @section coda C4/4"
            },
            {
                "two at one moment inside a note",
                [C4(Rational.Zero, Rational.Whole)],
                [Bpm(Rational.Quarter), Section("coda", Rational.Quarter)],
                "C4/4~ @bpm 90 @section coda C4/2."
            },
            {
                "inside a note of the first voice of a polyphonic passage",
                [C4(Rational.Zero, Rational.Half), new(64, Rational.Zero, Rational.Quarter), new(65, Rational.Quarter, Rational.Quarter)],
                [Section("x", Rational.Eighth)],
                "<< E4/8~ @section x E4/8 F4/4 | C4/2 >>"
            },
            {
                "at the boundaries, where nothing is cut",
                [C4(Rational.Zero, Rational.Quarter), new(64, Rational.Quarter, Rational.Quarter)],
                [Bpm(Rational.Zero), Section("b", Rational.Quarter), Section("c", Rational.Half)],
                "@bpm 90 C4/4 @section \"b\" E4/4 @section c"
            },
        };
    }

    /// <summary>
    /// A sequence with no notes still writes its directives at their times, carried by rests.
    /// Written bare, as they were, every one of them read back at time zero.
    /// </summary>
    [Fact]
    public void ADirectiveOnlySequenceKeepsItsTimes()
    {
        NotationDirective[] directives =
        [
            new TempoBpmDirective { Bpm = 90, Time = Rational.Zero },
            new SectionDirective { Label = "coda", Time = Rational.Half },
        ];

        var written = MusicNotation.FormatWithDirectives([], directives);

        Assert.Equal("@bpm 90 R/2 @section coda", written);
        Assert.Equal(DescribeDirectives(directives), DescribeDirectives(MusicNotation.ParseFull(written).Directives));
    }

    [Fact]
    public void AnyDirectiveInAnyPassage_ReadsBackAtItsOwnTime()
    {
        (from pitches in Gen.Int[48, 72].Array[1, 6]
         from starts in Gen.Int[0, 16].Array[1, 6]
         from lengths in Gen.Int[1, 12].Array[1, 6]
         from rests in Gen.Bool.Array[1, 6]
         from times in Gen.Int[0, 40].Array[1, 3]
         select (pitches, starts, lengths, rests, times)).Sample(t =>
        {
            // Notes and directive times on a sixteenth grid, so a directive lands inside a note
            // or a rest far more often than on a boundary.
            var notes = new List<NoteEvent>();
            for (var i = 0; i < t.pitches.Length; i++)
            {
                var pitch = t.rests[i % t.rests.Length] && i % 3 == 2 ? MusicNotation.RestPitch : t.pitches[i];
                notes.Add(new NoteEvent(
                    pitch,
                    new Rational(t.starts[i % t.starts.Length], 16),
                    new Rational(t.lengths[i % t.lengths.Length], 16)));
            }

            // Two notes of the same pitch at the same instant are one note, not two.
            var distinct = notes.GroupBy(x => (x.Pitch, x.Offset)).Select(g => g.First()).ToArray();

            var directives = t.times
                .Select((time, i) => (NotationDirective)new TempoBpmDirective { Bpm = 60 + i, Time = new Rational(time, 16) })
                .ToArray();

            var readBack = MusicNotation.ParseFull(MusicNotation.FormatWithDirectives(distinct, directives));

            return DescribeDirectives(directives) == DescribeDirectives(readBack.Directives)
                && Describe(distinct) == Describe(readBack.Notes);
        }, iter: 1000);
    }
}
