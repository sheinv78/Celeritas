// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// The seventh table of <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: the sixth
/// reviewer's passages, written after the texture residuals were closed and folded in afterwards — the
/// texture lens taken where the rules had not been: a waltz accompaniment (bass on one, the chord on two
/// and three), a bossa's chords anticipated by an eighth and by a sixteenth, a ragtime left hand,
/// a chorale restruck on every beat, silence inside the new key's first phrase, 3/4 with the
/// chords an eighth late, a minor piece closing on its Picardy third after its relative major and
/// its dominant minor, a Picardy third mid-piece that is no close, a hymn repeating the Picardy
/// chord under a fermata, an accompaniment that drops out while the melody carries the new key, a
/// tremolo in sixteenths, a broken chord in quarters, a chain of dominant sevenths landing on the
/// dominant, the parallel major established and closing in the major (no Picardy), the Picardy
/// chord with the melody still moving over it, a piece beginning a bar in, and a vi IV I V loop
/// told either of its keys. Positions in the plans are the musician's, in whole notes; a 4/4 bar =
/// 1, a 3/4 bar = 3/4, bar k begins at position k-1; an empty plan means neither road may report a
/// modulation.
/// </summary>
/// <remarks>
/// <para>
/// Chord tokens are the fixture's — <c>root[:quality][h|q|t]</c> — plus <c>R</c> for a bar of
/// silence, as the sixth table writes it. Textures the fixture's builders cannot write are built
/// here as note lists and handed to the four-voice road, whose <c>Build</c> transposes every note.
/// </para>
/// <para>
/// Five of the rows were wrong on both roads on the library as it stood after the sixth table,
/// and were folded in after the reviewer's second look: a bar of silence inside the new key's
/// first phrase, alone and under the melody, put G at bar 9 where a musician hears it from bar
/// 5 — the phrase G C – G has no F sharp to be named for, and the frame that hears a key from
/// where its own chords began reached only the phrase that holds one; the Picardy close with
/// the melody arpeggiating up through the final chord was a modulation to A major at bar 11 —
/// three melody notes struck after the chord, so it was not the last harmony and no Picardy
/// third; and Am F C G four times, told C major, was no modulation on the detector and C at bar
/// 2 on the trajectory, which opens on the A minor chord, and told A minor was C at bar 2 on
/// both — the relative major, which owns no note the minor lacks, named by the profile with no
/// cadence and no frame to confirm it.
/// </para>
/// <para>
/// Four rows pin what the seventh reviewer's held-out passages found the rules overreaching or
/// stopping short on: a phrase V I V I closing on the old tonic between C's phrase and G's is
/// C's, not G's (the frame reaching back had taken it for G's and put G at bar 5, where the
/// library before it had bar 9); the seam of Am F C G | C F G C is no cadence in C, and C begins
/// with the phrase framed by its chord at bar 9, not at bar 5; and a scale run down to the final
/// note, or a turn on it, ends the piece on the Picardy chord as an arpeggio of its tones does.
/// </para>
/// </remarks>
internal static class AccompanimentTexturesPassages
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

            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            var quality = colon >= 0 ? token[(colon + 1)..] : "";
            parsed.Add(new Chord(root, Qualities[quality], time, duration));
            time += duration;
        }

        return parsed;
    }

    /// <summary>Block chords in close root position from C3, each for its written length.</summary>
    private static NoteEvent[] Block(string chords) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration)))];

    /// <summary>A waltz's left hand: the bass root alone on the first beat, the chord above it on the second and the third — for chords written in 3/4 (<c>t</c>).</summary>
    private static NoteEvent[] Waltz(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            notes.Add(new NoteEvent(36 + c.Root, c.Offset, Rational.Quarter));
            for (var beat = 1; beat < 3; beat++)
            {
                foreach (var i in c.Intervals)
                    notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, c.Offset + (Rational.Quarter * beat), Rational.Quarter));
            }
        }

        return [.. notes];
    }

    /// <summary>A ragtime left hand: bass root, chord, bass fifth, chord — a quarter each, for chords written a whole note long.</summary>
    private static NoteEvent[] Ragtime(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            notes.Add(new NoteEvent(36 + c.Root, c.Offset, Rational.Quarter));
            notes.Add(new NoteEvent(36 + c.Root + 7, c.Offset + Rational.Half, Rational.Quarter));
            foreach (var beat in new[] { 1, 3 })
            {
                foreach (var i in c.Intervals)
                    notes.Add(new NoteEvent(ChordRegister + 12 + c.Root + i, c.Offset + (Rational.Quarter * beat), Rational.Quarter));
            }
        }

        return [.. notes];
    }

    /// <summary>Every chord restruck on every beat: quarters for the chord's length.</summary>
    private static NoteEvent[] Restruck(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            var beats = (int)(c.Duration / Rational.Quarter).ToDouble();
            for (var beat = 0; beat < beats; beat++)
            {
                foreach (var i in c.Intervals)
                    notes.Add(new NoteEvent(ChordRegister + c.Root + i, c.Offset + (Rational.Quarter * beat), Rational.Quarter));
            }
        }

        return [.. notes];
    }

    /// <summary>Every chord anticipated by <paramref name="by"/> — the pushed chords of a bossa; the first is cut, not moved.</summary>
    private static NoteEvent[] Pushed(string chords, Rational by) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => c.Offset == Rational.Zero
            ? new NoteEvent(ChordRegister + c.Root + i, c.Offset, c.Duration - by)
            : new NoteEvent(ChordRegister + c.Root + i, c.Offset - by, c.Duration)))];

    /// <summary>Every chord <paramref name="by"/> late.</summary>
    private static NoteEvent[] Delayed(string chords, Rational by) =>
        [.. ParseChords(chords).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, c.Offset + by, c.Duration)))];

    /// <summary>A tremolo in sixteenths: the chord's root and third against its fifth and octave (or seventh), alternating for the chord's length.</summary>
    private static NoteEvent[] Tremolo(string chords)
    {
        var sixteenth = new Rational(1, 16);
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] lower = [c.Intervals[0], c.Intervals[1]];
            int[] upper = c.Intervals.Length == 4 ? [c.Intervals[2], c.Intervals[3]] : [c.Intervals[2], 12];
            var count = (int)(c.Duration / sixteenth).ToDouble();
            for (var k = 0; k < count; k++)
            {
                foreach (var i in k % 2 == 0 ? lower : upper)
                    notes.Add(new NoteEvent(ChordRegister + c.Root + i, c.Offset + (sixteenth * k), sixteenth));
            }
        }

        return [.. notes];
    }

    /// <summary>Every chord broken in quarters: root, third, fifth, octave (a seventh chord's fourth quarter is its seventh).</summary>
    private static NoteEvent[] Broken(string chords)
    {
        var notes = new List<NoteEvent>();
        foreach (var c in ParseChords(chords))
        {
            int[] tones = c.Intervals.Length == 4 ? c.Intervals : [.. c.Intervals, 12];
            var beats = (int)(c.Duration / Rational.Quarter).ToDouble();
            for (var k = 0; k < beats; k++)
                notes.Add(new NoteEvent(ChordRegister + c.Root + tones[k % 4], c.Offset + (Rational.Quarter * k), Rational.Quarter));
        }

        return [.. notes];
    }

    /// <summary>One chord held from <paramref name="from"/> for <paramref name="length"/> — a fermata.</summary>
    private static NoteEvent[] Fermata(string chord, Rational from, Rational length) =>
        [.. ParseChords(chord).SelectMany(c => c.Intervals.Select(i => new NoteEvent(ChordRegister + c.Root + i, from, length)))];

    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    private static NoteEvent[] Shift(NoteEvent[] notes, Rational by) =>
        [.. notes.Select(n => new NoteEvent(n.Pitch, n.Offset + by, n.Duration, n.Velocity))];

    private static NoteEvent[] Together(params NoteEvent[][] parts) =>
        [.. parts.SelectMany(p => p).Where(n => n.Pitch != MusicNotation.RestPitch)];

    private static RealModulationPassages.PlannedModulation[] Nowhere => [];

    private static RealModulationPassages.PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    /// <summary>The key beginning in bar <paramref name="bar"/> of 3/4, counting from one.</summary>
    private static RealModulationPassages.PlannedModulation AtWaltzBar(int bar, int toRoot, bool toMajor) =>
        new(new Rational(3 * (bar - 1), 4), toRoot, toMajor);

    private static RealModulationPassages.Passage Notes(string name, NoteEvent[] notes, RealModulationPassages.PlannedModulation[] plan, bool major = true) =>
        new(name, RealModulationPassages.Texture.FourVoices, major, "", notes, plan);

    private static RealModulationPassages.Passage Blocks(string name, string chords, RealModulationPassages.PlannedModulation[] plan, bool major = true) =>
        new(name, RealModulationPassages.Texture.BlockChords, major, chords, [], plan);

    // ---------- the tunes ----------

    /// <summary>Twelve bars: four in C, eight in G with the F sharp in bars 7 and 11 — the sixth table's tune, over C F G C | G C D7 G | G C D7 G.</summary>
    private const string TuneToG =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | F#5/4 A5/4 D5/2 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    private const string ChordsToG = "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    /// <summary>The same twelve bars in 3/4: three quarters a bar.</summary>
    private const string WaltzTuneToG =
        "E4/4 G4/4 C5/4 | A4/4 F4/4 C5/4 | D5/4 B4/4 G4/4 | E5/4 D5/4 C5/4 | "
        + "B4/4 D5/4 G5/4 | E5/4 C5/4 G4/4 | F#5/4 A5/4 D5/4 | B4/4 D5/4 G4/4 | "
        + "D5/4 B4/4 G4/4 | E5/4 G5/4 C5/4 | A4/4 C5/4 F#5/4 | G5/2.";

    private const string WaltzChordsToG = "0t 5t 7t 0t | 7t 0t 2:7t 7t | 7t 0t 2:7t 7t";

    /// <summary>The tune to G with bar 7 silent in the melody too.</summary>
    private const string TuneToGBarSevenSilent =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | R/1 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | A4/4 C5/4 F#5/2 | G5/1";

    /// <summary>A minor, its relative major, A minor again closing on a Picardy third — the melody arpeggiating up through the final A major chord.</summary>
    private const string PicardyTuneMovingClose =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 F5/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C#5/4 E5/4 A5/2";

    private const string PicardyChordsMovingClose = "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7 9";

    /// <summary>The Picardy tune without its last bar; the closes below supply it.</summary>
    private const string PicardyTuneOpen =
        "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | C5/4 B4/4 A4/2 | "
        + "E5/4 G5/4 C5/2 | A4/4 F5/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "C5/4 B4/4 A4/2 | F5/4 D5/4 A4/2 | G#4/4 B4/4 E5/2 | ";

    // ---------- the table ----------

    internal static IReadOnlyList<RealModulationPassages.Passage> Table { get; } =
    [
        // ---------- a waltz accompaniment: bass on one, the chord on two and three ----------
        Notes("a waltz accompaniment, the bass on one and the chord on two and three: C then G (left hand alone, 3/4)",
            Waltz(WaltzChordsToG), [AtWaltzBar(5, 7, true)]),
        Notes("a waltz accompaniment under a melody in quarters: C then G (3/4)",
            Together(Waltz(WaltzChordsToG), Notated(WaltzTuneToG)), [AtWaltzBar(5, 7, true)]),

        // ---------- a bossa: every chord anticipated ----------
        Notes("a bossa: every chord anticipated by an eighth, nothing else sounding: C then G (block chords as notes)",
            Pushed(ChordsToG, Rational.Eighth), At(5, 7, true)),
        Notes("a bossa: every chord anticipated by a sixteenth, nothing else sounding: C then G (block chords as notes)",
            Pushed(ChordsToG, new Rational(1, 16)), At(5, 7, true)),
        Notes("a bossa: every chord anticipated by a sixteenth under a melody on the beat: C then G (four voices)",
            Together(Pushed(ChordsToG, new Rational(1, 16)), Notated(TuneToG)), At(5, 7, true)),

        // ---------- a ragtime left hand ----------
        Notes("a ragtime left hand, bass-chord-bass-chord in quarters: C then G (left hand alone)",
            Ragtime(ChordsToG), At(5, 7, true)),
        Notes("a ragtime left hand under the melody: C then G (four voices)",
            Together(Ragtime(ChordsToG), Notated(TuneToG)), At(5, 7, true)),

        // ---------- every chord restruck on every beat ----------
        Notes("a chorale with every chord restruck on every beat: C then G (block chords as quarters)",
            Restruck(ChordsToG), At(5, 7, true)),
        Notes("every chord restruck on every beat under the melody: C then G (four voices)",
            Together(Restruck(ChordsToG), Notated(TuneToG)), At(5, 7, true)),

        // ---------- silence inside the new key's first phrase ----------
        Notes("a bar of silence before the new key's own note: C F G C | G R D7 G | G C D7 G (block chords as notes)",
            Block("0 5 7 0 | 7 R 2:7 7 | 7 0 2:7 7"), At(5, 7, true)),
        Notes("a bar of silence inside the new key's first phrase: C F G C | G C R G | G C D7 G (block chords as notes)",
            Block("0 5 7 0 | 7 0 R 7 | 7 0 2:7 7"), At(5, 7, true)),
        Notes("a bar of silence inside the new key's first phrase, melody and chords both (four voices)",
            Together(Block("0 5 7 0 | 7 0 R 7 | 7 0 2:7 7"), Notated(TuneToGBarSevenSilent)), At(5, 7, true)),

        // ---------- 3/4 with the chords an eighth late ----------
        Notes("a waltz in block chords, every chord an eighth late: C then G (3/4)",
            Delayed(WaltzChordsToG, Rational.Eighth), [AtWaltzBar(5, 7, true)]),
        Notes("a waltz in block chords an eighth late under a melody on the beat: C then G (3/4)",
            Together(Delayed(WaltzChordsToG, Rational.Eighth), Notated(WaltzTuneToG)), [AtWaltzBar(5, 7, true)]),

        // ---------- a minor piece, its relative major, its dominant minor, home on a Picardy third ----------
        new("A minor, its relative major, its dominant minor, and home to A minor closing on a Picardy third (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9:m | 0 5 7 0 | 4:m 9:m 11:7 4:m | 9:m 2:m 4:7 9", [], [new(5, 0, true), new(9, 4, false), new(13, 9, false)]) { OpeningRoot = 9 },
        new("A minor, its dominant minor for eight bars, and home to A minor closing on a Picardy third (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9:m | 4:m 9:m 11:7 4:m | 4:m 9:m 11:7 4:m | 9:m 2:m 4:7 9", [], [new(5, 4, false), new(13, 9, false)]) { OpeningRoot = 9 },

        // ---------- a Picardy third mid-piece that is no close ----------
        new("a Picardy third closing the first phrase only, then the relative major: Am Dm E7 A | C F G C | C F G C (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9 | 0 5 7 0 | 0 5 7 0", [], At(5, 0, true)) { OpeningRoot = 9 },
        new("a Picardy third closing the first phrase only, the relative major, and home to plain A minor (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9 | 0 5 7 0 | 0 5 7 0 | 9:m 2:m 4:7 9:m", [], [new(5, 0, true), new(13, 9, false)]) { OpeningRoot = 9 },

        // ---------- a hymn repeating the Picardy chord under a fermata ----------
        new("a hymn: A minor, its relative major, home to A minor, the Picardy chord restruck for two more bars (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7 9 | 9 9", [], [new(5, 0, true), new(9, 9, false)]) { OpeningRoot = 9 },
        Notes("a hymn: A minor, its relative major, home to A minor, the Picardy chord held under a fermata for three whole notes (block chords as notes)",
            Together(Block("9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7"), Fermata("9", new Rational(11, 1), new Rational(3, 1))), [new(5, 0, true), new(9, 9, false)], major: false),

        // ---------- the accompaniment drops out while the melody carries the new key ----------
        Notes("the accompaniment silent for two bars while the melody carries the new key: C then G (four voices)",
            Together(Block("0 5 7 0 | 7 0 R R | 7 0 2:7 7"), Notated(TuneToG)), At(5, 7, true)),

        // ---------- a tremolo in sixteenths ----------
        Notes("an accompaniment in tremolo sixteenths, root and third against fifth and octave: C then G (tremolo alone)",
            Tremolo(ChordsToG), At(5, 7, true)),
        Notes("an accompaniment in tremolo sixteenths under the melody: C then G (four voices)",
            Together(Tremolo(ChordsToG), Notated(TuneToG)), At(5, 7, true)),

        // ---------- every chord a broken chord in quarters ----------
        Notes("every chord broken in quarters, root third fifth octave: C then G (broken chords)",
            Broken(ChordsToG), At(5, 7, true)),
        Notes("every chord broken in quarters under the melody: C then G (four voices)",
            Together(Broken(ChordsToG), Notated(TuneToG)), At(5, 7, true)),

        // ---------- a chain of dominant sevenths landing on the dominant ----------
        Blocks("a chain of dominant sevenths B7 E7 A7 D7 landing on G, then G established: to the dominant (block chords)",
            "0 5 7 0 | 11:7 4:7 9:7 2:7 | 7 0 2:7 7 | 7 0 2:7 7", At(9, 7, true)),

        // ---------- the parallel major established and closing in the major: no Picardy ----------
        new("C minor, then its parallel major established for eight bars, closing in C major with G7 C: no Picardy (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "0:m 5:m 7:7 0:m | 0 5 7 0 | 0 5 7 0 | 7:7 0", [], At(5, 0, true)),
        new("A minor, then A major established for eight bars, closing in A major with E7 A: no Picardy (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 2:m 4:7 9:m | 9 2 4:7 9 | 9 2 4:7 9 | 4:7 9", [], At(5, 9, true)) { OpeningRoot = 9 },

        // ---------- the Picardy chord with the melody still moving over it ----------
        Notes("A minor, its relative major, home to A minor closing on a Picardy third, the melody arpeggiating up through the final chord (four voices)",
            Together(Block(PicardyChordsMovingClose), Notated(PicardyTuneMovingClose)), [new(5, 0, true), new(9, 9, false)], major: false),
        Notes("the Picardy close under a scale run down to the final note, E D C sharp B A (four voices)",
            Together(Block(PicardyChordsMovingClose), Notated(PicardyTuneOpen + "E5/8 D5/8 C#5/8 B4/8 A4/2")), [new(5, 0, true), new(9, 9, false)], major: false),
        Notes("the Picardy close under a turn on the final note, A B A G sharp A (four voices)",
            Together(Block(PicardyChordsMovingClose), Notated(PicardyTuneOpen + "A4/8 B4/8 A4/8 G#4/8 A4/2")), [new(5, 0, true), new(9, 9, false)], major: false),

        // ---------- the music begins a bar in ----------
        Notes("a count-in bar of silence, then the pickup passage: R | G(q) C F G C | G C D7 G G (block chords as notes)",
            Shift(Block("7q 0 5 7 0 | 7 0 2:7 7 7"), Rational.Whole), [new(new Rational(21, 4), 7, true)]),
        Notes("a count-in bar of silence, then C F G C | G C D7 G | G C D7 G with every chord an eighth late (block chords as notes)",
            Shift(Delayed(ChordsToG, Rational.Eighth), Rational.Whole), [new(new Rational(5, 1), 7, true)]),

        // ---------- a vi IV I V loop, told either of its keys ----------
        new("Am F C G four times, told C major (block chords)", RealModulationPassages.Texture.BlockChords, true,
            "9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7", [], Nowhere),
        new("Am F C G four times, told A minor (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7", [], Nowhere) { OpeningRoot = 9 },
        new("Am F C G twice, then C F G C twice: the relative major begins with the phrase framed by its chord, told A minor (block chords)", RealModulationPassages.Texture.BlockChords, false,
            "9:m 5 0 7 | 9:m 5 0 7 | 0 5 7 0 | 0 5 7 0", [], At(9, 0, true)) { OpeningRoot = 9 },

        // ---------- a phrase closing on the old tonic between the old key's phrase and the new key's ----------
        Blocks("C F G C | G C G C | G C D7 G: the V I V I closing on C is C's, and G begins at bar 9 (block chords)",
            "0 5 7 0 | 7 0 7 0 | 7 0 2:7 7", At(9, 7, true)),
    ];
}
