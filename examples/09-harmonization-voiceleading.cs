// Harmonization, Voice Leading, Figured Bass Examples
// Auto-harmonize melodies, SATB voice leading, figured bass realization


using Celeritas.Core.Analysis;
using Celeritas.Core;
using System.Collections.Generic;
using System.Linq;
using Celeritas.Core.Harmonization;
using Celeritas.Core.VoiceLeading;
using Celeritas.Core.FiguredBass;

namespace CeleritasExamples;

class HarmonizationAndVoiceLeading
{
    static void Main()
    {
        // ===== Basic Melody Harmonization =====

        var melody = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/2");
        var key = new KeySignature("C", true);

        var harmonizer = new MelodyHarmonizer();
        var result = harmonizer.Harmonize(melody, key);

        Console.WriteLine($"=== Harmonization Result ===");
        Console.WriteLine($"Melody notes: {melody.Length}");
        Console.WriteLine($"Chords generated: {result.Chords.Count}");

        Console.WriteLine($"\nChord progression:");
        foreach (var chord in result.Chords)
        {
            Console.WriteLine($"  {chord.Start}: {chord.Chord}");
        }

        Console.WriteLine($"Cost (lower is better): {result.TotalCost:F2}");

        // ===== SATB Voice Leading =====

        var chordSymbols = new[] { "C", "F", "G", "C" };
        var solver = new VoiceLeadingSolver();
        var voicingSolution = solver.SolveFromSymbols(chordSymbols);

        Console.WriteLine("\n=== SATB Voice Leading ===");
        Console.WriteLine($"Chords: {string.Join(" - ", chordSymbols)}");
        Console.WriteLine($"Total cost: {voicingSolution.TotalCost:F2}");
        Console.WriteLine($"Valid solution: {voicingSolution.IsValid}");

        if (voicingSolution.IsValid)
        {
            Console.WriteLine("\nVoicings:");
            Console.WriteLine($"{"#",-3} {"Bass",-6} {"Tenor",-6} {"Alto",-6} {"Soprano",-6}");
            Console.WriteLine(new string('-', 30));

            for (int i = 0; i < voicingSolution.Voicings.Count; i++)
            {
                var v = voicingSolution.Voicings[i];
                Console.WriteLine($"{i + 1,-3} " +
                    $"{MusicMath.MidiToNoteName(v.Bass),-6} " +
                    $"{MusicMath.MidiToNoteName(v.Tenor),-6} " +
                    $"{MusicMath.MidiToNoteName(v.Alto),-6} " +
                    $"{MusicMath.MidiToNoteName(v.Soprano),-6}");
            }

            // Export to score format
            Console.WriteLine("\n" + voicingSolution.ToScore());
        }

        // ===== Pluggable Strategy Pattern =====

        // Use default implementations with custom strategy
        var customProvider = new DefaultChordCandidateProvider();
        var customScorer = new DefaultTransitionScorer();
        var customRhythm = new DefaultHarmonicRhythmStrategy();

        var customHarmonizer = new MelodyHarmonizer(
            candidateProvider: customProvider,
            transitionScorer: customScorer,
            fitScorer: customScorer,  // DefaultTransitionScorer implements both interfaces
            rhythmStrategy: customRhythm
        );

        var customMelody = MusicNotation.Parse("C4/4 E4/4 G4/4 B4/4 D5/2");
        var customResult = customHarmonizer.Harmonize(customMelody, key);

        Console.WriteLine("\n=== Custom Strategy Harmonization ===");
        foreach (var chord in customResult.Chords)
        {
            Console.WriteLine($"  {chord.Start}: {chord.Chord}");
        }

        // ===== Custom Voice Leading Options =====

        var strictSolver = new VoiceLeadingSolver(VoiceLeadingSolverOptions.Strict);
        var strictSolution = strictSolver.SolveFromSymbols(new[] { "Am", "Dm", "G7", "C" });

        Console.WriteLine("\n=== Strict Voice Leading ===");
        Console.WriteLine($"Total cost: {strictSolution.TotalCost:F2}");
        if (strictSolution.IsValid)
        {
            foreach (var v in strictSolution.Voicings)
            {
                Console.WriteLine($"  {v}");
            }
        }
        
        if (strictSolution.Warnings.Count > 0)
        {
            Console.WriteLine("Warnings:");
            foreach (var w in strictSolution.Warnings)
                Console.WriteLine($"  - {w}");
        }

        // ===== Figured Bass Realization =====

        var realizer = new FiguredBassRealizer();

        // Root position (no figures = 5/3)
        var rootPosition = new FiguredBassSymbol
        {
            BassPitch = 60,  // C
            Figures = Array.Empty<int>(),
            Time = Rational.Zero,
            Duration = new Rational(1, 1)
        };

        var rootVoicing = realizer.RealizeSymbol(rootPosition);
        Console.WriteLine($"\n=== Figured Bass Realization ===");
        Console.WriteLine($"C (root position):");
        Console.WriteLine($"  {string.Join(", ", rootVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // First inversion (6)
        var firstInv = new FiguredBassSymbol
        {
            BassPitch = 64,  // E
            Figures = new[] { 6 },
            Time = new Rational(1, 1),
            Duration = new Rational(1, 1)
        };

        var firstInvVoicing = realizer.RealizeSymbol(firstInv);
        Console.WriteLine($"\nC/E (6):");
        Console.WriteLine($"  {string.Join(", ", firstInvVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // Second inversion (6/4)
        var secondInv = new FiguredBassSymbol
        {
            BassPitch = 67,  // G
            Figures = new[] { 6, 4 },
            Time = new Rational(2, 1),
            Duration = new Rational(1, 1)
        };

        var secondInvVoicing = realizer.RealizeSymbol(secondInv);
        Console.WriteLine($"\nC/G (6/4):");
        Console.WriteLine($"  {string.Join(", ", secondInvVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // Seventh chord (7)
        var seventh = new FiguredBassSymbol
        {
            BassPitch = 67,  // G
            Figures = new[] { 7 },
            Time = new Rational(3, 1),
            Duration = new Rational(1, 1)
        };

        var seventhVoicing = realizer.RealizeSymbol(seventh);
        Console.WriteLine($"\nG7 (7):");
        Console.WriteLine($"  {string.Join(", ", seventhVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // ===== Figured Bass with Accidentals =====
        var withAccidentals = new FiguredBassSymbol
        {
            BassPitch = 62,  // D
            Figures = new[] { 6 },
            Accidentals = new Dictionary<int, char> { { 6, '#' } }, // Raised sixth
            Time = new Rational(4, 1),
            Duration = new Rational(1, 1)
        };

        var accidentalVoicing = realizer.RealizeSymbol(withAccidentals);
        Console.WriteLine($"\nD (6#):");
        Console.WriteLine($"  {string.Join(", ", accidentalVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // ===== Figured Bass Sequence =====

        var bassLine = new[]
        {
            (60, new int[] { }),        // C
            (64, new int[] { 6 }),      // E (6)
            (67, new int[] { 6, 4 }),   // G (6/4)
            (67, new int[] { 7 }),      // G (7)
            (60, new int[] { })         // C
        };

        Console.WriteLine($"\n=== Figured Bass Sequence ===");
        for (int i = 0; i < bassLine.Length; i++)
        {
            var (pitch, figures) = bassLine[i];
            var symbol = new FiguredBassSymbol
            {
                BassPitch = pitch,
                Figures = figures,
                Time = new Rational(i, 1),
                Duration = new Rational(1, 1)
            };

            var voicing = realizer.RealizeSymbol(symbol);
            var figuresStr = figures.Length > 0
                ? $"({string.Join("/", figures)})"
                : "(root)";

            Console.WriteLine($"{MusicMath.MidiToNoteName(pitch)} {figuresStr}: " +
                $"{string.Join(", ", voicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        }

        // ===== Voice Leading Styles =====
        // FiguredBassRealizerOptions is available (see README.md for overview)
    }
}

/* Expected Output:

=== Harmonization Result ===
Melody notes: 5
Chords generated: 6

Chord progression:
  0: C Major
  1/4: G Major
  1/2: C Major
  3/4: F Major
  1: G Major
  5/4: C Major
Cost (lower is better): -0.20

=== SATB Voice Leading ===
Chords: C - F - G - C
Total cost: 39.00
Valid solution: True

Voicings:
#   Bass   Tenor  Alto   Soprano
------------------------------
1   G2     C3     C4     E4    
2   A2     C3     C4     F4    
3   G2     D3     B3     G4    
4   G2     E3     C4     G4    

SATB Voice Leading:
═══════════════════════════════════════

     Bass      Tenor     Alto      Soprano
     ────      ─────     ────      ───────
 1.  G2        C3        C4        E4       
 2.  A2        C3        C4        F4       
 3.  G2        D3        B3        G4       
 4.  G2        E3        C4        G4       

Total voice leading cost: 39.0


=== Custom Strategy Harmonization ===
  0: C Major
  1/4: C Major
  1/2: C Major
  3/4: G Major
  1: G Major
  5/4: G Major

=== Strict Voice Leading ===
Total cost: 55.00
  [E2, C3, A3, A4]
  [F2, D3, A3, A4]
  [F2, D3, B3, G4]
  [E2, C3, C4, G4]

=== Figured Bass Realization ===
C (root position):
  C4, E4, G4

C/E (6):
  E4, G4, C5

C/G (6/4):
  G4, C5, E5

G7 (7):
  G4, B4, D5, F5

D (6#):
  D4, F4, C5

=== Figured Bass Sequence ===
C4 (root): C4, E4, G4
E4 (6): E4, G4, C5
G4 (6/4): G4, C5, E5
G4 (7): G4, B4, D5, F5
C4 (root): C4, E4, G4

*/
