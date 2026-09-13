// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The eleventh table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// tenth reviewer's passages, written after a given key came to be refuted where the piece opens,
/// a bar in which nothing is struck came to be a pause however much is held over it, and a
/// restrike came to carry its chord's harmony on — and folded in afterwards. Five lenses: METRE
/// (one modulating passage written in 4/4, 3/4, 6/8, 2/4, 5/4 and 12/8 with the chord lengths to
/// match, chords of a bar and a half across the 4/4 bar line, a slow piece of two-whole-note
/// chords, and a piece that changes metre at the modulation), the PEDAL and what is still sounding
/// (a silent chord-bar over a tonic pedal and over a dominant pedal in 3/4, a slow piece over a
/// tonic pedal, a pedal that changes with the key, one inner voice held across the silent bar, the
/// old key's chord tied across the pivot, a fermata in the upper voices while the bass rests), the
/// OPENING of a piece
/// told a key (told a key whose tonic chord arrives only at the end, told the key of the piece's
/// second chord, told the relative minor of a piece opening on I6, a piece opening on a lone bass
/// note or a bare fifth), the CLOSE where the tune and the accompaniment share a register (the
/// closing chord as a two-hand arpeggio over three octaves, the melody arpeggiating it alone in
/// sixteenths, a block chord under sixteenths of its own tones) and the LOOPS and SEQUENCES under
/// all of it (the loop in 3/4 and over its own pedal, a real sequence through three keys in 6/8);
/// and, folded in with the eleventh iteration, the closing chord's figure two octaves ABOVE the
/// tune over a held bass.
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c> — with the lengths each metre
/// asks for, plus <c>R</c> for a rest. The plans are a musician's, in whole notes; a 4/4 bar = 1,
/// bar k begins at position k-1, and a bar of another metre is as long as its own chords say.
/// Of the reviewer's thirty-four, thirty are here: the twenty-two folded in with the tenth
/// iteration and eight more with the eleventh, which read the bar from the music's own pace, made
/// a pedal one note under the harmony rather than the harmony, and let the figure lie above the
/// tune as well as below it. The four the library still reads otherwise are the next iteration's
/// work: a 3/4 piece whose phrases are THREE bars, where the judge's phrase is four bars of the
/// music and the piece's is three; a piece told C that opens on vi and sounds no C triad in its
/// opening phrase — Am Em F G | C F G C | C F G7 C — where the detector told C hears one key and
/// the trajectory, which opens on the A minor chord, hears C from bar 4; and two closes where
/// tune and figure share a register — the tune crossing below the figure with both in eighths,
/// which reaches the detector road as a stream of dyads with no pitches to tell the voices apart,
/// and the last chord rolled as a spread chord, which is heard note by note and is no chord.
/// </remarks>
internal static class MetresPedalsAndSharedRegistersPassages
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
            if (token.EndsWith('h')) { duration = Rational.Half; token = token[..^1]; }
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

    /// <summary>One chord held from <paramref name="from"/> for <paramref name="length"/> — a fermata.</summary>
    private static NoteEvent[] Held(string chord, Rational from, Rational length) =>
        [.. ParseChords(chord).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, from, length)))];

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

    /// <summary>The pitches given, struck a sixteenth apart from <paramref name="from"/> and all held to <paramref name="until"/> — a chord rolled.</summary>
    private static NoteEvent[] Rolled(Rational from, Rational until, params int[] pitches) =>
        [.. pitches.Select((p, k) => new NoteEvent(p, from + (new Rational(1, 16) * k), until - from - (new Rational(1, 16) * k)))];

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    private static RealModulationPassages.PlannedModulation[] AtPosition(Rational position, int toRoot, bool toMajor) => [new(position, toRoot, toMajor)];

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan) { OpeningRoot = openingRoot };

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan) { OpeningRoot = openingRoot };

    // ---------- the tunes ----------

    /// <summary>Eleven bars of the Picardy tune — A minor, its relative major, A minor again — without its last bar.</summary>
    private const string PicardyTuneOpen =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 F5/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | ";

    private const string PicardyChordsOpen = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7";

    private static RealModulationPassages.PlannedModulation[] PicardyPlan => [new(5, 0, true), new(9, 9, false)];

    /// <summary>The metre passage: C, ii, V7, I, then two phrases in the dominant — one chord a bar, whatever the bar is.</summary>
    private const string MetrePassage = "0 2:m 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    private static string InLength(string chords, string suffix) =>
        string.Join(' ', chords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t == "|" ? t : t + suffix));

    private static string TwicePerBar(string chords, string suffix) =>
        string.Join(' ', chords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t == "|" ? t : t + suffix + " " + t + suffix));

    internal static readonly RealModulationPassages.Passage[] Table =
    [
        // ---------- METRE everywhere: one passage, six metres, the positions scaled ----------
        Notes("METRE 4/4: C Dm G7 C | G C D7 G | G C D7 G, one chord a bar of four quarters (block chords)",
            Block(MetrePassage), AtPosition(new Rational(4, 1), 7, true)),
        Notes("METRE 3/4: the same passage, one chord a bar of three quarters (block chords)",
            Block(InLength(MetrePassage, "t")), AtPosition(new Rational(3, 1), 7, true)),
        Notes("METRE 6/8: the same passage, one chord a bar struck on both dotted quarters (block chords)",
            Block(TwicePerBar(MetrePassage, "e")), AtPosition(new Rational(3, 1), 7, true)),
        Notes("METRE 2/4: the same passage, one chord a bar of two quarters (block chords)",
            Block(InLength(MetrePassage, "h")), AtPosition(new Rational(2, 1), 7, true)),
        Notes("METRE 5/4: the same passage, one chord a bar of five quarters (block chords)",
            Block(InLength(MetrePassage, "f")), AtPosition(new Rational(5, 1), 7, true)),
        Notes("METRE 12/8: the same passage, one chord a bar of a bar and a half (block chords)",
            Block(InLength(MetrePassage, "n")), AtPosition(new Rational(6, 1), 7, true)),
        Notes("METRE: the metre CHANGES at the modulation — C Dm G7 C in 4/4, then two phrases in 3/4 (block chords)",
            Block("0 2:m 7:7 0 | 7t 0t 2:7t 7t | 7t 0t 2:7t 7t"), AtPosition(new Rational(4, 1), 7, true)),
        Notes("METRE: chords of a BAR AND A HALF across the 4/4 bar line: C Dm G7 C | G C D7 G | G C D7 G (block chords)",
            Block(InLength("0 2:m 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7", "n")), AtPosition(new Rational(6, 1), 7, true)),
        Notes("a SLOW piece, every chord two whole notes: C F G C | G C D7 G, nothing struck in every second bar (block chords)",
            Block(InLength("0 5 7 0 | 7 0 2:7 7", "d")), AtPosition(new Rational(8, 1), 7, true)),
        Notes("a SLOW piece of twelve two-bar chords: C F G C | G C D7 G | G C D7 G (block chords)",
            Block(InLength(MetrePassage, "d")), AtPosition(new Rational(8, 1), 7, true)),

        // ---------- the PEDAL and what is still sounding ----------
        Notes("a silent chord-bar over a held TONIC pedal: C F G C | R | G C D7 G, the C pedal sounding throughout (four voices)",
            Together(Block("0 5 7 0 | R | 7 0 2:7 7"), Pedal(36, Rational.Zero, new Rational(9, 1))), At(6, 7, true)),
        Notes("a silent chord-bar over a held DOMINANT pedal in 3/4: C F G C | R | G C D7 G (block chords in 3/4)",
            Together(Block("0t 5t 7t 0t | Rt | 7t 0t 2:7t 7t"), Pedal(43, Rational.Zero, new Rational(27, 4))),
            AtPosition(new Rational(15, 4), 7, true)),
        Notes("a SLOW piece over a held tonic pedal, every chord two whole notes: C F G C | G C D7 G (four voices)",
            Together(Block(InLength("0 5 7 0 | 7 0 2:7 7", "d")), Pedal(36, Rational.Zero, new Rational(16, 1))),
            AtPosition(new Rational(8, 1), 7, true)),
        Notes("a pedal that CHANGES with the key: C under the first phrase, G under the rest, no bar silent (four voices)",
            Together(Block("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7"), Pedal(36, Rational.Zero, new Rational(4, 1)), Pedal(43, new Rational(4, 1), new Rational(8, 1))),
            At(5, 7, true)),
        Notes("ONE inner voice held across the silent bar, the others resting: C F G C | (G alone) | G C D7 G (four voices)",
            Together(Block("0 5 7 0 | R | 7 0 2:7 7"), Pedal(55, new Rational(3, 1), new Rational(2, 1))), At(6, 7, true)),
        Notes("TWO inner voices held across the silent bar, the others resting: C F G C | (E and G alone) | G C D7 G (four voices)",
            Together(Block("0 5 7 0 | R | 7 0 2:7 7"), Pedal(55, new Rational(3, 1), new Rational(2, 1)), Pedal(52, new Rational(3, 1), new Rational(2, 1))),
            At(6, 7, true)),
        Notes("the old key's chord TIED across the pivot, held two bars with nothing struck under it: C F G [C held bars 4 and 5] | G C D7 G (block chords as notes)",
            Block("0 5 7 0d | 7 0 2:7 7"), At(6, 7, true)),
        Notes("a FERMATA in the upper voices while the bass rests: C F G [E G C held two bars, no bass] | G C D7 G (four voices)",
            Together(Block("0 5 7"), Pedal(52, new Rational(3, 1), new Rational(2, 1)), Pedal(55, new Rational(3, 1), new Rational(2, 1)), Pedal(60, new Rational(3, 1), new Rational(2, 1)),
                Block("R R R R R | 7 0 2:7 7")), At(6, 7, true)),

        // ---------- the OPENING of a piece told a key ----------
        Blocks("told a key whose tonic chord arrives only at the END: Am Dm E7 Am | Am Dm E7 Am | C F G C, told C (block chords)",
            "9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m | 0 5 7 0", At(9, 0, true)),
        Blocks("told the key of the piece's SECOND chord: Am F C G four times, told F (block chords)",
            "9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7", Nowhere, openingRoot: 5),
        Notes("told the RELATIVE MINOR of a piece opening on I6: C6 F G C | C F G C | C F G7 C, told A minor (block chords as notes)",
            Block("0:6 5 7 0 | 0 5 7 0 | 0 5 7:7 0"), Nowhere, major: false, openingRoot: 9),
        Notes("a piece opening on a LONE BASS NOTE, the chord entering on the second quarter: C F G C | G C D7 G | G C D7 G (four voices)",
            Together(Pedal(48, Rational.Zero, Rational.Quarter), Block("Rq 0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7")),
            AtPosition(new Rational(17, 4), 7, true)),
        Notes("a piece opening on a BARE FIFTH, the third arriving in the second bar: C-G | F G C | G C D7 G | G C D7 G (four voices)",
            Together(Pedal(48, Rational.Zero, Rational.Whole), Pedal(55, Rational.Zero, Rational.Whole), Block("R 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7")),
            At(5, 7, true)),

        // ---------- the CLOSE where the tune and the accompaniment share a register ----------
        Notes("the Picardy close, the figure ABOVE the tune: the chord arpeggiated two octaves up over a held bass (four voices)",
            Together(Block(PicardyChordsOpen), Pedal(45, new Rational(11, 1), Rational.Whole), Figure("9", new Rational(11, 1), Rational.Whole, ChordRegister + 24),
                Notated(PicardyTuneOpen + "C5/4 B4/4 A4/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, a TWO-HAND arpeggio of the final chord spanning three octaves (four voices)",
            Together(Block(PicardyChordsOpen), Run(new Rational(11, 1), Rational.Eighth, 45, 52, 57, 61, 64, 69, 73, 76)),
            PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, the melody arpeggiating the final chord ALONE in sixteenths (four voices)",
            Together(Block(PicardyChordsOpen), Run(new Rational(11, 1), new Rational(1, 16), 69, 73, 76, 81, 76, 73, 69, 73, 76, 81, 76, 73, 69, 73, 76, 81)),
            PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, a block final chord under SIXTEENTHS of its own tones (four voices)",
            Together(Block(PicardyChordsOpen + " 9"), Run(new Rational(11, 1), new Rational(1, 16), 69, 73, 76, 81, 76, 73, 69, 73, 76, 81, 76, 73, 69, 73, 76, 81)),
            PicardyPlan, major: false, openingRoot: 9),

        // ---------- the LOOPS and SEQUENCES under all of it ----------
        Notes("the loop Am F C G four times in 3/4, told A minor (block chords in 3/4)",
            Block("9:mt 5t 0t 7t | 9:mt 5t 0t 7t | 9:mt 5t 0t 7t | 9:mt 5t 0t 7t"), Nowhere, major: false, openingRoot: 9),
        Notes("the loop Am F C G four times over its own held A pedal, told A minor (four voices)",
            Together(Block("9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7"), Pedal(45, Rational.Zero, new Rational(16, 1))),
            Nowhere, major: false, openingRoot: 9),
        Notes("a real sequence of one cadencing phrase through three keys in 6/8: C F G7 C | D G A7 D | E A B7 E (block chords in 6/8)",
            Block("0t 5t 7:7t 0t | 2t 7t 9:7t 2t | 4t 9t 11:7t 4t"),
            [new(new Rational(3, 1), 2, true), new(new Rational(6, 1), 4, true)]),
    ];
}
