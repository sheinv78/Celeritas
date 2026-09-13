// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The twelfth table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// eleventh reviewer's passages, written against the bar the judge now reads from the music's own
/// chord changes, the pedal that is one note under the harmony, and the closing figure that may
/// lie either side of the tune — and folded in afterwards. Four lenses: the BAR read from the
/// music (the same modulating passage in 9/8, 5/8, 7/8 and 2/2; a 6/8 piece with a harmony on each
/// dotted quarter; a piece that changes metre twice; a rubato whose chords drift by a sixteenth;
/// chords struck on the half bar throughout; whole-bar chords with one bar of four quarters; an
/// accompaniment in eighths under whole-bar harmonies — the bar comes from the harmony, not from
/// the fastest note; a harmony held two bars throughout; a half-bar pickup of two chords), the
/// PEDAL (a pedal entering late and holding under the pivot, two pedal notes a fifth apart, a
/// pedal in the top voice), the CLOSE where tune and figure share a register (an inner-voice
/// figure between a held bass and the tune, the tune holding one chord tone while the figure
/// moves) and the OPENING of a piece told C that begins on ii.
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality]</c> — plus <c>R</c> for a rest, the lengths
/// <c>h</c> (half), <c>q</c> (quarter), <c>t</c> (three quarters), <c>e</c> (three eighths),
/// <c>f</c> (five quarters), <c>n</c> (a bar and a half), <c>d</c> (two whole notes), and an exact
/// length written <c>@num/den</c> in whole notes. The plans are a musician's, in whole notes; a
/// 4/4 bar = 1, bar k begins at position k-1, and a bar of another metre is as long as its own
/// chords say. Of the reviewer's thirty-eight, twenty-five are here; thirteen the library still
/// reads otherwise and are the next iteration's work, in four families. The BAR where the chord
/// changes have no majority: chords of irregular length in no pattern fall back to the clock and
/// the dominant is heard three bars late, and a melody alone in 3/4 — no chords to read a bar
/// from — parts the roads. The PEDAL rule reaches only a pedal struck with a chord: struck again
/// every bar, or struck an eighth before the first chord, the pedal is a note of the line again.
/// The CLOSE where the figure is not a run of quick single notes below or above the tune: a chord
/// rolled a sixteenth at a time and held, rolled over two bars, a figure in the tune's own octave,
/// a two-hand arpeggio under a trill, and a tune outlining the chord's upper thirds above it all
/// lose the homecoming. And the OPENING told C of a piece that begins on vi or iii, which parts
/// the roads in every texture — a family wider than the ninth table's statement of it.
/// </remarks>
internal static class BarsFromTheMusicAndSharedRegistersPassages
{
    private const int ChordRegister = 48;

    private static readonly Dictionary<string, int[]> Qualities = new()
    {
        [""] = [0, 4, 7],
        ["m"] = [0, 3, 7],
        ["dim"] = [0, 3, 6],
        ["7"] = [0, 4, 7, 10],
        ["m7"] = [0, 3, 7, 10],
        ["maj7"] = [0, 4, 7, 11],
        ["6"] = [0, 4, 7, 9],
        ["m6"] = [0, 3, 7, 9],
    };

    private readonly record struct Chord(int Root, int[] Intervals, Rational Offset, Rational Duration);

    private static List<Chord> ParseChords(string chords)
    {
        var parsed = new List<Chord>();
        var time = Rational.Zero;
        foreach (var raw in chords.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "|") continue;
            var token = raw;
            var duration = Rational.Whole;
            var at = token.IndexOf('@');
            if (at >= 0)
            {
                var parts = token[(at + 1)..].Split('/');
                duration = new Rational(long.Parse(parts[0]), long.Parse(parts[1]));
                token = token[..at];
            }
            else if (token.EndsWith('h')) { duration = Rational.Half; token = token[..^1]; }
            else if (token.EndsWith('q')) { duration = Rational.Quarter; token = token[..^1]; }
            else if (token.EndsWith('t')) { duration = new Rational(3, 4); token = token[..^1]; }
            else if (token.EndsWith('e')) { duration = new Rational(3, 8); token = token[..^1]; }
            else if (token.EndsWith('f')) { duration = new Rational(5, 4); token = token[..^1]; }
            else if (token.EndsWith('n')) { duration = new Rational(3, 2); token = token[..^1]; }
            else if (token.EndsWith('d')) { duration = new Rational(2, 1); token = token[..^1]; }
            if (token == "R") { time += duration; continue; }
            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            var quality = colon >= 0 ? token[(colon + 1)..] : "";
            parsed.Add(new Chord(root, Qualities[quality], time, duration));
            time += duration;
        }

        return parsed;
    }

    // ---------- builders ----------

    /// <summary>Block chords in close root position from C3, each for its written length.</summary>
    private static NoteEvent[] Block(string chords) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration)))];

    /// <summary>One note held from <paramref name="from"/> for <paramref name="length"/> — an organ pedal.</summary>
    private static NoteEvent[] Pedal(int pitch, Rational from, Rational length) => [new NoteEvent(pitch, from, length)];

    /// <summary>One note struck again every <paramref name="every"/> from <paramref name="from"/>, <paramref name="times"/> times.</summary>
    private static NoteEvent[] Restruck(int pitch, Rational from, Rational every, int times) =>
        [.. Enumerable.Range(0, times).Select(k => new NoteEvent(pitch, from + (every * k), every))];

    /// <summary>One chord from <paramref name="from"/> as a figure in eighths — root, fifth, third, fifth — at <paramref name="register"/>.</summary>
    private static NoteEvent[] Figure(string chord, Rational from, Rational length, int register)
    {
        var c = ParseChords(chord)[0];
        int[] pattern = [c.Intervals[0], c.Intervals[2], c.Intervals[1], c.Intervals[2]];
        var count = (int)(length / Rational.Eighth).ToDouble();
        var notes = new List<NoteEvent>();
        for (var k = 0; k < count; k++)
            notes.Add(new NoteEvent(register + c.Root + pattern[k % 4], from + (Rational.Eighth * k), Rational.Eighth));
        return [.. notes];
    }

    /// <summary>The pitches given, one after another from <paramref name="from"/>, each of <paramref name="each"/>.</summary>
    private static NoteEvent[] Run(Rational from, Rational each, params int[] pitches) =>
        [.. pitches.Select((p, k) => new NoteEvent(p, from + (each * k), each))];

    /// <summary>The pitches given, struck <paramref name="apart"/> apart from <paramref name="from"/> and all held to <paramref name="until"/> — a chord rolled.</summary>
    private static NoteEvent[] Rolled(Rational from, Rational until, Rational apart, params int[] pitches) =>
        [.. pitches.Select((p, k) => new NoteEvent(p, from + (apart * k), until - from - (apart * k)))];

    /// <summary>Each chord as a bass note on the first beat and its triad on the beats after — a waltz or an oom-pah left hand, the beat a quarter.</summary>
    private static NoteEvent[] OomPah(string chords, int beatsInABar, params int[] bassBeats)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            for (var beat = 0; beat < beatsInABar; beat++)
            {
                var at = c.Offset + (Rational.Quarter * beat);
                if (bassBeats.Contains(beat)) notes.Add(new NoteEvent(36 + c.Root, at, Rational.Quarter));
                else foreach (var i in c.Intervals) notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, at, Rational.Quarter));
            }
        }

        return [.. notes];
    }

    /// <summary>Each chord as an Alberti bass in eighths — root, fifth, third, fifth — for its written length.</summary>
    private static NoteEvent[] Alberti(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] pattern = c.Intervals.Length == 4
                ? [c.Intervals[0], c.Intervals[2], c.Intervals[1], c.Intervals[3]]
                : [c.Intervals[0], c.Intervals[2], c.Intervals[1], c.Intervals[2]];
            var eighths = (int)(c.Duration / Rational.Eighth).ToDouble();
            for (var k = 0; k < eighths; k++)
                notes.Add(new NoteEvent(ChordRegister + c.Root + pattern[k % 4], c.Offset + (Rational.Eighth * k), Rational.Eighth));
        }

        return [.. notes];
    }

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] AtPosition(Rational position, int toRoot, bool toMajor) => [new(position, toRoot, toMajor)];

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan) { OpeningRoot = openingRoot };

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan) { OpeningRoot = openingRoot };

    // ---------- the material ----------

    /// <summary>The metre passage: C, ii, V7, I, then two phrases in the dominant — one chord a bar, whatever the bar is.</summary>
    private const string MetrePassage = "0 2:m 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    private static string InLength(string chords, string suffix) =>
        string.Join(' ', chords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t == "|" ? t : t + suffix));

    /// <summary>Eleven bars of the Picardy tune — A minor, its relative major, A minor again — without its last bar.</summary>
    private const string PicardyTuneOpen =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 F5/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | ";

    private const string PicardyTuneOpenHigh =
        "C6/4 B5/4 A5/2 | F6/4 D6/4 A5/2 | G#5/4 B5/4 E6/2 | C6/4 B5/4 A5/2 | "
        + "E6/4 G6/4 C6/2 | A5/4 F6/4 C6/2 | D6/4 B5/4 G5/2 | E6/4 D6/4 C6/2 | "
        + "C6/4 B5/4 A5/2 | F6/4 D6/4 A5/2 | G#5/4 B5/4 E6/2 | ";

    private const string PicardyChordsOpen = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7";

    private static RealModulationPassages.PlannedModulation[] PicardyPlan => [new(5, 0, true), new(9, 9, false)];

    internal static readonly RealModulationPassages.Passage[] Table =
    [
        // ---------- the BAR, read from the music ----------
        Notes("BAR 9/8: C Dm G7 C | G C D7 G | G C D7 G, one chord a bar of nine eighths (block chords)",
            Block(InLength(MetrePassage, "@9/8")), AtPosition(new Rational(9, 2), 7, true)),
        Notes("BAR 5/8: the same passage, one chord a bar of five eighths (block chords)",
            Block(InLength(MetrePassage, "@5/8")), AtPosition(new Rational(5, 2), 7, true)),
        Notes("BAR 7/8: the same passage, one chord a bar of seven eighths (block chords)",
            Block(InLength(MetrePassage, "@7/8")), AtPosition(new Rational(7, 2), 7, true)),
        Notes("BAR 2/2: two half-note chords a bar, four bars to the phrase: C F G C Dm G7 C C, then two phrases in the dominant (block chords)",
            Block(InLength("0 5 7 0 2:m 7:7 0 0 | 7 0 2:7 7 4:m 9:m 2:7 7 | 7 0 2:7 7 4:m 9:m 2:7 7", "h")),
            AtPosition(new Rational(4, 1), 7, true)),
        Notes("BAR 6/8, a harmony on EACH dotted quarter: C F | G7 C | Am Dm | G7 C, then two phrases in the dominant (block chords)",
            Block(InLength("0 5 | 7:7 0 | 9:m 2:m | 7:7 0 | 7 0 | 2:7 7 | 4:m 9:m | 2:7 7 | 7 0 | 2:7 7 | 4:m 9:m | 2:7 7", "e")),
            AtPosition(new Rational(3, 1), 7, true)),
        Notes("BAR: the metre changes TWICE — C Dm G7 C in 4/4, two phrases in 3/4, then home in 4/4 (block chords)",
            Block("0 2:m 7:7 0 | 7t 0t 2:7t 7t | 7t 0t 2:7t 7t | 2:m 7:7 0 0"),
            [new(new Rational(4, 1), 7, true), new(new Rational(10, 1), 0, true)]),
        Notes("BAR: a RUBATO whose chords drift by a sixteenth, no two of them the same length running (block chords)",
            Block("0@17/16 2:m@15/16 7:7@17/16 0@15/16 | 7@17/16 0@15/16 2:7@17/16 7@15/16 | 7@17/16 0@15/16 2:7@17/16 7@15/16"),
            AtPosition(new Rational(4, 1), 7, true)),
        Notes("BAR: every chord struck on the HALF BAR throughout, a whole note long (block chords)",
            Block("Rh 0 2:m 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7"), AtPosition(new Rational(9, 2), 7, true)),
        Notes("BAR: whole-bar chords but ONE bar of four quarters — C vi ii V7 in the dominant's second bar (block chords)",
            Block("0 2:m 7:7 0 | 7 0q 4:mq 9:mq 2:7q 2:7 7 | 7 0 2:7 7"), AtPosition(new Rational(4, 1), 7, true)),
        Notes("BAR: an accompaniment in EIGHTHS under whole-bar harmonies — the bar is the harmony's, not the fastest note's (four voices)",
            Together(Block(MetrePassage), Notated(
                "E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | F5/8 A5/8 D6/8 A5/8 F5/8 A5/8 D6/8 A5/8 | "
                + "F5/8 B5/8 D6/8 B5/8 F5/8 B5/8 D6/8 B5/8 | E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | "
                + "D5/8 G5/8 B5/8 G5/8 D5/8 G5/8 B5/8 G5/8 | E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | "
                + "F#5/8 A5/8 D6/8 A5/8 F#5/8 A5/8 D6/8 A5/8 | D5/8 G5/8 B5/8 G5/8 D5/8 G5/8 B5/8 G5/8 | "
                + "D5/8 G5/8 B5/8 G5/8 D5/8 G5/8 B5/8 G5/8 | E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | "
                + "F#5/8 A5/8 D6/8 A5/8 F#5/8 A5/8 D6/8 A5/8 | D5/8 G5/8 B5/8 G5/8 D5/8 G5/8 B5/8 G5/8")),
            AtPosition(new Rational(4, 1), 7, true)),
        Notes("BAR: every harmony held two whole bars, struck again on the second — C F G C | G C D7 G in 4/4 (block chords)",
            Block("0 0 5 5 7 7 0 0 | 7 7 0 0 2:7 2:7 7 7"), AtPosition(new Rational(8, 1), 7, true)),
        Notes("BAR: a PICKUP of half a bar, two quarter chords before the first full bar (block chords)",
            Block("0q 7:7q 0 2:m 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7"), AtPosition(new Rational(9, 2), 7, true)),

        Notes("BAR: a WALTZ accompaniment — the bass on one, the chord on two and three — under a tune in quarters, 3/4 (four voices)",
            Together(OomPah(InLength(MetrePassage, "t"), 3, 0), Notated("E5/4 G5/4 C5/4 | F5/4 A5/4 D5/4 | F5/4 B4/4 D5/4 | E5/4 C5/4 E5/4 | "
                + "D5/4 G5/4 B5/4 | E5/4 C5/4 G5/4 | F#5/4 A5/4 D5/4 | G5/4 B5/4 D5/4 | "
                + "D5/4 G5/4 B5/4 | E5/4 C5/4 G5/4 | F#5/4 A5/4 C6/4 | B5/4 G5/4 G5/4")),
            AtPosition(new Rational(3, 1), 7, true)),
        Notes("BAR: an OOM-PAH in 4/4 — the bass on one and three, the chord on two and four — under a tune in quarters (four voices)",
            Together(OomPah(MetrePassage, 4, 0, 2), Notated("E5/4 G5/4 C5/4 E5/4 | F5/4 A5/4 D5/4 F5/4 | F5/4 B4/4 D5/4 F5/4 | E5/4 C5/4 E5/4 G5/4 | "
                + "D5/4 G5/4 B5/4 D5/4 | E5/4 C5/4 G5/4 E5/4 | F#5/4 A5/4 D5/4 F#5/4 | G5/4 B5/4 D5/4 G5/4 | "
                + "D5/4 G5/4 B5/4 D5/4 | E5/4 C5/4 G5/4 E5/4 | F#5/4 A5/4 C6/4 A5/4 | B5/4 G5/4 G5/4 B5/4")),
            AtPosition(new Rational(4, 1), 7, true)),
        Notes("BAR: an ALBERTI bass in eighths under a tune in quarters, 3/4 (four voices)",
            Together(Alberti(InLength(MetrePassage, "t")), Notated("E5/4 G5/4 C5/4 | F5/4 A5/4 D5/4 | F5/4 B4/4 D5/4 | E5/4 C5/4 E5/4 | "
                + "D5/4 G5/4 B5/4 | E5/4 C5/4 G5/4 | F#5/4 A5/4 D5/4 | G5/4 B5/4 D5/4 | "
                + "D5/4 G5/4 B5/4 | E5/4 C5/4 G5/4 | F#5/4 A5/4 C6/4 | B5/4 G5/4 G5/4")),
            AtPosition(new Rational(3, 1), 7, true)),

        // ---------- the PEDAL ----------
        Notes("a silent chord-bar over a held TONIC pedal in 3/4: C F G C | R | G C D7 G, the C pedal sounding throughout (four voices)",
            Together(Block("0t 5t 7t 0t | Rt | 7t 0t 2:7t 7t"), Pedal(36, Rational.Zero, new Rational(27, 4))),
            AtPosition(new Rational(15, 4), 7, true)),
        Notes("a pedal that ENTERS LATE, struck alone in the middle of the fourth bar and held under the pivot (four voices)",
            Together(Block(MetrePassage), Pedal(43, new Rational(7, 2), new Rational(9, 2))), AtPosition(new Rational(4, 1), 7, true)),
        Notes("TWO pedal notes a fifth apart, C and G held under the whole passage (four voices)",
            Together(Block(MetrePassage), Pedal(36, Rational.Zero, new Rational(12, 1)), Pedal(43, Rational.Zero, new Rational(12, 1))),
            AtPosition(new Rational(4, 1), 7, true)),
        Notes("a pedal in the TOP voice, a high C held over the whole passage (four voices)",
            Together(Block(MetrePassage), Pedal(84, Rational.Zero, new Rational(12, 1))), AtPosition(new Rational(4, 1), 7, true)),

        // ---------- the CLOSE where tune and figure share a register ----------
        Notes("the Picardy close, an INNER-VOICE figure between a held bass and the tune above it (four voices)",
            Together(Block(PicardyChordsOpen), Pedal(33, new Rational(11, 1), Rational.Whole), Figure("9", new Rational(11, 1), Rational.Whole, ChordRegister + 12),
                Notated(PicardyTuneOpenHigh + "C6/4 B5/4 A5/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, the tune HOLDING one chord tone while the figure moves below it (four voices)",
            Together(Block(PicardyChordsOpen), Figure("9", new Rational(11, 1), Rational.Whole, ChordRegister + 12),
                Notated(PicardyTuneOpen + "A5/1")), PicardyPlan, major: false, openingRoot: 9),

        Notes("sustained chords with the tune in eighths ABOVE them, outlining each chord's upper thirds (four voices)",
            Together(Block(MetrePassage), Notated(
                "E5/8 G5/8 B5/8 G5/8 E5/8 G5/8 B5/8 G5/8 | F5/8 A5/8 C6/8 A5/8 F5/8 A5/8 C6/8 A5/8 | "
                + "B5/8 D6/8 F6/8 D6/8 B5/8 D6/8 F6/8 D6/8 | E5/8 G5/8 B5/8 G5/8 E5/8 G5/8 B5/8 G5/8 | "
                + "B5/8 D6/8 G6/8 D6/8 B5/8 D6/8 G6/8 D6/8 | E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | "
                + "F#5/8 A5/8 C6/8 A5/8 F#5/8 A5/8 C6/8 A5/8 | B5/8 D6/8 G6/8 D6/8 B5/8 D6/8 G6/8 D6/8 | "
                + "B5/8 D6/8 G6/8 D6/8 B5/8 D6/8 G6/8 D6/8 | E5/8 G5/8 C6/8 G5/8 E5/8 G5/8 C6/8 G5/8 | "
                + "F#5/8 A5/8 C6/8 A5/8 F#5/8 A5/8 C6/8 A5/8 | B5/8 D6/8 G6/8 D6/8 B5/8 D6/8 G6/8 D6/8")),
            AtPosition(new Rational(4, 1), 7, true)),

        // ---------- the OPENING of a piece told C ----------
        Blocks("told C, opening on III with no tonic triad in the opening phrase: Em Am Dm G | C F G C | C F G7 C (block chords)",
            "4:m 9:m 2:m 7 | 0 5 7 0 | 0 5 7:7 0", Nowhere),
        Blocks("told C, opening on II with the tonic triad in the opening phrase: Dm G7 C C | C F G C | C F G7 C (block chords)",
            "2:m 7:7 0 0 | 0 5 7 0 | 0 5 7:7 0", Nowhere),
        Blocks("told C, opening on II with no tonic triad in the opening phrase: Dm G7 Em Am | C F G C | C F G7 C (block chords)",
            "2:m 7:7 4:m 9:m | 0 5 7 0 | 0 5 7:7 0", Nowhere),
    ];
}
