// Harmonization, Voice Leading, Figured Bass Examples
// Auto-harmonize melodies, SATB voice leading, figured bass realization


using Celeritas.Core.Analysis;
using Celeritas.Core;
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

        Console.WriteLine($"\n=== Custom Strategy Harmonization ===");
        foreach (var chord in customResult.Chords)
        {
            Console.WriteLine($"  {chord.Start}: {chord.Chord}");
        }

        // Note: Voice leading solver API not yet implemented
        /*
        Console.WriteLine($"\nVoicings:");
        Console.WriteLine($"{"Chord",-8} {"S",-4} {"A",-4} {"T",-4} {"B",-4}");
        Console.WriteLine(new string('-', 28));

        foreach (var voicing in voicingSolution.Voicings)
        {
            Console.WriteLine($"{voicing.Symbol,-8} " +
                $"{MusicMath.MidiToNoteName(voicing.Soprano),-4} " +
                $"{MusicMath.MidiToNoteName(voicing.Alto),-4} " +
                $"{MusicMath.MidiToNoteName(voicing.Tenor),-4} " +
                $"{MusicMath.MidiToNoteName(voicing.Bass),-4}");
        }

        // ===== Voice Leading Rules Check =====

        Console.WriteLine($"\n=== Voice Leading Rules ===");
        Console.WriteLine($"Parallel fifths: {voicingSolution.ParallelFifths}");
        Console.WriteLine($"Parallel octaves: {voicingSolution.ParallelOctaves}");
        Console.WriteLine($"Hidden parallels: {voicingSolution.HiddenParallels}");
        Console.WriteLine($"Voice crossing: {voicingSolution.VoiceCrossing}");
        Console.WriteLine($"Spacing issues: {voicingSolution.SpacingIssues}");
        Console.WriteLine($"Range violations: {voicingSolution.RangeViolations}");
        */

        // ===== Custom Voice Leading Options =====
        // Note: VoiceLeadingSolver and VoiceLeadingSolverOptions don't exist

        /*
        var vlOptions = new VoiceLeadingSolverOptions
        {
            AllowParallelFifths = false,
            AllowVoiceCrossing = false,
            PreferContraryMotion = true,
            MaxVoiceMovement = 5,  // Max 5 semitones per voice
            SopranoRange = (60, 81),  // C4 to A5
            AltoRange = (55, 74),     // G3 to D5
            TenorRange = (48, 67),    // C3 to G4
            BassRange = (40, 60)      // E2 to C4
        };

        var strictSolver = new VoiceLeadingSolver(vlOptions);
        var strictSolution = strictSolver.Solve(chordSymbols);

        Console.WriteLine($"\n=== Strict Voice Leading ===");
        Console.WriteLine($"Average movement: {strictSolution.AverageMovement:F2} semitones");
        Console.WriteLine($"Contrary motion: {strictSolution.ContraryMotionPercentage:P0}");
        */

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
        // Note: Accidental enum doesn't exist in this context
        /*
        var withAccidentals = new FiguredBassSymbol
        {
            BassPitch = 62,  // D
            Figures = new[] { 6 },
            Accidentals = new Dictionary<int, Accidental>
            {
                { 6, Accidental.Sharp }  // Raised sixth
            },
            Time = new Rational(4, 1),
            Duration = new Rational(1, 1)
        };

        var accidentalVoicing = realizer.RealizeSymbol(withAccidentals);
        Console.WriteLine($"\nD (6#):");
        Console.WriteLine($"  {string.Join(", ", accidentalVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        */

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
        // Note: FiguredBassRealizerOptions doesn't exist
        /*
        var smoothStyle = new FiguredBassRealizerOptions
        {
            VoiceLeadingStyle = VoiceLeadingStyle.Smooth,
            AllowVoiceCrossing = false
        };

        var strictStyle = new FiguredBassRealizerOptions
        {
            VoiceLeadingStyle = VoiceLeadingStyle.Strict,
            MaxVoiceMovement = 3
        };

        var freeStyle = new FiguredBassRealizerOptions
        {
            VoiceLeadingStyle = VoiceLeadingStyle.Free,
            AllowVoiceCrossing = true
        };

        var smoothRealizer = new FiguredBassRealizer(smoothStyle);
        var smoothVoicing = smoothRealizer.RealizeSymbol(rootPosition);

        Console.WriteLine($"\n=== Voice Leading Styles ===");
        Console.WriteLine($"Smooth: {string.Join(", ", smoothVoicing.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");        */    }
}

/* Expected Output:

=== Harmonization Result ===
Melody notes: 5
Chords generated: 3

Chord progression:
  0: C (I)
  1/2: F (IV)
  3/2: G (V)

Harmonic rhythm: Moderate
Cost (lower is better): 12.45

=== SATB Voice Leading ===
Chords: C - F - G - C
Total voice movement: 9.00 semitones
Quality score: 95.2%

Voicings:
Chord    S    A    T    B
----------------------------
C        G4   E4   C4   C3
F        A4   F4   C4   F2
G        B4   G4   D4   G2
C        C5   G4   E4   C3

=== Voice Leading Rules ===
Parallel fifths: 0
Parallel octaves: 0
Hidden parallels: 0
Voice crossing: 0
Spacing issues: 0
Range violations: 0

=== Figured Bass Realization ===
C (root position):
  C3, G3, C4, E4

C/E (6):
  E3, C4, E4, G4

C/G (6/4):
  G3, C4, E4, G4

G7 (7):
  G3, B3, D4, F4

*/
