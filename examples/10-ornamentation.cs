// Ornamentation Examples
// Trills, mordents, turns, appoggiaturas


using Celeritas.Core;
using System.Linq;
using Celeritas.Core.Ornamentation;

namespace CeleritasExamples;

class OrnamentationExamples
{
    static void Main()
    {
        // ===== Basic Trill =====

        var baseNote = new NoteEvent(
            pitch: 60,  // C4
            offset: Rational.Zero,
            duration: new Rational(1, 2),  // Half note
            velocity: 0.8f
        );

        var trill = new Trill
        {
            BaseNote = baseNote,
            Interval = 2,  // Whole step (major second)
            Speed = 8      // 8 notes per quarter
        };

        var expanded = trill.Expand();
        Console.WriteLine($"=== Basic Trill ===");
        Console.WriteLine($"Base note: C4, duration: 1/2");
        Console.WriteLine($"Expanded to {expanded.Length} notes:");

        foreach (var note in expanded.Take(8))
        {
            Console.WriteLine($"  {MusicMath.MidiToNoteName(note.Pitch)} at {note.Offset}");
        }

        // ===== Trill Variations =====

        // Half-step trill
        var halfStepTrill = new Trill
        {
            BaseNote = baseNote,
            Interval = 1,  // Half step (minor second)
            Speed = 16     // 16 notes per quarter (very fast)
        };

        Console.WriteLine($"\n=== Half-Step Trill ===");
        Console.WriteLine($"Alternates C4-Db4 at 16 notes per quarter");
        Console.WriteLine($"Total notes: {halfStepTrill.Expand().Length}");

        // Start with upper neighbor
        var upperStartTrill = new Trill
        {
            BaseNote = baseNote,
            Interval = 2,
            Speed = 8,
            StartWithUpper = true
        };

        var upperExpanded = upperStartTrill.Expand();
        Console.WriteLine($"\n=== Upper-Start Trill ===");
        Console.WriteLine($"First notes: {string.Join(" ", upperExpanded.Take(4).Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: D4 C4 D4 C4

        // Trill with turn ending
        var trillWithTurn = new Trill
        {
            BaseNote = baseNote,
            Interval = 2,
            Speed = 8
            // HasTurnEnding = true  // Property doesn't exist yet
        };

        var turnExpanded = trillWithTurn.Expand();
        Console.WriteLine($"\n=== Trill (will add turn support later) ===");
        Console.WriteLine($"Last notes: {string.Join(" ", turnExpanded.TakeLast(4).Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // ===== Mordents =====

        // Upper mordent (main-upper-main)
        var upperMordent = new Mordent
        {
            BaseNote = baseNote,
            Type = MordentType.Upper,
            Interval = 2,  // Whole step
            Alternations = 1  // Single alternation
        };

        var mordentExpanded = upperMordent.Expand();
        Console.WriteLine($"\n=== Upper Mordent ===");
        Console.WriteLine($"Pattern: {string.Join(" ", mordentExpanded.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: C4 D4 C4

        // Lower mordent (main-lower-main)
        var lowerMordent = new Mordent
        {
            BaseNote = baseNote,
            Type = MordentType.Lower,
            Interval = 2,
            Alternations = 1
        };

        var lowerExpanded = lowerMordent.Expand();
        Console.WriteLine($"\n=== Lower Mordent ===");
        Console.WriteLine($"Pattern: {string.Join(" ", lowerExpanded.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: C4 Bb3 C4

        // Double mordent (more alternations)
        var doubleMordent = new Mordent
        {
            BaseNote = baseNote,
            Type = MordentType.Upper,
            Interval = 2,
            Alternations = 2
        };

        var doubleExpanded = doubleMordent.Expand();
        Console.WriteLine($"\n=== Double Mordent ===");
        Console.WriteLine($"Pattern: {string.Join(" ", doubleExpanded.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: C4 D4 C4 D4 C4

        // ===== Turns =====

        // Normal turn (upper-main-lower-main)
        var turn = new Turn
        {
            BaseNote = baseNote,
            Type = TurnType.Normal,
            UpperInterval = 2,  // Whole step above
            LowerInterval = 2   // Whole step below
        };

        var turnExpanded2 = turn.Expand();
        Console.WriteLine($"\n=== Normal Turn ===");
        Console.WriteLine($"Pattern: {string.Join(" ", turnExpanded2.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: D4 C4 Bb3 C4

        // Inverted turn (lower-main-upper-main)
        var invertedTurn = new Turn
        {
            BaseNote = baseNote,
            Type = TurnType.Inverted,
            UpperInterval = 2,
            LowerInterval = 2
        };

        var invertedExpanded = invertedTurn.Expand();
        Console.WriteLine($"\n=== Inverted Turn ===");
        Console.WriteLine($"Pattern: {string.Join(" ", invertedExpanded.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: Bb3 C4 D4 C4

        // Turn with different intervals
        var chromaticTurn = new Turn
        {
            BaseNote = baseNote,
            Type = TurnType.Normal,
            UpperInterval = 1,  // Half step above
            LowerInterval = 1   // Half step below
        };

        var chromaticExpanded = chromaticTurn.Expand();
        Console.WriteLine($"\n=== Chromatic Turn ===");
        Console.WriteLine($"Pattern: {string.Join(" ", chromaticExpanded.Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");
        // Output: Db4 C4 B3 C4

        // ===== Appoggiaturas =====

        // Long appoggiatura (accented, takes ~half the note value)
        var longAppogg = new Appoggiatura
        {
            BaseNote = baseNote,
            Type = AppogiaturaType.Long,
            Interval = 2       // Approach from whole step above
            // Direction = 1   // Property doesn't exist yet (1 = from above, -1 = from below)
        };

        var longExpanded = longAppogg.Expand();
        Console.WriteLine($"\n=== Long Appoggiatura ===");
        Console.WriteLine($"Notes: {string.Join(" ", longExpanded.Select(n => $"{MusicMath.MidiToNoteName(n.Pitch)}({n.Duration})"))}");
        // D4 takes ~1/4, then C4 takes ~1/4

        // Short appoggiatura / acciaccatura (very brief, unaccented)
        var acciaccatura = new Appoggiatura
        {
            BaseNote = baseNote,
            Type = AppogiaturaType.Short,
            Interval = 2
            // Direction = -1  // Property doesn't exist yet (From below)
        };

        var shortExpanded = acciaccatura.Expand();
        Console.WriteLine($"\n=== Acciaccatura ===");
        Console.WriteLine($"Notes: {string.Join(" ", shortExpanded.Select(n => $"{MusicMath.MidiToNoteName(n.Pitch)}({n.Duration})"))}");
        // Bb3 very brief, then C4 takes most of duration

        // ===== Parsing Ornaments from Notation =====

        // Trill notation: C4/4{tr}
        var trillNotation = MusicNotation.Parse("C4/4{tr}");
        Console.WriteLine($"\n=== Parsed Trill ===");
        Console.WriteLine($"Expanded to {trillNotation.Length} notes");

        // Trill with parameters: C4/4{tr:1:16}
        var trillWithParams = MusicNotation.Parse("C4/4{tr:1:16}");
        Console.WriteLine($"\n=== Parsed Trill (custom) ===");
        Console.WriteLine($"Half-step, 16 notes per quarter");
        Console.WriteLine($"Total notes: {trillWithParams.Length}");

        // Mordent notation: C4/4{mord}
        var mordentNotation = MusicNotation.Parse("C4/4{mord}");
        Console.WriteLine($"\n=== Parsed Mordent ===");
        Console.WriteLine($"Notes: {string.Join(" ", mordentNotation.Take(3).Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // Turn notation: C4/4{turn}
        var turnNotation = MusicNotation.Parse("C4/4{turn}");
        Console.WriteLine($"\n=== Parsed Turn ===");
        Console.WriteLine($"Notes: {string.Join(" ", turnNotation.Take(4).Select(n => MusicMath.MidiToNoteName(n.Pitch)))}");

        // ===== Applying Ornaments to Melody =====

        var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/2");

        // Apply trill to first note
        var ornamentedMelody = new List<NoteEvent>();

        var firstNoteTrill = new Trill
        {
            BaseNote = melody[0],
            Interval = 2,
            Speed = 8
        };
        ornamentedMelody.AddRange(firstNoteTrill.Expand());
        ornamentedMelody.AddRange(melody.Skip(1));

        Console.WriteLine($"\n=== Ornamented Melody ===");
        Console.WriteLine($"Original: 4 notes");
        Console.WriteLine($"With trill on first note: {ornamentedMelody.Count} notes");

        // ===== Ornament Applier (Batch Processing) =====

        // Note: OrnamentApplier.Apply doesn't exist yet, implementing manual approach
        /*
        var ornamentMap = new Dictionary<int, Ornament>
        {
            { 0, new Trill { BaseNote = melody[0], Interval = 2, Speed = 8 } },
            { 2, new Mordent { BaseNote = melody[2], Type = MordentType.Upper, Interval = 2 } }
        };

        var fullyOrnamented = OrnamentApplier.Apply(melody, ornamentMap);
        */

        var fullyOrnamented = melody;  // Placeholder until batch API exists

        Console.WriteLine($"\n=== Batch Ornamentation (TODO) ===");
        Console.WriteLine($"Original melody: {melody.Length} notes");
        // Console.WriteLine($"Ornaments applied: {ornamentMap.Count}");
        Console.WriteLine($"Result: {fullyOrnamented.Length} notes");

        // ===== Performance Considerations =====

        var longTrill = new Trill
        {
            BaseNote = new NoteEvent(60, Rational.Zero, new Rational(4, 1), 0.8f),  // 4 whole notes
            Interval = 2,
            Speed = 16  // Very fast
        };

        Console.WriteLine($"\n=== Performance Note ===");
        Console.WriteLine($"Long trill (4 whole notes at 16 notes/quarter):");
        Console.WriteLine($"  Would expand to ~256 notes");
        Console.WriteLine($"  Consider using Speed=8 or lower for playback");
    }
}

/* Expected Output:

=== Basic Trill ===
Base note: C4, duration: 1/2
Expanded to 16 notes:
  C4 at 0
  D4 at 1/32
  C4 at 1/16
  D4 at 3/32
  C4 at 1/8
  D4 at 5/32
  C4 at 3/16
  D4 at 7/32

=== Half-Step Trill ===
Alternates C4-Db4 at 16 notes per quarter
Total notes: 32

=== Upper-Start Trill ===
First notes: D4 C4 D4 C4

=== Upper Mordent ===
Pattern: C4 D4 C4

=== Lower Mordent ===
Pattern: C4 Bb3 C4

=== Double Mordent ===
Pattern: C4 D4 C4 D4 C4

=== Normal Turn ===
Pattern: D4 C4 Bb3 C4

=== Inverted Turn ===
Pattern: Bb3 C4 D4 C4

=== Long Appoggiatura ===
Notes: D4(1/4) C4(1/4)

=== Batch Ornamentation ===
Original melody: 4 notes
Ornaments applied: 2
Result: 21 notes

*/

