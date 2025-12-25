using Celeritas.Core;

namespace Celeritas.Tests;

public class MusicNotationTests
{
    [Theory]
    [InlineData("C4", 60)]
    [InlineData("C#4", 61)]
    [InlineData("D4", 62)]
    [InlineData("Eb4", 63)]
    [InlineData("E4", 64)]
    [InlineData("A4", 69)]
    [InlineData("C5", 72)]
    [InlineData("C3", 48)]
    [InlineData("Bb3", 58)]
    [InlineData("F#5", 78)]
    public void ParseNote_ValidNotation_ShouldReturnCorrectMidi(string notation, int expectedMidi)
    {
        // Act
        var midi = MusicNotation.ParseNote(notation);

        // Assert
        Assert.Equal(expectedMidi, midi);
    }

    [Theory]
    [InlineData("c4", 60)]  // lowercase
    [InlineData("C#4", 61)] // sharp
    [InlineData("db4", 61)] // flat (enharmonic)
    public void ParseNote_CaseInsensitive_ShouldWork(string notation, int expectedMidi)
    {
        // Act
        var midi = MusicNotation.ParseNote(notation);

        // Assert
        Assert.Equal(expectedMidi, midi);
    }

    [Theory]
    [InlineData("60", 60)]
    [InlineData("69", 69)]
    [InlineData("127", 127)]
    [InlineData("0", 0)]
    public void ParseNote_MidiNumber_ShouldReturnSameNumber(string notation, int expectedMidi)
    {
        // Act
        var midi = MusicNotation.ParseNote(notation);

        // Assert
        Assert.Equal(expectedMidi, midi);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("H4")]      // Invalid note name
    [InlineData("C")]       // Missing octave
    [InlineData("C44")]     // Invalid octave
    [InlineData("C#b4")]    // Double accidental
    public void ParseNote_InvalidNotation_ShouldThrow(string notation)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => MusicNotation.ParseNote(notation));
    }

    [Fact]
    public void ParseNotes_MultipleNotes_ShouldParseAll()
    {
        // Arrange
        var notation = "C4 E4 G4";

        // Act
        var notes = MusicNotation.Parse(notation);
        var pitches = notes.Select(n => n.Pitch).ToArray();

        // Assert
        Assert.Equal(3, pitches.Length);
        Assert.Equal(60, pitches[0]); // C4
        Assert.Equal(64, pitches[1]); // E4
        Assert.Equal(67, pitches[2]); // G4
    }

    [Fact]
    public void ParseNotes_CommaSeparated_ShouldParseAll()
    {
        // Arrange
        var notation = "C4 E4 G4"; // ANTLR parser uses spaces, not commas

        // Act
        var notes = MusicNotation.Parse(notation);
        var pitches = notes.Select(n => n.Pitch).ToArray();

        // Assert
        Assert.Equal(3, pitches.Length);
        Assert.Equal(60, pitches[0]);
        Assert.Equal(64, pitches[1]);
        Assert.Equal(67, pitches[2]);
    }

    [Fact]
    public void ParseNotes_MixedFormats_ShouldWork()
    {
        // Arrange - ANTLR parser uses scientific notation
        var notation = "C4 E4 G4";

        // Act
        var notes = MusicNotation.Parse(notation);
        var pitches = notes.Select(n => n.Pitch).ToArray();

        // Assert
        Assert.Equal(3, pitches.Length);
        Assert.Equal(60, pitches[0]);
        Assert.Equal(64, pitches[1]);
        Assert.Equal(67, pitches[2]);
    }

    [Theory]
    [InlineData(60, "C4")]
    [InlineData(61, "C#4")]
    [InlineData(69, "A4")]
    [InlineData(72, "C5")]
    [InlineData(48, "C3")]
    public void ToNotation_ValidMidi_ShouldReturnCorrectNotation(int midi, string expectedNotation)
    {
        // Act
        var notation = MusicNotation.ToNotation(midi);

        // Assert
        Assert.Equal(expectedNotation, notation);
    }

    [Theory]
    [InlineData(61, true, "C#4")]
    [InlineData(61, false, "Db4")]
    [InlineData(63, true, "D#4")]
    [InlineData(63, false, "Eb4")]
    public void ToNotation_WithPreference_ShouldRespectSharpsFlats(int midi, bool preferSharps, string expectedNotation)
    {
        // Act
        var notation = MusicNotation.ToNotation(midi, preferSharps);

        // Assert
        Assert.Equal(expectedNotation, notation);
    }

    [Theory]
    [InlineData("C", "C", true)]
    [InlineData("Cm", "C", false)]
    [InlineData("C minor", "C", false)]
    [InlineData("c", "C", true)]
    [InlineData("cm", "C", false)]
    [InlineData("C major", "C", true)]
    [InlineData("C#", "C#", true)]
    [InlineData("C#m", "C#", false)]
    [InlineData("Db minor", "C#", false)]  // Db is enharmonic to C#
    public void ParseKey_VariousFormats_ShouldParseCorrectly(string input, string expectedRoot, bool expectedMajor)
    {
        // Act
        var key = MusicNotation.ParseKey(input);

        // Assert
        Assert.Equal(expectedMajor, key.IsMajor);
        Assert.Equal(expectedRoot, ChordLibrary.NoteNames[key.Root]);
    }

    [Fact]
    public void ParseKey_EmptyString_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => MusicNotation.ParseKey(""));
    }

    // ===== Duration Parsing Tests =====

    [Theory]
    [InlineData("1", 1, 1)]
    [InlineData("2", 1, 2)]
    [InlineData("4", 1, 4)]
    [InlineData("8", 1, 8)]
    [InlineData("16", 1, 16)]
    [InlineData("32", 1, 32)]
    public void ParseDuration_NumericFormats(string input, int numerator, int denominator)
    {
        var duration = MusicNotation.ParseDuration(input);
        Assert.Equal(new Rational(numerator, denominator), duration);
    }

    [Theory]
    [InlineData("w", 1, 1)]
    [InlineData("h", 1, 2)]
    [InlineData("q", 1, 4)]
    [InlineData("e", 1, 8)]
    [InlineData("s", 1, 16)]
    [InlineData("whole", 1, 1)]
    [InlineData("half", 1, 2)]
    [InlineData("quarter", 1, 4)]
    [InlineData("eighth", 1, 8)]
    public void ParseDuration_LetterFormats(string input, int numerator, int denominator)
    {
        var duration = MusicNotation.ParseDuration(input);
        Assert.Equal(new Rational(numerator, denominator), duration);
    }

    [Theory]
    [InlineData("1.", 3, 2)]    // Whole + half = 3/2
    [InlineData("2.", 3, 4)]    // Half + quarter = 3/4
    [InlineData("4.", 3, 8)]    // Quarter + eighth = 3/8
    [InlineData("8.", 3, 16)]   // Eighth + sixteenth = 3/16
    [InlineData("q.", 3, 8)]
    [InlineData("h.", 3, 4)]
    [InlineData("e.", 3, 16)]
    public void ParseDuration_DottedNotes(string input, int numerator, int denominator)
    {
        var duration = MusicNotation.ParseDuration(input);
        Assert.Equal(new Rational(numerator, denominator), duration);
    }

    // ===== Note Sequence Parsing Tests =====

    [Fact]
    public void Parse_SimpleSequence()
    {
        var sequence = MusicNotation.Parse("C4/4 E4/4 G4/4");

        Assert.Equal(3, sequence.Length);

        // C4 at time 0
        Assert.Equal(60, sequence[0].Pitch);
        Assert.Equal(Rational.Zero, sequence[0].Offset);
        Assert.Equal(new Rational(1, 4), sequence[0].Duration);

        // E4 at time 1/4
        Assert.Equal(64, sequence[1].Pitch);
        Assert.Equal(new Rational(1, 4), sequence[1].Offset);

        // G4 at time 2/4
        Assert.Equal(67, sequence[2].Pitch);
        Assert.Equal(new Rational(1, 2), sequence[2].Offset);
    }

    [Fact]
    public void Parse_ColonSyntax()
    {
        var sequence = MusicNotation.Parse("C4:q E4:e G4:h");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(new Rational(1, 4), sequence[0].Duration);
        Assert.Equal(new Rational(1, 8), sequence[1].Duration);
        Assert.Equal(new Rational(1, 2), sequence[2].Duration);
    }

    [Fact]
    public void Parse_DottedNotes()
    {
        var sequence = MusicNotation.Parse("C4/4. E4/8 G4/2.");

        Assert.Equal(3, sequence.Length);

        // Dotted quarter: 3/8
        Assert.Equal(new Rational(3, 8), sequence[0].Duration);
        Assert.Equal(Rational.Zero, sequence[0].Offset);

        // Regular eighth: 1/8
        Assert.Equal(new Rational(1, 8), sequence[1].Duration);
        Assert.Equal(new Rational(3, 8), sequence[1].Offset);

        // Dotted half: 3/4
        Assert.Equal(new Rational(3, 4), sequence[2].Duration);
        Assert.Equal(new Rational(1, 2), sequence[2].Offset);
    }

    [Fact]
    public void Parse_RealMelody_TwinkleTwinkle()
    {
        // "Twinkle Twinkle Little Star" first phrase (8 quarter notes = 2 bars)
        var sequence = MusicNotation.Parse(
            "C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2");

        Assert.Equal(7, sequence.Length);

        // Pitches
        Assert.Equal(60, sequence[0].Pitch);  // C
        Assert.Equal(60, sequence[1].Pitch);  // C
        Assert.Equal(67, sequence[2].Pitch);  // G
        Assert.Equal(67, sequence[3].Pitch);  // G
        Assert.Equal(69, sequence[4].Pitch);  // A
        Assert.Equal(69, sequence[5].Pitch);  // A
        Assert.Equal(67, sequence[6].Pitch);  // G (half note)

        // Last note is half, others are quarters
        Assert.Equal(new Rational(1, 2), sequence[6].Duration);

        // Total duration: 6 quarters + 1 half = 2 whole notes
        var totalDuration = Rational.Zero;
        foreach (var note in sequence)
            totalDuration += note.Duration;
        Assert.Equal(new Rational(2, 1), totalDuration);
    }

    [Fact]
    public void Parse_MixedDurations()
    {
        var sequence = MusicNotation.Parse("C4/1 D4/2 E4/4 F4/8 G4/16");

        Assert.Equal(5, sequence.Length);

        // Check cumulative offsets
        Assert.Equal(Rational.Zero, sequence[0].Offset);
        Assert.Equal(new Rational(1, 1), sequence[1].Offset);
        Assert.Equal(new Rational(3, 2), sequence[2].Offset);
        Assert.Equal(new Rational(7, 4), sequence[3].Offset);
        Assert.Equal(new Rational(15, 8), sequence[4].Offset);
    }

    [Fact]
    public void Parse_WithSharpsAndFlats()
    {
        var sequence = MusicNotation.Parse("C#4/4 Eb4/4 F#4/4 Ab4/4");

        Assert.Equal(4, sequence.Length);
        Assert.Equal(61, sequence[0].Pitch);  // C#
        Assert.Equal(63, sequence[1].Pitch);  // Eb
        Assert.Equal(66, sequence[2].Pitch);  // F#
        Assert.Equal(68, sequence[3].Pitch);  // Ab
    }

    // ===== Duration Formatting Tests =====

    [Theory]
    [InlineData(1, 1, "1")]
    [InlineData(1, 2, "2")]
    [InlineData(1, 4, "4")]
    [InlineData(1, 8, "8")]
    [InlineData(1, 16, "16")]
    [InlineData(1, 32, "32")]
    public void FormatDuration_StandardDurations(int numerator, int denominator, string expected)
    {
        var duration = new Rational(numerator, denominator);
        var formatted = MusicNotation.FormatDuration(duration);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData(3, 2, "1.")]
    [InlineData(3, 4, "2.")]
    [InlineData(3, 8, "4.")]
    [InlineData(3, 16, "8.")]
    public void FormatDuration_DottedNotes(int numerator, int denominator, string expected)
    {
        var duration = new Rational(numerator, denominator);
        var formatted = MusicNotation.FormatDuration(duration);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void FormatNoteSequence_SimpleSequence()
    {
        var sequence = new[]
        {
            new NoteEvent(60, Rational.Zero, new Rational(1, 4)),
            new NoteEvent(64, new Rational(1, 4), new Rational(1, 4)),
            new NoteEvent(67, new Rational(1, 2), new Rational(1, 4))
        };

        var formatted = MusicNotation.FormatNoteSequence(sequence);
        Assert.Equal("C4/4 E4/4 G4/4", formatted);
    }

    [Fact]
    public void FormatNoteSequence_DottedNotes()
    {
        var sequence = new[]
        {
            new NoteEvent(60, Rational.Zero, new Rational(3, 8)),
            new NoteEvent(64, new Rational(3, 8), new Rational(1, 8)),
            new NoteEvent(67, new Rational(1, 2), new Rational(3, 4))
        };

        var formatted = MusicNotation.FormatNoteSequence(sequence);
        Assert.Equal("C4/4. E4/8 G4/2.", formatted);
    }

    [Fact]
    public void FormatNoteSequence_RoundTrip()
    {
        var original = "C4/4 E4/8 G4/2. B4/4.";
        var parsed = MusicNotation.Parse(original);
        var formatted = MusicNotation.FormatNoteSequence(parsed);

        Assert.Equal(original, formatted);
    }

    [Fact]
    public void FormatNoteSequence_TwinkleTwinkle()
    {
        var sequence = MusicNotation.Parse(
            "C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2");

        var formatted = MusicNotation.FormatNoteSequence(sequence);
        Assert.Equal("C4/4 C4/4 G4/4 G4/4 A4/4 A4/4 G4/2", formatted);
    }

    [Fact]
    public void Parse_WithRests()
    {
        var sequence = MusicNotation.Parse("C4/4 R/4 E4/4 R/2");

        Assert.Equal(4, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(MusicNotation.REST_PITCH, sequence[1].Pitch); // Rest
        Assert.Equal(64, sequence[2].Pitch); // E4
        Assert.Equal(MusicNotation.REST_PITCH, sequence[3].Pitch); // Rest

        Assert.Equal(new Rational(1, 4), sequence[1].Duration); // Quarter rest
        Assert.Equal(new Rational(1, 2), sequence[3].Duration); // Half rest
    }

    [Fact]
    public void Parse_RestsWithLetters()
    {
        var sequence = MusicNotation.Parse("C4:q R:q E4:e R:h");

        Assert.Equal(4, sequence.Length);
        Assert.Equal(MusicNotation.REST_PITCH, sequence[1].Pitch);
        Assert.Equal(MusicNotation.REST_PITCH, sequence[3].Pitch);
    }

    [Fact]
    public void FormatNoteSequence_WithRests()
    {
        var sequence = MusicNotation.Parse("C4/4 R/4 E4/8 R/2.");
        var formatted = MusicNotation.FormatNoteSequence(sequence);

        Assert.Equal("C4/4 R/4 E4/8 R/2.", formatted);
    }

    [Fact]
    public void FormatNoteSequence_RestsWithLetters()
    {
        var sequence = MusicNotation.Parse("C4:q R:q E4:e");
        var formatted = MusicNotation.FormatNoteSequence(sequence, useDot: true, useLetters: true);

        Assert.Equal("C4:q R:q E4:e", formatted);
    }

    [Fact]
    public void Parse_WithTimeSignature()
    {
        // Time signature is now parsed from string
        var sequence = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4");

        // Should parse normally
        Assert.Equal(3, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch);
        Assert.Equal(64, sequence[1].Pitch);
        Assert.Equal(67, sequence[2].Pitch);
    }

    [Fact]
    public void Parse_RestsRoundTrip()
    {
        var original = "C4/4 R/4 E4/8 G4/2. R/2";
        var parsed = MusicNotation.Parse(original);
        var formatted = MusicNotation.FormatNoteSequence(parsed);

        Assert.Equal(original, formatted);
    }

    [Fact]
    public void Parse_TimeSignatureInString_WithColon()
    {
        var sequence = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(64, sequence[1].Pitch); // E4
        Assert.Equal(67, sequence[2].Pitch); // G4
    }

    [Fact]
    public void Parse_TimeSignatureInString_WithBar()
    {
        var sequence = MusicNotation.Parse("4/4| C4/4 E4/4 G4/4 C5/4");

        Assert.Equal(4, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
    }

    [Fact]
    public void Parse_TimeSignatureInString_WithSpaces()
    {
        var sequence = MusicNotation.Parse("6/8 | C4:q E4:e G4:h");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(new Rational(1, 4), sequence[0].Duration);
    }

    [Fact]
    public void Parse_TimeSignatureInString_WithRests()
    {
        var sequence = MusicNotation.Parse("3/4: C4/4 R/4 E4/4");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(MusicNotation.REST_PITCH, sequence[1].Pitch);
    }

    [Fact]
    public void Parse_WithMeasureBars()
    {
        var sequence = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4 | D4/4 F4/4 A4/4 D5/4");

        Assert.Equal(8, sequence.Length);
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(62, sequence[4].Pitch); // D4 (first note of second measure)
    }

    [Fact]
    public void Parse_ValidateMeasures_Correct()
    {
        // Should not throw - measures are correct
        var sequence = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4 A4/4", validateMeasures: true);

        Assert.Equal(6, sequence.Length);
    }

    [Fact]
    public void Parse_ValidateMeasures_Incorrect()
    {
        // Should throw - second measure has only 2 quarter notes (missing 1/4)
        var ex = Assert.Throws<ArgumentException>(() =>
            MusicNotation.Parse("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4", validateMeasures: true));

        Assert.Contains("Measure 2 duration mismatch", ex.Message);
        Assert.Contains("expected 3/4", ex.Message);
        Assert.Contains("got 1/2", ex.Message);
    }

    [Fact]
    public void Parse_ValidateMeasures_WithDottedNotes()
    {
        // 6/8: two dotted quarters = 3/8 + 3/8 = 6/8 ✓
        var sequence = MusicNotation.Parse("6/8: C4/4. E4/4.", validateMeasures: true);

        Assert.Equal(2, sequence.Length);
    }

    [Fact]
    public void Parse_ValidateMeasures_WithRests()
    {
        // 4/4: quarter + quarter rest + half = 1/4 + 1/4 + 1/2 = 1 ✓
        var sequence = MusicNotation.Parse("4/4: C4/4 R/4 G4/2 | E4/4 G4/4 C5/2", validateMeasures: true);

        Assert.Equal(6, sequence.Length);
    }

    [Fact]
    public void Parse_MultipleMeasures_WithTrailingBar()
    {
        // Trailing bar should be ignored
        var sequence = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4 A4/4 |");

        Assert.Equal(6, sequence.Length);
    }

    [Fact]
    public void Parse_ChordWithSquareBrackets()
    {
        var sequence = MusicNotation.Parse("C4/4 [C4 E4 G4]/4 D4/4");

        Assert.Equal(5, sequence.Length);
        // First note: C4
        Assert.Equal(60, sequence[0].Pitch);
        Assert.Equal(new Rational(0, 1), sequence[0].Offset);

        // Chord notes: C4, E4, G4 (all at same time)
        Assert.Equal(60, sequence[1].Pitch); // C4
        Assert.Equal(64, sequence[2].Pitch); // E4
        Assert.Equal(67, sequence[3].Pitch); // G4
        Assert.Equal(new Rational(1, 4), sequence[1].Offset);
        Assert.Equal(new Rational(1, 4), sequence[2].Offset);
        Assert.Equal(new Rational(1, 4), sequence[3].Offset);

        // Last note: D4
        Assert.Equal(62, sequence[4].Pitch);
        Assert.Equal(new Rational(1, 2), sequence[4].Offset);
    }

    [Fact]
    public void Parse_ChordWithParentheses()
    {
        var sequence = MusicNotation.Parse("(C4 E4 G4):q (D4 F4 A4):q");

        Assert.Equal(6, sequence.Length);
        // First chord
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(64, sequence[1].Pitch); // E4
        Assert.Equal(67, sequence[2].Pitch); // G4

        // Second chord
        Assert.Equal(62, sequence[3].Pitch); // D4
        Assert.Equal(65, sequence[4].Pitch); // F4
        Assert.Equal(69, sequence[5].Pitch); // A4
    }

    [Fact]
    public void Parse_ChordWithDottedDuration()
    {
        var sequence = MusicNotation.Parse("[C4 E4 G4]/4.");

        Assert.Equal(3, sequence.Length);
        Assert.Equal(new Rational(3, 8), sequence[0].Duration); // Dotted quarter
        Assert.Equal(new Rational(3, 8), sequence[1].Duration);
        Assert.Equal(new Rational(3, 8), sequence[2].Duration);
    }

    [Fact]
    public void Parse_ChordInMeasure()
    {
        var sequence = MusicNotation.Parse("4/4: C4/4 [E4 G4]/4 C5/2 | [D4 F4 A4]/1");

        Assert.Equal(7, sequence.Length);
        // Measure 1: single, chord, single
        Assert.Equal(60, sequence[0].Pitch); // C4
        Assert.Equal(64, sequence[1].Pitch); // E4 (chord)
        Assert.Equal(67, sequence[2].Pitch); // G4 (chord)
        Assert.Equal(72, sequence[3].Pitch); // C5

        // Measure 2: whole note chord
        Assert.Equal(62, sequence[4].Pitch); // D4
        Assert.Equal(65, sequence[5].Pitch); // F4
        Assert.Equal(69, sequence[6].Pitch); // A4
    }

    [Fact]
    public void Parse_ChordValidation()
    {
        // 3/4 measure: quarter + chord quarter + quarter = 3/4 ✓
        var sequence = MusicNotation.Parse(
            "3/4: C4/4 [E4 G4]/4 B4/4",
            validateMeasures: true);

        Assert.Equal(4, sequence.Length);
    }

    [Fact]
    public void Parse_MixedNotationWithChords()
    {
        var sequence = MusicNotation.Parse("3/4: C4/4 [E4 G4]:q R/4 | [D4 F4 A4]/2.");

        Assert.Equal(7, sequence.Length);
        Assert.Equal(MusicNotation.REST_PITCH, sequence[3].Pitch); // Rest
    }
}
