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
        // TensionCurve is nullable: a report whose chords all failed to parse has none.
        var tensionCurve = analysis3.TensionCurve ?? [];
        for (int i = 0; i < tensionCurve.Length; i++)
        {
            var bar = new string('█', (int)(tensionCurve[i] * 20));
            Console.WriteLine($"  {tensionProgression[i],6}: {bar} ({tensionCurve[i]:P0})");
        }

        // ===== Cadence Detection =====

        // Given no key, DetectCadence infers one from the chords it was handed - and a
        // two-chord fragment is thin evidence. "F - C" alone reads as F major, i.e. I - V,
        // which is a half cadence, not a plagal one. Pass the key to pin it down.
        var cadenceKey = new KeySignature("C", true);

        // Authentic cadence (V - I)
        var authentic = new[] { "G7", "C" };
        var cadence1 = ProgressionAdvisor.DetectCadence(authentic, cadenceKey);
        Console.WriteLine($"\n{string.Join(" - ", authentic)}: {cadence1}");  // Authentic

        // Plagal cadence (IV - I)
        var plagal = new[] { "F", "C" };
        var cadence2 = ProgressionAdvisor.DetectCadence(plagal, cadenceKey);
        Console.WriteLine($"{string.Join(" - ", plagal)}: {cadence2}");  // Plagal (Amen)

        // Deceptive cadence (V - vi)
        var deceptive = new[] { "G7", "Am" };
        var cadence3 = ProgressionAdvisor.DetectCadence(deceptive, cadenceKey);
        Console.WriteLine($"{string.Join(" - ", deceptive)}: {cadence3}");  // Deceptive

        // Half cadence (ends on V)
        var half = new[] { "C", "Dm", "G" };
        var cadence4 = ProgressionAdvisor.DetectCadence(half, cadenceKey);
        Console.WriteLine($"{string.Join(" - ", half)}: {cadence4}");  // Half

        // The same three chords with no key: heard in G major as IV - v - I, an authentic cadence
        Console.WriteLine($"{string.Join(" - ", half)} (key inferred): {ProgressionAdvisor.DetectCadence(half)}");

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

        Console.WriteLine($"\nColor assessment: {colorAnalysis.ColorfulnessRating}/10");
        Console.WriteLine($"Description: {colorAnalysis.Description}");

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
Key: C Major
Pattern: ii7 - V7 - Imaj7

C - G - Am - F:
Pattern: I - V - vi - IV

Tension curve:
       C: ████ (20 %)
      Am: ████████ (40 %)
       F: █████ (25 %)
       G: █████████████████ (85 %)
       C: ████ (20 %)

G7 - C: Authentic
F - C: Plagal
G7 - Am: Deceptive
C - Dm - G: Half
C - Dm - G (key inferred): Authentic

Chord characters:
       C: Bright (80 %, 85 %)
   Cmaj7: Dreamy (60 %, 70 %)
      Cm: Melancholic (60 %, 30 %)
    Cdim: Dark (25 %, 20 %)
      C7: Tense (30 %, 45 %)
    Caug: Mysterious (40 %, 55 %)

=== Progression Report ===
Imaj7 - vi7 - ii7 - V7 - iii7 - VI7 - ii7 - V7 - Imaj7 in C Major (tension 49%, complexity 48%)

Key: C Major
Complexity: 0.48194444
Overall tension: 49.4 %

Highlights:
  • Cadences: Authentic
  • Modulations/tonicizations: 1

After C - Am - F, try:
       B - Subdominant to dominant (score: 1.00)
       C - Plagal cadence (score: 0.95)
      Dm - Retrograde progression (score: 0.70)
      Fm - Mediant for color (score: 0.65)

=== Harmonic Color Analysis ===
Chromatic notes: 0

Modal turns: 0

Non-chord tones: 4
  PassingTone - D4 at 1/4
  OtherNonChordTone - F4 at 3/4
  PassingTone - A4 at 5/4
  OtherNonChordTone - C5 at 7/4

Color assessment: 1.5/10
Description: Mostly diatonic and stable.

C - A7 - Dm - G7 - C:
Secondary dominants: True
  A7 → Dm (V/ii)

C - Fm - C - G7 - C:
Borrowed chords: True
  Fm from C Minor

C - F - G - C voice leading:
  Smoothness: 61.1 %
  Average movement: 4.67 semitones
  Parallel fifths: 3
  Parallel octaves: 0
  Quality: Rough

*/
