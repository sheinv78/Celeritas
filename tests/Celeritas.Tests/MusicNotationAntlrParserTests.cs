// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;

namespace Celeritas.Tests;

public class MusicNotationAntlrParserTests
{
    [Fact]
    public void Parse_SimpleNote()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4/4");

        Assert.Single(notes);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(new Rational(1, 4), notes[0].Duration);
    }

    [Fact]
    public void Parse_MultipleNotes()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4/4 E4/4 G4/2");

        Assert.Equal(3, notes.Length);
        Assert.Equal(60, notes[0].Pitch); // C4
        Assert.Equal(64, notes[1].Pitch); // E4
        Assert.Equal(67, notes[2].Pitch); // G4

        Assert.Equal(new Rational(0, 1), notes[0].Offset);
        Assert.Equal(new Rational(1, 4), notes[1].Offset);
        Assert.Equal(new Rational(1, 2), notes[2].Offset);
    }

    [Fact]
    public void Parse_WithAccidentals()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C#4/4 Db4/4 F#4/4 Bb4/4");

        Assert.Equal(4, notes.Length);
        Assert.Equal(61, notes[0].Pitch); // C#4
        Assert.Equal(61, notes[1].Pitch); // Db4 = C#4
        Assert.Equal(66, notes[2].Pitch); // F#4
        Assert.Equal(70, notes[3].Pitch); // Bb4
    }

    [Fact]
    public void Parse_DottedNotes()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4/4. E4/2.");

        Assert.Equal(2, notes.Length);
        Assert.Equal(new Rational(3, 8), notes[0].Duration); // Dotted quarter
        Assert.Equal(new Rational(3, 4), notes[1].Duration); // Dotted half
    }

    [Fact]
    public void Parse_LetterDurations()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4:q E4:h G4:w");

        Assert.Equal(3, notes.Length);
        Assert.Equal(new Rational(1, 4), notes[0].Duration); // quarter
        Assert.Equal(new Rational(1, 2), notes[1].Duration); // half
        Assert.Equal(new Rational(1, 1), notes[2].Duration); // whole
    }

    [Fact]
    public void Parse_Rest()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4/4 R/4 E4/4");

        Assert.Equal(3, notes.Length);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(MusicNotation.REST_PITCH, notes[1].Pitch);
        Assert.Equal(64, notes[2].Pitch);

        Assert.Equal(new Rational(1, 2), notes[2].Offset); // After quarter + quarter rest
    }

    [Fact]
    public void Parse_Chord_SquareBrackets()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("[C4 E4 G4]/4");

        Assert.Equal(3, notes.Length);
        Assert.Equal(60, notes[0].Pitch); // C4
        Assert.Equal(64, notes[1].Pitch); // E4
        Assert.Equal(67, notes[2].Pitch); // G4

        // All at same time
        Assert.Equal(new Rational(0, 1), notes[0].Offset);
        Assert.Equal(new Rational(0, 1), notes[1].Offset);
        Assert.Equal(new Rational(0, 1), notes[2].Offset);

        // Same duration
        Assert.Equal(new Rational(1, 4), notes[0].Duration);
        Assert.Equal(new Rational(1, 4), notes[1].Duration);
        Assert.Equal(new Rational(1, 4), notes[2].Duration);
    }

    [Fact]
    public void Parse_Chord_Parentheses()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("(C4 E4 G4):q");

        Assert.Equal(3, notes.Length);
        // All at same time with quarter duration
        Assert.Equal(new Rational(1, 4), notes[0].Duration);
    }

    [Fact]
    public void Parse_ChordWithIndividualDurations()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("[C4/1 E4/2 G4/4]/4");

        Assert.Equal(3, notes.Length);
        // Individual durations override chord duration
        Assert.Equal(new Rational(1, 1), notes[0].Duration); // whole
        Assert.Equal(new Rational(1, 2), notes[1].Duration); // half
        Assert.Equal(new Rational(1, 4), notes[2].Duration); // quarter
    }

    [Fact]
    public void Parse_ChordWithOnlyIndividualDurations()
    {
        // No chord duration - each note has its own
        var notes = MusicNotationAntlrParser.ParseNotes("[C4/1 E4/2 G4/4]");

        Assert.Equal(3, notes.Length);
        Assert.Equal(new Rational(1, 1), notes[0].Duration); // whole
        Assert.Equal(new Rational(1, 2), notes[1].Duration); // half
        Assert.Equal(new Rational(1, 4), notes[2].Duration); // quarter

        // All start at same time
        Assert.Equal(Rational.Zero, notes[0].Offset);
        Assert.Equal(Rational.Zero, notes[1].Offset);
        Assert.Equal(Rational.Zero, notes[2].Offset);
    }

    [Fact]
    public void Parse_ChordAdvancesByMaxDuration()
    {
        // Chord with mixed durations, next note starts after longest
        var notes = MusicNotationAntlrParser.ParseNotes("[C4/1 E4/2 G4/4] D4/4");

        Assert.Equal(4, notes.Length);
        // D4 starts after 1 whole note (max duration in chord)
        Assert.Equal(new Rational(1, 1), notes[3].Offset);
    }

    [Fact]
    public void Parse_TimeSignature_Colon()
    {
        var result = MusicNotationAntlrParser.Parse("3/4: C4/4 E4/4 G4/4");

        Assert.Equal(3, result.Notes.Length);
        Assert.NotNull(result.TimeSignature);
        Assert.Equal(3, result.TimeSignature.Value.BeatsPerMeasure);
        Assert.Equal(4, result.TimeSignature.Value.BeatUnit);
    }

    [Fact]
    public void Parse_TimeSignature_Bar()
    {
        var result = MusicNotationAntlrParser.Parse("4/4| C4/4 E4/4 G4/4 C5/4");

        Assert.Equal(4, result.Notes.Length);
        Assert.NotNull(result.TimeSignature);
        Assert.Equal(4, result.TimeSignature.Value.BeatsPerMeasure);
    }

    [Fact]
    public void Parse_Measures()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("4/4: C4/4 E4/4 G4/4 C5/4 | D4/4 F4/4 A4/4 D5/4");

        Assert.Equal(8, notes.Length);
        // Second measure starts at offset 1 (whole note)
        Assert.Equal(new Rational(1, 1), notes[4].Offset);
    }

    [Fact]
    public void Parse_Tie()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("C4/4~ C4/4");

        // Tie merges two notes into one
        Assert.Single(notes);
        Assert.Equal(60, notes[0].Pitch);
        Assert.Equal(new Rational(1, 2), notes[0].Duration); // 1/4 + 1/4
    }

    [Fact]
    public void Parse_TieAcrossMeasures()
    {
        var notes = MusicNotationAntlrParser.ParseNotes("4/4: C4/2 E4/4 G4/4~ | G4/4 A4/4 B4/2");

        // G4 tied across bar
        var gNotes = notes.Where(n => n.Pitch == 67).ToArray();
        Assert.Single(gNotes);
        Assert.Equal(new Rational(1, 2), gNotes[0].Duration); // 1/4 + 1/4
    }

    [Fact]
    public void Parse_ValidateMeasures_Correct()
    {
        // Should not throw
        var notes = MusicNotationAntlrParser.ParseNotes("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4 A4/4", validateMeasures: true);

        Assert.Equal(6, notes.Length);
    }

    [Fact]
    public void Parse_ValidateMeasures_Incorrect()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MusicNotationAntlrParser.ParseNotes("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4", validateMeasures: true));

        Assert.Contains("Measure 2", ex.Message);
        Assert.Contains("mismatch", ex.Message);
    }

    [Fact]
    public void Parse_ComplexSequence()
    {
        var notes = MusicNotationAntlrParser.ParseNotes(
            "4/4: C4/4 [E4 G4]/4 R/4 C5/4 | D4/2. R/8 R/8 | R/4 [F4 A4 C5]/2.");

        // C4, E4, G4, Rest, C5 | D4, Rest, Rest | Rest, F4, A4, C5
        Assert.True(notes.Length >= 8);
    }

    [Fact]
    public void Parse_TwinkleTwinkle()
    {
        var notes = MusicNotationAntlrParser.ParseNotes(
            "4/4: C4/4 C4/4 G4/4 G4/4 | A4/4 A4/4 G4/2 | " +
            "F4/4 F4/4 E4/4 E4/4 | D4/4 D4/4 C4/2");

        Assert.Equal(14, notes.Length);
    }

    [Fact]
    public void RoundTrip_SimpleSequence()
    {
        var input = "C4/4 E4/4 G4/2";
        var notes = MusicNotationAntlrParser.ParseNotes(input);
        var output = MusicNotation.FormatNoteSequence(notes);
        var reparsed = MusicNotationAntlrParser.ParseNotes(output);

        Assert.Equal(notes.Length, reparsed.Length);
        for (int i = 0; i < notes.Length; i++)
        {
            Assert.Equal(notes[i].Pitch, reparsed[i].Pitch);
            Assert.Equal(notes[i].Duration, reparsed[i].Duration);
        }
    }

    [Fact]
    public void RoundTrip_WithRests()
    {
        var input = "C4/4 R/4 E4/2";
        var notes = MusicNotationAntlrParser.ParseNotes(input);
        var output = MusicNotation.FormatNoteSequence(notes);
        var reparsed = MusicNotationAntlrParser.ParseNotes(output);

        Assert.Equal(notes.Length, reparsed.Length);
        Assert.Equal(MusicNotation.REST_PITCH, reparsed[1].Pitch);
    }

    [Fact]
    public void RoundTrip_DottedNotes()
    {
        var input = "C4/4. E4/2.";
        var notes = MusicNotationAntlrParser.ParseNotes(input);
        var output = MusicNotation.FormatNoteSequence(notes, useDot: true);
        var reparsed = MusicNotationAntlrParser.ParseNotes(output);

        Assert.Equal(notes.Length, reparsed.Length);
        Assert.Equal(new Rational(3, 8), reparsed[0].Duration);
        Assert.Equal(new Rational(3, 4), reparsed[1].Duration);
    }

    [Fact]
    public void Parse_TimeSignatureChange()
    {
        // Change from 4/4 to 3/4 mid-sequence
        var notes = MusicNotationAntlrParser.ParseNotes(
            "4/4: C4/4 E4/4 G4/4 C5/4 | 3/4: D4/4 F4/4 A4/4");

        Assert.Equal(7, notes.Length);
        // First measure: 4 quarter notes
        Assert.Equal(new Rational(1, 1), notes[3].Offset + notes[3].Duration);
        // Second measure starts at beat 1
        Assert.Equal(new Rational(1, 1), notes[4].Offset);
    }

    [Fact]
    public void Parse_TimeSignatureChange_WithValidation()
    {
        // 4/4 measure then 3/4 measure - both valid
        var notes = MusicNotationAntlrParser.ParseNotes(
            "4/4: C4/4 E4/4 G4/4 C5/4 | 3/4: D4/4 F4/4 A4/4",
            validateMeasures: true);

        Assert.Equal(7, notes.Length);
    }

    [Fact]
    public void Parse_MultipleTimeSignatureChanges()
    {
        // 4/4 -> 3/4 -> 2/4
        var notes = MusicNotationAntlrParser.ParseNotes(
            "4/4: C4/1 | 3/4: D4/2. | 2/4: E4/2");

        Assert.Equal(3, notes.Length);
        Assert.Equal(new Rational(0, 1), notes[0].Offset);
        Assert.Equal(new Rational(1, 1), notes[1].Offset);
        Assert.Equal(new Rational(7, 4), notes[2].Offset); // 1 + 3/4
    }
}
