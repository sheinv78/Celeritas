// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The eighth table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// seventh reviewer's passages, written after the pop loop, the silent bar inside a phrase and
/// the decorated close were repaired, and folded in afterwards. Three lenses: the pop LOOP —
/// which loop is one key (I V vi IV, i VII VI VII, the Andalusian descent, i III VII VI, told
/// either of its keys; the loop with the raised leading tone), which loop moves (the same loop a
/// tone up), and where a loop turns into a real move to the relative key (the phrase framed by
/// its chord; the phrase that opens on the minor's tonic and closes on the major's is the
/// minor's); the FRAME reaching back (a phrase of V I V I closing on the old tonic is the old
/// key's; a phrase with the old key's subdominant is too); SILENCE inside and around the new
/// key's first phrase (its last bar, two bars, its second bar, the bar closing the old key's
/// phrase, the chords silent under a melody that carries on); and the CLOSE — what the melody may
/// do over the Picardy chord and still end the piece on it (arpeggiate down, end on the third
/// and the fifth, arpeggiate through a two-bar fermata, close after the dominant minor).
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c> — plus <c>R</c> for a bar of
/// silence. The plans are a musician's, in whole notes; a 4/4 bar = 1, bar k begins at position
/// k-1; a move to the relative key is planned where a chord only the new key owns first sounds,
/// the pivot chord a bar before it (the fixture's convention). Of the reviewer's thirty-five,
/// twenty-eight are here: four were the seventh table's own rows under another name, and two
/// told the detector C over a loop the trajectory rightly opens in A minor — Am F C G twice then
/// C F G C twice, and Am F C G twice then Bm G D A twice — where no plan can speak for both
/// roads (the detector's readings of those are facts in
/// <see cref="TheTwoRoadsHearEveryTextureAlikeTests"/>). Four were the eighth iteration's work
/// and are here now: the minor twelve-bar blues in sevenths, which the trajectory opened in C
/// major from its Am7 — A C E G read as C6 — and took to A minor at the E7 (a seventh chord
/// opens the key of its root); A minor's cadence, C Dm G Am twice and A minor's cadence, which
/// both roads took to C at bar 4 and home at bar 13 for a pair of phrases opening on the
/// relative major's chord and closing on the minor's tonic (a loop that comes to rest on the
/// old tonic is the old key's); two bars of silence before the new key, which both roads heard
/// as a two-bar tonicization of G, the phrase count running through the silence (a musician
/// counts the new phrase from the re-entry); and the Picardy chord in tremolo eighths under
/// the melody, whose alternating dyads no rule read as one chord, so that both roads ended the
/// piece in A major (a tremolo is the chord it spells whatever its speed). One is kept out with
/// its debate: A minor's cadence, then F G C Am | F G C C — the reviewer plans the relative
/// major from its IV at bar 5, IV V I vi | IV V I I; the library hears C from the cadencing
/// phrase, at bar 9 with the Am of bar 8 its pivot, because the phrase F G C Am closes on the
/// minor's tonic and is the minor's by the rule that keeps C Dm G Am twice in A minor. Bar 9
/// has defenders and bar 5 has defenders; the lead decides.
/// </remarks>
internal static class LoopsSilencesAndClosesPassages
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

    /// <summary>One chord from <paramref name="from"/> in tremolo eighths: root and third against fifth and octave (or seventh), alternating for <paramref name="length"/>.</summary>
    private static NoteEvent[] TremoloEighths(string chord, Rational from, Rational length)
    {
        var c = ParseChords(chord)[0];
        int[] lower = [c.Intervals[0], c.Intervals[1]];
        int[] upper = c.Intervals.Length == 4 ? [c.Intervals[2], c.Intervals[3]] : [c.Intervals[2], 12];
        var count = (int)(length / Rational.Eighth).ToDouble();
        var notes = new List<NoteEvent>();
        for (var k = 0; k < count; k++)
        {
            foreach (var i in k % 2 == 0 ? lower : upper)
            {
                notes.Add(new NoteEvent(ChordRegister + c.Root + i, from + (Rational.Eighth * k), Rational.Eighth));
            }
        }

        return [.. notes];
    }

    /// <summary>One chord held from <paramref name="from"/> for <paramref name="length"/> — a fermata.</summary>
    private static NoteEvent[] Fermata(string chord, Rational from, Rational length) =>
        [.. ParseChords(chord).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, from, length)))];

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan) { OpeningRoot = openingRoot };

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0, bool trajectoryMayHearNoChange = false) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan, trajectoryMayHearNoChange) { OpeningRoot = openingRoot };

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

    private const string PicardyChords = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7 9";

    private const string PicardyChordsOpen = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7";

    /// <summary>A minor, its dominant minor for eight bars, home to A minor — the tune arpeggiating up through the final A major chord.</summary>
    private const string DominantMinorPicardyTune =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "B4/4 G4/4 E5/2 | C5/4 A4/4 E5/2 | D#5/4 F#5/4 B4/2 | G5/4 F#5/4 E5/2 | "
        + "E5/4 G5/4 B4/2 | A4/4 C5/4 E5/2 | F#5/4 D#5/4 B4/2 | G5/4 F#5/4 E5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C#5/4 E5/4 A5/2";

    private const string DominantMinorPicardyChords = "9:m 2:m 4:7 9:m | 4:m 9:m 11:7 4:m | 4:m 9:m 11:7 4:m | 9:m 2:m 4:7 9";

    private static RealModulationPassages.PlannedModulation[] PicardyPlan => [new(5, 0, true), new(9, 9, false)];

    // ---------- the table ----------

    internal static IReadOnlyList<RealModulationPassages.Passage> Table { get; } =
    [
        // ---------- the LOOP: loops that are one key ----------
        Blocks("I V vi IV four times, told I: C G Am F (block chords)", "0 7 9:m 5 | 0 7 9:m 5 | 0 7 9:m 5 | 0 7 9:m 5", Nowhere),
        Blocks("I V vi IV four times, told vi: C G Am F, told A minor (block chords)", "0 7 9:m 5 | 0 7 9:m 5 | 0 7 9:m 5 | 0 7 9:m 5", Nowhere, major: false, openingRoot: 9),
        Blocks("i VII VI VII four times: Am G F G, told A minor (block chords)", "9:m 7 5 7 | 9:m 7 5 7 | 9:m 7 5 7 | 9:m 7 5 7", Nowhere, major: false, openingRoot: 9),
        Blocks("the Andalusian descent i bVII bVI V four times: Am G F E, told A minor (block chords)", "9:m 7 5 4 | 9:m 7 5 4 | 9:m 7 5 4 | 9:m 7 5 4", Nowhere, major: false, openingRoot: 9),
        Blocks("i III VII VI four times: Am C G F, told A minor (block chords)", "9:m 0 7 5 | 9:m 0 7 5 | 9:m 0 7 5 | 9:m 0 7 5", Nowhere, major: false, openingRoot: 9),
        Blocks("i III VII VI four times: Am C G F, told C major (block chords)", "9:m 0 7 5 | 9:m 0 7 5 | 9:m 0 7 5 | 9:m 0 7 5", Nowhere),
        Blocks("a minor twelve-bar blues in sevenths: Am7 | Dm7 Am7 | E7 Dm7 Am7 E7, told A minor (block chords)", "9:m7 9:m7 9:m7 9:m7 | 2:m7 2:m7 9:m7 9:m7 | 4:7 2:m7 9:m7 4:7", Nowhere, major: false, openingRoot: 9),
        Blocks("Am F C E7 four times, the raised leading tone in the loop, told A minor (block chords)", "9:m 5 0 4:7 | 9:m 5 0 4:7 | 9:m 5 0 4:7 | 9:m 5 0 4:7", Nowhere, major: false, openingRoot: 9),
        Blocks("Am F C E7 four times, the raised leading tone in the loop, told C major (block chords)", "9:m 5 0 4:7 | 9:m 5 0 4:7 | 9:m 5 0 4:7 | 9:m 5 0 4:7", Nowhere),

        // ---------- the LOOP: loops that move ----------
        Blocks("Am F C G twice, then Bm G D A twice: the second loop a key of its own, told A minor (block chords)", "9:m 5 0 7 | 9:m 5 0 7 | 11:m 7 2 9 | 11:m 7 2 9", At(9, 11, false), major: false, openingRoot: 9),
        // Told C, the two bars of C and G are the key the caller named and A minor begins at its
        // dominant, the Am before it the pivot. Told nothing, the trajectory opens where a
        // musician opening cold opens: ten of the twelve bars are A minor's, the C is its III,
        // and there is no modulation to hear. Both readings are the musician's, and they are the
        // same music heard from a given key and from none; the trajectory may hear no change here
        // (the thirteenth iteration, which taught the judge that a triad of either mode opening a
        // piece is a chord of its key before it is a key).
        Blocks("a loop of the relative major cadencing into the minor: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told C — A minor from its dominant at bar 4, the Am before it the pivot (block chords)", "0 7 9:m 4:7 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m", At(4, 9, false), trajectoryMayHearNoChange: true),
        Blocks("A minor's cadence, then the mirror loop C Am F G three times, told A minor (block chords)", "9:m 2:m 4:7 9:m | 0 9:m 5 7 | 0 9:m 5 7 | 0 9:m 5 7", At(5, 0, true), major: false, openingRoot: 9),
        Blocks("A minor's cadence, then C Dm G Am twice closing on the minor tonic, then A minor's cadence: one key, told A minor (block chords)", "9:m 2:m 4:7 9:m | 0 2:m 7 9:m | 0 2:m 7 9:m | 9:m 2:m 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Blocks("A minor's cadence, a phrase Am F G C opening on the minor's tonic and closing on the relative major's chord, then A minor's cadence: one key, told A minor (block chords)", "9:m 2:m 4:7 9:m | 9:m 5 7 0 | 9:m 2:m 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Blocks("A minor's cadence, a phrase C G Am C framed by the relative major's tonic, then A minor's cadence, told A minor (block chords)", "9:m 2:m 4:7 9:m | 0 7 9:m 0 | 9:m 2:m 4:7 9:m", [new(5, 0, true), new(9, 9, false)], major: false, openingRoot: 9),

        // ---------- the FRAME reaching back: a phrase that closes on the old tonic is the old key's ----------
        Blocks("C F G C | G C G C | G C G C | G C D7 G: two phrases of V I V I closing on C before the dominant, told C (block chords)", "0 5 7 0 | 7 0 7 0 | 7 0 7 0 | 7 0 2:7 7", At(13, 7, true)),
        Blocks("C F G C | G C F C | G C D7 G: the second phrase with C's subdominant, told C (block chords)", "0 5 7 0 | 7 0 5 0 | 7 0 2:7 7", At(9, 7, true)),

        // ---------- SILENCE ----------
        Notes("a silent bar as the last bar of the new key's first phrase: C F G C | G C G R | G C D7 G (block chords as notes)", Block("0 5 7 0 | 7 0 7 R | 7 0 2:7 7"), At(5, 7, true)),
        Notes("two silent bars inside the new key's first phrase: C F G C | G R R G | G C D7 G (block chords as notes)", Block("0 5 7 0 | 7 R R 7 | 7 0 2:7 7"), At(5, 7, true)),
        Notes("a silent bar closing the old key's phrase: C F G R | G C D7 G | G C D7 G (block chords as notes)", Block("0 5 7 R | 7 0 2:7 7 | 7 0 2:7 7"), At(5, 7, true)),
        Notes("a silent bar in the chords while the melody carries on with its F sharp: C F G C | G C R G | G C D7 G under the tune (four voices)", Together(Block("0 5 7 0 | 7 0 R 7 | 7 0 2:7 7"), Notated(TuneToG)), At(5, 7, true)),
        Notes("two bars of silence before the new key: C F G C | R | R | G C D7 G (block chords as notes)", Block("0 5 7 0 | R | R | 7 0 2:7 7"), At(7, 7, true)),
        Notes("a silent second bar in the new key's first phrase: C F G C | G R C G | G C D7 G (block chords as notes)", Block("0 5 7 0 | 7 R 0 7 | 7 0 2:7 7"), At(5, 7, true)),

        // ---------- the CLOSE: what the melody may do over the Picardy chord ----------
        Notes("the Picardy close, the melody arpeggiating DOWN through the final chord: A E C sharp (four voices)",
            Together(Block(PicardyChords), Notated(PicardyTuneOpen + "A5/4 E5/4 C#5/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, the melody ending on the third and the fifth, not the tonic (four voices)",
            Together(Block(PicardyChords), Notated(PicardyTuneOpen + "C#5/2 E5/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, the final chord in tremolo eighths under the melody arpeggiating up (four voices)",
            Together(Block(PicardyChordsOpen), TremoloEighths("9", new Rational(11, 1), Rational.Whole), Notated(PicardyTuneOpen + "C#5/4 E5/4 A5/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("the Picardy close, the final chord held under a fermata for two bars, the melody arpeggiating through both (four voices)",
            Together(Block(PicardyChordsOpen), Fermata("9", new Rational(11, 1), new Rational(2, 1)), Notated(PicardyTuneOpen + "C#5/4 E5/4 A5/4 C#6/4 | E5/4 C#5/4 A4/2")), PicardyPlan, major: false, openingRoot: 9),
        Notes("A minor, its dominant minor for eight bars, home to A minor on a Picardy third, the melody arpeggiating up through the final chord (four voices)",
            Together(Block(DominantMinorPicardyChords), Notated(DominantMinorPicardyTune)), [new(5, 4, false), new(13, 9, false)], major: false, openingRoot: 9),
    ];
}
