// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The thirteenth table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// twelfth reviewer's passages — ordinary music only, written after the judge came to hear that
/// vi, iii and ii are chords of a key and not keys, that a chord spread is that chord struck at
/// its first note, and that a pedal struck again with every chord is one note under the harmony.
/// Four lenses: the OPENING of a piece told a key it opens away from (told the major, opening on
/// vi, on iii, on ii, on IV, with and without the tonic triad in the first phrase; a hymn opening
/// on a first-inversion tonic; a pop loop opening on vi and closing on the tonic; told the minor,
/// opening on its III and on its VI; a song whose verse is the relative
/// minor and whose chorus the major — where a musician does hear two keys; and the fence that
/// matters most, a piece told the major that opens on vi and later modulates for real, to the
/// relative minor and to the dominant, which the new rule must not swallow), the CLOSE as it is
/// actually written (a rolled final chord under a fermata, a spread chord in a hymn's last bar,
/// the last chord arpeggiated by both hands over two octaves, a plain block close after a rolled
/// one earlier in the piece, a close whose tune ends on the fifth, a nocturne's pedalled left
/// hand), the PEDAL as it is actually played (a tonic struck on every beat and repeated in
/// eighths, an oom-pah whose bass is the tonic throughout, a
/// drone under a folk tune, a dominant pedal struck under a cadence — and a walking bass, which is
/// no pedal), and the EVERYDAY SHAPES the whole series leans on, in case something moved: a hymn,
/// a waltz, a twelve-bar blues, a pop loop, a march, a folk tune to the dominant and home again.
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c> — plus <c>R</c> for a rest. The
/// plans are a musician's, in whole notes; a 4/4 bar = 1, bar k begins at position k-1. All
/// thirty of the reviewer's passages are here: the thirteenth iteration — the last of this series
/// — added the last five, and they are the three shapes the twelfth left. The fence that keeps a
/// minor loop a minor loop was the whole rest of the piece, so one later vi kept the older
/// reading; it is now a loop that the piece keeps to, and the pop loop <c>Am F C G7</c> three
/// times closing <c>C F G7 C</c> is C with a vi first on both roads. The opening rule was the
/// minor triad's alone; it now reads a triad of either mode, so <c>C G Am E7 | Am Dm E7 Am</c>
/// told A minor is A minor throughout with a III first. And the struck pedal reached only a pedal
/// struck once a bar; the guitar strumming the tonic on every beat and the bass repeating it in
/// eighths are now one note under the harmony too.
/// <para>
/// What the table still does not hold, because the judge does not hear it, is written in the
/// remarks of <see cref="Celeritas.Core.Analysis.ModulationDetector"/>'s <c>Analyze</c> and of the
/// key trajectory's <c>DetectModulations</c>, and at length in the judge's own class remarks:
/// four shapes, each named there with the reading both roads give, the reading a musician
/// gives, and why no rule reaches it. There is no fourteenth iteration; those four are the
/// library's documented limits.
/// </para>
/// </remarks>
internal static class OrdinaryMusicPassages
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

    /// <summary>The pitches given, struck <paramref name="apart"/> apart from <paramref name="from"/> and all held to <paramref name="until"/> — a chord rolled.</summary>
    private static NoteEvent[] Rolled(Rational from, Rational until, Rational apart, params int[] pitches) =>
        [.. pitches.Select((p, k) => new NoteEvent(p, from + (apart * k), until - from - (apart * k)))];

    /// <summary>The pitches given, all struck at <paramref name="from"/> for <paramref name="length"/> — one chord, voiced.</summary>
    private static NoteEvent[] Voiced(Rational from, Rational length, params int[] pitches) =>
        [.. pitches.Select(p => new NoteEvent(p, from, length))];

    /// <summary>Each chord as a bass note on <paramref name="bassBeats"/> and its triad on the beats after — a waltz, an oom-pah or a march left hand, the beat a quarter.</summary>
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

    /// <summary>An oom-pah whose bass note never moves — the one pitch <paramref name="bass"/> on the bass beats, the chords above it.</summary>
    private static NoteEvent[] OomPahOverAPedal(string chords, int bass, int beatsInABar, params int[] bassBeats)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            for (var beat = 0; beat < beatsInABar; beat++)
            {
                var at = c.Offset + (Rational.Quarter * beat);
                if (bassBeats.Contains(beat)) notes.Add(new NoteEvent(bass, at, Rational.Quarter));
                else foreach (var i in c.Intervals) notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, at, Rational.Quarter));
            }
        }

        return [.. notes];
    }

    /// <summary>A bass line that WALKS — root, third, fifth, third in quarters, a new note every beat.</summary>
    private static NoteEvent[] WalkingBass(string chords, int register)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] steps = [c.Intervals[0], c.Intervals[1], c.Intervals[2], c.Intervals[1]];
            var quarters = (int)(c.Duration / Rational.Quarter).ToDouble();
            for (var k = 0; k < quarters; k++)
                notes.Add(new NoteEvent(register + c.Root + steps[k % steps.Length], c.Offset + (Rational.Quarter * k), Rational.Quarter));
        }

        return [.. notes];
    }

    /// <summary>A broken chord in eighths with the sustaining pedal down: every note of the bar held to the bar's end, so the bar's notes are let go together — a nocturne's left hand.</summary>
    private static NoteEvent[] Pedalled(string chords, int register)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] pattern = c.Intervals.Length == 4
                ? [c.Intervals[0], c.Intervals[2], c.Intervals[1], c.Intervals[3]]
                : [c.Intervals[0], c.Intervals[2], c.Intervals[1], c.Intervals[2]];
            var eighths = (int)(c.Duration / Rational.Eighth).ToDouble();
            for (var k = 0; k < eighths; k++)
            {
                var at = c.Offset + (Rational.Eighth * k);
                notes.Add(new NoteEvent(register + c.Root + pattern[k % 4], at, c.Offset + c.Duration - at));
            }
        }

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

    private static RealModulationPassages.Passage Tune(string name, string chords, string notation, RealModulationPassages.PlannedModulation[] plan, bool major = true, int openingRoot = 0) =>
        new(name, RealModulationPassages.Texture.MelodyOverChords, major, chords, Notated(notation), plan) { OpeningRoot = openingRoot };

    // ---------- the material ----------

    /// <summary>C F Dm C, then two phrases in the dominant — one chord a bar, twelve bars.</summary>
    private const string ToTheDominant = "0 5 2:m 0 | 7 2:7 7 2:7 | 7 2:7 7 7";

    /// <summary>The same, but the last phrase comes home: C F Dm C | G D7 G D7 | C F G7 C.</summary>
    private const string ToTheDominantAndHome = "0 5 2:m 0 | 7 2:7 7 2:7 | 0 5 7:7 0";

    /// <summary>Eleven bars of a minor piece — A minor, its relative major, A minor again — without its last bar.</summary>
    private const string MinorChordsOpen = "9:m 4:7 9:m 4:7 | 0 5 7 0 | 9:m 4:7 9:m";

    private const string MinorTuneOpen =
        "A4/4 C5/4 E5/2 | D5/4 B4/4 G#4/2 | C5/4 A4/4 E5/2 | D5/4 B4/4 G#4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 C5/4 F5/2 | B4/4 D5/4 G5/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 A4/4 E5/2 | D5/4 B4/4 G#4/2 | C5/4 B4/4 A4/2 | ";

    /// <summary>A minor, the relative major from bar 5, home to A minor at bar 9.</summary>
    private static RealModulationPassages.PlannedModulation[] MinorPlan => [new(5, 0, true), new(9, 9, false)];

    internal static readonly RealModulationPassages.Passage[] Table =
    [
        // ---------- the OPENING: a piece told a key it opens away from ----------
        Blocks("told C, a hymn opening on vi with the tonic triad in its first phrase: Am Em F C | C F G C | C Dm G7 C (block chords)",
            "9:m 4:m 5 0 | 0 5 7 0 | 0 2:m 7:7 0", Nowhere),
        Blocks("told C, opening on vi with no tonic triad in its first phrase: Am Em Dm G | C F G C | C Dm G7 C (block chords)",
            "9:m 4:m 2:m 7 | 0 5 7 0 | 0 2:m 7:7 0", Nowhere),
        Blocks("told C, opening on iii and resting on vi: Em Dm G Am | C F G C | C Dm G7 C (block chords)",
            "4:m 2:m 7 9:m | 0 5 7 0 | 0 2:m 7:7 0", Nowhere),
        Blocks("told C, opening on ii with the tonic triad in its first phrase: Dm Am C G | C F G C | C Dm G7 C (block chords)",
            "2:m 9:m 0 7 | 0 5 7 0 | 0 2:m 7:7 0", Nowhere),
        Blocks("told C, opening on IV: F C Dm G | C F G C | C Dm G7 C (block chords)",
            "5 0 2:m 7 | 0 5 7 0 | 0 2:m 7:7 0", Nowhere),
        Notes("told C, a hymn opening on a FIRST-INVERSION tonic, E in the bass (four voices)",
            Together(Voiced(Rational.Zero, Rational.Whole, 52, 55, 60, 64), Block("R 5 7 0 | 0 5 7 0 | 0 2:m 7:7 0")), Nowhere),
        Blocks("told A minor, opening on its VI: F G Am E7 | Am Dm E7 Am | Am Dm E7 Am (block chords)",
            "5 7 9:m 4:7 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Blocks("a SONG told A minor: a verse in A minor, a chorus in C from bar 9 (block chords)",
            "9:m 5 2:m 4:7 | 9:m 5 2:m 4:7 | 0 5 7 0 | 0 5 7:7 0", At(9, 0, true), major: false, openingRoot: 9),
        Blocks("THE FENCE: told C, opening on vi and moving to the DOMINANT at bar 9 (block chords)",
            "9:m 2:m 7:7 0 | 0 5 7 0 | 7 2:7 7 2:7 | 7 2:7 7 7", At(9, 7, true)),
        Blocks("THE FENCE: told C, opening on iii and moving to a REAL A minor at bar 9 (block chords)",
            "4:m 9:m 0 7 | 0 5 7 0 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m", At(9, 9, false)),

        // ---------- the CLOSE, as it is actually written ----------
        Notes("the minor close PLAIN, the last chord struck as a block (four voices)",
            Together(Block(MinorChordsOpen + " 9:m"), Notated(MinorTuneOpen + "C5/4 B4/4 A4/2")), MinorPlan, major: false, openingRoot: 9),
        Notes("the minor close ROLLED UNDER A FERMATA, a sixteenth at a time and held two whole bars (four voices)",
            Together(Block(MinorChordsOpen), Rolled(new Rational(11, 1), new Rational(13, 1), new Rational(1, 16), 45, 57, 60, 64),
                Notated(MinorTuneOpen + "C5/4 B4/4 A4/2")), MinorPlan, major: false, openingRoot: 9),
        Notes("the minor close ARPEGGIATED BY BOTH HANDS, seven notes over two octaves a sixteenth apart, all held (four voices)",
            Together(Block(MinorChordsOpen), Rolled(new Rational(11, 1), new Rational(12, 1), new Rational(1, 16), 33, 40, 45, 52, 57, 60, 64),
                Notated(MinorTuneOpen + "C5/4 B4/4 A4/2")), MinorPlan, major: false, openingRoot: 9),
        Notes("a PLAIN BLOCK close after a ROLLED chord earlier in the piece: the relative major's bar 8 rolled, the last bar struck (four voices)",
            Together(Block("9:m 4:7 9:m 4:7 | 0 5 7 R | 9:m 4:7 9:m 9:m"), Rolled(new Rational(7, 1), new Rational(8, 1), new Rational(1, 16), 48, 55, 60, 64),
                Notated(MinorTuneOpen + "C5/4 B4/4 A4/2")), MinorPlan, major: false, openingRoot: 9),
        Notes("the minor close rolled, the TUNE ENDING ON THE FIFTH above it (four voices)",
            Together(Block(MinorChordsOpen), Rolled(new Rational(11, 1), new Rational(12, 1), new Rational(1, 16), 45, 57, 60, 64),
                Notated(MinorTuneOpen + "A4/4 B4/4 E5/2")), MinorPlan, major: false, openingRoot: 9),
        Notes("a NOCTURNE: the tune over a pedalled left hand, every bar's broken chord let go together (four voices)",
            Together(Pedalled(ToTheDominantAndHome, ChordRegister), Notated(
                "E5/4 G5/4 C6/4 G5/4 | F5/4 A5/4 C6/4 A5/4 | F5/4 A5/4 D6/4 A5/4 | E5/4 G5/4 C6/4 E5/4 | "
                + "D5/4 G5/4 B5/4 G5/4 | F#5/4 A5/4 D6/4 A5/4 | D5/4 G5/4 B5/4 D6/4 | F#5/4 A5/4 D6/4 C6/4 | "
                + "E5/4 G5/4 C6/4 G5/4 | F5/4 A5/4 C6/4 A5/4 | F5/4 B5/4 D6/4 B5/4 | E5/4 G5/4 C6/2")),
            [new(5, 7, true), new(9, 0, true)]),

        // ---------- the PEDAL, as it is actually played ----------
        Notes("an OOM-PAH whose bass is the tonic throughout — the pedal on one and three, the chords on two and four (four voices)",
            Together(OomPahOverAPedal(ToTheDominant, 36, 4, 0, 2), Notated(
                "E5/4 G5/4 C6/4 G5/4 | F5/4 A5/4 C6/4 A5/4 | F5/4 A5/4 D6/4 A5/4 | E5/4 G5/4 C6/4 E5/4 | "
                + "D5/4 G5/4 B5/4 G5/4 | F#5/4 A5/4 D6/4 A5/4 | D5/4 G5/4 B5/4 D6/4 | F#5/4 A5/4 D6/4 C6/4 | "
                + "D5/4 G5/4 B5/4 G5/4 | F#5/4 A5/4 D6/4 A5/4 | D5/4 G5/4 B5/4 D6/4 | G5/4 B5/4 D6/2")),
            At(5, 7, true)),
        Notes("THE FENCE: a WALKING BASS in quarters, a new note every beat, under the same twelve bars (four voices)",
            Together(Block(ToTheDominant), WalkingBass(ToTheDominant, 36)), At(5, 7, true)),
        Notes("a DRONE, the tonic and its fifth held under a hymn that goes to the dominant and home (four voices)",
            Together(Block(ToTheDominantAndHome), Pedal(36, Rational.Zero, new Rational(12, 1)), Pedal(43, Rational.Zero, new Rational(12, 1))),
            [new(5, 7, true), new(9, 0, true)]),
        Notes("a DOMINANT pedal struck on every beat under the cadence that closes the piece (four voices)",
            Together(Block(ToTheDominantAndHome), Restruck(43, new Rational(8, 1), Rational.Quarter, 16)),
            [new(5, 7, true), new(9, 0, true)]),

        // ---------- the everyday shapes ----------
        Blocks("a TWELVE-BAR BLUES in sevenths with the quick change, told C (block chords)",
            "0:7 5:7 0:7 0:7 | 5:7 5:7 0:7 0:7 | 7:7 5:7 0:7 7:7", Nowhere),
        Blocks("a HYMN to the dominant and home again, told C (block chords)",
            ToTheDominantAndHome, [new(5, 7, true), new(9, 0, true)]),
        Notes("a WALTZ in 3/4 to the dominant, the bass on one and the chord on two and three (four voices)",
            Together(OomPah("0t 5t 2:mt 0t | 7t 2:7t 7t 2:7t | 7t 2:7t 7t 7t", 3, 0), Notated(
                "E5/4 G5/4 C6/4 | F5/4 A5/4 C6/4 | F5/4 A5/4 D6/4 | E5/4 G5/4 C6/4 | "
                + "D5/4 G5/4 B5/4 | F#5/4 A5/4 D6/4 | D5/4 G5/4 B5/4 | F#5/4 A5/4 C6/4 | "
                + "D5/4 G5/4 B5/4 | F#5/4 A5/4 C6/4 | D5/4 G5/4 B5/4 | G5/4 B5/4 D6/4")),
            At(4, 7, true)),
        Notes("a MARCH: the bass on one and three, the chords off the beat, a dotted tune above (four voices)",
            Together(OomPah(ToTheDominant, 4, 0, 2), Notated(
                "E5/8 E5/8 G5/4 C6/4 G5/4 | F5/8 F5/8 A5/4 C6/4 A5/4 | F5/8 F5/8 A5/4 D6/4 A5/4 | E5/8 E5/8 G5/4 C6/4 E5/4 | "
                + "D5/8 D5/8 G5/4 B5/4 G5/4 | F#5/8 F#5/8 A5/4 D6/4 A5/4 | D5/8 D5/8 G5/4 B5/4 D6/4 | F#5/8 F#5/8 A5/4 D6/4 C6/4 | "
                + "D5/8 D5/8 G5/4 B5/4 G5/4 | F#5/8 F#5/8 A5/4 D6/4 A5/4 | D5/8 D5/8 G5/4 B5/4 D6/4 | G5/8 G5/8 B5/4 D6/4 G5/4")),
            At(5, 7, true)),
        Tune("a FOLK TUNE over its chords, to the dominant and home again (melody over chords)",
            ToTheDominantAndHome,
            "C5/4 E5/4 G5/4 E5/4 | F5/4 A5/4 C6/4 A5/4 | F5/4 A5/4 D5/4 F5/4 | E5/4 G5/4 C5/2 | "
            + "D5/4 G5/4 B4/4 D5/4 | F#5/4 A5/4 D5/4 C5/4 | B4/4 D5/4 G5/4 B5/4 | A5/4 F#5/4 D5/2 | "
            + "E5/4 G5/4 C6/4 G5/4 | F5/4 A5/4 C6/4 F5/4 | D5/4 F5/4 B4/4 D5/4 | C5/4 E5/4 G5/2",
            [new(5, 7, true), new(9, 0, true)]),

        // ---------- the thirteenth iteration's five: the LOOP, the OPENING TRIAD, the PEDAL ----------
        Blocks("told C, a POP LOOP opening on vi four times over, closing on the tonic: Am F C G7 x3 | C F G7 C (block chords)",
            "9:m 5 0 7:7 | 9:m 5 0 7:7 | 9:m 5 0 7:7 | 0 5 7:7 0", Nowhere),
        Blocks("THE FENCE: told C, opening on vi and moving to a REAL A minor at bar 9 (block chords)",
            "9:m 2:m 7:7 0 | 0 5 7 0 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m", At(9, 9, false)),
        Blocks("told A minor, opening on its III: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am (block chords)",
            "0 7 9:m 4:7 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m", Nowhere, major: false, openingRoot: 9),
        Notes("a tonic pedal STRUCK ON EVERY BEAT, four to the bar, under C F Dm C | G D7 G D7 | G D7 G G (four voices)",
            Together(Block(ToTheDominant), Restruck(36, Rational.Zero, Rational.Quarter, 48)), At(5, 7, true)),
        Notes("a tonic bass note REPEATED IN EIGHTHS under the same twelve bars (four voices)",
            Together(Block(ToTheDominant), Restruck(36, Rational.Zero, Rational.Eighth, 96)), At(5, 7, true)),
    ];
}
