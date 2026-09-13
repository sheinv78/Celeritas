// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The ninth table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// eighth reviewer's passages, written after a seventh chord came to open the key of its root, a
/// sequence to keep the reading of its model, a loop coming to rest on the old tonic to stay the
/// old key's, the phrase count to restart at a re-entry after silence and a tremolo to be the
/// chord it spells — and folded in afterwards. Five lenses: the OPENING (a piece opening on a
/// seventh chord — the tonic's, ii's, vi's, V's — or on ii or IV as a triad, or told a key it never
/// sounds a chord of; a major blues told its relative minor), the SEQUENCE (the same loop moved up
/// a tone, a fourth, a minor third, twice and three times, told the minor; a real sequence through
/// three keys; a circle of fifths that is no modulation; a loop whose transposition carries a
/// leading tone; a loop cadencing into its relative major, and its copy's repeat, one key each),
/// SILENCE (three bars; a re-entry in the old key; two silent bars between two old-key phrases),
/// TREMOLO (tremolo eighths for a whole passage, alone and under the tune; on a major piece's
/// close; on the dominant sevenths; the close in tremolo quarters; a trill on the Picardy third)
/// and the RELATIVE-MAJOR boundary (ii V I in the major; the major's V I inside a phrase that
/// closes on the minor's tonic; the major's cadence at the phrase end).
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c> — plus <c>R</c> for a bar of
/// silence. The plans are a musician's, in whole notes; a 4/4 bar = 1, bar k begins at position
/// k-1; a move to the relative key is planned where a chord only the new key owns first sounds,
/// the pivot chord a bar before it. Of the reviewer's forty-seven, thirty-five are here. Four are
/// the transposed loop told C — a fourth up, a minor third up, a tone up twice, moved once — where
/// the detector, told C, names the copy the major with vi first and the trajectory, told nothing
/// and opening on the A minor chord, names it the minor: the same music read alike from each
/// opening key, and no plan can speak for both; the readings are asserted road by road in
/// <see cref="TheTwoRoadsHearEveryTextureAlikeTests"/>. Eight the library still reads otherwise
/// and are the next iteration's work: the minor blues in sevenths told C (the detector's own test
/// of the given key still takes Am7 for C's tonic with a note above it, and hears A minor only at
/// the E7); a silent bar then the new key on a two-bar fermata, a silent 3/4 bar, and two silent
/// bars off the phrase grid (the count restarts only at a pause that begins where a phrase would);
/// the Picardy chord as an Alberti bass, restruck in quarters under a melody ending on its second,
/// and in tremolo eighths under a passing tone; and two Am F G C loops under a tune whose B rises
/// to C at each close (debatable: the loop opens on the minor's tonic, the tune says the major).
/// </remarks>
internal static class OpeningsSequencesAndTremolosPassages
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

    /// <summary>Block chords in close root position from C3, each for its written length; R is a bar of silence.</summary>
    private static NoteEvent[] Block(string chords) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration)))];

    /// <summary>Every chord in tremolo eighths for its written length: root and third against fifth and octave (or seventh), alternating.</summary>
    private static NoteEvent[] TremoloEighthsAll(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] lower = [c.Intervals[0], c.Intervals[1]];
            int[] upper = c.Intervals.Length == 4 ? [c.Intervals[2], c.Intervals[3]] : [c.Intervals[2], 12];
            var count = (int)(c.Duration / Rational.Eighth).ToDouble();
            for (var k = 0; k < count; k++)
                foreach (var i in k % 2 == 0 ? lower : upper)
                    notes.Add(new NoteEvent(ChordRegister + c.Root + i, c.Offset + (Rational.Eighth * k), Rational.Eighth));
        }
        return [.. notes];
    }

    /// <summary>One chord from <paramref name="from"/> in tremolo eighths: root and third against fifth and octave, alternating for <paramref name="length"/>.</summary>
    private static NoteEvent[] TremoloEighths(string chord, Rational from, Rational length)
    {
        var c = ParseChords(chord)[0];
        int[] lower = [c.Intervals[0], c.Intervals[1]];
        int[] upper = c.Intervals.Length == 4 ? [c.Intervals[2], c.Intervals[3]] : [c.Intervals[2], 12];
        var count = (int)(length / Rational.Eighth).ToDouble();
        var notes = new List<NoteEvent>();
        for (var k = 0; k < count; k++)
            foreach (var i in k % 2 == 0 ? lower : upper)
                notes.Add(new NoteEvent(ChordRegister + c.Root + i, from + (Rational.Eighth * k), Rational.Eighth));
        return [.. notes];
    }

    /// <summary>One chord from <paramref name="from"/> struck in quarters for <paramref name="length"/>.</summary>
    private static NoteEvent[] Quarters(string chord, Rational from, Rational length)
    {
        var c = ParseChords(chord)[0];
        var count = (int)(length / Rational.Quarter).ToDouble();
        var notes = new List<NoteEvent>();
        for (var k = 0; k < count; k++)
            foreach (var i in c.Intervals)
                notes.Add(new NoteEvent(ChordRegister + c.Root + i, from + (Rational.Quarter * k), Rational.Quarter));
        return [.. notes];
    }

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan) { OpeningRoot = openingRoot };

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan) { OpeningRoot = openingRoot };

    // ---------- the tunes ----------

    /// <summary>The sixth table's twelve-bar tune to G: four bars in C, eight in G with the F sharp in bars 7 and 11.</summary>
    private const string TuneToG =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | F#5/4 A5/4 D5/2 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    /// <summary>Eleven bars of the Picardy tune — A minor, its relative major, A minor again — without its last bar.</summary>
    private const string PicardyTuneOpen =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 F5/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | ";

    private const string PicardyChordsOpen = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7";

    private static RealModulationPassages.PlannedModulation[] PicardyPlan => [new(5, 0, true), new(9, 9, false)];


    // ---------- the table ----------

    internal static IReadOnlyList<RealModulationPassages.Passage> Table { get; } =
    [
        // ---------- the OPENING: a seventh chord opens the key of its root — and the chords a piece may open on ----------
        Blocks("a piece in sevenths opening on Imaj7: Cmaj7 Fmaj7 G7 Cmaj7 | Am7 Dm7 G7 Cmaj7, told C (block chords)", "0:maj7 5:maj7 7:7 0:maj7 | 9:m7 2:m7 7:7 0:maj7", Nowhere),
        Blocks("a piece opening on ii7: Dm7 G7 Cmaj7 Cmaj7 twice, told C (block chords)", "2:m7 7:7 0:maj7 0:maj7 | 2:m7 7:7 0:maj7 0:maj7", Nowhere),
        Blocks("a piece opening on ii7 with plain triads after it: Dm7 G7 C C twice, told C (block chords)", "2:m7 7:7 0 0 | 2:m7 7:7 0 0", Nowhere),
        Blocks("a piece opening on vi7: Am7 Dm7 G7 Cmaj7 twice, told C (block chords)", "9:m7 2:m7 7:7 0:maj7 | 9:m7 2:m7 7:7 0:maj7", Nowhere),
        Blocks("a piece opening on ii7 in G: Am7 D7 G G twice, told G (block chords)", "9:m7 2:7 7 7 | 9:m7 2:7 7 7", Nowhere, openingRoot: 7),
        Blocks("a lone V7 pickup bar, then the piece: G7 C Am F | G7 C F C, told C (block chords)", "7:7 0 9:m 5 | 7:7 0 5 0", Nowhere),
        Blocks("a major twelve-bar blues in sevenths told its relative minor: C7 | F7 C7 | G7 F7 C7 G7, told A minor (block chords)", "0:7 0:7 0:7 0:7 | 5:7 5:7 0:7 0:7 | 7:7 5:7 0:7 7:7", Nowhere, major: false, openingRoot: 9),
        Blocks("a piece opening on IV: F G C C | F G C C | Dm G C C, told C (block chords)", "5 7 0 0 | 5 7 0 0 | 2:m 7 0 0", Nowhere),
        Blocks("a piece opening on ii as a triad: Dm G C C twice, told C (block chords)", "2:m 7 0 0 | 2:m 7 0 0", Nowhere),
        Blocks("a piece in D throughout told a key it never sounds a chord of: D G A D | D G A7 D, told C (block chords)", "2 7 9 2 | 2 7 9:7 2", Nowhere),
        Blocks("a minor piece in sevenths opening on i7 and closing on its dominant seventh: Am7 Dm7 E7 Am7 | Am7 Dm7 E7 Am7, told A minor (block chords)", "9:m7 2:m7 4:7 9:m7 | 9:m7 2:m7 4:7 9:m7", Nowhere, major: false, openingRoot: 9),

        // ---------- the SEQUENCE: the same loop moved, the reading of its model kept ----------
        Blocks("Am F C G twice, then the loop a fourth up, Dm Bb F C twice, told A minor: D minor (block chords)", "9:m 5 0 7 | 9:m 5 0 7 | 2:m 10 5 0 | 2:m 10 5 0", At(9, 2, false), major: false, openingRoot: 9),
        Blocks("Am F C G twice, then the loop a minor third up, Cm Ab Eb Bb twice, told A minor: C minor (block chords)", "9:m 5 0 7 | 9:m 5 0 7 | 0:m 8 3 10 | 0:m 8 3 10", At(9, 0, false), major: false, openingRoot: 9),
        Blocks("the loop a tone up and a tone up again: Am F C G x2 | Bm G D A x2 | C#m A E B x2, told A minor: B minor then C sharp minor (block chords)", "9:m 5 0 7 | 9:m 5 0 7 | 11:m 7 2 9 | 11:m 7 2 9 | 1:m 9 4 11 | 1:m 9 4 11", [new(9, 11, false), new(17, 1, false)], major: false, openingRoot: 9),
        Blocks("a real sequence of one cadencing phrase through three keys: C F G7 C | D G A7 D | E A B7 E, told C (block chords)", "0 5 7:7 0 | 2 7 9:7 2 | 4 9 11:7 4", [new(5, 2, true), new(9, 4, true)]),
        Blocks("a circle of fifths in A minor, twice: Am Dm G C | F Bdim E7 Am, told A minor — no modulation (block chords)", "9:m 2:m 7 0 | 5 11:dim 4:7 9:m | 9:m 2:m 7 0 | 5 11:dim 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Blocks("a loop whose transposition carries the leading tone: Am F C E7 twice, then Bm G D F#7 twice, told A minor (block chords)", "9:m 5 0 4:7 | 9:m 5 0 4:7 | 11:m 7 2 6:7 | 11:m 7 2 6:7", At(9, 11, false), major: false, openingRoot: 9),
        Blocks("a loop whose transposition carries the leading tone: Am F C E7 twice, then Bm G D F#7 twice, told C (block chords)", "9:m 5 0 4:7 | 9:m 5 0 4:7 | 11:m 7 2 6:7 | 11:m 7 2 6:7", At(9, 11, false)),
        Blocks("the major loop a tone up: C G Am F twice, then D A Bm G twice, told C (block chords)", "0 7 9:m 5 | 0 7 9:m 5 | 2 9 11:m 7 | 2 9 11:m 7", At(9, 2, true)),
        Blocks("the major loop a tone up: C G Am F twice, then D A Bm G twice, told A minor (block chords)", "0 7 9:m 5 | 0 7 9:m 5 | 2 9 11:m 7 | 2 9 11:m 7", At(9, 2, true), major: false, openingRoot: 9),
        // the copy loop cadencing into its relative major: one key for the copy and its repeat (found after the first run; the author's build read Bm@9 then D@13)
        Blocks("a loop cadencing V I into the relative major, a tone up: Am Dm G C twice, then Bm Em A D twice, told A minor: B minor, one key for the copy and its repeat (block chords)", "9:m 2:m 7 0 | 9:m 2:m 7 0 | 11:m 4:m 9 2 | 11:m 4:m 9 2", At(9, 11, false), major: false, openingRoot: 9),
        Blocks("the Axis loop a tone up: Am F G C twice, then Bm G A D twice, told A minor: B minor, one key for the copy and its repeat (block chords)", "9:m 5 7 0 | 9:m 5 7 0 | 11:m 7 9 2 | 11:m 7 9 2", At(9, 11, false), major: false, openingRoot: 9),
        Blocks("a loop cadencing V I into the relative major four times: Am Dm G C, told A minor - one key (block chords)", "9:m 2:m 7 0 | 9:m 2:m 7 0 | 9:m 2:m 7 0 | 9:m 2:m 7 0", Nowhere, major: false, openingRoot: 9),

        // ---------- SILENCE: the phrase counted from the re-entry ----------
        Notes("three bars of silence before the new key: C F G C | R | R | R | G C D7 G (block chords as notes)", Block("0 5 7 0 | R | R | R | 7 0 2:7 7"), At(8, 7, true)),
        Notes("two silent bars, the music re-entering in the OLD key: C F G C | R | R | C F G7 C | G C D7 G (block chords as notes)", Block("0 5 7 0 | R | R | 0 5 7:7 0 | 7 0 2:7 7"), At(11, 7, true)),
        Notes("two silent bars between phrases, both in the old key, then the new: C F G C | R | R | C F G C | R | R | G C D7 G (block chords as notes)", Block("0 5 7 0 | R | R | 0 5 7 0 | R | R | 7 0 2:7 7"), At(13, 7, true)),

        // ---------- TREMOLO and oscillation: the chord it spells whatever its speed ----------
        Notes("every chord in tremolo eighths for the whole passage: C then G (tremolo alone)", TremoloEighthsAll("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7"), At(5, 7, true)),
        Notes("every chord in tremolo eighths under the melody: C then G (four voices)", Together(TremoloEighthsAll("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7"), Notated(TuneToG)), At(5, 7, true)),
        Notes("a major piece to the dominant and home, the closing chord in tremolo eighths: C F G C | G C D7 G | G C G7 C(tremolo) (block chords as notes)", Together(Block("0 5 7 0 | 7 0 2:7 7 | 7 0 7:7"), TremoloEighths("0", new Rational(11, 1), Rational.Whole)), [new(5, 7, true), new(11, 0, true)]),
        Notes("the Picardy close, a trill on the third of the final chord: C sharp D C sharp D ... over A major (four voices)",
            Together(Block(PicardyChordsOpen + " 9"), Notated(PicardyTuneOpen + "C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the dominant sevenths in tremolo eighths, the rest struck: C F G C | G C D7(tremolo) G | G C D7(tremolo) G (block chords as notes)",
            Together(Block("0 5 7 0 | 7 0 R 7 | 7 0 R 7"), TremoloEighths("2:7", new Rational(6, 1), Rational.Whole), TremoloEighths("2:7", new Rational(10, 1), Rational.Whole)), At(5, 7, true)),
        Notes("the Picardy close in tremolo QUARTERS under the melody arpeggiating up (four voices)",
            Together(Block(PicardyChordsOpen), Quarters("9", new Rational(11, 1), Rational.Whole), Notated(PicardyTuneOpen + "C#5/4 E5/4 A5/2")), PicardyPlan, major: false, openingRoot: 9),

        // ---------- the RELATIVE-MAJOR boundary ----------
        Blocks("A minor's cadence, ii V I I in the relative major twice, A minor's cadence: Am Dm E7 Am | Dm G C C | Dm G C C | Am Dm E7 Am, told A minor (block chords)", "9:m 2:m 4:7 9:m | 2:m 7 0 0 | 2:m 7 0 0 | 9:m 2:m 4:7 9:m", [new(5, 0, true), new(13, 9, false)], major: false, openingRoot: 9),
        Blocks("A minor's cadence, the major's V I inside the phrase but the phrase closing on the minor's tonic, twice: G C Dm Am, told A minor — one key (block chords)", "9:m 2:m 4:7 9:m | 7 0 2:m 9:m | 7 0 2:m 9:m | 9:m 2:m 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Blocks("A minor's cadence, the major's cadence at the phrase end twice: Dm Am G C, told A minor — C, then home (block chords)", "9:m 2:m 4:7 9:m | 2:m 9:m 7 0 | 2:m 9:m 7 0 | 9:m 2:m 4:7 9:m", [new(5, 0, true), new(13, 9, false)], major: false, openingRoot: 9),
    ];
}
