// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// Two roads answer "where does this piece modulate". <see cref="KeyProfiler.AnalyzeModulations"/>
/// profiles fixed windows of the notes and its <see cref="KeyTrajectory.DetectModulations"/>
/// reports a change wherever two confident windows disagree; <see cref="ModulationDetector.Analyze(NoteBuffer, KeySignature)"/>
/// reads the chords through a sliding window and gates on decisiveness, separation from the key
/// it is in, and stability. Both were tuned on synthetic scale passages, never on music. Judged
/// on the forty passages in <see cref="RealModulationPassages"/> — the nursery tune and the
/// textbook modulation, in block chords, arpeggios, melody alone and melody over chords, each
/// with the key plan a musician would write — the trajectory was wrong on thirty-four and the
/// detector on sixteen. The trajectory, at a two-bar window, hears I IV V I in block chords as
/// C then G, and reports four modulations in a twelve-bar blues, three in I V7/V V I and three in
/// a minor phrase that never leaves its key. The detector hears no modulation from four bars of
/// C to four of D flat in block chords, none to the relative minor in any texture, none at all
/// in thirteen bars that go to the subdominant and come home; arpeggiated, it hears every
/// I IV V I opening as A minor at the IV chord and back, and two bars of V7/V - V as a modulation
/// to the dominant. A musician hears each passage move once, if at all, to the key named, at the
/// bar where it begins. Every passage is asked in all twelve keys.
/// </summary>
/// <remarks>
/// A tonicization is not a modulation: an applied dominant, a borrowed chord or the raised
/// leading tone of a minor key does not move the piece. A modulation is heard at the bar where
/// the new key begins — within one bar either side, to allow the pivot chord — and is named by
/// its tonic and mode. The relative minor is the one case where scale content alone cannot tell
/// the two keys apart, so the trajectory may hear nothing there; the detector, which reads the
/// chords and their dominant with the raised seventh, may not.
/// </remarks>
public class RealModulationsAreHeardWhereAMusicianHearsThemTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    public static TheoryData<string> Passages => [.. RealModulationPassages.All.Select(p => p.Name)];

    [Theory]
    [MemberData(nameof(Passages))]
    public void TheKeyTrajectoryHearsTheModulationsAMusicianHearsInEveryKey(string name)
    {
        var passage = RealModulationPassages.Named(name);

        AssertHeardInEveryKey(
            passage,
            "the key trajectory",
            (buffer, _) => KeyProfiler.AnalyzeModulations(buffer, new Rational(2, 1), new Rational(1, 1))
                .DetectModulations()
                .Select(m => (m.Position, m.ToKey))
                .ToList(),
            mayHearNoChange: passage.TrajectoryMayHearNoChange);
    }

    [Theory]
    [MemberData(nameof(Passages))]
    public void TheModulationDetectorHearsTheModulationsAMusicianHearsInEveryKey(string name)
    {
        var passage = RealModulationPassages.Named(name);

        AssertHeardInEveryKey(
            passage,
            "the modulation detector",
            (buffer, openingKey) => ModulationDetector.Analyze(buffer, openingKey).Modulations
                .Where(m => m.Type != ModulationType.Tonicization)
                .Select(m => (m.Offset, m.ToKey))
                .ToList(),
            mayHearNoChange: false);
    }

    [Fact]
    public void EveryPassageIsBuiltAsItIsDescribed()
    {
        // The table is only as good as its builders: every passage must build in every key,
        // the melody of a melody-over-chords passage must last as long as its chords, every
        // key area of a modulation plan must be at least four bars unless a later plan entry
        // begins sooner, and the set must cover all four textures with at least six passages
        // that do not modulate.
        foreach (var passage in RealModulationPassages.All)
        {
            for (var tonic = 0; tonic < 12; tonic++)
            {
                using var buffer = passage.Build(tonic);
                Assert.True(buffer.Count > 0, passage.Name);
                for (var i = 0; i < buffer.Count; i++)
                {
                    Assert.InRange(buffer.Get(i).Pitch, 0, 127);
                }
            }

            var length = passage.Length;
            if (passage.Texture == RealModulationPassages.Texture.MelodyOverChords)
            {
                Assert.Equal(RealModulationPassages.LengthOf(passage.Chords), RealModulationPassages.LengthOf(passage.Melody));
            }

            var starts = passage.Plan.Select(p => p.Position).Prepend(Rational.Zero).Append(length).ToList();
            for (var i = 1; i < starts.Count; i++)
            {
                Assert.True(starts[i] - starts[i - 1] >= new Rational(4, 1), $"{passage.Name}: a key area shorter than four bars");
            }
        }

        Assert.Equal(4, RealModulationPassages.All.Select(p => p.Texture).Distinct().Count());
        Assert.True(RealModulationPassages.All.Count(p => p.Plan.Length == 0) >= 6);
        Assert.True(RealModulationPassages.All.Count >= 24);
        Assert.Equal(RealModulationPassages.All.Count, RealModulationPassages.All.Select(p => p.Name).Distinct().Count());
    }

    private static void AssertHeardInEveryKey(
        RealModulationPassages.Passage passage,
        string road,
        Func<NoteBuffer, KeySignature, List<(Rational Position, KeySignature ToKey)>> ask,
        bool mayHearNoChange)
    {
        var readings = new HashSet<string>();
        var wrong = new List<string>();

        for (var tonic = 0; tonic < 12; tonic++)
        {
            var openingKey = new KeySignature((byte)tonic, passage.OpeningIsMajor);
            using var buffer = passage.Build(tonic);

            var heard = ask(buffer, openingKey);

            readings.Add(Describe(heard, tonic));
            if (!IsHeard(passage.Plan, heard, tonic) && !(mayHearNoChange && heard.Count == 0))
            {
                wrong.Add(Names[tonic]);
            }
        }

        var plan = passage.Plan.Length == 0 ? "none" : string.Join(", ", passage.Plan);
        Assert.True(
            wrong.Count == 0,
            $"{passage.Name}: {road} heard [{string.Join(" | ", readings)}] in {string.Join(" ", wrong)}, "
            + $"where a musician hears [{plan}]");

        // The same music in twelve keys is one reading.
        Assert.Single(readings);
    }

    /// <summary>
    /// The plan is heard when as many modulations are reported as planned and each planned one
    /// has a reported modulation to its key within one whole note of the bar it begins in.
    /// </summary>
    private static bool IsHeard(
        RealModulationPassages.PlannedModulation[] plan,
        List<(Rational Position, KeySignature ToKey)> heard,
        int tonic) =>
        heard.Count == plan.Length
        && plan.All(p => heard.Any(h =>
            h.ToKey.Root == (tonic + p.ToRoot) % 12
            && h.ToKey.IsMajor == p.ToMajor
            && Math.Abs((h.Position - p.Position).ToDouble()) <= 1.0));

    /// <summary>The reading relative to the opening tonic, so twelve keys can be compared.</summary>
    private static string Describe(List<(Rational Position, KeySignature ToKey)> heard, int tonic) =>
        heard.Count == 0
            ? "none"
            : string.Join(", ", heard.Select(h =>
                $"{PitchMath.Fold(h.ToKey.Root - tonic)}{(h.ToKey.IsMajor ? "M" : "m")}@{h.Position}"));
}

/// <summary>
/// The passages a musician would use to judge a modulation finder, built in C and transposed
/// by <see cref="Passage.Build"/>. Chords are tokens of the form <c>root[:quality][h]</c>: the
/// root as semitones above the tonic, the quality one of <c>m 7 m7 maj7 dim dim7 m7b5</c>
/// (none for a major triad), a trailing <c>h</c> for a half-bar chord; bar lines are ignored.
/// Melodies are notation in C. The plan is where a musician writes each new key: the bar it
/// begins in (counting from one), its tonic as semitones above the opening tonic, and its mode.
/// </summary>
internal static class RealModulationPassages
{
    internal enum Texture
    {
        BlockChords,
        Arpeggios,
        MelodyAlone,
        MelodyOverChords,
    }

    /// <summary>A key a musician hears the passage move to.</summary>
    internal readonly record struct PlannedModulation(int Bar, int ToRoot, bool ToMajor)
    {
        /// <summary>The whole-note position of the bar the new key begins in.</summary>
        public Rational Position => new(Bar - 1, 1);

        public override string ToString() => $"{ToRoot}{(ToMajor ? "M" : "m")}@{Position}";
    }

    /// <summary>
    /// One passage in one texture. <paramref name="Chords"/> is empty for a melody alone and
    /// <paramref name="Melody"/> is empty for block chords and arpeggios.
    /// </summary>
    internal sealed record Passage(
        string Name,
        Texture Texture,
        bool OpeningIsMajor,
        string Chords,
        NoteEvent[] Melody,
        PlannedModulation[] Plan,
        bool TrajectoryMayHearNoChange = false)
    {
        /// <summary>How long the passage is, in whole notes.</summary>
        public Rational Length => Texture == Texture.MelodyAlone ? LengthOf(Melody) : LengthOf(Chords);

        /// <summary>The passage transposed up by <paramref name="tonic"/> semitones, sorted by onset.</summary>
        public NoteBuffer Build(int tonic)
        {
            var notes = new List<NoteEvent>();

            if (Texture is Texture.BlockChords or Texture.MelodyOverChords)
            {
                foreach (var chord in ParseChords(Chords))
                {
                    foreach (var interval in chord.Intervals)
                    {
                        notes.Add(new NoteEvent(ChordRegister + chord.Root + interval + tonic, chord.Offset, chord.Duration));
                    }
                }
            }

            if (Texture == Texture.Arpeggios)
            {
                foreach (var chord in ParseChords(Chords))
                {
                    // Up through the chord and back: R 3 5 8 5 3 R 3 for a triad over a bar,
                    // R 3 5 7 5 3 R 3 for a seventh, the first half of that for a half bar.
                    int[] tones = chord.Intervals.Length == 4 ? chord.Intervals : [.. chord.Intervals, 12];
                    var eighths = (int)(chord.Duration / Rational.Eighth).ToDouble();
                    for (var k = 0; k < eighths; k++)
                    {
                        notes.Add(new NoteEvent(
                            ChordRegister + chord.Root + tones[Rise[k]] + tonic,
                            chord.Offset + (Rational.Eighth * k),
                            Rational.Eighth));
                    }
                }
            }

            if (Texture is Texture.MelodyAlone or Texture.MelodyOverChords)
            {
                foreach (var note in Melody)
                {
                    notes.Add(new NoteEvent(note.Pitch + tonic, note.Offset, note.Duration, note.Velocity));
                }
            }

            var buffer = new NoteBuffer(notes.Count);
            buffer.AddRange(notes.OrderBy(n => n.Offset).ThenBy(n => n.Pitch).ToArray());
            return buffer;
        }
    }

    internal static Passage Named(string name) => All.Single(p => p.Name == name);

    // ---------- the tunes ----------

    private const string Twinkle =
        "C4/4 C4/4 G4/4 G4/4 | A4/4 A4/4 G4/2 | F4/4 F4/4 E4/4 E4/4 | D4/4 D4/4 C4/2 | "
        + "G4/4 G4/4 F4/4 F4/4 | E4/4 E4/4 D4/2 | G4/4 G4/4 F4/4 F4/4 | E4/4 E4/4 D4/2 | "
        + "C4/4 C4/4 G4/4 G4/4 | A4/4 A4/4 G4/2 | F4/4 F4/4 E4/4 E4/4 | D4/4 D4/4 C4/2";

    private const string OdeToJoy =
        "E4/4 E4/4 F4/4 G4/4 | G4/4 F4/4 E4/4 D4/4 | C4/4 C4/4 D4/4 E4/4 | E4/4. D4/8 D4/2 | "
        + "E4/4 E4/4 F4/4 G4/4 | G4/4 F4/4 E4/4 D4/4 | C4/4 C4/4 D4/4 E4/4 | D4/4. C4/8 C4/2";

    private const string OdeToJoyChords = "0 7 0 7 | 0 7 0 7h 0h";

    /// <summary>Eight bars in C that touch every chromatic note once, each as a passing or neighbour eighth on a weak beat.</summary>
    private const string EveryPassingTone =
        "C4/4 D4/8 D#4/8 E4/4 G4/4 | F4/4 A4/8 Ab4/8 G4/4 F4/4 | G4/4 F4/8 F#4/8 G4/4 B4/4 | C5/4 B4/8 Bb4/8 A4/4 G4/4 | "
        + "E4/4 D4/8 C#4/8 D4/4 E4/4 | F4/4 E4/8 Eb4/8 D4/4 F4/4 | G4/4 G#4/8 A4/8 B4/4 D5/4 | C5/1";

    private const string EveryPassingToneChords = "0 5 7:7 0 | 0 5 7:7 0";

    /// <summary>Eight bars in C minor with the raised leading tone, B natural, at every cadence.</summary>
    private const string MinorPhrase =
        "C4/4 D4/4 Eb4/4 F4/4 | G4/2 F4/4 Eb4/4 | D4/4 C4/4 B3/4 D4/4 | C4/1 | "
        + "Eb4/4 D4/4 C4/4 B3/4 | C4/4 D4/4 Eb4/4 C4/4 | Ab4/4 G4/4 D4/4 B3/4 | C4/1";

    private const string MinorPhraseChords = "0:m 5:m 7 0:m | 0:m 8 7:7 0:m";

    private const string TwelveBarBlues = "0:7 0:7 0:7 0:7 | 5:7 5:7 0:7 0:7 | 7:7 5:7 0:7 7:7";

    private const string AppliedDominant = "0 2:7 7 0 | 0 2:7 7:7 0";

    private const string BorrowedIv = "0 5 5:m 0 | 0 5 5:m 7:7h 0h";

    private const string BorrowedIvMelody =
        "E4/4 G4/4 C5/2 | A4/4 C5/4 F4/2 | Ab4/4 C5/4 F4/2 | G4/4 E4/4 C4/2 | "
        + "C5/4 G4/4 E4/2 | F4/4 A4/4 C5/2 | Ab4/4 F4/4 C5/2 | D5/4 B4/4 C5/2";

    private const string DooWopLoop = "0 9:m 5 7 | 0 9:m 5 7 | 0 9:m 5 7 | 0 9:m 5 7";

    /// <summary>
    /// An eight-bar tune in C, restated in the new key by the melody-alone modulations. Its
    /// first bar has both the fourth and the seventh degree, so the note that tells the new key
    /// from the old — F sharp for the dominant, B flat for the subdominant — sounds in the first
    /// bar of the restatement, as it does in any tune that means to be heard in its key.
    /// </summary>
    private const string Tune =
        "E4/4 F4/4 D4/4 B3/4 | C4/4 E4/4 G4/4 E4/4 | F4/4 A4/4 G4/4 F4/4 | E4/4 D4/4 C4/2 | "
        + "G4/4 F4/4 E4/4 D4/4 | E4/4 C4/4 B3/4 G3/4 | A3/4 B3/4 C4/4 D4/4 | C4/1";

    /// <summary>Four bars over I IV V I.</summary>
    private const string ShortTune = "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2";

    // ---------- the modulations ----------

    private const string ToTheDominant = "0 5 7:7 0 | 7 2:7 7 0h 2:7h 7";

    private const string ToTheDominantThroughAPivot = "0 5 7 0 | 9:m 2:7 7 0 2:7 7";

    private const string ToTheDominantThroughAPivotMelody =
        "C5/4 E4/4 G4/2 | A4/4 F4/4 C5/2 | B4/4 G4/4 D5/2 | C5/4 E5/4 G4/2 | "
        + "A4/4 C5/4 E5/2 | F#5/4 D5/4 A4/4 C5/4 | B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | A4/4 C5/4 F#5/2 | G5/1";

    private const string ToTheSubdominant = "0 5 7:7 0 | 5 10 0:7 5 10h 0:7h 5";

    private const string ToTheRelativeMinor = "0 5 7 0 | 4:7 9:m 2:m 4:7 9:m";

    private const string ToTheRelativeMinorMelody =
        "E4/4 F4/4 G4/4 E4/4 | F4/4 A4/4 G4/2 | G4/4 F4/4 E4/4 D4/4 | C4/1 | "
        + "G#4/4 B4/4 D5/4 B4/4 | C5/4 B4/4 A4/2 | F4/4 E4/4 D4/4 F4/4 | E4/4 G#4/4 B4/4 D5/4 | C5/2 A4/2";

    private const string ToTheParallelMinor = "0 5 7:7 0 | 0:m 5:m 7:7 0:m 8h 7:7h 0:m";

    private const string ToTheParallelMinorMelody =
        "C5/4 E4/4 G4/2 | A4/4 F4/4 C5/2 | B4/4 D5/4 F4/4 G4/4 | E4/2 C5/2 | "
        + "C5/4 Eb4/4 G4/2 | Ab4/4 F4/4 C5/2 | B4/4 D5/4 F4/4 G4/4 | Eb4/2 C5/2 | Ab4/4 C5/4 B4/4 D5/4 | C5/1";

    private const string UpASemitone = "0 5 7 0 | 1 6 8 1";

    private const string ToTheFlatSubmediant = "0 5 7 0 | 8 1 3 8 1h 3:7h 8";

    private const string ToTheChromaticMediant = "0 5 7 0 | 4 9 11:7 4 9h 11:7h 4";

    private const string ToTheChromaticMediantMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "G#4/4 B4/4 E5/2 | C#5/4 A4/4 E5/2 | F#5/4 D#5/4 B4/2 | G#5/4 F#5/4 E5/2 | A4/4 C#5/4 D#5/4 F#5/4 | E5/1";

    private const string DownAFifthAndHome = "0 5 7:7 0 | 5 10 0:7 5 | 7:7 0 5 7:7 0";

    private const string EightEightEight =
        "0 5 7 0 9:m 5 7 0 | 7 2:7 7 0 4:m 9:m 2:7 7 | 7:7 0 5 0 9:m 5 7:7 0";

    private const string EightEightEightMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | B4/4 D5/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "A4/4 C5/4 E5/2 | F5/4 C5/4 A4/2 | G4/4 B4/4 D5/2 | C5/1 | "
        + "B4/4 D5/4 G5/2 | F#5/4 D5/4 A4/2 | G4/4 B4/4 D5/2 | E5/4 C5/4 G4/2 | "
        + "E5/4 G5/4 B4/2 | C5/4 A4/4 E5/2 | F#5/4 A4/4 C5/2 | G5/1 | "
        + "F5/4 D5/4 B4/2 | C5/4 E5/4 G4/2 | A4/4 C5/4 F5/2 | E5/4 C5/4 G4/2 | "
        + "A4/4 C5/4 E5/2 | F5/4 A4/4 C5/2 | D5/4 F5/4 B4/2 | C5/1";

    /// <summary>Chords are voiced in close root position from C3.</summary>
    private const int ChordRegister = 48;

    private static readonly int[] Rise = [0, 1, 2, 3, 2, 1, 0, 1];

    private static readonly Dictionary<string, int[]> Qualities = new()
    {
        [""] = [0, 4, 7],
        ["m"] = [0, 3, 7],
        ["dim"] = [0, 3, 6],
        ["7"] = [0, 4, 7, 10],
        ["m7"] = [0, 3, 7, 10],
        ["maj7"] = [0, 4, 7, 11],
        ["dim7"] = [0, 3, 6, 9],
        ["m7b5"] = [0, 3, 6, 10],
    };

    private static readonly PlannedModulation[] Nowhere = [];

    private static PlannedModulation[] At(int bar, int toRoot, bool toMajor) => [new(bar, toRoot, toMajor)];

    internal static IReadOnlyList<Passage> All { get; } =
    [
        // ---------- passages that never leave their key ----------
        new("Twinkle, Twinkle (melody alone)", Texture.MelodyAlone, true, "", Notated(Twinkle), Nowhere),
        new("Ode to Joy (melody alone)", Texture.MelodyAlone, true, "", Notated(OdeToJoy), Nowhere),
        new("Ode to Joy (melody over chords)", Texture.MelodyOverChords, true, OdeToJoyChords, Notated(OdeToJoy), Nowhere),
        new("I vi IV V four times (block chords)", Texture.BlockChords, true, DooWopLoop, [], Nowhere),
        new("every chromatic passing tone (melody alone)", Texture.MelodyAlone, true, "", Notated(EveryPassingTone), Nowhere),
        new("every chromatic passing tone (melody over chords)", Texture.MelodyOverChords, true, EveryPassingToneChords, Notated(EveryPassingTone), Nowhere),
        new("minor with the raised leading tone (block chords)", Texture.BlockChords, false, MinorPhraseChords, [], Nowhere),
        new("minor with the raised leading tone (melody alone)", Texture.MelodyAlone, false, "", Notated(MinorPhrase), Nowhere),
        new("twelve-bar blues in sevenths (block chords)", Texture.BlockChords, true, TwelveBarBlues, [], Nowhere),
        new("twelve-bar blues in sevenths (arpeggios)", Texture.Arpeggios, true, TwelveBarBlues, [], Nowhere),
        new("I V7/V V I (block chords)", Texture.BlockChords, true, AppliedDominant, [], Nowhere),
        new("I V7/V V I (arpeggios)", Texture.Arpeggios, true, AppliedDominant, [], Nowhere),
        new("borrowed iv (block chords)", Texture.BlockChords, true, BorrowedIv, [], Nowhere),
        new("borrowed iv (melody over chords)", Texture.MelodyOverChords, true, BorrowedIv, Notated(BorrowedIvMelody), Nowhere),

        // ---------- to the dominant ----------
        new("to the dominant, a phrase modulation (block chords)", Texture.BlockChords, true, ToTheDominant, [], At(5, 7, true)),
        new("to the dominant, a phrase modulation (arpeggios)", Texture.Arpeggios, true, ToTheDominant, [], At(5, 7, true)),
        new("to the dominant, the tune restated (melody alone)", Texture.MelodyAlone, true, "", Restated(Tune, 7), At(9, 7, true)),
        new("to the dominant through a pivot chord (block chords)", Texture.BlockChords, true, ToTheDominantThroughAPivot, [], At(5, 7, true)),
        new("to the dominant through a pivot chord (melody over chords)", Texture.MelodyOverChords, true, ToTheDominantThroughAPivot, Notated(ToTheDominantThroughAPivotMelody), At(5, 7, true)),

        // ---------- to the subdominant ----------
        new("to the subdominant (block chords)", Texture.BlockChords, true, ToTheSubdominant, [], At(5, 5, true)),
        new("to the subdominant (arpeggios)", Texture.Arpeggios, true, ToTheSubdominant, [], At(5, 5, true)),
        new("to the subdominant, the tune restated (melody alone)", Texture.MelodyAlone, true, "", Restated(Tune, 5), At(9, 5, true)),

        // ---------- to the relative minor, through its dominant with the raised seventh ----------
        new("to the relative minor through its dominant (block chords)", Texture.BlockChords, true, ToTheRelativeMinor, [], At(5, 9, false), TrajectoryMayHearNoChange: true),
        new("to the relative minor through its dominant (melody over chords)", Texture.MelodyOverChords, true, ToTheRelativeMinor, Notated(ToTheRelativeMinorMelody), At(5, 9, false), TrajectoryMayHearNoChange: true),
        new("to the relative minor through its dominant (arpeggios)", Texture.Arpeggios, true, ToTheRelativeMinor, [], At(5, 9, false), TrajectoryMayHearNoChange: true),

        // ---------- to the parallel minor ----------
        new("to the parallel minor (block chords)", Texture.BlockChords, true, ToTheParallelMinor, [], At(5, 0, false)),
        new("to the parallel minor (melody over chords)", Texture.MelodyOverChords, true, ToTheParallelMinor, Notated(ToTheParallelMinorMelody), At(5, 0, false)),

        // ---------- up a semitone, the gear change ----------
        new("up a semitone (block chords)", Texture.BlockChords, true, UpASemitone, [], At(5, 1, true)),
        new("up a semitone (arpeggios)", Texture.Arpeggios, true, UpASemitone, [], At(5, 1, true)),
        new("up a semitone, the tune restated (melody over chords)", Texture.MelodyOverChords, true, UpASemitone, Restated(ShortTune, 1), At(5, 1, true)),
        new("Twinkle, then Twinkle a semitone up (melody alone)", Texture.MelodyAlone, true, "", Restated(Twinkle, 1), At(13, 1, true)),

        // ---------- to the flat submediant ----------
        new("to the flat submediant (block chords)", Texture.BlockChords, true, ToTheFlatSubmediant, [], At(5, 8, true)),
        new("to the flat submediant (arpeggios)", Texture.Arpeggios, true, ToTheFlatSubmediant, [], At(5, 8, true)),

        // ---------- to the chromatic mediant, III major ----------
        new("to the chromatic mediant (block chords)", Texture.BlockChords, true, ToTheChromaticMediant, [], At(5, 4, true)),
        new("to the chromatic mediant (melody over chords)", Texture.MelodyOverChords, true, ToTheChromaticMediant, Notated(ToTheChromaticMediantMelody), At(5, 4, true)),

        // ---------- two modulations ----------
        new("down a fifth and home again (block chords)", Texture.BlockChords, true, DownAFifthAndHome, [], [new(5, 5, true), new(9, 0, true)]),
        new("down a fifth and home again (arpeggios)", Texture.Arpeggios, true, DownAFifthAndHome, [], [new(5, 5, true), new(9, 0, true)]),
        new("eight bars home, eight in the dominant, eight home (block chords)", Texture.BlockChords, true, EightEightEight, [], [new(9, 7, true), new(17, 0, true)]),
        new("eight bars home, eight in the dominant, eight home (melody over chords)", Texture.MelodyOverChords, true, EightEightEight, Notated(EightEightEightMelody), [new(9, 7, true), new(17, 0, true)]),
        new("eight bars home, eight in the dominant, eight home (arpeggios)", Texture.Arpeggios, true, EightEightEight, [], [new(9, 7, true), new(17, 0, true)]),
    ];

    // ---------- the builders ----------

    private readonly record struct Chord(int Root, int[] Intervals, Rational Offset, Rational Duration);

    private static List<Chord> ParseChords(string chords)
    {
        var parsed = new List<Chord>();
        var time = Rational.Zero;

        foreach (var raw in chords.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "|")
            {
                continue;
            }

            var token = raw;
            var duration = Rational.Whole;
            if (token.EndsWith('h'))
            {
                duration = Rational.Half;
                token = token[..^1];
            }

            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            var quality = colon >= 0 ? token[(colon + 1)..] : "";

            parsed.Add(new Chord(root, Qualities[quality], time, duration));
            time += duration;
        }

        return parsed;
    }

    /// <summary>The melody as written, in C.</summary>
    private static NoteEvent[] Notated(string notation) => MusicNotation.Parse(notation);

    /// <summary>The tune, then the same tune again transposed by each of the given semitones in turn.</summary>
    private static NoteEvent[] Restated(string notation, params int[] transpositions)
    {
        var tune = MusicNotation.Parse(notation);
        var length = LengthOf(tune);
        var notes = new List<NoteEvent>(tune);
        var start = length;

        foreach (var semitones in transpositions)
        {
            var from = start;
            notes.AddRange(tune.Select(n => new NoteEvent(n.Pitch + semitones, n.Offset + from, n.Duration, n.Velocity)));
            start += length;
        }

        return [.. notes];
    }

    internal static Rational LengthOf(string chords) =>
        ParseChords(chords).Select(c => c.Offset + c.Duration).DefaultIfEmpty(Rational.Zero).Max();

    internal static Rational LengthOf(IEnumerable<NoteEvent> notes) =>
        notes.Select(n => n.Offset + n.Duration).DefaultIfEmpty(Rational.Zero).Max();
}
