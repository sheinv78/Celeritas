// Basic Notation Parsing Examples
// Simple note parsing, durations, rests, ties, chords

using Celeritas.Core;

namespace CeleritasExamples;

class NotationBasics
{
    static void Main()
    {
        // ===== Simple Notes =====

        // Parse notes with octave numbers
        var notes = MusicNotation.Parse("C4 E4 G4 B4");
        Console.WriteLine($"Parsed {notes.Length} notes");

        // ===== Durations =====

        // Quarter notes (1/4)
        var quarters = MusicNotation.Parse("C4/4 E4/4 G4/4 B4/4");

        // Mixed durations
        var mixed = MusicNotation.Parse("C4/4 E4/8 G4/2 B4/1");

        // Dotted notes (dot adds half the duration)
        var dotted = MusicNotation.Parse("C4/4. E4/8 G4/2.");
        // C4 = 3/8 (1/4 + 1/8), G4 = 3/4 (1/2 + 1/4)

        // Alternative letter syntax (q=quarter, h=half, e=eighth, w=whole)
        var letters = MusicNotation.Parse("C4:q E4:e G4:h C5:w");

        // Dotted with letters
        var dottedLetters = MusicNotation.Parse("C4:q. E4:e G4:h.");

        // ===== Rests =====

        // R for rest with duration
        var withRests = MusicNotation.Parse("C4/4 R/4 E4/4 R/2 G4/2");

        // Rests with letter notation
        var restLetters = MusicNotation.Parse("C4:q R:q E4:e R:h");

        // ===== Ties =====

        // Tie consecutive notes (same pitch)
        var tied = MusicNotation.Parse("C4/4~ C4/4");
        // Creates single note with Duration = 1/2

        // Multiple ties
        var longTie = MusicNotation.Parse("C4/4~ C4/4~ C4/4~ C4/4");
        // Single note, Duration = 1/1 (whole note)

        // ===== Chords (Simultaneous Notes) =====

        // Brackets for chords
        var chord = MusicNotation.Parse("[C4 E4 G4]/4");
        Console.WriteLine($"C major chord has {chord.Length} notes");

        // Parentheses also work
        var parenChord = MusicNotation.Parse("(C4 E4 G4):q");

        // Chord progression
        var progression = MusicNotation.Parse("[C4 E4 G4]/4 [D4 F4 A4]/4 [E4 G4 B4]/2");

        // ===== Polyphonic Chords =====
        // Each note can have different duration

        var polyphonic = MusicNotation.Parse("[C4/1 E4/2 G4/4]");
        // C4 = whole, E4 = half, G4 = quarter
        // All start together, next element starts after C4 (longest)

        var mixedDurations = MusicNotation.Parse("[C4 E4/2 G4]/4");
        // C4 = 1/4 (default), E4 = 1/2 (override), G4 = 1/4 (default)

        // ===== Time Signatures =====

        // Inline time signature
        var waltz = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4");

        // Common time
        var common = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4");

        // Measure bars
        var measures = MusicNotation.Parse("3/4: C4/4 E4/4 G4/4 | D4/4 F4/4 A4/4");

        // Time signature changes
        var meterChange = MusicNotation.Parse(
            "4/4: C4/4 E4/4 G4/4 C5/4 | 3/4: D4/4 F4/4 A4/4 | 2/4: E4/2");

        // Validate measures
        var validated = MusicNotation.Parse(
            "4/4: C4/4 E4/4 G4/2 | D4/2 R/4 A4/4",
            validateMeasures: true);
        // Throws if measure durations don't match time signature!

        // ===== Complete Musical Phrase =====

        var phrase = MusicNotation.Parse(
            @"4/4:
            C4/4 E4/4 G4/4 C5/4 |
            B4/2 G4/4 E4/4 |
            A4/4 F4/4 D4/4 B3/4 |
            C4/1");

        Console.WriteLine($"\nParsed complete phrase with {phrase.Length} notes");

        // ===== Inspect Note Properties =====

        foreach (var note in MusicNotation.Parse("C4/4 E4/2. G4/8"))
        {
            Console.WriteLine($"Pitch: {note.Pitch}, " +
                            $"Time: {note.Time}, " +
                            $"Duration: {note.Duration}, " +
                            $"MIDI: {note.Pitch}");
        }
    }
}

/* Expected Output:

Parsed 4 notes
C major chord has 3 notes

Parsed complete phrase with 16 notes

Pitch: 60, Time: 0, Duration: 1/4, MIDI: 60
Pitch: 64, Time: 1/4, Duration: 3/4, MIDI: 64
Pitch: 67, Time: 1, Duration: 1/8, MIDI: 67

*/
