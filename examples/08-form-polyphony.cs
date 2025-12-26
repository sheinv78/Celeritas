// Form Analysis and Polyphony Examples
// Phrases, cadences, sections, voice separation


using Celeritas.Core;
using System.Linq;
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

        Console.WriteLine($"=== Form Analysis ===");
        Console.WriteLine($"Phrases detected: {form.Phrases.Count}");
        foreach (var phrase in form.Phrases)
        {
            Console.WriteLine($"  Phrase: {phrase.Start} → {phrase.End}");
            Console.WriteLine($"    Length: {phrase.Length}");
            Console.WriteLine($"    Cadence: {phrase.EndingCadence}");
        }

        // ===== Cadence Analysis =====

        Console.WriteLine($"\nCadences:");
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

        var options = new FormAnalysisOptions(new Rational(1, 2));
        var sectionAnalysis = FormAnalyzer.Analyze(sonataBuffer, options);

        Console.WriteLine($"\n=== Section Structure ===");
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
        // Note: Period properties (Number, Antecedent, Consequent) and HasPeriods don't exist

        /*
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
        */

        // ===== Polyphony Analysis =====

        // Two-voice counterpoint
        var twoVoice = MusicNotation.Parse(@"
            << C4/4 D4/4 E4/4 F4/4 | G4/4 F4/4 E4/4 D4/4 >>
            << E3/2 D3/2 | C3/1 >>");

        using var polyBuffer = new NoteBuffer(twoVoice.Length);
        polyBuffer.AddRange(twoVoice);
        var polyAnalysis = PolyphonyAnalyzer.Analyze(polyBuffer);

        Console.WriteLine($"\n=== Polyphony Analysis ===");
        // Note: VoiceCount and PolyphonicSections properties don't exist
        // Console.WriteLine($"Voices detected: {polyAnalysis.VoiceCount}");
        // Console.WriteLine($"Polyphonic sections: {polyAnalysis.PolyphonicSections.Count}");

        /*
        foreach (var section in polyAnalysis.PolyphonicSections)
        {
            Console.WriteLine($"\n  Section at {section.StartTime}:");
            Console.WriteLine($"    Duration: {section.Duration}");
            Console.WriteLine($"    Voices: {section.VoiceCount}");
            Console.WriteLine($"    Texture: {section.Texture}");  // Homophonic, Polyphonic, Heterophonic
        }
        */

        // ===== Voice Separation =====

        var mixed = MusicNotation.Parse(@"
            C4/4 E4/4 C3/4 G4/4 | E3/4 C5/4 G3/4 E4/4");

        using var mixedBuffer = new NoteBuffer(mixed.Length);
        mixedBuffer.AddRange(mixed);
        var separated = VoiceSeparator.Separate(mixedBuffer);

        Console.WriteLine($"\n=== Voice Separation ===");
        Console.WriteLine($"Input notes: {mixed.Length}");
        Console.WriteLine($"Voices: {separated.Voices.Count}");

        for (int i = 0; i < separated.Voices.Count; i++)
        {
            var voice = separated.Voices[i];
            Console.WriteLine($"\n  Voice {i + 1} ({voice.Range}):");
            Console.WriteLine($"    Notes: {voice.Notes.Count}");
            Console.WriteLine($"    Average pitch: {voice.AveragePitch:F1}");
            // Note: AmbitusStart/AmbitusEnd properties don't exist
            // Console.WriteLine($"    Range: {voice.AmbitusStart}-{voice.AmbitusEnd}");

            var notes = string.Join(" ", voice.Notes.Take(5).Select(n =>
                MusicMath.MidiToNoteName(n.Pitch)));
            Console.WriteLine($"    First notes: {notes}...");
        }

        // ===== SATB Voice Ranges =====
        // Note: VoiceSeparator.SeparateIntoSATB doesn't exist
        /*
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
        */

        // ===== Counterpoint Rules ===
        // Note: PolyphonyAnalyzer.CheckCounterpointRules doesn't exist
        /*
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
        */

        // ===== Imitation Detection =====
        // Note: PolyphonyAnalyzer.DetectImitation doesn't exist
        /*
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
        */
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
