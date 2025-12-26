// Chord Analysis Examples
// Identify chords, inversions, extended chords


using Celeritas.Core;
using System.Linq;
using Celeritas.Core.Analysis;

namespace CeleritasExamples;

class ChordAnalysis
{
    static void Main()
    {
        // ===== Basic Chord Identification =====

        // Major triads
        var cMajor = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G = {cMajor}");  // Output: C

        var gMajor = ChordAnalyzer.Identify(MusicNotation.Parse("G3 B3 D4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"G B D = {gMajor}");  // Output: G

        // Minor triads
        var aMinor = ChordAnalyzer.Identify(MusicNotation.Parse("A3 C4 E4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"A C E = {aMinor}");  // Output: Am

        var dMinor = ChordAnalyzer.Identify(MusicNotation.Parse("D4 F4 A4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"D F A = {dMinor}");  // Output: Dm

        // Diminished
        var bDim = ChordAnalyzer.Identify(MusicNotation.Parse("B3 D4 F4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"B D F = {bDim}");  // Output: Bdim or B°

        // Augmented
        var cAug = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G#4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G# = {cAug}");  // Output: Caug or C+

        // ===== Seventh Chords =====

        // Dominant seventh
        var g7 = ChordAnalyzer.Identify(MusicNotation.Parse("G3 B3 D4 F4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"G B D F = {g7}");  // Output: G7

        // Major seventh
        var cmaj7 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 B4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G B = {cmaj7}");  // Output: Cmaj7 or CΔ7

        // Minor seventh
        var dm7 = ChordAnalyzer.Identify(MusicNotation.Parse("D4 F4 A4 C5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"D F A C = {dm7}");  // Output: Dm7

        // Half-diminished
        var bm7b5 = ChordAnalyzer.Identify(MusicNotation.Parse("B3 D4 F4 A4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"B D F A = {bm7b5}");  // Output: Bm7b5 or Bø7

        // Fully diminished
        var bdim7 = ChordAnalyzer.Identify(MusicNotation.Parse("B3 D4 F4 Ab4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"B D F Ab = {bdim7}");  // Output: Bdim7 or B°7

        // ===== Extended Chords =====

        // Ninth chords
        var cmaj9 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 B4 D5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G B D = {cmaj9}");  // Output: Cmaj9

        var g9 = ChordAnalyzer.Identify(MusicNotation.Parse("G3 B3 D4 F4 A4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"G B D F A = {g9}");  // Output: G9

        // Eleventh chords
        var c11 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 Bb4 D5 F5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G Bb D F = {c11}");  // Output: C11

        // Thirteenth chords
        var c13 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 Bb4 D5 F5 A5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G Bb D F A = {c13}");  // Output: C13

        // ===== Inversions =====

        // Root position
        var root = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G (root) = {root}");  // Output: C

        // First inversion (bass = 3rd)
        var first = ChordAnalyzer.Identify(MusicNotation.Parse("E3 G3 C4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"E G C (1st inv) = {first}");  // Output: C/E

        // Second inversion (bass = 5th)
        var second = ChordAnalyzer.Identify(MusicNotation.Parse("G3 C4 E4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"G C E (2nd inv) = {second}");  // Output: C/G

        // Seventh chord inversions
        var g7First = ChordAnalyzer.Identify(MusicNotation.Parse("B3 D4 F4 G4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"B D F G (G7 1st) = {g7First}");  // Output: G7/B

        var g7Second = ChordAnalyzer.Identify(MusicNotation.Parse("D3 F3 G3 B3").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"D F G B (G7 2nd) = {g7Second}");  // Output: G7/D

        var g7Third = ChordAnalyzer.Identify(MusicNotation.Parse("F3 G3 B3 D4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"F G B D (G7 3rd) = {g7Third}");  // Output: G7/F

        // ===== From NoteEvent Arrays =====

        var notes = MusicNotation.Parse("[C4 E4 G4 Bb4]/1");
        var chordFromNotes = ChordAnalyzer.Identify(notes.Select(n => n.Pitch).ToArray());
        Console.WriteLine($"\nFrom parsed notes: {chordFromNotes}");  // Output: C7

        // ===== Altered Chords =====

        // Flat five
        var c7b5 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 Gb4 Bb4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E Gb Bb = {c7b5}");  // Output: C7b5

        // Sharp five
        var caug7 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G#4 Bb4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G# Bb = {caug7}");  // Output: C7#5 or C7+

        // Flat nine
        var c7b9 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 Bb4 Db5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G Bb Db = {c7b9}");  // Output: C7b9

        // Sharp nine
        var c7s9 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 E4 G4 Bb4 D#5").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C E G Bb D# = {c7s9}");  // Output: C7#9

        // ===== Suspended Chords =====

        var csus2 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 D4 G4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C D G = {csus2}");  // Output: Csus2

        var csus4 = ChordAnalyzer.Identify(MusicNotation.Parse("C4 F4 G4").Select(n => n.Pitch).ToArray());
        Console.WriteLine($"C F G = {csus4}");  // Output: Csus4

        // ===== Slash Chords (Non-Chord Bass) =====

        var cOverE = ChordAnalyzer.Identify(MusicNotation.Parse("E3 C4 E4 G4").Select(n => n.Pitch).ToArray());  // E in bass, but with C chord
        Console.WriteLine($"E C E G = {cOverE}");  // May output: C/E or analyze as Am/E

        // ===== Analyze Chord Progression =====

        var progression = new[] { "C4 E4 G4", "F4 A4 C5", "G3 B3 D4", "C4 E4 G4" };
        Console.WriteLine("\nChord progression:");
        foreach (var chordNotes in progression)
        {
            var chordParsed = MusicNotation.Parse(chordNotes);
            var symbol = ChordAnalyzer.Identify(chordParsed.Select(n => n.Pitch).ToArray());
            Console.WriteLine($"  {chordNotes} → {symbol}");
        }
    }
}

/* Expected Output:

C E G = C
G B D = G
A C E = Am
D F A = Dm
B D F = Bdim
C E G# = Caug
G B D F = G7
C E G B = Cmaj7
D F A C = Dm7
B D F A = Bm7b5
B D F Ab = Bdim7
C E G B D = Cmaj9
G B D F A = G9
C E G Bb D F = C11
C E G Bb D F A = C13
C E G (root) = C
E G C (1st inv) = C/E
G C E (2nd inv) = C/G
B D F G (G7 1st) = G7/B
D F G B (G7 2nd) = G7/D
F G B D (G7 3rd) = G7/F

From parsed notes: C7
C E Gb Bb = C7b5
C E G# Bb = C7#5
C E G Bb Db = C7b9
C E G Bb D# = C7#9
C D G = Csus2
C F G = Csus4

Chord progression:
  C4 E4 G4 → C
  F4 A4 C5 → F
  G3 B3 D4 → G
  C4 E4 G4 → C

*/
