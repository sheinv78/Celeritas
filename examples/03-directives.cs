// Directives Examples
// Tempo, dynamics, sections, BPM ramps, crescendo/diminuendo

using Celeritas.Core;
using Celeritas.Grammar;

namespace CeleritasExamples;

class Directives
{
    static void Main()
    {
        // ===== BPM (Beats Per Minute) =====

        // Static BPM
        var withBpm = MusicNotationAntlrParser.Parse("@bpm 120 C4/4 E4/4 G4/4");
        Console.WriteLine($"BPM directive at {withBpm.Directives[0].Time}: {withBpm.Directives[0].Value}");

        // BPM changes
        var bpmChanges = MusicNotationAntlrParser.Parse(
            "@bpm 120 C4/1 @bpm 140 D4/1 @bpm 100 E4/1");
        Console.WriteLine($"BPM changes: {bpmChanges.Directives.Length} directives");

        // BPM ramp (accelerando/ritardando)
        var ramp = MusicNotationAntlrParser.Parse("@bpm 120 -> 140 /2 C4/1 D4/1");
        // Ramps from 120 to 140 over 2 beats (1/2 duration)
        Console.WriteLine($"BPM ramp: {ramp.Directives[0].Type}");

        // Gradual slowdown
        var slowdown = MusicNotationAntlrParser.Parse("@bpm 140 -> 80 /4 C4/1 D4/1 E4/1 F4/1");

        // ===== Tempo Markings =====

        var tempo = MusicNotationAntlrParser.Parse("@tempo allegro C4/4 E4/4 G4/4");
        Console.WriteLine($"Tempo: {tempo.Directives[0].Value}");

        // Common tempo markings:
        // Slow: grave, largo, lento, adagio
        // Medium: andante, moderato, allegretto
        // Fast: allegro, vivace, presto, prestissimo

        var tempoChanges = MusicNotationAntlrParser.Parse(@"
            @tempo adagio C4/1
            @tempo allegro D4/1
            @tempo presto E4/1");

        // ===== Dynamics (Volume) =====

        // Static dynamics levels
        var dynamics = MusicNotationAntlrParser.Parse(
            "@dynamics pp C4/4 @dynamics mf E4/4 @dynamics ff G4/4");
        Console.WriteLine($"Dynamics: {dynamics.Directives.Length} directives");

        // All dynamic levels:
        // pppp (pianississimo) - extremely soft
        // ppp (pianissimo) - very very soft
        // pp (pianissimo) - very soft
        // p (piano) - soft
        // mp (mezzo-piano) - moderately soft
        // mf (mezzo-forte) - moderately loud
        // f (forte) - loud
        // ff (fortissimo) - very loud
        // fff (fortississimo) - very very loud
        // ffff (fortissississimo) - extremely loud

        // Accents:
        // sf (sforzando), sfz (sforzato) - sudden accent
        // fp (forte-piano) - loud then immediately soft
        // rf (rinforzando) - reinforced accent

        var fullRange = MusicNotationAntlrParser.Parse(
            "@dynamics pppp C4/4 @dynamics pp D4/4 @dynamics mf E4/4 @dynamics ff F4/4 @dynamics ffff G4/4");

        // ===== Crescendo (Gradual Volume Increase) =====

        // Crescendo without target
        var cresc = MusicNotationAntlrParser.Parse(
            "@dynamics p @cresc C4/4 D4/4 E4/4 F4/4");
        Console.WriteLine($"Crescendo: {cresc.Directives[1].Type}");

        // Crescendo to specific level
        var crescTarget = MusicNotationAntlrParser.Parse(
            "@dynamics mp @cresc to ff C4/2 D4/2");
        Console.WriteLine($"Crescendo to: {crescTarget.Directives[1].TargetValue}");

        // ===== Diminuendo (Gradual Volume Decrease) =====

        // Diminuendo without target
        var dim = MusicNotationAntlrParser.Parse(
            "@dynamics f @dim C4/4 D4/4 E4/4 F4/4");

        // Diminuendo to specific level
        var dimTarget = MusicNotationAntlrParser.Parse(
            "@dynamics ff @dim to pp C4/2 D4/2");

        // ===== Sections (Form Structure) =====

        var sections = MusicNotationAntlrParser.Parse(@"
            @section intro C4/4 E4/4 G4/4 C5/4
            @section verse D4/1 E4/1
            @section chorus F4/2 G4/2
            @section bridge A4/1
            @section outro C5/1");

        foreach (var dir in sections.Directives)
            Console.WriteLine($"Section at {dir.Time}: {dir.Value}");

        // Section labels can be anything
        var customSections = MusicNotationAntlrParser.Parse(@"
            @section ""verse 1"" C4/1
            @section ""pre-chorus"" D4/1
            @section ""chorus x2"" E4/1");

        // ===== Parts (Instrument/Voice Assignment) =====

        var parts = MusicNotationAntlrParser.Parse(@"
            @part piano [C4 E4 G4]/1
            @part bass C2/1
            @part drums R/1");

        // ===== Combining Multiple Directives =====

        var combined = MusicNotationAntlrParser.Parse(@"
            @bpm 120
            @tempo allegro
            @section intro
            @dynamics mf
            C4/4 E4/4 G4/4 C5/4 |

            @section verse
            @dynamics p
            D4/2 E4/2 |

            @section chorus
            @cresc to ff
            F4/4 G4/4 A4/4 B4/4 |

            @bpm 140
            @dim to mf
            C5/1
        ");

        Console.WriteLine($"\nCombined example:");
        Console.WriteLine($"  Notes: {combined.Notes.Length}");
        Console.WriteLine($"  Directives: {combined.Directives.Length}");

        // ===== Export with Directives =====

        var exported = MusicNotation.FormatWithDirectives(
            combined.Notes, combined.Directives, groupChords: true);
        Console.WriteLine($"\nExported:\n{exported}");

        // ===== Timeline Order =====
        // FormatWithDirectives merges notes and directives in timeline order

        var timeline = MusicNotationAntlrParser.Parse(
            "C4/4 @dynamics mf E4/4 @cresc G4/4 @dynamics ff C5/4");
        var timelineExport = MusicNotation.FormatWithDirectives(
            timeline.Notes, timeline.Directives);
        Console.WriteLine($"\nTimeline: {timelineExport}");
        // Directives appear at their exact timeline positions
    }
}

/* Expected Output:

BPM directive at 0: 120
BPM changes: 3 directives
BPM ramp: BpmRamp
Tempo: allegro
Dynamics: 3 directives
Crescendo: Crescendo
Crescendo to: ff
Section at 0: intro
Section at 1: verse
Section at 3: chorus
Section at 5: bridge
Section at 6: outro

Combined example:
  Notes: 16
  Directives: 8

*/
