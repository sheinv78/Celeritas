// Progression Analysis and Harmonic Color Examples
// Analyze chord progressions, cadences, harmonic color

using Celeritas.Core;
using Celeritas.Core.Analysis;
using System.Linq;

namespace CeleritasExamples;

class ProgressionAnalysis
{
    static void Main()
    {
        // ===== Basic Progression Analysis =====

        // Classic ii-V-I progression
        var jazzProgression = new[] { "Dm7", "G7", "Cmaj7" };
        var analysis1 = ProgressionAdvisor.Analyze(jazzProgression);

        Console.WriteLine($"Progression: {string.Join(" - ", jazzProgression)}");
        Console.WriteLine($"Key: {analysis1.Key}");
        Console.WriteLine($"Pattern: {analysis1.Pattern}");

        // ===== Roman Numeral Analysis =====

        var popProgression = new[] { "C", "G", "Am", "F" };
        var analysis2 = ProgressionAdvisor.Analyze(popProgression);

        Console.WriteLine($"\n{string.Join(" - ", popProgression)}:");
        Console.WriteLine($"Pattern: {analysis2.Pattern}");  // I - V - vi - IV

        // ===== Tension Curve =====

        var tensionProgression = new[] { "C", "Am", "F", "G", "C" };
        var analysis3 = ProgressionAdvisor.AnalyzeFromSymbols(tensionProgression);

        Console.WriteLine($"\nTension curve:");
        for (int i = 0; i < analysis3.TensionCurve.Length; i++)
        {
            var bar = new string('█', (int)(analysis3.TensionCurve[i] * 20));
            Console.WriteLine($"  {tensionProgression[i],6}: {bar} ({analysis3.TensionCurve[i]:P0})");
        }

        // ===== Cadence Detection =====

        // Authentic cadence (V - I)
        var authentic = new[] { "G7", "C" };
        var cadence1 = ProgressionAdvisor.DetectCadence(authentic);
        Console.WriteLine($"\n{string.Join(" - ", authentic)}: {cadence1}");  // Authentic

        // Plagal cadence (IV - I)
        var plagal = new[] { "F", "C" };
        var cadence2 = ProgressionAdvisor.DetectCadence(plagal);
        Console.WriteLine($"{string.Join(" - ", plagal)}: {cadence2}");  // Plagal (Amen)

        // Deceptive cadence (V - vi)
        var deceptive = new[] { "G7", "Am" };
        var cadence3 = ProgressionAdvisor.DetectCadence(deceptive);
        Console.WriteLine($"{string.Join(" - ", deceptive)}: {cadence3}");  // Deceptive

        // Half cadence (ends on V)
        var half = new[] { "C", "Dm", "G" };
        var cadence4 = ProgressionAdvisor.DetectCadence(half);
        Console.WriteLine($"{string.Join(" - ", half)}: {cadence4}");  // Half

        // ===== Chord Character Classification =====

        var chords = new[] { "C", "Cmaj7", "Cm", "Cdim", "C7", "Caug" };
        Console.WriteLine($"\nChord characters:");
        foreach (var chord in chords)
        {
            var character = ChordCharacterClassifier.Classify(chord);
            Console.WriteLine($"  {chord,6}: {character.Mood} ({character.Stability:P0}, {character.Brightness:P0})");
        }

        // ===== Progression Report =====

        var complexProgression = new[] { "Cmaj7", "Am7", "Dm7", "G7", "Em7", "A7", "Dm7", "G7", "Cmaj7" };
        var report = ProgressionReport.Generate(complexProgression);

        Console.WriteLine($"\n=== Progression Report ===");
        Console.WriteLine(report.Summary);
        Console.WriteLine($"\nKey: {report.Key}");
        Console.WriteLine($"Complexity: {report.Complexity}");
        Console.WriteLine($"Overall tension: {report.AverageTension:P1}");
        Console.WriteLine($"\nHighlights:");
        foreach (var highlight in report.Highlights)
        {
            Console.WriteLine($"  • {highlight}");
        }

        // ===== Chord Recommendations =====

        // Get suggestions for next chord
        var currentChords = new[] { "C", "Am", "F" };
        var suggestions = ProgressionAdvisor.SuggestNext(currentChords);

        Console.WriteLine($"\nAfter {string.Join(" - ", currentChords)}, try:");
        foreach (var suggestion in suggestions.Take(5))
        {
            Console.WriteLine($"  {suggestion.Chord,6} - {suggestion.Reason} (score: {suggestion.Score:F2})");
        }

        // ===== Modal Progressions =====
        // ModalProgressions.Analyze is available (see README.md for overview)

        // ===== Harmonic Color Analysis =====

        var melody = MusicNotation.Parse("C4/4 D4/4 E4/4 F4/4 G4/4 A4/4 B4/4 C5/2");
        var chordProgression = new[] {
            ("C", Rational.Zero),
            ("G", new Rational(1, 1)),
            ("Am", new Rational(2, 1)),
            ("F", new Rational(3, 1))
        };

        var key = new KeySignature("C", true);
        var colorAnalysis = HarmonicColorAnalyzer.Analyze(melody, chordProgression, key);

        Console.WriteLine($"\n=== Harmonic Color Analysis ===");

        Console.WriteLine($"Chromatic notes: {colorAnalysis.ChromaticNotes.Count}");
        foreach (var chromatic in colorAnalysis.ChromaticNotes)
        {
            Console.WriteLine($"  {MusicMath.MidiToNoteName(chromatic.Pitch)} at {chromatic.Offset}");
        }

        Console.WriteLine($"\nModal turns: {colorAnalysis.ModalTurns.Count}");
        foreach (var turn in colorAnalysis.ModalTurns)
        {
            Console.WriteLine($"  {turn.Mode} chords[{turn.StartChordIndex}..{turn.EndChordIndex}] (conf: {turn.Confidence:F2})");
        }

        var nonChordTones = colorAnalysis.MelodicHarmony
            .Where(e => !e.IsChordTone)
            .ToArray();

        Console.WriteLine($"\nNon-chord tones: {nonChordTones.Length}");
        foreach (var nct in nonChordTones)
        {
            Console.WriteLine($"  {nct.Type} - {MusicMath.MidiToNoteName(nct.Pitch)} at {nct.Offset}");
        }

        // Note: colorAnalysis properties not available
        // Console.WriteLine($"\nColor assessment: {colorAnalysis.ColorfulnessRating}/10");
        // Console.WriteLine($"Description: {colorAnalysis.Description}");

        // ===== Secondary Dominants =====

        var withSecondaryDom = new[] { "C", "A7", "Dm", "G7", "C" };
        var secDomAnalysis = ProgressionAdvisor.Analyze(withSecondaryDom);

        Console.WriteLine($"\n{string.Join(" - ", withSecondaryDom)}:");

        Console.WriteLine($"Secondary dominants: {secDomAnalysis.HasSecondaryDominants}");
        if (secDomAnalysis.SecondaryDominants.Count > 0)
        {
            foreach (var secDom in secDomAnalysis.SecondaryDominants)
            {
                Console.WriteLine($"  {secDom.Chord} → {secDom.Target} (V/{secDom.TargetDegree})");
            }
        }

        // ===== Borrowed Chords =====
        var withBorrowed = new[] { "C", "Fm", "C", "G7", "C" };
        var borrowedAnalysis = ProgressionAdvisor.Analyze(withBorrowed);

        Console.WriteLine($"\n{string.Join(" - ", withBorrowed)}:");
        Console.WriteLine($"Borrowed chords: {borrowedAnalysis.HasBorrowedChords}");
        if (borrowedAnalysis.BorrowedChords.Count > 0)
        {
            foreach (var borrowed in borrowedAnalysis.BorrowedChords)
            {
                Console.WriteLine($"  {borrowed.Chord} from {borrowed.SourceKey}");
            }
        }

        // ===== Voice Leading Analysis =====
        var voiceLeadingProg = new[] { "C", "F", "G", "C" };
        var vlAnalysis = ProgressionAdvisor.Analyze(voiceLeadingProg);

        Console.WriteLine($"\n{string.Join(" - ", voiceLeadingProg)} voice leading:");
        Console.WriteLine($"  Smoothness: {vlAnalysis.Smoothness:P1}");
        Console.WriteLine($"  Average movement: {vlAnalysis.AverageMovement:F2} semitones");
        Console.WriteLine($"  Parallel fifths: {vlAnalysis.ParallelFifths}");
        Console.WriteLine($"  Parallel octaves: {vlAnalysis.ParallelOctaves}");
        Console.WriteLine($"  Quality: {vlAnalysis.QualityRating}");
    }
}

/* Expected Output:

Progression: Dm7 - G7 - Cmaj7
Key: C major
Summary: Classic ii-V-I in C major
Character: Smooth, jazzy resolution

C - G - Am - F:
Roman numerals: I - V - vi - IV
Functions: Tonic - Dominant - Tonic - Subdominant

Tension curve:
       C: (0%)
      Am: ██ (10%)
       F: ████ (20%)
       G: ████████████ (60%)
       C: (0%)

G7 - C: Authentic
F - C: Plagal (Amen)
G7 - Am: Deceptive

Chord characters:
      C: Bright (Stable, Major)
  Cmaj7: Dreamy (Stable, Major)
     Cm: Melancholic (Stable, Minor)
   Cdim: Tense (Unstable, Dark)
     C7: Bluesy (Tension, Mixed)
   Caug: Mysterious (Unstable, Bright)

*/

