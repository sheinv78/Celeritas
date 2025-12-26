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
        var formOptions = new FormAnalysisOptions
        {
            MinRestForPhraseBoundary = new Rational(1, 4),
            Key = key
        };

        var form = FormAnalyzer.Analyze(melody, formOptions);

        Console.WriteLine($"=== Form Analysis ===");
        Console.WriteLine($"Phrases detected: {form.Phrases.Count}");
        foreach (var phrase in form.Phrases)
        {
            Console.WriteLine($"  Phrase {phrase.Number}: {phrase.StartTime} → {phrase.EndTime}");
            Console.WriteLine($"    Length: {phrase.Length}");
            Console.WriteLine($"    Cadence: {phrase.CadenceType}");
            Console.WriteLine($"    Character: {phrase.Character}");
        }

        // ===== Cadence Analysis =====

        Console.WriteLine($"\nCadences:");
        foreach (var cadence in form.Cadences)
        {
            Console.WriteLine($"  {cadence.Type} at {cadence.Time}");
            Console.WriteLine($"    Strength: {cadence.Strength:P0}");
            Console.WriteLine($"    Chords: {cadence.ChordProgression}");
        }

        // ===== Section Detection =====

        var sonata = MusicNotation.Parse(@"
            4/4: C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2 |
            D4/4 F4/4 A4/4 D5/4 | A4/4 F4/4 D4/2 |
            C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2");

        var sectionAnalysis = FormAnalyzer.Analyze(sonata, formOptions);

        Console.WriteLine($"\n=== Section Structure ===");
        Console.WriteLine($"Form label: {sectionAnalysis.FormLabel}");  // A B A
        Console.WriteLine($"Sections: {sectionAnalysis.Sections.Count}");

        foreach (var section in sectionAnalysis.Sections)
        {
            Console.WriteLine($"\n  Section {section.Label}:");
            Console.WriteLine($"    Time: {section.StartTime} → {section.EndTime}");
            Console.WriteLine($"    Similarity to A: {section.SimilarityToFirst:P1}");
            Console.WriteLine($"    Key: {section.LocalKey}");
            Console.WriteLine($"    Density: {section.NoteDensity:F2} notes/beat");
        }

        // ===== Period Detection =====

        var period = MusicNotation.Parse(@"
            4/4: C4/4 E4/4 G4/4 C5/4 | B4/4 G4/4 D4/2 |
            C4/4 E4/4 G4/4 C5/4 | G4/4 E4/4 C4/2");

        var periodAnalysis = FormAnalyzer.Analyze(period, formOptions);

        if (periodAnalysis.HasPeriods)
        {
            Console.WriteLine($"\n=== Period Structure ===");
            foreach (var p in periodAnalysis.Periods)
            {
                Console.WriteLine($"Period {p.Number}:");
                Console.WriteLine($"  Antecedent: {p.Antecedent.StartTime} → {p.Antecedent.EndTime}");
                Console.WriteLine($"    Cadence: {p.Antecedent.CadenceType}");
                Console.WriteLine($"  Consequent: {p.Consequent.StartTime} → {p.Consequent.EndTime}");
                Console.WriteLine($"    Cadence: {p.Consequent.CadenceType}");
                Console.WriteLine($"  Relationship: {p.Relationship}");
            }
        }

        // ===== Polyphony Analysis =====

        // Two-voice counterpoint
        var twoVoice = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 | G4/4 F4/4 E4/4 D4/4 >>
            << E3/2 D3/2 | C3/1 >>");

        var polyAnalysis = PolyphonyAnalyzer.Analyze(twoVoice);

        Console.WriteLine($"\n=== Polyphony Analysis ===");
        Console.WriteLine($"Voices detected: {polyAnalysis.VoiceCount}");
        Console.WriteLine($"Polyphonic sections: {polyAnalysis.PolyphonicSections.Count}");

        foreach (var section in polyAnalysis.PolyphonicSections)
        {
            Console.WriteLine($"\n  Section at {section.StartTime}:");
            Console.WriteLine($"    Duration: {section.Duration}");
            Console.WriteLine($"    Voices: {section.VoiceCount}");
            Console.WriteLine($"    Texture: {section.Texture}");  // Homophonic, Polyphonic, Heterophonic
        }

        // ===== Voice Separation =====

        var mixed = MusicNotation.Parse(@"
            C4/4 E4/4 C3/4 G4/4 | E3/4 C5/4 G3/4 E4/4");

        var separated = VoiceSeparator.Separate(mixed);

        Console.WriteLine($"\n=== Voice Separation ===");
        Console.WriteLine($"Input notes: {mixed.Length}");
        Console.WriteLine($"Voices: {separated.Voices.Count}");

        for (int i = 0; i < separated.Voices.Count; i++)
        {
            var voice = separated.Voices[i];
            Console.WriteLine($"\n  Voice {i + 1} ({voice.Range}):");
            Console.WriteLine($"    Notes: {voice.Notes.Length}");
            Console.WriteLine($"    Average pitch: {voice.AveragePitch:F1}");
            Console.WriteLine($"    Range: {voice.AmbitusStart}-{voice.AmbitusEnd}");

            var notes = string.Join(" ", voice.Notes.Take(5).Select(n =>
                MusicNotation.PitchToNoteName(n.Pitch)));
            Console.WriteLine($"    First notes: {notes}...");
        }

        // ===== SATB Voice Ranges =====

        var satb = MusicNotation.Parse(@"
            << C5/1 | G4/1 | E4/1 | C3/1 >>");

        var satbSeparated = VoiceSeparator.SeparateIntoSATB(satb);

        Console.WriteLine($"\n=== SATB Separation ===");
        Console.WriteLine($"Soprano: {satbSeparated.Soprano.Notes.Length} notes");
        Console.WriteLine($"  Range: {satbSeparated.Soprano.AmbitusStart}-{satbSeparated.Soprano.AmbitusEnd}");

        Console.WriteLine($"Alto: {satbSeparated.Alto.Notes.Length} notes");
        Console.WriteLine($"  Range: {satbSeparated.Alto.AmbitusStart}-{satbSeparated.Alto.AmbitusEnd}");

        Console.WriteLine($"Tenor: {satbSeparated.Tenor.Notes.Length} notes");
        Console.WriteLine($"  Range: {satbSeparated.Tenor.AmbitusStart}-{satbSeparated.Tenor.AmbitusEnd}");

        Console.WriteLine($"Bass: {satbSeparated.Bass.Notes.Length} notes");
        Console.WriteLine($"  Range: {satbSeparated.Bass.AmbitusStart}-{satbSeparated.Bass.AmbitusEnd}");

        // ===== Counterpoint Rules ===

        var counterpoint = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 | E3/4 F3/4 G3/4 A3/4 >>");

        var rulesCheck = PolyphonyAnalyzer.CheckCounterpointRules(counterpoint);

        Console.WriteLine($"\n=== Counterpoint Rules ===");
        Console.WriteLine($"Parallel fifths: {rulesCheck.ParallelFifths}");
        Console.WriteLine($"Parallel octaves: {rulesCheck.ParallelOctaves}");
        Console.WriteLine($"Hidden parallels: {rulesCheck.HiddenParallels}");
        Console.WriteLine($"Voice crossing: {rulesCheck.VoiceCrossing}");
        Console.WriteLine($"Spacing violations: {rulesCheck.SpacingViolations}");
        Console.WriteLine($"Overall quality: {rulesCheck.QualityScore:P1}");

        if (rulesCheck.Violations.Any())
        {
            Console.WriteLine($"\nViolations:");
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

        Console.WriteLine($"\n=== Imitation ===");
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
  Phrase 1: 0 → 2
    Length: 2
    Cadence: HalfCadence
    Character: Opening, questioning

  Phrase 2: 2 → 4
    Length: 2
    Cadence: HalfCadence
    Character: Continuing

  Phrase 3: 4 → 6
    Length: 2
    Cadence: AuthenticCadence
    Character: Conclusive

=== Section Structure ===
Form label: A B A
Sections: 3

  Section A:
    Time: 0 → 2
    Similarity to A: 100.0%
    Key: C major
    Density: 2.00 notes/beat

  Section B:
    Time: 2 → 4
    Similarity to A: 45.2%
    Key: D minor
    Density: 2.00 notes/beat

*/
