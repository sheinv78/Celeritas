// Directives Examples
// Tempo, dynamics, sections, BPM ramps, crescendo/diminuendo

using Celeritas.Core;

namespace CeleritasExamples;

class Directives
{
    static void Main()
    {
        // ===== BPM (Beats Per Minute) =====

        // Static BPM
        var withBpm = MusicNotation.ParseFull("@bpm 120 C4/4 E4/4 G4/4");
        Console.WriteLine($"BPM directive at {withBpm.Directives[0].Time}: {((TempoBpmDirective)withBpm.Directives[0]).Bpm}");

        // BPM changes
        var bpmChanges = MusicNotation.ParseFull(
            "@bpm 120 C4/1 @bpm 140 D4/1 @bpm 100 E4/1");
        Console.WriteLine($"BPM changes: {bpmChanges.Directives.Length} directives");

        // BPM ramp (accelerando/ritardando)
        var ramp = MusicNotation.ParseFull("@bpm 120 -> 140 /2 C4/1 D4/1");
        // Ramps from 120 to 140 over 2 beats (1/2 duration)
        Console.WriteLine($"BPM ramp: {((TempoBpmDirective)ramp.Directives[0]).GetType().Name}");

        // Gradual slowdown
        var slowdown = MusicNotation.ParseFull("@bpm 140 -> 80 /4 C4/1 D4/1 E4/1 F4/1");

        // ===== Tempo Markings =====

        var tempo = MusicNotation.ParseFull("@tempo allegro C4/4 E4/4 G4/4");
        Console.WriteLine($"Tempo: {((TempoCharacterDirective)tempo.Directives[0]).Character}");

        // Common tempo markings:
        // Slow: grave, largo, lento, adagio
        // Medium: andante, moderato, allegretto
        // Fast: allegro, vivace, presto, prestissimo

        var tempoChanges = MusicNotation.ParseFull(@"
            @tempo adagio C4/1
            @tempo allegro D4/1
            @tempo presto E4/1");

        // ===== Dynamics (Volume) =====

        // Static dynamics levels
        var dynamics = MusicNotation.ParseFull(
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

        var fullRange = MusicNotation.ParseFull(
            "@dynamics pppp C4/4 @dynamics pp D4/4 @dynamics mf E4/4 @dynamics ff F4/4 @dynamics ffff G4/4");

        // ===== Crescendo (Gradual Volume Increase) =====

        // Crescendo without target
        var cresc = MusicNotation.ParseFull(
            "@dynamics p @cresc C4/4 D4/4 E4/4 F4/4");
        if (cresc.Directives[1] is DynamicsDirective dynDir)
            Console.WriteLine($"Crescendo: {dynDir.Type}");

        // Crescendo to specific level
        var crescTarget = MusicNotation.ParseFull(
            "@dynamics mp @cresc to ff C4/2 D4/2");
        if (crescTarget.Directives[1] is DynamicsDirective crescDyn)
            Console.WriteLine($"Crescendo to: {crescDyn.TargetLevel}");

        // ===== Diminuendo (Gradual Volume Decrease) =====

        // Diminuendo without target
        var dim = MusicNotation.ParseFull(
            "@dynamics f @dim C4/4 D4/4 E4/4 F4/4");

        // Diminuendo to specific level
        var dimTarget = MusicNotation.ParseFull(
            "@dynamics ff @dim to pp C4/2 D4/2");

        // ===== Sections (Form Structure) =====

        var sections = MusicNotation.ParseFull(@"
            @section intro C4/4 E4/4 G4/4 C5/4
            @section verse D4/1 E4/1
            @section chorus F4/2 G4/2
            @section bridge A4/1
            @section outro C5/1");

        foreach (var dir in sections.Directives)
        {
            if (dir is SectionDirective sectionDir)
                Console.WriteLine($"Section at {dir.Time}: {sectionDir.Label}");
        }

        // Section labels can be anything
        var customSections = MusicNotation.ParseFull(@"
            @section ""verse 1"" C4/1
            @section ""pre-chorus"" D4/1
            @section ""chorus x2"" E4/1");

        // ===== Parts (Instrument/Voice Assignment) =====

        var parts = MusicNotation.ParseFull(@"
            @part piano [C4 E4 G4]/1
            @part bass C2/1
            @part drums R/1");

        // ===== Combining Multiple Directives =====

        var combined = MusicNotation.ParseFull(@"
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

        var timeline = MusicNotation.ParseFull(
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
BPM ramp: TempoBpmDirective
Tempo: allegro
Dynamics: 3 directives
Crescendo: Crescendo
Crescendo to: ff
Section at 0: intro
Section at 1: verse
Section at 3: chorus
Section at 4: bridge
Section at 5: outro

Combined example:
  Notes: 11
  Directives: 10

Exported:
@bpm 120 @tempo allegro @section intro @dynamics mf C4/4 E4/4 G4/4 C5/4 @section verse @dynamics p D4/2 E4/2 @section chorus @cresc to ff F4/4 G4/4 A4/4 B4/4 @bpm 140 @dim to mf C5/1

Timeline: C4/4 @dynamics mf E4/4 @cresc G4/4 @dynamics ff C5/4

*/
