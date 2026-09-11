// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Notation output: the dotted and short-value duration names, chord grouping and rests in
/// <see cref="MusicNotation.FormatWithDirectives"/>, and the two key-profile entry points that
/// had no test at all. Formatting failures are silent — a wrong duration name still round-trips
/// as a string — so each case pins the exact text.
/// </summary>
public class NotationFormattingTests
{
    // ---------- dotted durations ----------

    [Theory]
    [InlineData(3, 2, "w.")]
    [InlineData(3, 4, "h.")]
    [InlineData(3, 8, "q.")]
    [InlineData(3, 16, "e.")]
    [InlineData(3, 32, "s.")]
    [InlineData(3, 64, "t.")]
    public void DottedDurations_HaveLetterNames(int num, int den, string expected)
    {
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den), useDot: true, useLetters: true));
    }

    [Theory]
    [InlineData(3, 2, "1.")]
    [InlineData(3, 4, "2.")]
    [InlineData(3, 8, "4.")]
    [InlineData(3, 16, "8.")]
    [InlineData(3, 32, "16.")]
    [InlineData(3, 64, "32.")]
    public void DottedDurations_HaveNumericNames(int num, int den, string expected)
    {
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den), useDot: true, useLetters: false));
    }

    [Theory]
    [InlineData(3, 128, "64.")]
    [InlineData(3, 256, "128.")]
    [InlineData(3, 2048, "1024.")]
    public void ADotShorterThanASixtyFourth_IsStillADot(int num, int den, string expected)
    {
        // This test used to pin "3/128" with the comment "anything shorter has no name to give".
        // The reader gives it one — ParseDuration("64.") is 3/128 — so the pin cemented a writer
        // whose text its own reader refused: a passage holding a dotted 64th could not be saved
        // and read back at all.
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den), useDot: true, useLetters: false));
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den), useDot: true, useLetters: true));
        Assert.Equal(new Rational(num, den), MusicNotation.ParseDuration(expected));

        var text = MusicNotation.FormatNoteSequence([new NoteEvent(60, Rational.Zero, new Rational(num, den))]);
        Assert.Equal(new Rational(num, den), Assert.Single(MusicNotation.Parse(text)).Duration);
    }

    [Fact]
    public void WithDotsOff_ADottedValueIsPrintedAsARatio()
    {
        Assert.Equal("3/8", MusicNotation.FormatDuration(new Rational(3, 8), useDot: false));
    }

    // ---------- plain durations ----------

    [Theory]
    [InlineData(1, 1, "w")]
    [InlineData(1, 2, "h")]
    [InlineData(1, 4, "q")]
    [InlineData(1, 8, "e")]
    [InlineData(1, 16, "s")]
    [InlineData(1, 32, "t")]
    public void PlainDurations_HaveLetterNames(int num, int den, string expected)
    {
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den), useLetters: true));
    }

    [Theory]
    [InlineData(1, 16, "16")]
    [InlineData(1, 32, "32")]
    public void ShortPlainDurations_HaveNumericNames(int num, int den, string expected)
    {
        Assert.Equal(expected, MusicNotation.FormatDuration(new Rational(num, den)));
    }

    [Fact]
    public void ADurationTheLetterTableCannotName_FallsBackToTheFormThatParses()
    {
        // This used to require "1/64" of the letter arm, pinning as correct a string nothing in
        // the library reads: ParseDuration rejected it, and so did the grammar inside a note. The
        // numeric arm beside it already answered "64", which is how the grammar spells every note
        // value outside the named ones, and both arms now agree.
        Assert.Equal("64", MusicNotation.FormatDuration(new Rational(1, 64), useLetters: true));
        Assert.Equal("64", MusicNotation.FormatDuration(new Rational(1, 64), useLetters: false));
        Assert.Equal(new Rational(1, 64), MusicNotation.ParseDuration("64"));
    }

    [Fact]
    public void ADurationWithNoSingleWrittenForm_IsPrintedAsARatio()
    {
        // Five eighths is tied notes, which only a sequence can express — FormatNoteSequence
        // splits it — so a lone duration being displayed shows the rational. That string is for
        // reading, not for parsing, and ParseDuration says so.
        Assert.Equal("5/8", MusicNotation.FormatDuration(new Rational(5, 8)));
        Assert.Throws<ArgumentException>(() => MusicNotation.ParseDuration("5/8"));

        // ...and the sequence writer never puts one in front of the parser.
        NoteEvent[] five = [new(60, Rational.Zero, new Rational(5, 8))];
        var text = MusicNotation.FormatNoteSequence(five);
        Assert.Equal(
            new Rational(5, 8),
            MusicNotation.Parse(text).Aggregate(Rational.Zero, (total, n) => total + n.Duration));
    }

    // ---------- FormatWithDirectives ----------

    private static NoteEvent Note(int pitch, Rational offset, Rational duration) => new(pitch, offset, duration);

    [Fact]
    public void SimultaneousNotes_AreGroupedAsAChord()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Quarter),
            Note(64, Rational.Zero, Rational.Quarter),
            Note(67, Rational.Zero, Rational.Quarter),
        ];

        var text = MusicNotation.FormatWithDirectives(notes, []);

        Assert.Equal("[C4 E4 G4]/4", text);
    }

    [Fact]
    public void ChordGrouping_UsesTheLetterSeparatorInLetterMode()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Quarter),
            Note(64, Rational.Zero, Rational.Quarter),
        ];

        var text = MusicNotation.FormatWithDirectives(notes, [], useLetters: true);

        Assert.Equal("[C4 E4]:q", text);
    }

    [Fact]
    public void ChordGrouping_CanBeTurnedOff()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Quarter),
            Note(64, Rational.Zero, Rational.Quarter),
        ];

        var text = MusicNotation.FormatWithDirectives(notes, [], groupChords: false);

        // Not "C4/4 E4/4": that reads back as a C followed by an E. Declining to write the two
        // as one chord does not make them consecutive — they are still struck together, so the
        // notation needs two voices to say so. This asserted the succession until the writer
        // was made to separate voices the way FormatNoteSequence already did.
        Assert.Equal("<< C4/4 | E4/4 >>", text);
        Assert.Equal(MusicNotation.FormatNoteSequence(notes, groupChords: false), text);
    }

    [Fact]
    public void ARest_IsPrintedAsR()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Quarter),
            Note(MusicNotation.RestPitch, Rational.Quarter, Rational.Half),
            Note(67, new Rational(3, 4), Rational.Quarter),
        ];

        var text = MusicNotation.FormatWithDirectives(notes, []);

        Assert.Equal("C4/4 R/2 G4/4", text);
    }

    [Fact]
    public void NotesAtTheSameOffsetButDifferentLengths_AreNotAChord()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Half),
            Note(64, Rational.Zero, Rational.Quarter),
        ];

        var text = MusicNotation.FormatWithDirectives(notes, []);

        Assert.DoesNotContain('[', text);
    }

    [Fact]
    public void ADirectiveFallingBetweenTwoNotes_IsEmittedInTimelineOrder()
    {
        NoteEvent[] notes =
        [
            Note(60, Rational.Zero, Rational.Quarter),
            Note(64, Rational.Half, Rational.Quarter),
        ];
        NotationDirective[] directives =
        [
            new TempoBpmDirective { Time = new Rational(3, 8), Bpm = 90 },
        ];

        var text = MusicNotation.FormatWithDirectives(notes, directives);

        var directivePos = text.IndexOf("@bpm", StringComparison.Ordinal);
        Assert.True(directivePos > 0, $"the directive was dropped: {text}");
        Assert.True(text.IndexOf("C4", StringComparison.Ordinal) < directivePos);
        Assert.True(text.IndexOf("E4", StringComparison.Ordinal) > directivePos);
    }

    [Fact]
    public void ADirectiveAtTheStart_ComesFirst()
    {
        NoteEvent[] notes = [Note(60, Rational.Zero, Rational.Quarter)];
        NotationDirective[] directives = [new TempoBpmDirective { Time = Rational.Zero, Bpm = 120 }];

        var text = MusicNotation.FormatWithDirectives(notes, directives);

        Assert.StartsWith("@bpm 120", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingToFormat_IsAnEmptyString()
    {
        Assert.Equal(string.Empty, MusicNotation.FormatWithDirectives([], []));
        Assert.Equal(string.Empty, MusicNotation.FormatNoteSequence([]));
    }

    // ---------- key profiles ----------

    [Fact]
    public void ChordKeyFit_PrefersTheKeyTheChordBelongsTo()
    {
        var cMajorTriad = ChordAnalyzer.GetMask([60, 64, 67]);

        var atHome = KeyProfiler.ChordKeyFit(cMajorTriad, new KeySignature(0, true));
        var faraway = KeyProfiler.ChordKeyFit(cMajorTriad, new KeySignature(1, true));

        Assert.True(atHome > faraway, $"C major triad fit C major {atHome} but C# major {faraway}");
    }

    [Fact]
    public void ChordKeyFit_IsTheSumOfTheProfileWeightsTheChordTouches()
    {
        var key = new KeySignature(0, true);
        var profile = KeyProfiler.GetKeyProfile(0, isMajor: true);
        var mask = ChordAnalyzer.GetMask([60, 64, 67]);

        var expected = profile[0] + profile[4] + profile[7];

        Assert.Equal(expected, KeyProfiler.ChordKeyFit(mask, key), 4);
    }

    [Fact]
    public void ChordKeyFit_AnEmptyMaskFitsNothing()
    {
        Assert.Equal(0f, KeyProfiler.ChordKeyFit(0, new KeySignature(0, true)));
    }

    [Fact]
    public void ChordKeyFit_ReadsTheMinorProfileForAMinorKey()
    {
        var aMinorTriad = ChordAnalyzer.GetMask([57, 60, 64]);

        var minor = KeyProfiler.ChordKeyFit(aMinorTriad, new KeySignature(9, false));
        var major = KeyProfiler.ChordKeyFit(aMinorTriad, new KeySignature(9, true));

        Assert.NotEqual(minor, major);
    }

    [Fact]
    public void ChordKeyFit_IsTranspositionInvariant()
    {
        // The same chord shape a fifth up in the key a fifth up must fit exactly as well.
        var c = ChordAnalyzer.GetMask([60, 64, 67]);
        var g = ChordAnalyzer.GetMask([67, 71, 74]);

        Assert.Equal(
            KeyProfiler.ChordKeyFit(c, new KeySignature(0, true)),
            KeyProfiler.ChordKeyFit(g, new KeySignature(7, true)),
            4);
    }

    [Fact]
    public void DetectFromPitches_NotationWithNoNotes_IsUndecided()
    {
        var result = KeyProfiler.DetectFromPitches("");

        Assert.Equal(0f, result.Confidence);
        Assert.Empty(result.AllCorrelations);
        Assert.False(result.IsDecidable);
    }
}
