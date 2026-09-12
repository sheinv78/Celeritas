using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The sixth table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the
/// fifth reviewer's passages, written after the return home and the detector's chord lengths were
/// repaired, and folded in afterwards. Textures the fixture's builders cannot write — staccato
/// chords under a legato melody, pushed and delayed chord changes, a nocturne's bass-then-chord, a
/// bass held under released upper voices, a pedal under a melody that enters later, a pickup into
/// every phrase, fermatas — are built here as note lists and handed to the four-voice road, whose
/// <c>Build</c> transposes every note; and the homecoming rule is pushed both ways: returns of two,
/// three, four and five bars, returns to a key established second, returns that skip a key, a
/// half cadence and a tonicization of V that must stay what they are.
/// </summary>
/// <remarks>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c>, <c>~n</c> a single note held
/// alone — plus <c>R</c> for a bar of silence (<c>Rh</c>, <c>Rq</c> a half and a quarter). The plans
/// are a musician's, in whole notes; a 4/4 bar = 1, bar k begins at position k-1.
/// </remarks>
internal static class TexturesAndHomecomingsPassages
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

    // ---------- builders ----------

    /// <summary>The fixture's chord tokens, with R for silence.</summary>
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
            if (token.StartsWith('~')) { parsed.Add(new Chord(int.Parse(token[1..]), [0], time, duration)); time += duration; continue; }

            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            var quality = colon >= 0 ? token[(colon + 1)..] : "";
            parsed.Add(new Chord(root, Qualities[quality], time, duration));
            time += duration;
        }

        return parsed;
    }

    /// <summary>Block chords in close root position from C3, each for its written length — the fixture's BlockChords texture as notes.</summary>
    private static NoteEvent[] Block(string chords) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration)))];

    /// <summary>Each chord struck for <paramref name="length"/> at most, then silence to its written length: a staccato accompaniment.</summary>
    private static NoteEvent[] Staccato(string chords, Rational length) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration < length ? c.Duration : length)))];

    /// <summary>Every chord anticipated by <paramref name="by"/> — the pushed chords of a pop accompaniment; the first is cut, not moved.</summary>
    private static NoteEvent[] Pushed(string chords, Rational by) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => c.Offset == Rational.Zero
            ? new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration - by)
            : new NoteEvent(ChordRegister + c.Root + i, c.Offset - by, c.Duration)))];

    /// <summary>Every chord change <paramref name="by"/> late: the chord of each bar is struck on the second beat and holds into the next bar.</summary>
    private static NoteEvent[] Delayed(string chords, Rational by) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset + by, c.Duration)))];

    /// <summary>
    /// A nocturne's left hand: the bass note alone on the first beat (a quarter, or the bar when
    /// <paramref name="bassHeld"/>), then the chord above it as eighths on beats two, three and four.
    /// </summary>
    private static NoteEvent[] Nocturne(string chords, bool bassHeld)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            notes.Add(new NoteEvent(36 + c.Root, c.Offset, bassHeld ? c.Duration : Rational.Quarter));
            for (var beat = 1; beat < 4; beat++)
            {
                foreach (var i in c.Intervals)
                    notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, c.Offset + (Rational.Quarter * beat), Rational.Eighth));
            }
        }

        return [.. notes];
    }

    /// <summary>The bass root held for the bar under the upper voices struck for a half and released.</summary>
    private static NoteEvent[] Layered(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            notes.Add(new NoteEvent(36 + c.Root, c.Offset, c.Duration));
            foreach (var i in c.Intervals)
                notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, c.Offset, c.Duration / 2));
        }

        return [.. notes];
    }

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Voices(params string[] voices) => [.. voices.SelectMany(v => MusicNotation.Parse(v))];

    private static NoteEvent[] Shift(NoteEvent[] notes, Rational by) =>
        [.. notes.Select(n => new NoteEvent(n.Pitch, n.Offset + by, n.Duration, n.Velocity))];

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static NoteEvent[] Pedal(int pitch, Rational from, Rational length) => [new NoteEvent(pitch, from, length)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan);

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan);

    // ---------- the tunes ----------

    /// <summary>Twelve bars: four in C, eight in G with the F sharp in bars 7 and 11 — over C F G C | G C D7 G | G C D7 G.</summary>
    private const string TuneToG =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | F#5/4 A5/4 D5/2 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    private const string ChordsToG = "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    /// <summary>The same tune with two bars more: home to C through G7, the melody's F natural on the G7.</summary>
    private const string TuneToGAndHome = TuneToG + " | F5/4 D5/4 B4/2 | C5/1";

    private const string ChordsToGAndHome = ChordsToG + " | 7:7 0";

    /// <summary>
    /// A legato line whose every note but the first two is a whole note beginning at the half bar,
    /// so that every note is held across a chord change — a common tone of the two chords each
    /// time: C F G C | F Bb C7 F | F Bb C7 F.
    /// </summary>
    private const string HeldAcrossMelody =
        "E4/4 G4/4 C5/1 D5/1 G4/1 C5/1 F5/1 Bb4/1 C5/1 A4/1 F5/1 D5/1 C5/1 A4/2";

    private const string ChordsToF = "0 5 7 0 | 5 10 0:7 5 | 5 10 0:7 5";

    /// <summary>The tune to G with its fifth bar silent: the chords stop with it.</summary>
    private const string TuneWithASilentBar =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | R/1 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | F#5/4 A5/4 D5/2 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    private const string ChordsWithASilentBar = "0 5 7 0 | R | 7 0 2:7 7 | 7 0 2:7 7";

    // A chorale with fermatas: phrase 1 in C, its last chord held two whole notes; phrase 2 in G
    // likewise; phrase 3 home in C, two bars and a final chord held one whole note. Chords by
    // beat: C F C G | C Am Dm G | C F G C | C(2) || G C G D | G Em Am D | G C D G | G(2) || C G C F | C F G C | C(1).
    private const string FermataS = "E5/4 F5/4 E5/4 D5/4 | C5/4 C5/4 D5/4 D5/4 | E5/4 F5/4 D5/4 E5/4 | C5/1~ C5/1 | B4/4 C5/4 B4/4 A4/4 | G4/4 G4/4 A4/4 A4/4 | B4/4 C5/4 A4/4 B4/4 | G4/1~ G4/1";
    private const string FermataA = "G4/4 A4/4 G4/4 G4/4 | E4/4 E4/4 F4/4 F4/4 | G4/4 A4/4 G4/4 G4/4 | E4/1~ E4/1 | D4/4 E4/4 D4/4 F#4/4 | B3/4 B3/4 C4/4 C4/4 | D4/4 E4/4 D4/4 D4/4 | B3/1~ B3/1";
    private const string FermataT = "C4/4 C4/4 C4/4 B3/4 | G3/4 A3/4 A3/4 B3/4 | C4/4 C4/4 B3/4 C4/4 | G3/1~ G3/1 | G3/4 G3/4 G3/4 A3/4 | D3/4 E3/4 E3/4 F#3/4 | G3/4 G3/4 F#3/4 G3/4 | D3/1~ D3/1";
    private const string FermataB = "C3/4 F3/4 C3/4 G3/4 | C3/4 A2/4 D3/4 G2/4 | C3/4 F3/4 G3/4 C3/4 | C3/1~ C3/1 | G2/4 C3/4 G2/4 D3/4 | G2/4 E2/4 A2/4 D2/4 | G2/4 C3/4 D3/4 G2/4 | G2/1~ G2/1";
    private const string HomeS = " | E5/4 D5/4 E5/4 F5/4 | E5/4 F5/4 D5/4 E5/4 | C5/1";
    private const string HomeA = " | G4/4 G4/4 G4/4 A4/4 | G4/4 A4/4 G4/4 G4/4 | E4/1";
    private const string HomeT = " | C4/4 B3/4 C4/4 C4/4 | C4/4 D4/4 B3/4 C4/4 | G3/1";
    private const string HomeB = " | C3/4 G2/4 C3/4 F3/4 | C3/4 F3/4 G2/4 C3/4 | C3/1";

    /// <summary>Two bars of tune over the opening pedal alone, before the chords enter.</summary>
    private const string EntryOverPedal = "E4/4 F4/4 G4/2 | E4/4 D4/4 C4/2";

    /// <summary>Four phrases with a quarter-note pickup into each — the third and fourth in G. The chords begin a quarter after the melody.</summary>
    private const string PickupTune =
        "G3/4 | C4/4 E4/4 G4/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/4 G4/4 | "
        + "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/4 D4/4 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | F#5/4 A5/4 D5/2 | B4/4 D5/4 G4/4 D5/4 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    private const string PickupChords = "0 5 7 0 | 0 5 7:7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    /// <summary>Eight bars in C with the fourth and the seventh degree in the first bar, and the same tune in G.</summary>
    private const string EightBarTune =
        "E4/4 F4/4 D4/4 B3/4 | C4/4 E4/4 G4/4 E4/4 | F4/4 A4/4 G4/4 F4/4 | E4/4 D4/4 C4/2 | "
        + "G4/4 F4/4 E4/4 D4/4 | E4/4 C4/4 B3/4 G3/4 | A3/4 B3/4 C4/4 D4/4 | C4/1";
    private const string EightBarTuneInG =
        "B4/4 C5/4 A4/4 F#4/4 | G4/4 B4/4 D5/4 B4/4 | C5/4 E5/4 D5/4 C5/4 | B4/4 A4/4 G4/2 | "
        + "D5/4 C5/4 B4/4 A4/4 | B4/4 G4/4 F#4/4 D4/4 | E4/4 F#4/4 G4/4 A4/4 | G4/1";

    // A canon at the fifth, diatonic: the comes answers each degree a fifth higher within C
    // major (C G, D A, E B, F C, G D, A E, B F), one bar later. Nine bars of dux, the comes to
    // the tenth.
    private const string CanonDux =
        "C4/4 D4/4 E4/4 F4/4 | G4/2 E4/2 | F4/4 E4/4 D4/4 C4/4 | D4/2 G4/2 | E4/4 F4/4 G4/4 A4/4 | G4/2 C5/2 | D5/4 C5/4 B4/4 A4/4 | G4/1 | C4/1";
    private const string CanonComes =
        "R/1 | G4/4 A4/4 B4/4 C5/4 | D5/2 B4/2 | C5/4 B4/4 A4/4 G4/4 | A4/2 D5/2 | B4/4 C5/4 D5/4 E5/4 | D5/2 G5/2 | A5/4 G5/4 F5/4 E5/4 | D5/1 | G5/1";

    // ---------- the table ----------

    internal static IReadOnlyList<RealModulationPassages.Passage> Table { get; } =
    [
        // ---------- staccato chords under a legato melody ----------
        Notes("staccato quarter chords, then silence, under a legato melody: to the dominant (four voices)",
            Together(Staccato(ChordsToG, Rational.Quarter), Notated(TuneToG)), At(5, 7, true)),
        Notes("staccato eighth chords, then silence, under a legato melody: to the dominant (four voices)",
            Together(Staccato(ChordsToG, Rational.Eighth), Notated(TuneToG)), At(5, 7, true)),
        Notes("staccato quarter chords under a legato melody: to the dominant and home through G7 at the close (four voices)",
            Together(Staccato(ChordsToGAndHome, Rational.Quarter), Notated(TuneToGAndHome)), [new(5, 7, true), new(13, 0, true)]),
        Notes("a staccato chord every other bar only, the melody carrying the harmony between: to the dominant (four voices)",
            Together(Staccato("0 R 7 R | 7 R 2:7 R | 7 R 2:7 R", Rational.Quarter), Notated(TuneToG)), At(5, 7, true)),

        // ---------- a melody note held across every chord change ----------
        Notes("every melody note a whole note from the half bar, held across each chord change: to the subdominant (four voices)",
            Together(Block(ChordsToF), Notated(HeldAcrossMelody)), At(5, 5, true)),

        // ---------- a bar of silence before the new key ----------
        Notes("a bar of silence before the new key (block chords as notes)", Block(ChordsWithASilentBar), At(6, 7, true)),
        Notes("a bar of silence before the new key, melody and chords both (four voices)",
            Together(Block(ChordsWithASilentBar), Notated(TuneWithASilentBar)), At(6, 7, true)),

        // ---------- chord changes off the beat ----------
        Notes("every chord pushed an eighth ahead of the bar under a melody on the beat: to the dominant (four voices)",
            Together(Pushed(ChordsToG, Rational.Eighth), Notated(TuneToG)), At(5, 7, true)),
        Notes("every chord struck on the second beat and held into the next bar, the melody on the beat: to the dominant (four voices)",
            Together(Delayed(ChordsToG, Rational.Quarter), Notated(TuneToG)), At(5, 7, true)),

        // ---------- a chorale with fermatas ----------
        Notes("a chorale, C then G, the last chord of each phrase held under a fermata for two whole notes (four voices)",
            Voices(FermataS, FermataA, FermataT, FermataB), [new(new Rational(5, 1), 7, true)]),
        Notes("a chorale, C, G, and home to C in a two-bar phrase, fermatas on every phrase (four voices)",
            Voices(FermataS + HomeS, FermataA + HomeA, FermataT + HomeT, FermataB + HomeB), [new(new Rational(5, 1), 7, true), new(new Rational(10, 1), 0, true)]),

        // ---------- a nocturne's left hand ----------
        Notes("a nocturne: the bass note alone on the beat, the chord above it in eighths, the melody over: to the dominant (four voices)",
            Together(Nocturne(ChordsToG, bassHeld: false), Shift(Notated(TuneToG), Rational.Zero)), At(5, 7, true)),
        Notes("a nocturne with the bass held through the bar under the chord's eighths: to the dominant (four voices)",
            Together(Nocturne(ChordsToG, bassHeld: true), Notated(TuneToG)), At(5, 7, true)),
        Notes("the bass held for the bar, the upper voices released after a half: to the dominant (four voices)",
            Layered(ChordsToG), At(5, 7, true)),

        // ---------- a pedal, and a pickup into every phrase ----------
        Notes("eight bars of tonic pedal, the melody entering in bar 3 and the chords in bar 5; to the dominant in bar 9 (four voices)",
            Together(Pedal(36, Rational.Zero, new Rational(8, 1)), Shift(Notated(EntryOverPedal), new Rational(2, 1)), Shift(Block(ChordsToG), new Rational(4, 1)), Shift(Notated(TuneToG), new Rational(4, 1))),
            At(9, 7, true)),
        Notes("a tune with a quarter-note pickup into every phrase, the third and fourth phrases in G (four voices)",
            Together(Notated(PickupTune), Shift(Block(PickupChords), Rational.Quarter)), [new(new Rational(33, 4), 7, true)]),

        // ---------- returns: the homecoming rule both ways ----------
        Blocks("to the dominant, and home through G7 in the last two bars (block chords)", ChordsToGAndHome, [new(5, 7, true), new(13, 0, true)]),
        Blocks("in C throughout, closing on two bars of V7/V and V: a half cadence, no key (block chords)", "0 5 7 0 | 0 5 7:7 0 | 0 5 7 0 | 2:7 7", Nowhere),
        Blocks("in C throughout, a tonicization of V in bars 9-10 and V7 I to close (block chords)", "0 5 7 0 | 0 5 7:7 0 | 2:7 7 | 7:7 0", Nowhere),
        Blocks("to the dominant, then two bars of V7/V and V in the dominant's dominant: D never established (block chords)", ChordsToG + " | 4:m 9:7 2", At(5, 7, true)),
        Blocks("C, G, D, and back to G through its dominant at the close: a return to a key that was not the opening (block chords)",
            "0 5 7 0 | 7 0 2:7 7 | 2 7 9:7 2 | 2:7 7", [new(5, 7, true), new(9, 2, true), new(13, 7, true)]),
        Blocks("C, G, D, and home to C through G7 at the close, skipping the key between (block chords)",
            "0 5 7 0 | 7 0 2:7 7 | 2 7 9:7 2 | 7:7 0", [new(5, 7, true), new(9, 2, true), new(13, 0, true)]),
        Blocks("C, its relative minor established, and home through G7 at the close (block chords)",
            "0 5 7 0 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m | 7:7 0", [new(5, 9, false), new(13, 0, true)]),
        new("A minor, its relative major established, and home through E7 at the close (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9:m | 0 5 7 0 | 0 5 7 0 | 4:7 9:m", [], [new(5, 0, true), new(13, 9, false)]) { OpeningRoot = 9 },
        new("opening in G, to C, and closing in C: no return to the opening key (block chords)", RealModulationPassages.Texture.BlockChords, true,
            "7 0 2:7 7 | 0 5 7:7 0 | 0 5 7:7 0", [], At(5, 0, true)) { OpeningRoot = 7 },
        Blocks("to the dominant, home at the close through G's own I vi ii V7 and then G7 C (block chords)",
            ChordsToG + " | 7 4:m 9:m 2:7 | 7:7 0", [new(5, 7, true), new(17, 0, true)]),
        Blocks("to the dominant, G's own I vi ii V7, then three bars home: G7 C C (block chords)",
            "0 5 7 0 | 7 0 2:7 7 | 7 4:m 9:m 2:7 | 7:7 0 0", [new(5, 7, true), new(13, 0, true)]),
        Blocks("to the dominant, then a full phrase of tonic home: G7 C C C (block chords)", ChordsToG + " | 7:7 0 0 0", [new(5, 7, true), new(13, 0, true)]),
        Blocks("to the dominant, then five bars home: G7 C C C C (block chords)", ChordsToG + " | 7:7 0 0 0 0", [new(5, 7, true), new(13, 0, true)]),
        Blocks("to the dominant, then a phrase home with the subdominant in it: G7 C F C (block chords)", ChordsToG + " | 7:7 0 5 0", [new(5, 7, true), new(13, 0, true)]),
        Blocks("C, G, C, G, and home through G7: the mid-piece return faces the old rules, the close is a homecoming (block chords)",
            "0 5 7 0 | 7 0 2:7 7 | 0 5 7:7 0 | 7 0 2:7 7 | 7:7 0", [new(5, 7, true), new(9, 0, true), new(13, 7, true), new(17, 0, true)]),
        new("opening in G, to C for eight bars, and back to G through D7 in the last two: home, not C's half cadence (block chords)", RealModulationPassages.Texture.BlockChords, true,
            "7 0 2:7 7 | 0 5 7:7 0 | 0 5 7:7 0 | 2:7 7", [], [new(5, 0, true), new(13, 7, true)]) { OpeningRoot = 7 },
        Notes("melody alone: the tune in C, the tune in G, and two bars home in C (melody alone as notes)",
            Together(Notated(EightBarTune), Shift(Notated(EightBarTuneInG), new Rational(8, 1)), Shift(Notated("G4/4 F4/4 E4/4 D4/4 | C4/1"), new Rational(16, 1))),
            [new(9, 7, true), new(17, 0, true)]),
        // The musician's G begins at the D7 (V7 of G) after the pivot Am, as the fixture's sonata
        // exposition writes it a bar after its Am; the roads write it at the Am, a bar earlier.
        Blocks("a phrase in C with V7/vi and V7/V in its second bar, then the dominant: C F G C | C E7 Am D7 | G C D7 G | G C D7 G (block chords)",
            "0 5 7 0 | 0 4:7 9:m 2:7 | 7 0 2:7 7 | 7 0 2:7 7", At(8, 7, true)),

        // ---------- common tones, held for a quarter and for a bar ----------
        Blocks("common tone to the flat submediant, the tonic held alone for a quarter (block chords)", "0 5 7 0t ~12q | 8 1 3:7 8 | 8 1 3:7 8", At(5, 8, true)),
        Blocks("common tone to the chromatic mediant, the third held alone for a whole bar (block chords)", "0 5 7 0 | ~16 | 4 9 11:7 4 | 4 9 11:7 4", At(6, 4, true)),

        // ---------- a canon at the fifth that never modulates ----------
        Notes("a canon at the fifth, diatonic in C, the comes a bar behind: no modulation (four voices)", Voices(CanonDux, CanonComes), Nowhere),
    ];
}
