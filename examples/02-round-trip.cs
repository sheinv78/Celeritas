// Round-Trip Formatting Examples
// Parse notation → internal representation → format back to string

using Celeritas.Core;
using Celeritas.Grammar;

namespace CeleritasExamples;

class RoundTrip
{
    static void Main()
    {
        // ===== Basic Formatting =====

        // Parse and format back
        var notes = MusicNotation.Parse("C4/4 E4/8 G4/2.");
        var formatted = MusicNotation.FormatNoteSequence(notes);
        Console.WriteLine($"Formatted: {formatted}");
        // Output: "C4/4 E4/8 G4/2."

        // ===== Output Format Control =====

        // Numeric format (default)
        var numeric = MusicNotation.FormatNoteSequence(notes,
            useDot: true, useLetters: false);
        Console.WriteLine($"Numeric: {numeric}");
        // Output: "C4/4 E4/8 G4/2."

        // Letter format (q/h/e/w)
        var letters = MusicNotation.FormatNoteSequence(notes,
            useDot: true, useLetters: true);
        Console.WriteLine($"Letters: {letters}");
        // Output: "C4:q E4:e G4:h."

        // Without dots (expand dotted notes)
        var noDots = MusicNotation.FormatNoteSequence(notes,
            useDot: false, useLetters: false);
        Console.WriteLine($"No dots: {noDots}");
        // Output: "C4/4 E4/8 G4/4~ G4/4" (dotted half becomes tied notes)

        // ===== Chord Grouping =====

        var chordMusic = MusicNotation.Parse("[C4 E4 G4]/4 [D4 F4 A4]/4 B4/2");

        // Without groupChords (flat list)
        var flat = MusicNotation.FormatNoteSequence(chordMusic, groupChords: false);
        Console.WriteLine($"Flat: {flat}");
        // Output: "C4/4 E4/4 G4/4 D4/4 F4/4 A4/4 B4/2"

        // With groupChords (brackets restored)
        var grouped = MusicNotation.FormatNoteSequence(chordMusic, groupChords: true);
        Console.WriteLine($"Grouped: {grouped}");
        // Output: "[C4 E4 G4]/4 [D4 F4 A4]/4 B4/2"

        // ===== Directives =====

        // Parse with directives (use ANTLR parser)
        var result = MusicNotationAntlrParser.Parse(
            "@bpm 120 @dynamics mf C4/4 E4/4 G4/4");

        // Format back with directives
        var withDirectives = MusicNotation.FormatWithDirectives(
            result.Notes, result.Directives);
        Console.WriteLine($"With directives: {withDirectives}");
        // Output: "@bpm 120 @dynamics mf C4/4 E4/4 G4/4"

        // ===== BPM Ramps =====

        var rampResult = MusicNotationAntlrParser.Parse(
            "@bpm 120 -> 140 /2 C4/1 D4/1");
        var rampFormatted = MusicNotation.FormatWithDirectives(
            rampResult.Notes, rampResult.Directives);
        Console.WriteLine($"BPM ramp: {rampFormatted}");
        // Output: "@bpm 120 -> 140 /2 C4/1 D4/1"

        // ===== Dynamics Changes =====

        var dynamicsResult = MusicNotationAntlrParser.Parse(
            "@dynamics p @cresc to ff C4/2 @dim to pp D4/2");
        var dynamicsFormatted = MusicNotation.FormatWithDirectives(
            dynamicsResult.Notes, dynamicsResult.Directives);
        Console.WriteLine($"Dynamics: {dynamicsFormatted}");
        // Output: "@dynamics p @cresc to ff C4/2 @dim to pp D4/2"

        // ===== Complete Round-Trip Test =====

        var original = "@bpm 120 @section intro @dynamics mf [C4 E4 G4]/4 @cresc to ff [D4 F4]/4 C5/2";
        var parsed = MusicNotationAntlrParser.Parse(original);
        var exported = MusicNotation.FormatWithDirectives(
            parsed.Notes, parsed.Directives, groupChords: true);
        var reparsed = MusicNotationAntlrParser.Parse(exported);

        Console.WriteLine($"\nOriginal:  {original}");
        Console.WriteLine($"Exported:  {exported}");
        Console.WriteLine($"Match: {original == exported}");
        Console.WriteLine($"Notes match: {parsed.Notes.Length == reparsed.Notes.Length}");
        Console.WriteLine($"Directives match: {parsed.Directives.Length == reparsed.Directives.Length}");

        // ===== Quoted Strings in Directives =====

        var quotedResult = MusicNotationAntlrParser.Parse(
            "@section \"verse 1\" @tempo allegro C4/4");
        var quotedExported = MusicNotation.FormatWithDirectives(
            quotedResult.Notes, quotedResult.Directives);
        Console.WriteLine($"Quoted: {quotedExported}");
        // Output: @section "verse 1" @tempo allegro C4/4
        // (quotes preserved when needed)

        // ===== Complex Example =====

        var complex = MusicNotationAntlrParser.Parse(@"
            @bpm 120
            @section intro
            @dynamics mf
            4/4: [C4 E4 G4]/4 R/4 [D4 F4 A4]/2 |
            @cresc to ff
            [G4 B4 D5]/2 @dim to p [C4 E4 G4]/2
        ");

        var complexExported = MusicNotation.FormatWithDirectives(
            complex.Notes, complex.Directives,
            groupChords: true, useLetters: false);

        Console.WriteLine($"\nComplex export:\n{complexExported}");
    }
}

/* Expected Output:

Formatted: C4/4 E4/8 G4/2.
Numeric: C4/4 E4/8 G4/2.
Letters: C4:q E4:e G4:h.
No dots: C4/4 E4/8 G4/4~ G4/4
Flat: C4/4 E4/4 G4/4 D4/4 F4/4 A4/4 B4/2
Grouped: [C4 E4 G4]/4 [D4 F4 A4]/4 B4/2
With directives: @bpm 120 @dynamics mf C4/4 E4/4 G4/4
BPM ramp: @bpm 120 -> 140 /2 C4/1 D4/1
Dynamics: @dynamics p @cresc to ff C4/2 @dim to pp D4/2

Original:  @bpm 120 @section intro @dynamics mf [C4 E4 G4]/4 @cresc to ff [D4 F4]/4 C5/2
Exported:  @bpm 120 @section intro @dynamics mf [C4 E4 G4]/4 @cresc to ff [D4 F4]/4 C5/2
Match: True
Notes match: True
Directives match: True
Quoted: @section "verse 1" @tempo allegro C4/4

*/
