// Chord Analysis Examples
// Identify chords from pitch sets: triads, sevenths, sus/quartal, add chords


using Celeritas.Core;

namespace CeleritasExamples;

class ChordAnalysis
{
    static void Main()
    {
        // ===== What Identify Returns =====

        // Identify returns a ChordInfo value - a root pitch class plus one member of
        // the ChordQuality enum. There is no chord-symbol string API: ChordInfo.ToString()
        // prints "<Root> <Quality>" ("C Major"), not a lead-sheet symbol ("C").

        var info = ChordAnalyzer.Identify("C4 E4 G4");
        Console.WriteLine($"ToString():     {info}");
        Console.WriteLine($"Root:           {info.Root}");
        Console.WriteLine($"RootPitchClass: {info.RootPitchClass}");
        Console.WriteLine($"Quality:        {info.Quality}");

        // ChordInfo is a record struct, so it deconstructs
        var (rootPc, quality) = info;
        Console.WriteLine($"Deconstructed:  {rootPc} / {quality}");

        // ===== Basic Chord Identification =====

        Console.WriteLine();

        // Major triads
        var cMajor = ChordAnalyzer.Identify("C4 E4 G4");
        Console.WriteLine($"C E G = {cMajor}");  // Output: C Major

        var gMajor = ChordAnalyzer.Identify("G3 B3 D4");
        Console.WriteLine($"G B D = {gMajor}");  // Output: G Major

        // Minor triads
        var aMinor = ChordAnalyzer.Identify("A3 C4 E4");
        Console.WriteLine($"A C E = {aMinor}");  // Output: A Minor

        var dMinor = ChordAnalyzer.Identify("D4 F4 A4");
        Console.WriteLine($"D F A = {dMinor}");  // Output: D Minor

        // Diminished
        var bDim = ChordAnalyzer.Identify("B3 D4 F4");
        Console.WriteLine($"B D F = {bDim}");  // Output: B Diminished

        // Augmented
        var cAug = ChordAnalyzer.Identify("C4 E4 G#4");
        Console.WriteLine($"C E G# = {cAug}");  // Output: C Augmented

        // Power chord (root + fifth, no third)
        var c5 = ChordAnalyzer.Identify("C4 G4");
        Console.WriteLine($"C G = {c5}");  // Output: C Power

        // ===== Seventh Chords =====

        Console.WriteLine();

        // Dominant seventh
        var g7 = ChordAnalyzer.Identify("G3 B3 D4 F4");
        Console.WriteLine($"G B D F = {g7}");  // Output: G Dominant7

        // Major seventh
        var cmaj7 = ChordAnalyzer.Identify("C4 E4 G4 B4");
        Console.WriteLine($"C E G B = {cmaj7}");  // Output: C Major7

        // Minor seventh
        var dm7 = ChordAnalyzer.Identify("D4 F4 A4 C5");
        Console.WriteLine($"D F A C = {dm7}");  // Output: D Minor7

        // Half-diminished
        var bm7b5 = ChordAnalyzer.Identify("B3 D4 F4 A4");
        Console.WriteLine($"B D F A = {bm7b5}");  // Output: B HalfDim7

        // Fully diminished
        var bdim7 = ChordAnalyzer.Identify("B3 D4 F4 Ab4");
        Console.WriteLine($"B D F Ab = {bdim7}");  // Output: B Diminished7

        // Minor-major seventh
        var cmM7 = ChordAnalyzer.Identify("C4 Eb4 G4 B4");
        Console.WriteLine($"C Eb G B = {cmM7}");  // Output: C MinorMajor7

        // Augmented seventh
        var caug7 = ChordAnalyzer.Identify("C4 E4 G#4 Bb4");
        Console.WriteLine($"C E G# Bb = {caug7}");  // Output: C Augmented7

        // Dominant seventh with flat five
        var c7b5 = ChordAnalyzer.Identify("C4 E4 Gb4 Bb4");
        Console.WriteLine($"C E Gb Bb = {c7b5}");  // Output: C Dominant7Flat5

        // ===== Add Chords =====

        Console.WriteLine();

        // Major triad plus the ninth (the second)
        var cadd9 = ChordAnalyzer.Identify("C4 D4 E4 G4");
        Console.WriteLine($"C D E G = {cadd9}");  // Output: C Add9

        // Major triad plus the eleventh (the fourth)
        var cadd11 = ChordAnalyzer.Identify("C4 E4 F4 G4");
        Console.WriteLine($"C E F G = {cadd11}");  // Output: C Add11

        // ===== Suspended and Quartal =====

        Console.WriteLine();

        // Sus2, sus4 and quartal are rotations of ONE pitch-class set, so the mask alone
        // cannot tell them apart. Identify uses the lowest sounding note to pick the
        // rotation - the same three pitch classes answer differently per bass note.

        var csus2 = ChordAnalyzer.Identify("C4 D4 G4");
        Console.WriteLine($"C D G = {csus2}");  // Output: C Sus2

        var gsus4 = ChordAnalyzer.Identify("G3 C4 D4");
        Console.WriteLine($"G C D = {gsus4}");  // Output: G Sus4

        var dQuartal = ChordAnalyzer.Identify("D4 G4 C5");
        Console.WriteLine($"D G C = {dQuartal}");  // Output: D Quartal

        // ===== Inversions Are Not Reported =====

        Console.WriteLine();

        // Identify works from a 12-bit pitch-class mask, so every inversion of a chord
        // yields the same ChordInfo. There are no slash chords: the bass note is used
        // only to disambiguate the symmetric sets shown below.

        var root = ChordAnalyzer.Identify("C4 E4 G4");
        Console.WriteLine($"C E G (root)    = {root}");  // Output: C Major

        var first = ChordAnalyzer.Identify("E3 G3 C4");
        Console.WriteLine($"E G C (1st inv) = {first}");  // Output: C Major (not C/E)

        var second = ChordAnalyzer.Identify("G3 C4 E4");
        Console.WriteLine($"G C E (2nd inv) = {second}");  // Output: C Major (not C/G)

        var g7First = ChordAnalyzer.Identify("B3 D4 F4 G4");
        Console.WriteLine($"B D F G (G7/B)  = {g7First}");  // Output: G Dominant7

        // Symmetric sets are the exception: augmented triads and diminished sevenths
        // have one mask per four (resp. three) roots, so Identify re-roots them on
        // the actual bass note.

        var augC = ChordAnalyzer.Identify("C4 E4 G#4");
        var augE = ChordAnalyzer.Identify("E4 G#4 C5");
        Console.WriteLine($"C E G# / E G# C = {augC} / {augE}");

        var dim7B = ChordAnalyzer.Identify("B3 D4 F4 Ab4");
        var dim7D = ChordAnalyzer.Identify("D4 F4 Ab4 B4");
        Console.WriteLine($"B D F Ab / D F Ab B = {dim7B} / {dim7D}");

        // ===== Outside the Template Set =====

        Console.WriteLine();

        // ChordLibrary registers 19 qualities (see the ChordQuality enum). Ninth,
        // eleventh and thirteenth chords and altered dominants have no template, so
        // they come back as ChordQuality.Unknown with root C (pitch class 0).

        var cmaj9 = ChordAnalyzer.Identify("C4 E4 G4 B4 D5");
        Console.WriteLine($"C E G B D = {cmaj9}");  // Output: C Unknown

        var g9 = ChordAnalyzer.Identify("G3 B3 D4 F4 A4");
        Console.WriteLine($"G B D F A = {g9}");  // Output: C Unknown

        var c13 = ChordAnalyzer.Identify("C4 E4 G4 Bb4 D5 F5 A5");
        Console.WriteLine($"C E G Bb D F A = {c13}");  // Output: C Unknown

        var c7b9 = ChordAnalyzer.Identify("C4 E4 G4 Bb4 Db5");
        Console.WriteLine($"C E G Bb Db = {c7b9}");  // Output: C Unknown

        // Test the quality explicitly rather than trusting the root
        Console.WriteLine($"Cmaj9 recognized: {cmaj9.Quality != ChordQuality.Unknown}");

        // ===== Pitch-Class Masks =====

        Console.WriteLine();

        // Identify is a lookup over a 12-bit mask (bit n set = pitch class n present).
        // That layer is public, so you can query it directly.

        var mask = ChordAnalyzer.GetMask([60, 64, 67]);  // C4 E4 G4
        Console.WriteLine($"Mask of C E G: {mask} (0b{Convert.ToString(mask, 2).PadLeft(12, '0')})");

        // TryGetChord reports the miss instead of answering with an Unknown chord
        Console.WriteLine($"TryGetChord(C E G): {ChordLibrary.TryGetChord(mask, out var hit)} -> {hit}");

        var ninthMask = ChordAnalyzer.GetMask([60, 64, 67, 71, 74]);  // Cmaj9
        Console.WriteLine($"TryGetChord(Cmaj9): {ChordLibrary.TryGetChord(ninthMask, out _)}");

        // Note names and pitch classes. GetPitchClass accepts either spelling;
        // NoteNames always answers with the sharp one, so Eb comes back as D#.
        var ebPc = ChordLibrary.GetPitchClass("Eb");
        Console.WriteLine($"Pitch class of Eb: {ebPc} = {ChordLibrary.NoteNames[ebPc]}");

        // ===== From NoteEvent Arrays =====

        Console.WriteLine();

        var notes = MusicNotation.Parse("[C4 E4 G4 Bb4]/1");
        var chordFromNotes = ChordAnalyzer.Identify(notes);
        Console.WriteLine($"From parsed notes: {chordFromNotes}");  // Output: C Dominant7

        // ===== Analyze Chord Progression =====

        var progression = new[] { "C4 E4 G4", "F4 A4 C5", "G3 B3 D4", "C4 E4 G4" };
        Console.WriteLine("\nChord progression:");
        foreach (var chordNotes in progression)
        {
            var chord = ChordAnalyzer.Identify(chordNotes);
            Console.WriteLine($"  {chordNotes} -> {chord.Root} {chord.Quality}");
        }
    }
}

/* Expected Output:

ToString():     C Major
Root:           C
RootPitchClass: 0
Quality:        Major
Deconstructed:  0 / Major

C E G = C Major
G B D = G Major
A C E = A Minor
D F A = D Minor
B D F = B Diminished
C E G# = C Augmented
C G = C Power

G B D F = G Dominant7
C E G B = C Major7
D F A C = D Minor7
B D F A = B HalfDim7
B D F Ab = B Diminished7
C Eb G B = C MinorMajor7
C E G# Bb = C Augmented7
C E Gb Bb = C Dominant7Flat5

C D E G = C Add9
C E F G = C Add11

C D G = C Sus2
G C D = G Sus4
D G C = D Quartal

C E G (root)    = C Major
E G C (1st inv) = C Major
G C E (2nd inv) = C Major
B D F G (G7/B)  = G Dominant7
C E G# / E G# C = C Augmented / E Augmented
B D F Ab / D F Ab B = B Diminished7 / D Diminished7

C E G B D = C Unknown
G B D F A = C Unknown
C E G Bb D F A = C Unknown
C E G Bb Db = C Unknown
Cmaj9 recognized: False

Mask of C E G: 145 (0b000010010001)
TryGetChord(C E G): True -> C Major
TryGetChord(Cmaj9): False
Pitch class of Eb: 3 = D#

From parsed notes: C Dominant7

Chord progression:
  C4 E4 G4 -> C Major
  F4 A4 C5 -> F Major
  G3 B3 D4 -> G Major
  C4 E4 G4 -> C Major

*/
