// Form Analysis and Polyphony Examples
// Phrases, cadences, sections, voice separation

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace CeleritasExamples;

class FormAndPolyphony
{
    static void Main()
    {
        // ===== Phrase Detection =====

        var melody = MusicNotation.Parse(@"
            4/4: C4/4 E4/4 G4/4 C5/2 R/2 |
            D4/4 F4/4 A4/4 D5/2 R/2 |
            E4/4 G4/4 B4/4 E5/2 R/2 |
            C4/2 G4/2 C5/1");

        var key = new KeySignature("C", true);
        using var melodyBuffer = new NoteBuffer(melody.Length);
        melodyBuffer.AddRange(melody);

        var form = FormAnalyzer.Analyze(melodyBuffer, new FormAnalysisOptions(
            MinRestForPhraseBoundary: new Rational(1, 4),
            Key: key));

        Console.WriteLine("=== Form Analysis ===");
        Console.WriteLine($"Phrases detected: {form.Phrases.Count}");
        foreach (var phrase in form.Phrases)
        {
            Console.WriteLine($"  Phrase: {phrase.Start} → {phrase.End}");
            Console.WriteLine($"    Length: {phrase.Length}");
            Console.WriteLine($"    Cadence: {phrase.EndingCadence}");
        }

        // ===== Cadence Analysis =====

        Console.WriteLine("\nCadences:");
        foreach (var cadence in form.Cadences)
        {
            Console.WriteLine($"  {cadence.Type} at position {cadence.Position}");
            Console.WriteLine($"    From {cadence.FromChord} to {cadence.ToChord}");
            Console.WriteLine($"    {cadence.Description}");
        }

        // ===== Section Detection =====

        var sonata = MusicNotation.Parse(@"
            4/4: C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2 |
            D4/4 F4/4 A4/4 D5/4 | A4/4 F4/4 D4/2 |
            C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2");
        using var sonataBuffer = new NoteBuffer(sonata.Length);
        sonataBuffer.AddRange(sonata);

        var formOptions = new FormAnalysisOptions(new Rational(1, 2));
        var sectionAnalysis = FormAnalyzer.Analyze(sonataBuffer, formOptions);

        Console.WriteLine("\n=== Section Structure ===");
        Console.WriteLine($"Form label: {sectionAnalysis.FormLabel}");  // A B A
        Console.WriteLine($"Sections: {sectionAnalysis.Sections.Count}");

        foreach (var section in sectionAnalysis.Sections)
        {
            Console.WriteLine($"\n  Section {section.Label}:");
            Console.WriteLine($"    Time: {section.Start} → {section.End}");
            Console.WriteLine($"    Length: {section.Length}");
            Console.WriteLine($"    Phrases: {section.PhraseCount}");
        }

        // ===== Period Detection =====

        var periodMelody = MusicNotation.Parse(@"
            4/4: C4/4 E4/4 G4/4 C5/4 | B4/4 G4/4 D4/2 R/2 |
            C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2 R/2");
        using var periodBuffer = new NoteBuffer(periodMelody.Length);
        periodBuffer.AddRange(periodMelody);

        var periodAnalysis = FormAnalyzer.Analyze(periodBuffer, formOptions);

        if (periodAnalysis.Periods.Count > 0)
        {
            Console.WriteLine("\n=== Period Structure ===");
            for (int i = 0; i < periodAnalysis.Periods.Count; i++)
            {
                var p = periodAnalysis.Periods[i];
                Console.WriteLine($"Period {i + 1}:");
                Console.WriteLine($"  Antecedent phrase: {p.FirstPhraseIndex}");
                Console.WriteLine($"  Consequent phrase: {p.SecondPhraseIndex}");
                Console.WriteLine($"  Length A: {p.LengthA}, Length B: {p.LengthB}");
            }
        }

        // ===== Polyphony Analysis =====

        // Two-voice counterpoint
        var twoVoice = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 | G4/4 F4/4 E4/4 D4/4 >>
            << E3/2 D3/2 | C3/1 >>");

        using var polyBuffer = new NoteBuffer(twoVoice.Length);
        polyBuffer.AddRange(twoVoice);
        var polyAnalysis = PolyphonyAnalyzer.Analyze(polyBuffer);

        Console.WriteLine("\n=== Polyphony Analysis ===");
        Console.WriteLine($"Voices detected: {polyAnalysis.Voices.Voices.Count}");
        Console.WriteLine($"Texture density: {polyAnalysis.TextureDensity:F2}");
        Console.WriteLine($"Voice independence: {polyAnalysis.VoiceIndependence:P1}");
        Console.WriteLine($"Quality score: {polyAnalysis.QualityScore:P1}");

        Console.WriteLine("\nMotion statistics:");
        Console.WriteLine($"  Parallel: {polyAnalysis.MotionStats.ParallelPercent:F1}%");
        Console.WriteLine($"  Similar: {polyAnalysis.MotionStats.SimilarPercent:F1}%");
        Console.WriteLine($"  Contrary: {polyAnalysis.MotionStats.ContraryPercent:F1}%");
        Console.WriteLine($"  Oblique: {polyAnalysis.MotionStats.ObliquePercent:F1}%");

        // ===== Voice Separation =====

        var mixed = MusicNotation.Parse(@"
            C4/4 E4/4 C3/4 G4/4 | E3/4 C5/4 G3/4 E4/4");

        using var mixedBuffer = new NoteBuffer(mixed.Length);
        mixedBuffer.AddRange(mixed);
        var separated = VoiceSeparator.Separate(mixedBuffer);

        Console.WriteLine("\n=== Voice Separation ===");
        Console.WriteLine($"Input notes: {mixed.Length}");
        Console.WriteLine($"Voices: {separated.Voices.Count}");
        Console.WriteLine($"Separation quality: {separated.SeparationQuality:P1}");
        Console.WriteLine($"Voice crossings: {separated.VoiceCrossings}");

        foreach (var voice in separated.Voices)
        {
            Console.WriteLine($"\n  {voice.Name}:");
            Console.WriteLine($"    Notes: {voice.Notes.Count}");
            Console.WriteLine($"    Range: {voice.AmbitusStart}-{voice.AmbitusEnd}");
            Console.WriteLine($"    Average pitch: {voice.AveragePitch:F1}");

            var notes = string.Join(" ", voice.Notes.Take(5).Select(n =>
                MusicMath.MidiToNoteName(n.Pitch)));
            Console.WriteLine($"    First notes: {notes}");
        }

        // ===== SATB Voice Separation =====

        var satb = MusicNotation.Parse(@"
            << C5/1 >> << G4/1 >> << E4/1 >> << C3/1 >>");
        var satbSeparated = VoiceSeparator.SeparateIntoSATB(satb);

        Console.WriteLine("\n=== SATB Separation ===");
        Console.WriteLine($"Soprano: {satbSeparated.Soprano.Notes.Count} notes");
        Console.WriteLine($"  Range: {satbSeparated.Soprano.AmbitusStart}-{satbSeparated.Soprano.AmbitusEnd}");

        Console.WriteLine($"Alto: {satbSeparated.Alto.Notes.Count} notes");
        Console.WriteLine($"  Range: {satbSeparated.Alto.AmbitusStart}-{satbSeparated.Alto.AmbitusEnd}");

        Console.WriteLine($"Tenor: {satbSeparated.Tenor.Notes.Count} notes");
        Console.WriteLine($"  Range: {satbSeparated.Tenor.AmbitusStart}-{satbSeparated.Tenor.AmbitusEnd}");

        Console.WriteLine($"Bass: {satbSeparated.Bass.Notes.Count} notes");
        Console.WriteLine($"  Range: {satbSeparated.Bass.AmbitusStart}-{satbSeparated.Bass.AmbitusEnd}");

        // ===== Counterpoint Rules ===

        var counterpoint = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 >> << E3/4 F3/4 G3/4 A3/4 >>");

        var rulesCheck = PolyphonyAnalyzer.CheckCounterpointRules(counterpoint);

        Console.WriteLine("\n=== Counterpoint Rules ===");
        Console.WriteLine($"Parallel fifths: {rulesCheck.ParallelFifths}");
        Console.WriteLine($"Parallel octaves: {rulesCheck.ParallelOctaves}");
        Console.WriteLine($"Hidden parallels: {rulesCheck.HiddenParallels}");
        Console.WriteLine($"Voice crossing: {rulesCheck.VoiceCrossing}");
        Console.WriteLine($"Spacing violations: {rulesCheck.SpacingViolations}");
        Console.WriteLine($"Overall quality: {rulesCheck.QualityScore:P1}");

        if (rulesCheck.Violations.Any())
        {
            Console.WriteLine("\nViolations:");
            foreach (var violation in rulesCheck.Violations)
            {
                Console.WriteLine($"  {violation.Type} at {violation.Time}");
                Console.WriteLine($"    Severity: {violation.Severity}");
                Console.WriteLine($"    Description: {violation.Description}");
            }
        }

        // ===== Imitation Detection =====

        var fugue = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 | R/1 >>
            << R/1 | C3/4 D3/4 E3/4 F3/4 >>");

        var imitation = PolyphonyAnalyzer.DetectImitation(fugue);

        Console.WriteLine("\n=== Imitation ===");
        Console.WriteLine($"Has imitation: {imitation.HasImitation}");
        if (imitation.HasImitation)
        {
            Console.WriteLine($"Type: {imitation.Type}");  // Canon, Fugue, Free
            Console.WriteLine($"Interval: {imitation.Interval} semitones");
            Console.WriteLine($"Time delay: {imitation.TimeDelay}");
            Console.WriteLine($"Voices involved: {string.Join(", ", imitation.VoicesInvolved)}");
        }
    }
}

/* Expected Output:

=== Form Analysis ===
Phrases detected: 4
  Phrase: 0 → 3/2
    Length: 3/2
    Cadence: None
  ...

=== Section Structure ===
Form label: A B A
Sections: 3
  Section A:
    Time: 0 → 4
    Length: 4
    Phrases: 1

=== Polyphony Analysis ===
Voices detected: 2
Texture density: 1.50
Voice independence: 75.0%
Quality score: 85.0%

Motion statistics:
  Parallel: 25.0%
  Similar: 12.5%
  Contrary: 50.0%
  Oblique: 12.5%

=== Voice Separation ===
Input notes: 8
Voices: 2
Separation quality: 95.0%
Voice crossings: 0

  Soprano:
    Notes: 4
    Range: 60-72
    Average pitch: 66.0
    First notes: C4 E4 G4 C5

=== SATB Separation ===
Soprano: 1 notes
  Range: 72-72
Alto: 1 notes
  Range: 67-67
...

=== Counterpoint Rules ===
Parallel fifths: 0
Parallel octaves: 0
Hidden parallels: 0
Voice crossing: 0
Spacing violations: 0
Overall quality: 95.0%

=== Imitation ===
Has imitation: true
Type: Canon
Interval: -12 semitones
Time delay: 2
Voices involved: 1, 2

*/
