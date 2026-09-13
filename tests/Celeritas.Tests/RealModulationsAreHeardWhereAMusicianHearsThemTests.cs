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
/// on the forty passages in <see cref="RealModulationPassages.All"/> — the nursery tune and the
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
/// <para>
/// A tonicization is not a modulation: an applied dominant, a borrowed chord or the raised
/// leading tone of a minor key does not move the piece. A modulation is heard at the bar where
/// the new key begins — within one bar either side, to allow the pivot chord — and is named by
/// its tonic and mode. The relative minor is the one case where scale content alone cannot tell
/// the two keys apart, so the trajectory may hear nothing there; the detector, which reads the
/// chords and their dominant with the raised seventh, may not.
/// </para>
/// <para>
/// The second table, <see cref="RealModulationPassages.HeldOut"/>, holds the passages a reviewer
/// wrote after both roads were made to agree with the musician on the first forty, and kept out
/// of that work: modulations to the supertonic and the minor dominant, common-tone modulations,
/// a chorale in four voices, an Alberti bass, a waltz, an anacrusis, a bar of silence, a
/// modulation beginning mid-bar, real tunes with their chords, and the ordinary phrases with
/// applied dominants a pop verse is made of. Where a musician hears no settled key — a key every
/// two bars, a descending-fifths sequence, a chromatic scale, a tritone pair — the answer is
/// that neither road reports a modulation: a wrong key is worse than none, and a phrase no key
/// owns names no key. Judged on the library as it stood after the first forty, the roads were
/// wrong on nine of these: they modulated to the dominant in a pop verse whose V7/V–V is a half
/// cadence, named A major in a passage that visits D and E for two bars each, and heard
/// thirty-six modulations in a chromatic scale.
/// </para>
/// <para>
/// The third table, <see cref="RealModulationPassages.ReviewerHeldOut"/>, holds the passages a
/// second reviewer wrote after the second table was answered, and kept out of that work: a
/// sonata exposition, a minor key going to its dominant and its subdominant minor, a rag with
/// its trio in the subdominant, a chorale with a passing tone on every beat, a melody changing
/// key through the German sixth, 6/8 and a jig, a pickup, a dominant pedal, a Dorian tune, and
/// pieces opening on the dominant or analyzed from the wrong key — and, folded in after the
/// reviewer's second look, the seven both roads still got wrong: a hymn closing on a Picardy
/// third, V/ii as a plain triad and a borrowed iv in the new key's first phrase, and melodies
/// with chromatic passing tones or a quarter-note neighbour in every bar of their new key. Judged
/// on the library as it stood after the second table, the roads were wrong on those seven: with
/// only dominant sevenths applied, G began two bars late; the Picardy chord's E natural kept C
/// minor from coming home; and two passing eighths weighed a quarter of their bar, the most a
/// key may lack of a bar it owns, so the melodies named no key or named it four bars late.
/// </para>
/// <para>
/// The fourth table, <see cref="RealModulationPassages.ThirdReviewerHeldOut"/>, holds the
/// passages a third reviewer wrote after the third table was answered: a chorale through the
/// relative major closing on a Picardy third, a minuet with its trio in the parallel minor, a
/// chorus in the subdominant with a borrowed iv, chromatic appoggiaturas, a chromatic walking
/// bass, applied chords resolving deceptively, the Neapolitan before a modulation, the German
/// sixth in a return, chromatic runs and neighbours, a quoted bar of another key, and the minor
/// key's leading tone struck over its tonic — and, folded in after the reviewer's second look,
/// the twelve both roads still got wrong: an appoggiatura struck with the chord on every
/// downbeat, a passing tone inside an arch, a half-note passing tone, the German sixth in the
/// return's second bar, a bar of B flat quoted inside a G phrase, the V/V of a new key that the
/// old key owns, a phrase closing on its tonic seventh, a suspension and a chord tone struck
/// over V/ii, and V/ii and the borrowed iv arpeggiated. Judged on the library as it stood after
/// the third table, an appoggiatura was heard only when struck after its chord, a passing tone
/// only in a run that never turned and never longer than a quarter, a chord the key in force
/// owned was that key's whatever it resolved into, and an arpeggiated chord was so many single
/// notes; the melodies named no key, and the chord passages placed the new key a phrase late.
/// </para>
/// <para>
/// The fifth table, <see cref="RealModulationPassages.FourthReviewerHeldOut"/>, holds the
/// passages a fourth reviewer wrote after the fourth table was answered: repertoire shapes — a
/// Mozart transition over a rising chromatic bass, a hymn in 3/4 closing on a Picardy third, a
/// jazz turnaround with tritone substitutes, a Mixolydian folk tune, escape tones, a piece in
/// 5/4, whole-bar trills, a ground bass — and each of the fourth table's rules pushed the other
/// way; and, folded in after the reviewer's second look, the two both roads still got wrong: a
/// chorale phrase pair going to the dominant and home through V7/ii, and Schubert's way to the
/// flat submediant and back by the common tone C held alone. Judged on the library as it stood
/// after the fourth table, the chorale's two-bar return home was a one-bar tonicization on the
/// detector and nothing on the trajectory, because a stretch at the end shorter than a phrase
/// had to sound its tonic before the close and Dm G7 C does not; and the detector, whose chords
/// sounded until the next chord, heard the C major chord's E natural under the common tone held
/// alone after it and named F minor where the trajectory named A flat.
/// </para>
/// </remarks>
public class RealModulationsAreHeardWhereAMusicianHearsThemTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    public static TheoryData<string> Passages => [.. RealModulationPassages.All.Select(p => p.Name)];

    public static TheoryData<string> HeldOutPassages => [.. RealModulationPassages.HeldOut.Select(p => p.Name)];

    public static TheoryData<string> ReviewerHeldOutPassages => [.. RealModulationPassages.ReviewerHeldOut.Select(p => p.Name)];

    public static TheoryData<string> ThirdReviewerHeldOutPassages => [.. RealModulationPassages.ThirdReviewerHeldOut.Select(p => p.Name)];

    public static TheoryData<string> FourthReviewerHeldOutPassages => [.. RealModulationPassages.FourthReviewerHeldOut.Select(p => p.Name)];

    public static TheoryData<string> TexturesAndHomecomings => [.. TexturesAndHomecomingsPassages.Table.Select(p => p.Name)];

    public static TheoryData<string> AccompanimentTextures => [.. AccompanimentTexturesPassages.Table.Select(p => p.Name)];

    public static TheoryData<string> LoopsSilencesAndCloses => [.. LoopsSilencesAndClosesPassages.Table.Select(p => p.Name)];

    [Theory]
    [MemberData(nameof(Passages))]
    public void TheKeyTrajectoryHearsTheModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheTrajectoryHears(RealModulationPassages.Named(name));

    [Theory]
    [MemberData(nameof(Passages))]
    public void TheModulationDetectorHearsTheModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheDetectorHears(RealModulationPassages.Named(name));

    [Theory]
    [MemberData(nameof(HeldOutPassages))]
    public void TheKeyTrajectoryHearsTheHeldOutModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheTrajectoryHears(RealModulationPassages.HeldOutNamed(name));

    [Theory]
    [MemberData(nameof(HeldOutPassages))]
    public void TheModulationDetectorHearsTheHeldOutModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheDetectorHears(RealModulationPassages.HeldOutNamed(name));

    [Theory]
    [MemberData(nameof(ReviewerHeldOutPassages))]
    public void TheKeyTrajectoryHearsTheSecondReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheTrajectoryHears(RealModulationPassages.ReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(ReviewerHeldOutPassages))]
    public void TheModulationDetectorHearsTheSecondReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheDetectorHears(RealModulationPassages.ReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(ThirdReviewerHeldOutPassages))]
    public void TheKeyTrajectoryHearsTheThirdReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheTrajectoryHears(RealModulationPassages.ThirdReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(ThirdReviewerHeldOutPassages))]
    public void TheModulationDetectorHearsTheThirdReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheDetectorHears(RealModulationPassages.ThirdReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(FourthReviewerHeldOutPassages))]
    public void TheKeyTrajectoryHearsTheFourthReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheTrajectoryHears(RealModulationPassages.FourthReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(FourthReviewerHeldOutPassages))]
    public void TheModulationDetectorHearsTheFourthReviewersModulationsAMusicianHearsInEveryKey(string name) =>
        AssertTheDetectorHears(RealModulationPassages.FourthReviewerHeldOutNamed(name));

    [Theory]
    [MemberData(nameof(TexturesAndHomecomings))]
    public void TheKeyTrajectoryHearsTheTexturesAndHomecomingsAsAMusicianDoesInEveryKey(string name) =>
        AssertTheTrajectoryHears(TexturesAndHomecomingsPassages.Table.Single(p => p.Name == name));

    [Theory]
    [MemberData(nameof(TexturesAndHomecomings))]
    public void TheModulationDetectorHearsTheTexturesAndHomecomingsAsAMusicianDoesInEveryKey(string name) =>
        AssertTheDetectorHears(TexturesAndHomecomingsPassages.Table.Single(p => p.Name == name));

    [Theory]
    [MemberData(nameof(AccompanimentTextures))]
    public void TheKeyTrajectoryHearsTheAccompanimentTexturesAsAMusicianDoesInEveryKey(string name) =>
        AssertTheTrajectoryHears(AccompanimentTexturesPassages.Table.Single(p => p.Name == name));

    [Theory]
    [MemberData(nameof(AccompanimentTextures))]
    public void TheModulationDetectorHearsTheAccompanimentTexturesAsAMusicianDoesInEveryKey(string name) =>
        AssertTheDetectorHears(AccompanimentTexturesPassages.Table.Single(p => p.Name == name));

    [Theory]
    [MemberData(nameof(LoopsSilencesAndCloses))]
    public void TheKeyTrajectoryHearsTheLoopsSilencesAndClosesAsAMusicianDoesInEveryKey(string name) =>
        AssertTheTrajectoryHears(LoopsSilencesAndClosesPassages.Table.Single(p => p.Name == name));

    [Theory]
    [MemberData(nameof(LoopsSilencesAndCloses))]
    public void TheModulationDetectorHearsTheLoopsSilencesAndClosesAsAMusicianDoesInEveryKey(string name) =>
        AssertTheDetectorHears(LoopsSilencesAndClosesPassages.Table.Single(p => p.Name == name));

    private static void AssertTheTrajectoryHears(RealModulationPassages.Passage passage) =>
        AssertHeardInEveryKey(
            passage,
            "the key trajectory",
            (buffer, _) => KeyProfiler.AnalyzeModulations(buffer, passage.TrajectoryWindow, new Rational(1, 1))
                .DetectModulations()
                .Select(m => (m.Position, m.ToKey))
                .ToList(),
            mayHearNoChange: passage.TrajectoryMayHearNoChange);

    private static void AssertTheDetectorHears(RealModulationPassages.Passage passage) =>
        AssertHeardInEveryKey(
            passage,
            "the modulation detector",
            (buffer, openingKey) => ModulationDetector.Analyze(buffer, openingKey).Modulations
                .Where(m => m.Type != ModulationType.Tonicization)
                .Select(m => (m.Offset, m.ToKey))
                .ToList(),
            mayHearNoChange: false);

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
            AssertBuilds(passage);

            var length = passage.Length;
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

    [Fact]
    public void EveryHeldOutPassageIsBuiltAsItIsDescribed()
    {
        // The held-out table's key areas may be shorter than a phrase on purpose — a key every
        // two bars, two bars of C before six of G — so only the build is checked, and that the
        // table is distinct from the first and covers the textures the first does not.
        foreach (var passage in RealModulationPassages.HeldOut)
        {
            AssertBuilds(passage);
        }

        var textures = RealModulationPassages.HeldOut.Select(p => p.Texture).Distinct().ToList();
        Assert.Contains(RealModulationPassages.Texture.FourVoices, textures);
        Assert.Contains(RealModulationPassages.Texture.MelodyOverAlbertiBass, textures);
        Assert.Contains(RealModulationPassages.Texture.ThirtySecondArpeggios, textures);
        Assert.True(RealModulationPassages.HeldOut.Count >= 50);
        Assert.True(RealModulationPassages.HeldOut.Count(p => p.Plan.Length == 0) >= 20);
        Assert.Equal(RealModulationPassages.HeldOut.Count, RealModulationPassages.HeldOut.Select(p => p.Name).Distinct().Count());
        Assert.Empty(RealModulationPassages.HeldOut.Select(p => p.Name).Intersect(RealModulationPassages.All.Select(p => p.Name)));
    }

    [Fact]
    public void EveryPassageOfTheSecondReviewerIsBuiltAsItIsDescribed()
    {
        foreach (var passage in RealModulationPassages.ReviewerHeldOut)
        {
            AssertBuilds(passage);
        }

        var names = RealModulationPassages.ReviewerHeldOut.Select(p => p.Name).ToList();
        Assert.True(RealModulationPassages.ReviewerHeldOut.Count >= 30);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Empty(names.Intersect(RealModulationPassages.All.Select(p => p.Name)));
        Assert.Empty(names.Intersect(RealModulationPassages.HeldOut.Select(p => p.Name)));
    }

    [Fact]
    public void EveryPassageOfTheThirdReviewerIsBuiltAsItIsDescribed()
    {
        foreach (var passage in RealModulationPassages.ThirdReviewerHeldOut)
        {
            AssertBuilds(passage);
        }

        var names = RealModulationPassages.ThirdReviewerHeldOut.Select(p => p.Name).ToList();
        Assert.True(RealModulationPassages.ThirdReviewerHeldOut.Count >= 18);
        Assert.Equal(names.Count, names.Distinct().Count());
        var earlier = RealModulationPassages.All.Concat(RealModulationPassages.HeldOut).Concat(RealModulationPassages.ReviewerHeldOut).Select(p => p.Name);
        Assert.Empty(names.Intersect(earlier));
    }

    [Fact]
    public void EveryPassageOfTheFourthReviewerIsBuiltAsItIsDescribed()
    {
        foreach (var passage in RealModulationPassages.FourthReviewerHeldOut)
        {
            AssertBuilds(passage);
        }

        var names = RealModulationPassages.FourthReviewerHeldOut.Select(p => p.Name).ToList();
        Assert.True(RealModulationPassages.FourthReviewerHeldOut.Count >= 20);
        Assert.Equal(names.Count, names.Distinct().Count());
        var earlier = RealModulationPassages.All.Concat(RealModulationPassages.HeldOut)
            .Concat(RealModulationPassages.ReviewerHeldOut).Concat(RealModulationPassages.ThirdReviewerHeldOut).Select(p => p.Name);
        Assert.Empty(names.Intersect(earlier));
    }

    [Fact]
    public void EveryTextureAndHomecomingPassageIsBuiltAsItIsDescribed()
    {
        foreach (var passage in TexturesAndHomecomingsPassages.Table)
        {
            AssertBuilds(passage);
        }

        var names = TexturesAndHomecomingsPassages.Table.Select(p => p.Name).ToList();
        Assert.True(TexturesAndHomecomingsPassages.Table.Count >= 35);
        Assert.Equal(names.Count, names.Distinct().Count());
        var earlier = RealModulationPassages.All.Concat(RealModulationPassages.HeldOut).Concat(RealModulationPassages.ReviewerHeldOut)
            .Concat(RealModulationPassages.ThirdReviewerHeldOut).Concat(RealModulationPassages.FourthReviewerHeldOut).Select(p => p.Name);
        Assert.Empty(names.Intersect(earlier));
    }

    [Fact]
    public void EveryAccompanimentTexturePassageIsBuiltAsItIsDescribed()
    {
        foreach (var passage in AccompanimentTexturesPassages.Table)
        {
            AssertBuilds(passage);
        }

        var names = AccompanimentTexturesPassages.Table.Select(p => p.Name).ToList();
        Assert.True(AccompanimentTexturesPassages.Table.Count >= 37);
        Assert.Equal(names.Count, names.Distinct().Count());
        var earlier = RealModulationPassages.All.Concat(RealModulationPassages.HeldOut).Concat(RealModulationPassages.ReviewerHeldOut)
            .Concat(RealModulationPassages.ThirdReviewerHeldOut).Concat(RealModulationPassages.FourthReviewerHeldOut)
            .Concat(TexturesAndHomecomingsPassages.Table).Select(p => p.Name);
        Assert.Empty(names.Intersect(earlier));
    }

    [Fact]
    public void EveryLoopSilenceAndClosePassageIsBuiltAsItIsDescribed()
    {
        foreach (var passage in LoopsSilencesAndClosesPassages.Table)
        {
            AssertBuilds(passage);
        }

        var names = LoopsSilencesAndClosesPassages.Table.Select(p => p.Name).ToList();
        Assert.True(LoopsSilencesAndClosesPassages.Table.Count >= 20);
        Assert.Equal(names.Count, names.Distinct().Count());
        var earlier = RealModulationPassages.All.Concat(RealModulationPassages.HeldOut).Concat(RealModulationPassages.ReviewerHeldOut)
            .Concat(RealModulationPassages.ThirdReviewerHeldOut).Concat(RealModulationPassages.FourthReviewerHeldOut)
            .Concat(TexturesAndHomecomingsPassages.Table).Concat(AccompanimentTexturesPassages.Table).Select(p => p.Name);
        Assert.Empty(names.Intersect(earlier));
    }

    private static void AssertBuilds(RealModulationPassages.Passage passage)
    {
        for (var tonic = 0; tonic < 12; tonic++)
        {
            using var buffer = passage.Build(tonic);
            Assert.True(buffer.Count > 0, passage.Name);
            for (var i = 0; i < buffer.Count; i++)
            {
                var pitch = buffer.Get(i).Pitch;
                if (pitch != MusicNotation.RestPitch)
                {
                    Assert.InRange(pitch, 0, 127);
                }
            }
        }

        if (passage.Texture is RealModulationPassages.Texture.MelodyOverChords or RealModulationPassages.Texture.MelodyOverAlbertiBass)
        {
            Assert.Equal(RealModulationPassages.LengthOf(passage.Chords), RealModulationPassages.LengthOf(passage.Melody));
        }
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
            var openingKey = new KeySignature((byte)((tonic + passage.OpeningRoot) % 12), passage.OpeningIsMajor);
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
/// by <see cref="Passage.Build"/>. Chords are tokens of the form <c>root[:quality][h|q|t]</c>:
/// the root as semitones above the tonic, the quality one of <c>m 7 m7 maj7 dim dim7 m7b5</c>
/// (none for a major triad), a trailing <c>h</c> for a half-bar chord, <c>q</c> for a quarter,
/// <c>t</c> for three quarters — a bar of 3/4; <c>~n</c> is a single note <c>n</c> semitones
/// above the chord register, a common tone held alone; bar lines are ignored except by the
/// thirty-second-note texture, which cycles through the chords of each bar-line-separated
/// segment. Melodies are notation in C. The plan is where a musician writes each new key: the
/// whole-note position where it begins, its tonic as semitones above the opening tonic, and its
/// mode.
/// </summary>
internal static class RealModulationPassages
{
    internal enum Texture
    {
        BlockChords,
        Arpeggios,
        MelodyAlone,
        MelodyOverChords,

        /// <summary>Several notated voices sounding together, a chorale; <see cref="Passage.Melody"/> holds them all.</summary>
        FourVoices,

        /// <summary>The chords as an Alberti bass in eighths — root, fifth, third, fifth — under the melody an octave up.</summary>
        MelodyOverAlbertiBass,

        /// <summary>The chords of each bar-line-separated segment, their tones cycled in thirty-second notes for the segment's length.</summary>
        ThirtySecondArpeggios,
    }

    /// <summary>A key a musician hears the passage move to, at the whole-note position where it begins.</summary>
    internal readonly record struct PlannedModulation(Rational Position, int ToRoot, bool ToMajor)
    {
        /// <summary>The key beginning in bar <paramref name="bar"/>, counting from one.</summary>
        public PlannedModulation(int bar, int toRoot, bool toMajor)
            : this(new Rational(bar - 1, 1), toRoot, toMajor)
        {
        }

        public override string ToString() => $"{ToRoot}{(ToMajor ? "M" : "m")}@{Position}";
    }

    /// <summary>
    /// One passage in one texture. <paramref name="Chords"/> is empty for a melody alone and
    /// <paramref name="Melody"/> is empty for block chords and arpeggios. <paramref name="OpeningRoot"/>
    /// is the opening key's tonic as semitones above the build tonic — nine for a passage written
    /// in A minor and built in C — and <paramref name="TrajectoryWindow"/> the window the
    /// trajectory road reads at, two whole notes unless the passage is about the window.
    /// <paramref name="Anacrusis"/> shifts everything later, so that the bars do not begin on
    /// the whole notes.
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
        public int OpeningRoot { get; init; }

        public Rational TrajectoryWindow { get; init; } = new(2, 1);

        public Rational Anacrusis { get; init; } = Rational.Zero;

        /// <summary>How long the passage is, in whole notes.</summary>
        public Rational Length => Chords.Length == 0 ? LengthOf(Melody) : LengthOf(Chords);

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
                            ChordRegister + chord.Root + tones[Rise[k % 8]] + tonic,
                            chord.Offset + (Rational.Eighth * k),
                            Rational.Eighth));
                    }
                }
            }

            if (Texture == Texture.MelodyOverAlbertiBass)
            {
                foreach (var chord in ParseChords(Chords))
                {
                    // Root, fifth, third, fifth in eighths; a seventh chord's fourth eighth is
                    // its seventh.
                    int[] pattern = chord.Intervals.Length == 4
                        ? [chord.Intervals[0], chord.Intervals[2], chord.Intervals[1], chord.Intervals[3]]
                        : [chord.Intervals[0], chord.Intervals[2], chord.Intervals[1], chord.Intervals[2]];
                    var eighths = (int)(chord.Duration / Rational.Eighth).ToDouble();
                    for (var k = 0; k < eighths; k++)
                    {
                        notes.Add(new NoteEvent(
                            ChordRegister + chord.Root + pattern[k % 4] + tonic,
                            chord.Offset + (Rational.Eighth * k),
                            Rational.Eighth));
                    }
                }
            }

            if (Texture == Texture.ThirtySecondArpeggios)
            {
                var thirtySecond = new Rational(1, 32);
                var time = Rational.Zero;
                foreach (var segment in Chords.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var chords = ParseChords(segment);
                    var tones = chords.SelectMany(c => c.Intervals.Take(3).Select(i => c.Root + i)).ToArray();
                    var count = (int)(LengthOf(segment) / thirtySecond).ToDouble();
                    for (var k = 0; k < count; k++)
                    {
                        notes.Add(new NoteEvent(ChordRegister + 12 + tones[k % tones.Length] + tonic, time + (thirtySecond * k), thirtySecond));
                    }

                    time += LengthOf(segment);
                }
            }

            if (Texture is Texture.MelodyAlone or Texture.MelodyOverChords or Texture.FourVoices)
            {
                foreach (var note in Melody)
                {
                    notes.Add(note.Pitch == MusicNotation.RestPitch
                        ? note
                        : new NoteEvent(note.Pitch + tonic, note.Offset, note.Duration, note.Velocity));
                }
            }

            if (Texture == Texture.MelodyOverAlbertiBass)
            {
                foreach (var note in Melody)
                {
                    notes.Add(new NoteEvent(note.Pitch + 12 + tonic, note.Offset, note.Duration, note.Velocity));
                }
            }

            if (Anacrusis != Rational.Zero)
            {
                for (var i = 0; i < notes.Count; i++)
                {
                    notes[i] = new NoteEvent(notes[i].Pitch, notes[i].Offset + Anacrusis, notes[i].Duration, notes[i].Velocity);
                }
            }

            var buffer = new NoteBuffer(notes.Count);
            buffer.AddRange(notes.OrderBy(n => n.Offset).ThenBy(n => n.Pitch).ToArray());
            return buffer;
        }
    }

    internal static Passage Named(string name) => All.Single(p => p.Name == name);

    internal static Passage HeldOutNamed(string name) => HeldOut.Single(p => p.Name == name);

    internal static Passage ReviewerHeldOutNamed(string name) => ReviewerHeldOut.Single(p => p.Name == name);

    internal static Passage ThirdReviewerHeldOutNamed(string name) => ThirdReviewerHeldOut.Single(p => p.Name == name);

    internal static Passage FourthReviewerHeldOutNamed(string name) => FourthReviewerHeldOut.Single(p => p.Name == name);

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

    // ---------- the held-out tunes ----------

    /// <summary>Four bars in C with both the fourth and the seventh degree in the first bar, as <see cref="Tune"/> has.</summary>
    private const string ShortTuneWithLeadingTone = "E4/4 F4/4 D4/4 B3/4 | C4/4 E4/4 G4/4 E4/4 | F4/4 A4/4 G4/4 F4/4 | E4/4 D4/4 C4/2";

    /// <summary>An A minor melody with its G sharp in the cadence of bar 3, then a C major melody from bar 5.</summary>
    private const string MinorThenRelativeMajorMelody =
        "A4/4 B4/4 C5/4 D5/4 | E5/2 D5/4 C5/4 | B4/4 G#4/4 B4/4 D5/4 | C5/2 A4/2 | "
        + "C5/4 D5/4 E5/4 F5/4 | G5/2 F5/4 E5/4 | D5/4 B4/4 D5/4 F5/4 | E5/2 C5/2";

    private const string ToTheSupertonic = "0 5 7 0 | 9:7 2:m 7:m 9:7 2:m";

    private const string ToTheSupertonicMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "C#5/4 E5/4 A4/2 | F5/4 D5/4 A4/2 | Bb4/4 D5/4 G4/2 | C#5/4 E5/4 G5/2 | F5/4 E5/4 D5/2";

    private const string ChoraleSoprano = "E5/4 F5/4 E5/4 D5/4 | C5/4 C5/4 D5/4 D5/4 | E5/4 F5/4 D5/4 E5/4 | C5/1 | B4/4 C5/4 B4/4 A4/4 | G4/4 G4/4 A4/4 A4/4 | B4/4 C5/4 A4/4 B4/4 | G4/1";
    private const string ChoraleAlto = "G4/4 A4/4 G4/4 G4/4 | E4/4 E4/4 F4/4 F4/4 | G4/4 A4/4 G4/4 G4/4 | E4/1 | D4/4 E4/4 D4/4 F#4/4 | B3/4 B3/4 C4/4 C4/4 | D4/4 E4/4 D4/4 D4/4 | B3/1";
    private const string ChoraleTenor = "C4/4 C4/4 C4/4 B3/4 | G3/4 A3/4 A3/4 B3/4 | C4/4 C4/4 B3/4 C4/4 | G3/1 | G3/4 G3/4 G3/4 A3/4 | D3/4 E3/4 E3/4 F#3/4 | G3/4 G3/4 F#3/4 G3/4 | D3/1";
    private const string ChoraleBass = "C3/4 F3/4 C3/4 G3/4 | C3/4 A2/4 D3/4 G2/4 | C3/4 F3/4 G3/4 C3/4 | C3/1 | G2/4 C3/4 G2/4 D3/4 | G2/4 E2/4 A2/4 D2/4 | G2/4 C3/4 D3/4 G2/4 | G2/1";

    /// <summary><see cref="ToTheDominant"/> with a closing bar of G under the melody's last note.</summary>
    private const string AlbertiChords = "0 5 7:7 0 | 7 2:7 7 0h 2:7h 7 7";

    private const string AlbertiMelody =
        "E4/4 D4/4 C4/4 D4/4 | F4/4 E4/4 F4/4 A4/4 | G4/4 F4/4 D4/4 B3/4 | C4/2 E4/4 G4/4 | "
        + "G4/4 A4/4 B4/4 G4/4 | F#4/4 A4/4 C5/4 A4/4 | B4/4 D5/4 B4/4 G4/4 | C5/4 E5/8 D5/8 C5/4 A4/4 | B4/4 G4/4 F#4/4 A4/4 | G4/1";

    private const string RaisedFourthPassing =
        "C4/4 D4/4 E4/4 F4/4 | G4/4 A4/4 G4/4 F4/4 | E4/4 F#4/8 G4/8 G4/2 | A4/4 G4/4 F4/4 E4/4 | "
        + "D4/4 E4/4 F4/4 G4/4 | E4/4 C4/4 D4/4 E4/4 | D4/4 F4/4 E4/4 D4/4 | C4/1";

    private const string RaisedFourthOnADownbeat =
        "E4/4 F4/4 D4/4 B3/4 | C4/4 E4/4 G4/4 E4/4 | F4/4 A4/4 G4/4 F4/4 | E4/4 D4/4 C4/2 | "
        + "G4/4 F4/4 E4/4 D4/4 | F#4/4 G4/4 D4/4 G4/4 | A4/4 B4/4 C5/4 D5/4 | C5/1";

    private const string HappyBirthday =
        "G4/8 G4/8 A4/4 G4/4 | C5/4 B4/2 | G4/8 G4/8 A4/4 G4/4 | D5/4 C5/2 | G4/8 G4/8 G5/4 E5/4 | C5/4 B4/4 A4/4 | F5/8 F5/8 E5/4 C5/4 | D5/4 C5/2";

    private const string HappyBirthdayChords = "0t 7t 7t 0t 0t 5t 5t 0t";

    private const string AmazingGrace =
        "C4/4 | E4/4 G4/8 E4/8 G4/4 | E4/4 D4/4 C4/4 | A3/4 C4/4 E4/8 C4/8 | E4/4 D4/4 G4/4 | G4/2 E4/8 G4/8 | E4/4 D4/4 C4/4 | A3/4 C4/4 E4/8 C4/8 | E4/4 D4/4 C4/4 | C4/2";

    private const string AmazingGraceChords = "0q 0t 0t 5t 0t 7t 0t 5t 7t 0h";

    private const string Greensleeves =
        "A4/4 | C5/2 D5/4 | E5/4. F5/8 E5/4 | D5/2 B4/4 | G4/4. A4/8 B4/4 | C5/2 A4/4 | A4/4. G#4/8 A4/4 | B4/2 G#4/4 | E4/2 A4/4 | "
        + "C5/2 D5/4 | E5/4. F5/8 E5/4 | D5/2 B4/4 | G4/4. A4/8 B4/4 | C5/4. B4/8 A4/4 | G#4/4. F#4/8 G#4/8 A4/8 | A4/2.";

    private const string GreensleevesChords = "9:mq 9:mt 0t 7t 9:mt 5t 4:7t 4:7t 9:mt 9:mt 0t 7t 9:mt 5t 4:7t 9:mt";

    private const string SilentNight =
        "G4/4. A4/8 G4/4 | E4/2. | G4/4. A4/8 G4/4 | E4/2. | D5/2 D5/4 | B4/2. | C5/2 C5/4 | G4/2. | A4/2 A4/4 | C5/4. B4/8 A4/4 | G4/4. A4/8 G4/4 | E4/2. | A4/2 A4/4 | C5/4. B4/8 A4/4 | G4/4. A4/8 G4/4 | E4/2.";

    private const string SilentNightChords = "0t 0t 0t 0t 7t 7t 0t 0t 5t 5t 0t 0t 5t 5t 0t 0t";

    /// <summary>A tune in C with a rest in every bar, then the same in D flat.</summary>
    private const string TuneWithRests =
        "C4/4 E4/4 G4/4 R/4 | F4/4 A4/4 R/2 | G4/4 B4/4 D5/4 R/4 | C5/1 | Db4/4 F4/4 Ab4/4 R/4 | Gb4/4 Bb4/4 R/2 | Ab4/4 C5/4 Eb5/4 R/4 | Db5/1";

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

    /// <summary>A key beginning on <paramref name="beat"/> (a quarter, counting from one) of <paramref name="bar"/> (counting from one).</summary>
    private static PlannedModulation AtBeat(int bar, int beat, int toRoot, bool toMajor) =>
        new(new Rational(((bar - 1) * 4) + beat - 1, 4), toRoot, toMajor);

    private static Passage Block(string name, string chords, PlannedModulation[] plan) =>
        new(name, Texture.BlockChords, true, chords, [], plan);

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

    /// <summary>
    /// The reviewer's passages, held out from the work on <see cref="All"/>. Positions in the
    /// plans are the musician's, in whole notes; a passage with an empty plan is one a musician
    /// hears in one key throughout, or in no settled key at all — either way, neither road may
    /// report a modulation.
    /// </summary>
    internal static IReadOnlyList<Passage> HeldOut { get; } =
    [
        // ---------- to keys the first table does not visit ----------
        Block("to the supertonic through its dominant (block chords)", ToTheSupertonic, At(5, 2, false)),
        new("to the supertonic through its dominant (melody over chords)", Texture.MelodyOverChords, true, ToTheSupertonic, Notated(ToTheSupertonicMelody), At(5, 2, false)),
        new("to the supertonic through its dominant (arpeggios)", Texture.Arpeggios, true, ToTheSupertonic, [], At(5, 2, false)),
        Block("to the minor dominant (block chords)", "0 5 7 0 | 7:m 0:m 2:7 7:m 0:m 2:7 7:m", At(5, 7, false)),
        Block("to the tritone key (block chords)", "0 5 7 0 | 6 11 1 6", At(5, 6, true)),
        Block("to the tritone key and home (block chords)", "0 5 7 0 | 6 11 1 6 | 0 5 7 0", [new(5, 6, true), new(9, 0, true)]),
        Block("the gear change through the new key's dominant seventh (block chords)", "0 5 7 0 | 8:7h 1h 6 8:7 1 1 6 1", At(5, 1, true)),

        // ---------- common-tone modulations, one note held alone for a bar ----------
        Block("common tone to the submediant major, the third held alone (block chords)", "0 5 7 0 | ~16 | 9 2 4:7 9 2 4:7 9", At(6, 9, true)),
        Block("common tone to the flat submediant, the tonic held alone (block chords)", "0 5 7 0 | ~12 | 8 1 3:7 8 1 3:7 8", At(6, 8, true)),

        // ---------- textures the first table does not have ----------
        new("chorale in four voices, C then G (four voices)", Texture.FourVoices, true, "", Voices(ChoraleSoprano, ChoraleAlto, ChoraleTenor, ChoraleBass), At(5, 7, true)),
        new("to the dominant over an Alberti bass (melody over Alberti bass)", Texture.MelodyOverAlbertiBass, true, AlbertiChords, Notated(AlbertiMelody), At(5, 7, true)),
        new("up a semitone in thirty-second-note arpeggios (thirty-second arpeggios)", Texture.ThirtySecondArpeggios, true, "0 5 7 0 | 1 6 8 1", [], At(5, 1, true)),
        new("a waltz, five bars of C then G (block chords in 3/4)", Texture.BlockChords, true, "0t 5t 7t 0t 0t | 7t 2:7t 7t 0t 2:7t 7t 7t", [], [AtBeat(4, 4, 7, true)]),
        new("Twinkle, then Twinkle a semitone up, after a quarter-note anacrusis (melody alone)", Texture.MelodyAlone, true, "", Restated(Twinkle, 1), [AtBeat(13, 4, 1, true)]) { Anacrusis = new Rational(3, 4) },
        new("D flat begins mid-bar (half-bar chords)", Texture.BlockChords, true, "0h 5h 7h 0h 0h 5h 7:7h 0h | 0h 5h 7h 1h | 6h 8h 1h 1h 6h 8h 1h 1h", [], [AtBeat(6, 3, 1, true)]),
        new("the tune in C, a bar of silence, the tune in D flat (melody alone)", Texture.MelodyAlone, true, "", RestatedAfter(ShortTuneWithLeadingTone, new Rational(1, 1), 1), [new(new Rational(5, 1), 1, true)]),
        new("the tune with rests in C, then in D flat (melody alone)", Texture.MelodyAlone, true, "", Notated(TuneWithRests), At(5, 1, true)),
        new("Ode to Joy in C, then in D (melody alone)", Texture.MelodyAlone, true, "", Restated(OdeToJoy, 2), At(9, 2, true)),

        // ---------- the trajectory read at other windows ----------
        Block("eight bars home, eight in the dominant, eight home, read at an eight-bar window (block chords)", EightEightEight, [new(9, 7, true), new(17, 0, true)]) with { TrajectoryWindow = new Rational(8, 1) },
        Block("to the dominant, read at a one-bar window (block chords)", ToTheDominant, At(5, 7, true)) with { TrajectoryWindow = new Rational(1, 1) },

        // ---------- minor keys ----------
        new("minor to its relative major, four bars then eight (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 3 8 10 3 3 8 10 3", [], At(5, 3, true)),
        new("minor to its relative major (melody alone)", Texture.MelodyAlone, false, "", Notated(MinorThenRelativeMajorMelody), At(5, 0, true)) { OpeningRoot = 9 },
        new("minor, its relative major, minor again, eight bars each (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m 0:m 5:m 7:7 0:m | 3 8 10 3 3 8 10 3 | 0:m 5:m 7:7 0:m 0:m 5:m 7:7 0:m", [], [new(9, 3, true), new(17, 0, false)]),
        new("minor with the Neapolitan and V/iv (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 0:7 5:m 1 7:7 | 0:m 8 5:m 7:7 | 0:m 0:m", [], Nowhere),
        new("minor closing on a Picardy third (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 8 5:m 7:7 0", [], Nowhere),
        new("Greensleeves (melody over chords in 3/4)", Texture.MelodyOverChords, false, GreensleevesChords, Notated(Greensleeves), Nowhere) { OpeningRoot = 9 },

        // ---------- several key areas ----------
        Block("four bars each of C, F, G and C (block chords)", "0 5 7:7 0 | 5 10 0:7 5 | 7 0 2:7 7 | 0 5 7:7 0", [new(5, 5, true), new(9, 7, true), new(13, 0, true)]),
        Block("alternating four-bar areas, C G C G (block chords)", "0 5 7 0 | 7 0 2:7 7 | 0 5 7:7 0 | 7 0 2:7 7", [new(5, 7, true), new(9, 0, true), new(13, 7, true)]),
        Block("alternating four-bar areas, C G C G C G (block chords)", "0 5 7 0 | 7 0 2:7 7 | 0 5 7 0 | 7 0 2:7 7 | 0 5 7 0 | 7 0 2:7 7", [new(5, 7, true), new(9, 0, true), new(13, 7, true), new(17, 0, true), new(21, 7, true)]),
        Block("four bars of C, then eight of G whose F sharp falls late (block chords)", "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7", At(5, 7, true)),
        Block("AABA, the bridge in the chromatic mediant (block chords)", "0 9:m 2:m 7:7 0 9:m 2:m 7:7 | 0 9:m 2:m 7:7 0 5 7:7 0 | 4 1:m 6:m 11:7 4 1:m 6:m 11:7 | 0 9:m 2:m 7:7 0 5 7:7 0", [new(17, 4, true), new(25, 0, true)]),
        Block("two bars of C, then G for six (block chords)", "0 7 | 2:7 7 0 2:7 7 7", [new(3, 7, true)]),
        Block("four bars of C, then five of D flat (block chords)", "0 5 7 0 | 1 6 8 1 1", At(5, 1, true)),
        Block("four bars of C, four of D flat, then two of its dominant (block chords)", "0 5 7 0 | 1 6 8 1 8 8", At(5, 1, true)),

        // ---------- applied dominants in ordinary phrases: no modulation ----------
        Block("a chain of secondary dominants (block chords)", "0 4:7 9:7 2:7 7:7 0 0 0", Nowhere),
        new("a chain of secondary dominants (arpeggios)", Texture.Arpeggios, true, "0 4:7 9:7 2:7 7:7 0 0 0", [], Nowhere),
        Block("sixteen bars of C with an applied dominant in nearly every bar (block chords)", "0 2:7 7 0 | 4:7 9:m 9:7 2:m | 7:7 0 11:7 4:m | 5 2:7 7:7 0", Nowhere),
        Block("C E7 Am D7 | G C A7 Dm | G7 C F D7 | G G7 C C (block chords)", "0 4:7 9:m 2:7 | 7 0 9:7 2:m | 7:7 0 5 2:7 | 7 7:7 0 0", Nowhere),
        Block("a pop verse, C Am D7 G | C A7 Dm G7, twice (block chords)", "0 9:m 2:7 7 | 0 9:7 2:m 7:7 | 0 9:m 2:7 7 | 0 9:7 2:m 7:7", Nowhere),
        Block("sixteen varied bars in C (block chords)", "0 9:m 5 7 | 0 9:m 2:m 7:7 | 0 4:m 5 7 | 0 5:m 7:7 0 | 9:m 5 0 7 | 0 5 2:7 7:7 | 0 5:m 7:7 0 | 5 7:7 0 0", Nowhere),
        Block("I vi V/V V | I IV V7 I (block chords)", "0 9:m 2:7 7 | 0 5 7:7 0", Nowhere),
        Block("I IV V/V V | I IV V7 I (block chords)", "0 5 2:7 7 | 0 5 7:7 0", Nowhere),
        Block("I I V/V V | I IV V I (block chords)", "0 0 2:7 7 | 0 5 7 0", Nowhere),
        Block("I V/vi vi IV | V7 I I I (block chords)", "0 4:7 9:m 5 | 7:7 0 0 0", Nowhere),
        Block("I V/ii ii V7, twice (block chords)", "0 9:7 2:m 7:7 | 0 9:7 2:m 7:7", Nowhere),
        Block("I V/IV IV iv | I V7 I I (block chords)", "0 0:7 5 5:m | 0 7:7 0 0", Nowhere),
        Block("I IV V V/vi | vi V/V V I (block chords)", "0 5 7 4:7 | 9:m 2:7 7 0", Nowhere),
        Block("two half cadences through V/V (block chords)", "0 5 7:7 0 | 0 5 2:7 7 | 0 5 7:7 0 | 0 5 2:7 7", Nowhere),
        Block("I V I V with one V/V (block chords)", "0 7 0 7 | 0 7 2:7 7 | 0 7 0 7 | 0 7:7 0 0", Nowhere),
        Block("I vi IV V with one V/V (block chords)", "0 9:m 5 7 | 0 9:m 2:7 7 | 0 9:m 5 7 | 0 5 7:7 0", Nowhere),
        Block("I vi IV V | I vi V/V V, a half cadence (block chords)", "0 9:m 5 7 | 0 9:m 2:7 7", Nowhere),
        Block("I IV V I | vi IV V/V V, a half cadence (block chords)", "0 5 7 0 | 9:m 5 2:7 7", Nowhere),
        Block("a half cadence through V/V, then two phrases home (block chords)", "0 5 7:7 0 | 0 5 2:7 7 | 0 5 7:7 0 | 0 5 7:7 0", Nowhere),
        Block("C F G C D7 | G C F G7 C (block chords)", "0 5h 7h 0 2:7 | 7 0h 5h 7:7 0", Nowhere),
        new("a melody touching the raised fourth once, as a passing eighth (melody alone)", Texture.MelodyAlone, true, "", Notated(RaisedFourthPassing), Nowhere),
        new("a melody with the raised fourth once on a downbeat (melody alone)", Texture.MelodyAlone, true, "", Notated(RaisedFourthOnADownbeat), Nowhere),

        // ---------- real tunes with their chords: no modulation ----------
        new("Happy Birthday (melody over chords in 3/4)", Texture.MelodyOverChords, true, HappyBirthdayChords, Notated(HappyBirthday), Nowhere),
        new("Amazing Grace (melody over chords in 3/4)", Texture.MelodyOverChords, true, AmazingGraceChords, Notated(AmazingGrace), Nowhere),
        new("Silent Night (melody over chords in 3/4)", Texture.MelodyOverChords, true, SilentNightChords, Notated(SilentNight), Nowhere),

        // ---------- no settled key: a wrong key is worse than none ----------
        Block("a new key every two bars, C D E F sharp (half-bar chords)", "0h 5h 7h 0h | 2h 7h 9h 2h | 4h 9h 11h 4h | 6h 11h 1h 6h", Nowhere),
        Block("a descending-fifths sequence, one chord a bar (block chords)", "0 5 10 3 8 1 6 11 4 9 2 7 0", Nowhere),
        Block("C and G flat alternating (block chords)", "0 6 0 6 0 6 0 6", Nowhere),
        new("a chromatic scale in eighths (melody alone)", Texture.MelodyAlone, true, "", ChromaticScale(64), Nowhere),
        Block("one chord for eight bars (block chords)", "0 0 0 0 0 0 0 0", Nowhere),
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
            else if (token.EndsWith('q'))
            {
                duration = Rational.Quarter;
                token = token[..^1];
            }
            else if (token.EndsWith('t'))
            {
                duration = new Rational(3, 4);
                token = token[..^1];
            }

            if (token.StartsWith('~'))
            {
                // A single note, held alone.
                parsed.Add(new Chord(int.Parse(token[1..]), [0], time, duration));
                time += duration;
                continue;
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

    /// <summary>Several notated voices, together.</summary>
    private static NoteEvent[] Voices(params string[] voices) => [.. voices.SelectMany(v => MusicNotation.Parse(v))];

    /// <summary>The tune, then the same tune again transposed by each of the given semitones in turn.</summary>
    private static NoteEvent[] Restated(string notation, params int[] transpositions) =>
        RestatedAfter(notation, Rational.Zero, transpositions);

    /// <summary>
    /// The tune, then — after <paramref name="gap"/> of silence each time — the same tune again
    /// transposed by each of the given semitones in turn.
    /// </summary>
    private static NoteEvent[] RestatedAfter(string notation, Rational gap, params int[] transpositions)
    {
        var tune = MusicNotation.Parse(notation);
        var length = LengthOf(tune);
        var notes = new List<NoteEvent>(tune);
        var start = length;

        foreach (var semitones in transpositions)
        {
            var from = start + gap;
            notes.AddRange(tune.Select(n => new NoteEvent(n.Pitch + semitones, n.Offset + from, n.Duration, n.Velocity)));
            start = from + length;
        }

        return [.. notes];
    }

    /// <summary>A chromatic scale from middle C in eighths, <paramref name="count"/> notes long.</summary>
    private static NoteEvent[] ChromaticScale(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new NoteEvent(60 + (i % 12), Rational.Eighth * i, Rational.Eighth))];

    internal static Rational LengthOf(string chords) =>
        ParseChords(chords).Select(c => c.Offset + c.Duration).DefaultIfEmpty(Rational.Zero).Max();

    internal static Rational LengthOf(IEnumerable<NoteEvent> notes) =>
        notes.Select(n => n.Offset + n.Duration).DefaultIfEmpty(Rational.Zero).Max();

    // ---------- the second reviewer's passages ----------

    /// <summary>A jig in 6/8 — four bars of three quarters each, three whole notes — in C, with the fourth and seventh degrees in its first three bars.</summary>
    private const string Jig =
        "C4/8 E4/8 G4/8 C5/4 G4/8 | A4/8 G4/8 F4/8 E4/4 C4/8 | D4/8 F4/8 A4/8 G4/4 B3/8 | C4/4. C4/4.";

    /// <summary>Eight bars of a D Dorian folk tune: the raised sixth, B natural, in every other bar, and no C sharp anywhere.</summary>
    private const string DorianTune =
        "D4/4 E4/4 F4/4 G4/4 | A4/4 B4/4 G4/4 B4/4 | A4/4 F4/4 D4/4 F4/4 | G4/4 B4/4 D5/2 | "
        + "A4/4 G4/4 F4/4 E4/4 | D4/4 B4/4 G4/4 B4/4 | C5/4 E4/4 G4/4 E4/4 | D4/1";

    private const string DorianChords = "2:m 7 2:m 7 | 2:m 7 0 2:m";

    /// <summary>Four bars in C, then a bar arpeggiating A flat–C–E flat–G flat — the German sixth of C, heard as V7 of D flat — then four bars in D flat.</summary>
    private const string EnharmonicPivotMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "Ab4/4 C5/4 Eb5/4 Gb5/4 | "
        + "F4/4 Ab4/4 Db5/2 | Bb4/4 Gb4/4 Db5/2 | Eb5/4 C5/4 Ab4/2 | F4/4 Eb4/4 Db4/2";

    /// <summary>Four bars in C, then four in G — diatonic, the F sharp in bars 6, 7 and 8; the control for the three that follow.</summary>
    private const string GAreaDiatonic =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "D5/4 C5/4 B4/4 A4/4 | G4/4 F#4/4 G4/4 A4/4 | B4/4 D5/4 F#5/4 G5/4 | F#5/4 A5/4 G5/2";

    /// <summary>The same, with one chromatic passing eighth in each of the new key's first three bars: B flat, G sharp, C sharp.</summary>
    private const string GAreaOnePassingEighthPerBar =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "D5/4 C5/8 B4/8 Bb4/8 A4/8 A4/4 | G4/4 F#4/4 G4/8 G#4/8 A4/4 | B4/4 C5/8 C#5/8 D5/4 F#5/4 | G5/4 F#5/4 G5/2";

    /// <summary>The same, with one chromatic passing eighth (B flat) in the new key's first bar only.</summary>
    private const string GAreaOnePassingEighth =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "D5/4 C5/8 B4/8 Bb4/8 A4/8 A4/4 | G4/4 F#4/4 G4/4 A4/4 | B4/4 D5/4 F#5/4 G5/4 | F#5/4 A5/4 G5/2";

    /// <summary>As the one-per-bar melody, but bar 7's passing eighth is the old key's F natural: E F F sharp G.</summary>
    private const string GAreaPassingFNatural =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "D5/4 C5/8 B4/8 Bb4/8 A4/8 A4/4 | G4/4 F#4/4 G4/8 G#4/8 A4/4 | B4/4 E5/8 F5/8 F#5/8 G5/8 F#5/4 | G5/4 F#5/4 G5/2";

    /// <summary>A bar of the dominant arpeggiated, then the eight-bar <see cref="Tune"/>: a melody that opens on V.</summary>
    private const string OpeningOnTheDominantMelody =
        "G4/4 B4/4 D5/4 G5/4 | " + Tune;

    /// <summary>A sonata exposition in miniature: eight bars of C, a two-bar transition through vi and V7/V, eight bars of G.</summary>
    private const string SonataExposition = "0 5 7 0 9:m 5 7:7 0 | 9:m 2:7 | 7 0 2:7 7 4:m 9:m 2:7 7";

    private const string DeceptiveThenAuthenticChords = "0 5 7 0 | 7 0 2:7 4:m | 7 0 2:7 7";

    // A chorale, C then G, with the soprano in eighths — a passing or neighbour tone on every
    // beat — over quarter-note alto, tenor and bass. Chords by beat: C F C G | C Am F G | C F G7 C
    // | C || G C G D | G Em C D7 | G C D7 G | G.
    private const string PassingSoprano =
        "E5/8 D5/8 C5/8 D5/8 E5/8 F5/8 G5/8 F5/8 | E5/8 F5/8 E5/8 D5/8 C5/8 D5/8 D5/8 C5/8 | "
        + "C5/8 D5/8 C5/8 B4/8 B4/8 A4/8 G4/8 A4/8 | G4/8 A4/8 B4/8 C5/8 C5/2 | "
        + "B4/8 C5/8 C5/8 B4/8 B4/8 A4/8 A4/8 B4/8 | B4/8 C5/8 B4/8 C5/8 C5/8 D5/8 C5/8 A4/8 | "
        + "B4/8 A4/8 G4/8 A4/8 A4/8 C5/8 B4/8 A4/8 | G4/1";
    private const string PassingAlto = "G4/4 A4/4 G4/4 D4/4 | G4/4 E4/4 F4/4 G4/4 | E4/4 F4/4 D4/4 E4/4 | E4/1 | D4/4 E4/4 D4/4 F#4/4 | G4/4 G4/4 G4/4 F#4/4 | D4/4 E4/4 F#4/4 D4/4 | D4/1";
    private const string PassingTenor = "C4/4 C4/4 C4/4 B3/4 | C4/4 C4/4 A3/4 B3/4 | G3/4 A3/4 F3/4 C4/4 | G3/1 | G3/4 G3/4 G3/4 A3/4 | D4/4 B3/4 E4/4 D4/4 | G3/4 G3/4 A3/4 G3/4 | B3/1";
    private const string PassingBass = "C3/4 F3/4 C3/4 G3/4 | C3/4 A2/4 F3/4 G2/4 | C3/4 F3/4 G3/4 C3/4 | C3/1 | G2/4 C3/4 G2/4 D3/4 | G2/4 E3/4 C3/4 D3/4 | G2/4 C3/4 D3/4 G2/4 | G2/1";

    /// <summary>Four bars in C, then four in G whose melody has two chromatic passing eighths in each of its first three bars.</summary>
    private const string ChromaticGMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "D5/4 C5/8 B4/8 Bb4/8 A4/8 Ab4/8 G4/8 | G4/4 A4/8 Bb4/8 B4/8 C5/8 C#5/8 D5/8 | D5/4 E5/8 F5/8 F#5/8 G5/8 Ab5/8 A5/8 | B5/4 A5/4 G5/2";

    private const string ChromaticGChords = "0 5 7 0 | 7 0 2:7 7";

    /// <summary>Four bars in C, then G through a deceptive cadence and an authentic one; bar 6 has a chromatic run E–F–F sharp–G.</summary>
    private const string DeceptiveThenAuthenticMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "G4/4 B4/4 D5/2 | E5/4 F5/8 F#5/8 G5/4 E5/4 | F#5/4 A5/4 C5/4 A4/4 | E5/4 G5/4 B4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 C5/4 G4/2 | A4/4 C5/4 F#5/2 | G5/1";

    /// <summary>Four bars in C, then eight in G; the melody's bar 7 has a chromatic C sharp for a quarter, a neighbour of D over the D7.</summary>
    private const string AppoggiaturaMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
        + "B4/4 D5/4 G5/2 | E5/4 C5/4 G4/2 | A4/4 C#5/4 D5/2 | B4/4 D5/4 G4/2 | "
        + "D5/4 B4/4 G4/2 | E5/4 G5/4 C5/2 | F#5/4 A5/4 D5/2 | G5/1";

    private const string AppoggiaturaChords = "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7";

    // ---------- the builders this table adds ----------

    /// <summary>One note held from the start for <paramref name="wholeNotes"/> whole notes — an organ pedal.</summary>
    private static NoteEvent[] Held(int pitch, int wholeNotes) => [new NoteEvent(pitch, Rational.Zero, new Rational(wholeNotes, 1))];

    /// <summary>The same note struck at every whole note for <paramref name="wholeNotes"/> whole notes — a pedal re-struck each bar.</summary>
    private static NoteEvent[] Restruck(int pitch, int wholeNotes) =>
        [.. Enumerable.Range(0, wholeNotes).Select(i => new NoteEvent(pitch, new Rational(i, 1), Rational.Whole))];

    /// <summary>The 6/8 jig twice in C, then twice in the key <paramref name="semitones"/> up: six whole notes each.</summary>
    private static NoteEvent[] JigTwiceThenTwice(int semitones) => Restated(Jig + " | " + Jig, semitones);

    /// <summary>
    /// The second reviewer's passages, held out from the work on <see cref="HeldOut"/> and
    /// folded in afterwards. Positions in the plans are the musician's, in whole notes; an empty
    /// plan means neither road may report a modulation.
    /// </summary>
    internal static IReadOnlyList<Passage> ReviewerHeldOut { get; } =
    [
        // ---------- shapes from the repertoire ----------
        Block("a sonata exposition: eight bars home, a transition through vi and V7/V, eight in the dominant (block chords)", SonataExposition, At(10, 7, true)),
        new("a sonata exposition: eight bars home, a transition through vi and V7/V, eight in the dominant (arpeggios)", Texture.Arpeggios, true, SonataExposition, [], At(10, 7, true)),
        new("minor to its dominant minor (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 7:m 0:m 2:7 7:m 7:m 0:m 2:7 7:m", [], At(5, 7, false)),
        new("minor to its dominant minor (arpeggios)", Texture.Arpeggios, false, "0:m 5:m 7:7 0:m | 7:m 0:m 2:7 7:m 7:m 0:m 2:7 7:m", [], At(5, 7, false)),
        new("minor to its subdominant minor (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 5:m 10:m 0:7 5:m 10:m 0:7 5:m", [], At(5, 5, false)),
        Block("to the subdominant's relative minor through IV (block chords)", "0 5 7 0 | 5 2:m 9:7 2:m 7:m 9:7 2:m", At(6, 2, false)),
        Block("to the relative minor through a pivot, staying there (block chords)", "0 5 7 0 | 9:m 2:m 4:7 9:m 2:m 4:7 9:m", At(5, 9, false)),
        Block("a rag: eight bars of I vi ii V7, then the trio in the subdominant (block chords)", "0 9:m 2:m 7:7 0 9:m 2:m 7:7 | 5 2:m 7:m 0:7 5 2:m 7:m 0:7", At(9, 5, true)),
        Block("a phrase in the subdominant through V7/IV, then home (block chords)", "0 5 7 0 | 0:7 5 10 5 | 0 5 7 0", [new(5, 5, true), new(9, 0, true)]),
        Block("to the dominant, closing the piece: C F G C | G C D7 G (block chords)", "0 5 7 0 | 7 0 2:7 7", At(5, 7, true)),
        Block("to the dominant through a deceptive cadence, then an authentic one (block chords)", DeceptiveThenAuthenticChords, At(5, 7, true)),
        new("a chorale with a passing tone on every beat, C then G (four voices)", Texture.FourVoices, true, "", Voices(PassingSoprano, PassingAlto, PassingTenor, PassingBass), At(5, 7, true)),
        new("melody alone changing key through the German sixth heard as V7 of D flat (melody alone)", Texture.MelodyAlone, true, "", Notated(EnharmonicPivotMelody), At(6, 1, true)),

        // ---------- metre and texture ----------
        new("a 6/8 piece, eight bars of C then eight of G (block chords in 6/8)", Texture.BlockChords, true, "0t 5t 7:7t 0t 0t 5t 7:7t 0t | 7t 0t 2:7t 7t 7t 0t 2:7t 7t", [], [new(new Rational(6, 1), 7, true)]),
        new("a 6/8 jig, twice in C then twice in G (melody alone)", Texture.MelodyAlone, true, "", JigTwiceThenTwice(7), [new(new Rational(6, 1), 7, true)]),
        new("to the dominant after a quarter-note pickup chord (block chords)", Texture.BlockChords, true, "7q 0 5 7 0 | 7 0 2:7 7 7", [], [new(new Rational(17, 4), 7, true)]),
        new("to the dominant over a dominant pedal held throughout (melody over chords)", Texture.MelodyOverChords, true, "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7", Held(43, 12), At(5, 7, true)),
        new("to the dominant over a dominant pedal struck every bar (melody over chords)", Texture.MelodyOverChords, true, "0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7", Restruck(43, 12), At(5, 7, true)),

        // ---------- the new key's first phrase holds a chromatic chord of its own ----------
        Block("to the dominant with V7/ii in its second bar: C F G C | G E7 Am D7 | G C D7 G (block chords)", "0 5 7 0 | 7 4:7 9:m 2:7 | 7 0 2:7 7", At(5, 7, true)),
        new("to the dominant, the melody diatonic in the new key (melody alone)", Texture.MelodyAlone, true, "", Notated(GAreaDiatonic), At(5, 7, true)),
        new("to the dominant, one chromatic passing eighth in each bar of the new key (melody alone)", Texture.MelodyAlone, true, "", Notated(GAreaOnePassingEighthPerBar), At(5, 7, true)),
        new("to the dominant, one chromatic passing eighth in the first bar of the new key (melody alone)", Texture.MelodyAlone, true, "", Notated(GAreaOnePassingEighth), At(5, 7, true)),
        new("to the dominant, a passing F natural in the new key's third bar, E F F sharp G (melody alone)", Texture.MelodyAlone, true, "", Notated(GAreaPassingFNatural), At(5, 7, true)),

        // ---------- one key throughout ----------
        new("a D Dorian folk tune, the raised sixth in every other bar (melody over chords)", Texture.MelodyOverChords, false, DorianChords, Notated(DorianTune), Nowhere) { OpeningRoot = 2 },
        new("a D Dorian folk tune, the raised sixth in every other bar (melody alone)", Texture.MelodyAlone, false, "", Notated(DorianTune), Nowhere) { OpeningRoot = 2 },
        Block("opening on V7: G7 C F G7 | C F G7 C (block chords)", "7:7 0 5 7:7 | 0 5 7:7 0", Nowhere),
        new("a minor piece opening on V7 (block chords)", Texture.BlockChords, false, "7:7 0:m 5:m 7:7 | 0:m 5:m 7:7 0:m", [], Nowhere),
        new("a melody opening on the dominant arpeggio, then the tune (melody alone)", Texture.MelodyAlone, true, "", Notated(OpeningOnTheDominantMelody), Nowhere),
        Block("modal mixture: C Fm G C | Ab Bb C C | C Fm G7 C (block chords)", "0 5:m 7 0 | 8 10 0 0 | 0 5:m 7:7 0", Nowhere),
        new("in C throughout, the detector told the piece opens in G (block chords)", Texture.BlockChords, true, "0 5 7 0 | 0 5 7:7 0", [], Nowhere) { OpeningRoot = 7 },
        new("in C throughout, the detector told the piece opens in A minor (block chords)", Texture.BlockChords, false, "0 5 7 0 | 0 5 7:7 0", [], Nowhere) { OpeningRoot = 9 },

        // ---------- the seven the reviewer found still wrong after the tables above were answered ----------
        new("a hymn: minor, its relative major, minor again closing on a Picardy third (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 3 8 10 3 | 0:m 8 7:7 0", [], [new(5, 3, true), new(9, 0, false)]),
        new("to the dominant through a deceptive cadence, with a chromatic run E F F sharp G in bar 6 (melody over chords)", Texture.MelodyOverChords, true, DeceptiveThenAuthenticChords, Notated(DeceptiveThenAuthenticMelody), At(5, 7, true)),
        Block("to the dominant with V/ii as a plain triad in its second bar: C F G C | G E Am D7 | G C D7 G (block chords)", "0 5 7 0 | 7 4 9:m 2:7 | 7 0 2:7 7", At(5, 7, true)),
        Block("to the dominant with a borrowed iv in its second bar: C F G C | G Cm D7 G | G C D7 G (block chords)", "0 5 7 0 | 7 0:m 2:7 7 | 7 0 2:7 7", At(5, 7, true)),
        new("to the dominant, the melody with two chromatic passing eighths in each bar of the new key (melody alone)", Texture.MelodyAlone, true, "", Notated(ChromaticGMelody), At(5, 7, true)),
        new("to the dominant, the melody with two chromatic passing eighths in each bar of the new key (melody over chords)", Texture.MelodyOverChords, true, ChromaticGChords, Notated(ChromaticGMelody), At(5, 7, true)),
        new("to the dominant, the melody with one chromatic quarter-note neighbour in the new key (melody over chords)", Texture.MelodyOverChords, true, AppoggiaturaChords, Notated(AppoggiaturaMelody), At(5, 7, true)),
    ];

    // ---------- the third reviewer's passages ----------

    /// <summary>Four bars over I IV V I in C — the fixture's ShortTune, restated here because it is private to the other file.</summary>
    private const string OpeningInC = "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2";

    // A chorale in A minor: four bars of A minor, four of C major, four of A minor closing on a
    // Picardy third. Chords by beat: Am Am Dm E7 | Am F Dm E | Am Dm E7 Am | Am || C F G C |
    // C Am F G | C F G7 C | C || Am Dm E Am | F Dm E7 Am | Dm E7 Am E7 | A(major). Passing
    // eighths in the soprano in bars 6 and 7.
    private const string ChoraleS =
        "C5/4 E5/4 F5/4 D5/4 | C5/4 C5/4 A4/4 B4/4 | C5/4 F5/4 D5/4 C5/4 | C5/1 | "
        + "E5/4 F5/4 D5/4 E5/4 | G5/4 E5/4 F5/8 E5/8 D5/4 | E5/4 A5/4 G5/8 F5/8 E5/4 | C5/1 | "
        + "C5/4 D5/4 B4/4 C5/4 | A5/4 F5/4 D5/4 C5/4 | F5/4 E5/4 E5/4 D5/4 | C#5/1";
    private const string ChoraleA =
        "A4/4 A4/4 A4/4 G#4/4 | E4/4 F4/4 F4/4 G#4/4 | A4/4 A4/4 G#4/4 A4/4 | E4/1 | "
        + "G4/4 A4/4 B4/4 G4/4 | E4/4 C4/4 C4/4 B3/4 | G4/4 C5/4 B4/4 G4/4 | E4/1 | "
        + "E4/4 F4/4 G#4/4 E4/4 | C5/4 A4/4 G#4/4 A4/4 | A4/4 G#4/4 A4/4 G#4/4 | A4/1";
    private const string ChoraleT =
        "E4/4 E4/4 D4/4 E4/4 | A3/4 A3/4 D4/4 E4/4 | E4/4 F4/4 E4/4 E4/4 | A3/1 | "
        + "C4/4 C4/4 D4/4 C4/4 | G3/4 A3/4 A3/4 G3/4 | C4/4 F4/4 D4/4 C4/4 | G3/1 | "
        + "A3/4 A3/4 B3/4 A3/4 | F4/4 D4/4 E4/4 E4/4 | D4/4 D4/4 C4/4 B3/4 | E4/1";
    private const string ChoraleB =
        "A2/4 A2/4 D3/4 E3/4 | A2/4 F3/4 D3/4 E3/4 | A2/4 D3/4 E3/4 A2/4 | A2/1 | "
        + "C3/4 F3/4 G2/4 C3/4 | C3/4 A2/4 F3/4 G2/4 | C3/4 F3/4 G2/4 C3/4 | C3/1 | "
        + "A2/4 D3/4 E3/4 A2/4 | F3/4 D3/4 E3/4 A2/4 | D3/4 E3/4 A2/4 E3/4 | A2/1";

    /// <summary>A minuet in C, its trio in C minor, the minuet again: eight bars of 3/4 each, six whole notes.</summary>
    private const string MinuetChords = "0t 5t 7t 0t 9:mt 5t 7:7t 0t";
    private const string TrioChords = "0:mt 5:mt 7:7t 0:mt 8t 5:mt 7:7t 0:mt";

    /// <summary>A verse in C (I vi IV V twice), a chorus in F with the borrowed iv in its second bar.</summary>
    private const string VerseChorusChords = "0 9:m 5 7 | 0 9:m 5 7 | 5 10:m 0:7 5 | 5 10 0:7 5";
    private const string VerseChorusMelody =
        "E4/4 G4/4 C5/2 | A4/4 C5/4 E5/2 | F4/4 A4/4 C5/2 | D5/4 B4/4 G4/2 | "
        + "E4/4 G4/4 C5/2 | A4/4 C5/4 E5/2 | F4/4 A4/4 C5/2 | B4/4 D5/4 G4/2 | "
        + "F4/4 A4/4 C5/2 | Db5/4 Bb4/4 F4/2 | E5/4 G4/4 Bb4/2 | A4/4 F4/4 C5/2 | "
        + "C5/4 A4/4 F4/2 | D5/4 Bb4/4 F4/2 | G4/4 E4/4 Bb4/2 | F4/1";

    /// <summary>The same appoggiaturas on beat two, approached by leap over the chord struck on beat one.</summary>
    private const string OffbeatAppoggiaturaMelody =
        OpeningInC + " | G4/4 A#4/4 B4/2 | C5/4 D#5/4 E5/2 | A4/4 C#5/4 D5/2 | B4/4 Eb5/4 D5/2";

    private const string CThenGChords = "0 5 7 0 | 7 0 2:7 7";

    // Eight bars in C: C F G C | Am F G C held in the upper voices, a walking bass in quarters
    // with a chromatic passing tone in every bar.
    private const string WalkingS = "E5/1 | F5/1 | D5/1 | E5/1 | E5/1 | F5/1 | D5/1 | E5/1";
    private const string WalkingA = "G4/1 | A4/1 | B4/1 | G4/1 | C5/1 | A4/1 | B4/1 | G4/1";
    private const string WalkingT = "C4/1 | C4/1 | G3/1 | C4/1 | A3/1 | C4/1 | G3/1 | C4/1";
    private const string WalkingB =
        "C3/4 D3/4 D#3/4 E3/4 | F3/4 E3/4 F3/4 F#3/4 | G3/4 A3/4 Ab3/4 G3/4 | C3/4 B2/4 Bb2/4 A2/4 | "
        + "A2/4 G2/4 F#2/4 F2/4 | F3/4 E3/4 Eb3/4 D3/4 | G2/4 A2/4 Bb2/4 B2/4 | C3/1";

    /// <summary>The Neapolitan sixth of A minor in bar 6, before the phrase moves on.</summary>
    private const string MinorWithNeapolitan = "9:m 2:m 4:7 9:m | 2:m 10 4:7 9:m";

    /// <summary>C major, with a chromatic scale fragment rising through bar 3 and falling through bar 6.</summary>
    private const string ChromaticFragmentsInC =
        "E4/4 F4/4 D4/4 B3/4 | C4/4 E4/4 G4/4 E4/4 | E4/4 F4/8 F#4/8 G4/8 G#4/8 A4/8 A#4/8 | B4/4 C5/4 G4/2 | "
        + "G4/4 F4/4 E4/4 D4/4 | G4/4 Gb4/8 F4/8 E4/8 Eb4/8 D4/8 Db4/8 | C4/4 D4/4 E4/4 F4/4 | E4/4 D4/4 C4/2";

    /// <summary>Four bars in C, then four in G whose second bar rises chromatically D to G.</summary>
    private const string ChromaticFragmentInG =
        OpeningInC + " | D5/4 B4/4 G4/2 | D5/4 D#5/8 E5/8 F5/8 F#5/8 G5/4 | B4/4 D5/4 F#5/4 G5/4 | F#5/4 A5/4 G5/2";

    /// <summary>Four bars in C, then four in G in which every F sharp is a lower neighbour of G, an eighth between two G's.</summary>
    private const string LeadingToneAsNeighbourMelody =
        OpeningInC + " | D5/4 G4/8 F#4/8 G4/4 B4/4 | C5/4 A4/4 G4/8 F#4/8 G4/4 | D5/4 B4/4 G4/8 F#4/8 G4/4 | B4/4 G4/8 F#4/8 G4/2";

    /// <summary>Four bars in C, then eight in C minor; in bars 8 and 12 the leading tone B natural is struck on the downbeat over the tonic chord and resolves to C.</summary>
    private const string CThenCMinorChords = "0 5 7 0 | 0:m 5:m 7:7 0:m | 0:m 5:m 7:7 0:m";
    private const string LeadingToneOverMinorTonicMelody =
        OpeningInC + " | C5/4 Eb5/4 G5/2 | Ab5/4 F5/4 C5/2 | B4/4 D5/4 F5/2 | B4/4 C5/4 Eb5/2 | "
        + "G5/4 Eb5/4 C5/2 | Ab4/4 C5/4 F5/2 | D5/4 B4/4 G4/2 | B4/4 C5/2 C5/4";
    private const string LeadingToneOverDominantMelody =
        OpeningInC + " | C5/4 Eb5/4 G5/2 | Ab5/4 F5/4 C5/2 | B4/4 D5/4 F5/2 | C5/4 G4/4 Eb5/2 | "
        + "G5/4 Eb5/4 C5/2 | Ab4/4 C5/4 F5/2 | D5/4 B4/4 G4/2 | C5/2 C5/2";

    /// <summary>Four bars in C, then four in G with a chromatic appoggiatura struck on every downbeat, resolving by a semitone into the chord.</summary>
    private const string DownbeatAppoggiaturaMelody =
        OpeningInC + " | A#4/4 B4/4 D5/2 | D#5/4 E5/4 C5/2 | C#5/4 D5/4 F#5/2 | Eb5/4 D5/4 G4/2";

    /// <summary>C F G C | G E Am D7 | G C D7 G with a melody: over the E chord, A is struck on the downbeat and resolves to G sharp, a 4-3 suspension.</summary>
    private const string AppliedTriadChords = "0 5 7 0 | 7 4 9:m 2:7 | 7 0 2:7 7";
    private const string SuspensionOverAppliedTriadMelody =
        OpeningInC + " | B4/4 D5/4 G5/2 | A4/4 G#4/4 B4/2 | C5/4 E5/4 A4/2 | F#5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G4/2 | E5/4 G5/4 C5/2 | F#5/4 A5/4 D5/2 | G5/1";
    private const string ChordToneOverAppliedTriadMelody =
        OpeningInC + " | B4/4 D5/4 G5/2 | B4/4 G#4/4 E5/2 | C5/4 E5/4 A4/2 | F#5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G4/2 | E5/4 G5/4 C5/2 | F#5/4 A5/4 D5/2 | G5/1";

    /// <summary>Four bars in C, then four in G with a chromatic passing tone inside an arch in bars 5 and 6 (G A A flat G, D E E flat D) and a rising one in bar 7 (B C C sharp D).</summary>
    private const string ArchPassingToneMelody =
        OpeningInC + " | G4/4 A4/4 Ab4/4 G4/4 | D5/4 E5/4 Eb5/4 D5/4 | B4/4 C5/4 C#5/4 D5/4 | A4/4 F#4/4 G4/2";

    /// <summary>Four bars in C, then four in G whose third bar has a half-note C sharp between C and D over the D7.</summary>
    private const string HalfNotePassingToneMelody =
        OpeningInC + " | B4/4 D5/4 G5/2 | E5/4 G5/4 C5/2 | C5/4 C#5/2 D5/4 | B4/4 D5/4 G4/2";

    /// <summary>
    /// The third reviewer's passages, held out from the work on <see cref="ReviewerHeldOut"/>
    /// and folded in afterwards. Positions in the plans are the musician's, in whole notes; an
    /// empty plan means neither road may report a modulation.
    /// </summary>
    internal static IReadOnlyList<Passage> ThirdReviewerHeldOut { get; } =
    [
        // ---------- shapes from the repertoire ----------
        new("a chorale: A minor, its relative major, A minor again closing on a Picardy third (four voices)", Texture.FourVoices, false, "", Voices(ChoraleS, ChoraleA, ChoraleT, ChoraleB), [new(5, 0, true), new(9, 9, false)]) { OpeningRoot = 9 },
        new("a minuet, its trio in the parallel minor, the minuet da capo (block chords in 3/4)", Texture.BlockChords, true, MinuetChords + " | " + TrioChords + " | " + MinuetChords, [], [new(new Rational(6, 1), 0, false), new(new Rational(12, 1), 0, true)]),
        Block("a verse in C, a chorus in IV with the borrowed iv in its second bar (block chords)", VerseChorusChords, At(9, 5, true)),
        new("a verse in C, a chorus in IV with the borrowed iv in its second bar (melody over chords)", Texture.MelodyOverChords, true, VerseChorusChords, Notated(VerseChorusMelody), At(9, 5, true)),
        new("to the dominant, a chromatic appoggiatura on beat two of every bar of the new key (melody over chords)", Texture.MelodyOverChords, true, CThenGChords, Notated(OffbeatAppoggiaturaMelody), At(5, 7, true)),
        new("a chromatic walking bass under diatonic chords in C (four voices)", Texture.FourVoices, true, "", Voices(WalkingS, WalkingA, WalkingT, WalkingB), Nowhere),
        Block("V7/vi resolving deceptively to IV: C E7 F G | C F G7 C, twice (block chords)", "0 4:7 5 7 | 0 5 7:7 0 | 0 4:7 5 7 | 0 5 7:7 0", Nowhere),
        Block("V/vi as a triad resolving deceptively to IV: C E F G | C F G7 C, twice (block chords)", "0 4 5 7 | 0 5 7:7 0 | 0 4 5 7 | 0 5 7:7 0", Nowhere),
        new("A minor with the Neapolitan in bar 6, then its relative major (block chords)", Texture.BlockChords, false, MinorWithNeapolitan + " | 0 5 7 0 | 0 5 7:7 0", [], At(9, 0, true)) { OpeningRoot = 9 },
        new("A minor with the Neapolitan in bar 6, then its dominant minor (block chords)", Texture.BlockChords, false, MinorWithNeapolitan + " | 4:m 9:m 11:7 4:m | 4:m 9:m 11:7 4:m", [], At(9, 4, false)) { OpeningRoot = 9 },
        new("minor, its relative major, minor again beginning on a German sixth (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 3 8 10 3 | 8:7 7:7 0:m 0:m | 0:m 5:m 7:7 0:m", [], [new(5, 3, true), new(9, 0, false)]),

        // ---------- chromatic lines ----------
        new("a C major melody with a chromatic scale fragment rising in bar 3 and falling in bar 6 (melody alone)", Texture.MelodyAlone, true, "", Notated(ChromaticFragmentsInC), Nowhere),
        new("to the dominant, a chromatic run D to G in the new key's second bar (melody alone)", Texture.MelodyAlone, true, "", Notated(ChromaticFragmentInG), At(5, 7, true)),
        new("to the dominant, every F sharp of the new key a lower neighbour of G (melody alone)", Texture.MelodyAlone, true, "", Notated(LeadingToneAsNeighbourMelody), At(5, 7, true)),

        // ---------- quotations and chromatic chords in the new key's first phrase ----------
        Block("a C major tune quoting a bar of E flat major in its fifth bar (block chords)", "0 5 7 0 | 3q 8q 10q 3q 5 7:7 0 0", Nowhere),

        // ---------- the minor key's leading tone, and the Picardy chord ----------
        new("to the parallel minor, the leading tone struck on the downbeat over the tonic in bars 8 and 12 (melody over chords)", Texture.MelodyOverChords, true, CThenCMinorChords, Notated(LeadingToneOverMinorTonicMelody), At(5, 0, false)),
        new("to the parallel minor, the leading tone only over the dominant (melody over chords)", Texture.MelodyOverChords, true, CThenCMinorChords, Notated(LeadingToneOverDominantMelody), At(5, 0, false)),
        new("minor closing on a Picardy third, then a minor plagal Amen: Fm C (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 8 5:m 7:7 0 | 5:m 0", [], Nowhere),
        new("minor closing on a Picardy third struck twice (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 8 5:m 7:7 0 | 0", [], Nowhere),

        // ---------- the reviewer's second look: what iteration three still got wrong ----------
        new("to the dominant, a chromatic appoggiatura struck on every downbeat of the new key (melody over chords)", Texture.MelodyOverChords, true, CThenGChords, Notated(DownbeatAppoggiaturaMelody), At(5, 7, true)),
        new("minor, its relative major, minor again through a German sixth in the return's second bar (block chords)", Texture.BlockChords, false, "0:m 5:m 7:7 0:m | 3 8 10 3 | 0:m 8:7 7:7 0:m | 0:m 5:m 7:7 0:m", [], [new(5, 3, true), new(9, 0, false)]),
        new("to the dominant, a chromatic passing tone inside an arch in each of the new key's first two bars (melody alone)", Texture.MelodyAlone, true, "", Notated(ArchPassingToneMelody), At(5, 7, true)),
        new("to the dominant, a half-note chromatic passing tone C sharp in the new key's third bar (melody over chords)", Texture.MelodyOverChords, true, CThenGChords, Notated(HalfNotePassingToneMelody), At(5, 7, true)),
        Block("to the dominant, a bar of B flat major quoted in the new key's second bar (block chords)", "0 5 7 0 | 7 10q 3q 5q 10q 2:7 7 | 7 0 2:7 7", At(5, 7, true)),
        Block("to the subdominant with V/V in the new key's first phrase: C F G C | F Bb G C7 | F Bb C7 F (block chords)", "0 5 7 0 | 5 10 7 0:7 | 5 10 0:7 5", At(5, 5, true)),
        Block("to the flat seventh with V/V in the new key's first phrase: C F G C | Bb Eb C F | Bb Eb F7 Bb (block chords)", "0 5 7 0 | 10 3 0 5 | 10 3 5:7 10", At(5, 10, true)),
        Block("to the flat seventh, the new key's first phrase closing on its tonic seventh: C F G C | Bb Eb F Bb7 | Bb Eb F7 Bb (block chords)", "0 5 7 0 | 10 3 5 10:7 | 10 3 5:7 10", At(5, 10, true)),
        new("to the dominant with V/ii as a triad, a 4-3 suspension struck over it (melody over chords)", Texture.MelodyOverChords, true, AppliedTriadChords, Notated(SuspensionOverAppliedTriadMelody), At(5, 7, true)),
        new("to the dominant with V/ii as a triad, a chord tone struck over it (melody over chords)", Texture.MelodyOverChords, true, AppliedTriadChords, Notated(ChordToneOverAppliedTriadMelody), At(5, 7, true)),
        new("to the dominant with V/ii as a plain triad in its second bar (arpeggios)", Texture.Arpeggios, true, AppliedTriadChords, [], At(5, 7, true)),
        new("to the dominant with a borrowed iv in its second bar (arpeggios)", Texture.Arpeggios, true, "0 5 7 0 | 7 0:m 2:7 7 | 7 0 2:7 7", [], At(5, 7, true)),
    ];

    // ---------- the fourth reviewer's passages ----------

    // ---------- 3. a Mozart-like transition over a rising chromatic bass ----------
    // C F G C | C A7 Dm D#dim | Em F D7 G | G C D7 G, the bass C C# D D# E F F# G under bars 5-6.
    private const string TransitionChords = "0 5 7 0 | 0q 9:7q 2:mq 3:dimq | 4:mq 5q 2:7q 7q | 7 0 2:7 7";
    private const string TransitionBass = "C2/1 F2/1 G2/1 C2/1 | C2/4 C#2/4 D2/4 D#2/4 | E2/4 F2/4 F#2/4 G2/4 | G2/1 C2/1 D2/1 G2/1";

    // ---------- 6. a folk tune in C Mixolydian: B flat throughout, no modulation ----------
    private const string MixolydianTune =
        "C4/4 D4/4 E4/4 G4/4 | Bb4/4 A4/4 G4/2 | E4/4 G4/4 Bb4/4 A4/4 | G4/4 F4/4 E4/2 | "
        + "C4/4 E4/4 G4/4 Bb4/4 | A4/4 G4/4 E4/4 D4/4 | Bb3/4 C4/4 D4/4 E4/4 | D4/4 D4/4 C4/2";
    private const string MixolydianVamp = "0 10 5 0 | 0 10 5 0";

    // ---------- 7. escape tones (step in, leap out) and anticipations, C then G ----------
    // Chromatic escape tones: B flat in bar 2, C sharp in bar 5, B flat in bar 7; anticipations
    // of the next chord's tone on the last eighth of bars 3, 6 and 7.
    private const string EscapeToneChords = "0 5 7:7 0 | 7 0 2:7 7";
    private const string EscapeToneMelody =
        "E4/4 F4/8 C4/8 G4/4 A4/8 E4/8 | A4/4 Bb4/8 F4/8 C5/4 D5/8 A4/8 | D5/4 E5/8 B4/8 G4/4 F4/8 E4/8 | E4/4 D4/8 C4/8 C4/2 | "
        + "B4/4 C#5/8 G4/8 D5/4 E5/8 B4/8 | E5/4 F#5/8 C5/8 G4/4 A4/8 F#4/8 | F#4/4 G4/8 D4/8 A4/4 Bb4/8 G4/8 | G4/4 F#4/8 G4/8 G4/2";

    // ---------- 10. a piece in 5/4: each bar a whole note and a quarter ----------
    private const string FiveFourChords = "0t 0h 5t 5h 7t 7h 0t 0h | 7t 7h 0t 0h 2:7t 2:7h 7t 7h";
    private const string FiveFourMelody =
        "E4/4 G4/4 C5/4 D5/4 C5/4 | A4/4 F4/4 C5/4 A4/4 F4/4 | D5/4 B4/4 G4/4 A4/4 B4/4 | C5/4 E5/4 D5/4 C5/2 | "
        + "B4/4 D5/4 G5/4 F#5/4 G5/4 | E5/4 C5/4 G4/4 A4/4 C5/4 | F#5/4 A5/4 D5/4 C5/4 A4/4 | B4/4 A4/4 G4/4 G4/2";

    // ---------- 11. a long trill on a chromatic note ----------
    private const string OpeningTune = "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | ";
    private const string TrillCSharp = "C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16 C#5/16 D5/16";
    private const string TrillFSharp = "F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16 F#5/16 G5/16";

    /// <summary>C F G C | A7 Dm G7 C with a whole-bar trill on C sharp over the A7: V7/ii, no modulation.</summary>
    private const string TrillOverAppliedChords = "0 5 7 0 | 9:7 2:m 7:7 0";
    private const string TrillOverAppliedMelody = OpeningTune + TrillCSharp + " | D5/4 F5/4 A4/2 | B4/4 D5/4 G4/2 | C5/1";

    /// <summary>C F G7 C | G C D7 G with a whole-bar trill on F sharp over the D7 of the new key.</summary>
    private const string TrillInNewKeyChords = "0 5 7:7 0 | 7 0 2:7 7";
    private const string TrillInNewKeyMelody = OpeningTune + "B4/4 D5/4 G5/2 | E5/4 G5/4 C5/2 | " + TrillFSharp + " | G5/1";

    // ---------- 12. a ground bass: C B A G under changing harmonies, one modulation ----------
    // Cycles 1-2: C G7 Am G (I V7 vi V in C); cycles 3-4: C Bm D7 G (IV iii V7 I in G), the bass
    // unchanged — the G7 is in first inversion, the D7 in second, the Bm over its own root.
    private const string GroundBassChords = "0 7:7 9:m 7 | 0 7:7 9:m 7 | 0 11:m 2:7 7 | 0 11:m 2:7 7";
    private const string GroundBassLine = "C2/1 B1/1 A1/1 G1/1 | C2/1 B1/1 A1/1 G1/1 | C2/1 B1/1 A1/1 G1/1 | C2/1 B1/1 A1/1 G1/1";

    // ---------- 13. an accented chromatic appoggiatura on every downbeat, never leaving C ----------
    private const string AppoggiaturasInCChords = "0 2:m 7:7 0 | 0 5 7:7 0";
    private const string AppoggiaturasInCMelody =
        "D#5/4 E5/4 G5/2 | C#5/4 D5/4 F5/2 | Ab4/4 G4/4 B4/2 | F#4/4 G4/4 E4/2 | "
        + "D#5/4 E5/4 C5/2 | G#4/4 A4/4 F4/2 | C#5/4 D5/4 B4/2 | B4/4 C5/4 C5/2";

    // ---------- 18. a chain of applied chords to a half cadence on V7, never leaving C ----------
    private const string AppliedChainChords = "0 5 7 0 | 4 9:m 2:7 7:7 | 0 5 7 0";
    private const string AppliedChainMelody =
        "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E5/4 D5/4 C5/2 | "
        + "G#4/4 B4/4 E5/2 | C5/4 E5/4 A4/2 | F#5/4 D5/4 A4/2 | F4/4 B4/4 D5/2 | "
        + "E5/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | C5/1";

    // ---------- 19. passing tones inside arches in every bar, never leaving C ----------
    private const string ArchesInCMelody =
        "C4/4 D4/4 Db4/4 C4/4 | E4/4 F4/4 E4/4 Eb4/4 | G4/4 A4/4 Ab4/4 G4/4 | F4/4 E4/4 D4/4 C4/2 | "
        + "E4/4 F#4/4 G4/4 F4/4 | D4/4 E4/4 Eb4/4 D4/4 | B3/4 C4/4 C#4/4 D4/4 | E4/4 D4/4 C4/2";

    // ---------- 1. a Bach-like chorale phrase pair: to V, and home through V7/ii ----------
    // C F Dm G | Am G7 C(h) || C D7 G Em | Am D7 G(h) || G C D7 G | C D7 G(h) || G Em Am A7 | Dm G7 C(h)
    // Phrase 2 opens on the pivot C (I of C, IV of G) and cadences in G; phrase 4 opens on G (I
    // of G, V of C), and C is back at the A7 -> Dm (V7/ii ii) and the cadence.
    private const string ChoraleTwoSoprano = "E5/4 F5/4 F5/4 D5/4 | C5/4 D5/4 C5/2 | E5/4 C5/4 B4/4 B4/4 | C5/4 C5/4 B4/2 | D5/4 E5/4 C5/4 B4/4 | C5/4 C5/4 B4/2 | D5/4 B4/4 C5/4 C#5/4 | D5/4 D5/4 C5/2";
    private const string ChoraleTwoAlto = "G4/4 A4/4 A4/4 B4/4 | A4/4 B4/4 G4/2 | G4/4 A4/4 G4/4 G4/4 | A4/4 A4/4 G4/2 | B4/4 C5/4 A4/4 G4/4 | G4/4 A4/4 G4/2 | B4/4 G4/4 A4/4 G4/4 | A4/4 B4/4 G4/2";
    private const string ChoraleTwoTenor = "C4/4 C4/4 D4/4 D4/4 | E4/4 F4/4 E4/2 | C4/4 F#4/4 D4/4 E4/4 | E4/4 F#4/4 D4/2 | G4/4 G4/4 F#4/4 D4/4 | E4/4 F#4/4 D4/2 | G4/4 E4/4 E4/4 E4/4 | F4/4 F4/4 E4/2";
    private const string ChoraleTwoBass = "C3/4 F3/4 D3/4 G2/4 | A2/4 G2/4 C3/2 | C3/4 D3/4 G2/4 E3/4 | A2/4 D3/4 G2/2 | G3/4 C3/4 D3/4 G3/4 | C3/4 D3/4 G2/2 | G3/4 E3/4 A2/4 A2/4 | D3/4 G2/4 C3/2";

    /// <summary>
    /// The fourth reviewer's passages, held out from the work on the twelve rows that closed the
    /// fourth table and folded in afterwards: repertoire shapes, and each of that iteration's rules
    /// pushed the other way. Positions in the plans are the musician's, in whole notes; an empty
    /// plan means neither road may report a modulation.
    /// </summary>
    internal static IReadOnlyList<Passage> FourthReviewerHeldOut { get; } =
    [
        // ---------- repertoire shapes ----------
        new("Mozart: a transition to the dominant over a rising chromatic bass (melody over chords)", Texture.MelodyOverChords, true,
            TransitionChords, Notated(TransitionBass), At(7, 7, true)),
        new("a hymn in 3/4 in minor, closing on a Picardy third with a minor plagal Amen (block chords)", Texture.BlockChords, false,
            "0:mt 5:mt 7t 0:mt | 3t 8t 7:7t 0:mt | 5:mt 0:mt 7:7t 0:mt | 8t 5:mt 7:7t 0t | 5:mt 0t", [], Nowhere),
        Block("a jazz turnaround with tritone substitutes, Cmaj7 Eb7 Dm7 Db7, never leaving the key (half-bar chords)",
            "0:maj7h 3:7h 2:m7h 1:7h | 0:maj7h 9:7h 2:m7h 7:7h | 0:maj7h 3:7h 2:m7h 1:7h | 0:maj7h 9:7h 2:m7h 7:7h | "
            + "0:maj7h 3:7h 2:m7h 1:7h | 0:maj7h 9:7h 2:m7h 7:7h | 0:maj7h 3:7h 2:m7h 1:7h | 0:maj7", Nowhere),
        new("a folk tune in Mixolydian, the flat seventh throughout (melody alone)", Texture.MelodyAlone, true, "", Notated(MixolydianTune), Nowhere),
        new("a folk tune in Mixolydian over a I bVII IV I vamp (melody over chords)", Texture.MelodyOverChords, true, MixolydianVamp, Notated(MixolydianTune), Nowhere),
        new("to the dominant with escape tones and anticipations, three of the escape tones chromatic (melody over chords)", Texture.MelodyOverChords, true,
            EscapeToneChords, Notated(EscapeToneMelody), At(5, 7, true)),
        Block("A minor the pivot between three keys: C, G (ii), E minor (iv), C (vi) (block chords)",
            "0 5 7 0 | 9:m 2:7 7 2:7h 7h | 9:m 11:7 4:m 11:7h 4:mh | 9:m 2:m 7:7 0", [new(5, 7, true), new(9, 4, false), new(13, 0, true)]),
        Block("descending fifths, a phrase each, settling two fifths down: C, F, B flat (block chords)",
            "0 5 7 0 | 0:7 5 10 5 | 5:7 10 3 10 | 10 3 5:7 10", [new(5, 5, true), new(9, 10, true)]),
        Block("descending fifths through F in half a bar, settling in B flat (block chords)",
            "0 5 7 0 | 0:7h 5h 5:7h 10h | 10 3 5:7 10 | 10 3 5:7 10", At(7, 10, true)),
        new("to the dominant in 5/4 (melody over chords)", Texture.MelodyOverChords, true, FiveFourChords, Notated(FiveFourMelody), [new(new Rational(5, 1), 7, true)]),
        new("a whole-bar trill on C sharp over V7/ii, never leaving C (melody over chords)", Texture.MelodyOverChords, true,
            TrillOverAppliedChords, Notated(TrillOverAppliedMelody), Nowhere),
        new("to the dominant, a whole-bar trill on F sharp over the new key's V7 (melody over chords)", Texture.MelodyOverChords, true,
            TrillInNewKeyChords, Notated(TrillInNewKeyMelody), At(5, 7, true)),
        new("a ground bass C B A G, harmonies changing above it, to the dominant in the third cycle (melody over chords)", Texture.MelodyOverChords, true,
            GroundBassChords, Notated(GroundBassLine), At(9, 7, true)),

        // ---------- each new rule pushed the other way ----------
        new("an accented chromatic appoggiatura on every downbeat, never leaving C (melody over chords)", Texture.MelodyOverChords, true,
            AppoggiaturasInCChords, Notated(AppoggiaturasInCMelody), Nowhere),
        new("V/vi arpeggiated in eighths, resolving to vi: a tonicization at most (arpeggios)", Texture.Arpeggios, true, "0 5 7 0 | 4 9:m 2:m 7:7 | 0 5 7 0", [], Nowhere),
        new("V/vi arpeggiated in eighths, resolving deceptively to IV (arpeggios)", Texture.Arpeggios, true, "0 5 7 0 | 4 5 7:7 0 | 0 5 7 0", [], Nowhere),
        Block("a seventh on the tonic that does resolve down a fifth: C F G C7 | F Bb C7 F | F Bb C7 F (block chords)",
            "0 5 7 0:7 | 5 10 0:7 5 | 5 10 0:7 5", At(5, 5, true)),
        Block("a borrowed flat seventh opening the second phrase: C F G C | Bb F G C | F G7 C C (block chords)",
            "0 5 7 0 | 10 5 7 0 | 5 7:7 0 0", Nowhere),
        Block("three bars of B flat, E flat and A flat between a lone G and the cadence in G: no parenthesis (block chords)",
            "0 5 7 0 | 7 10q 3q 5q 10q 3q 8q 10q 3q 8q 1q 3q 8q 2:7 7 | 7 0 2:7 7", At(10, 7, true)),
        new("a chain of applied chords to a half cadence on V7, never leaving C (melody over chords)", Texture.MelodyOverChords, true,
            AppliedChainChords, Notated(AppliedChainMelody), Nowhere),
        new("a chromatic passing tone inside an arch in every bar, never leaving C (melody alone)", Texture.MelodyAlone, true, "", Notated(ArchesInCMelody), Nowhere),

        // ---------- folded in after the reviewer's second look: the two both roads still got wrong ----------
        new("chorale phrase pair: to the dominant, home through V7/ii (four voices)", Texture.FourVoices, true, "",
            Voices(ChoraleTwoSoprano, ChoraleTwoAlto, ChoraleTwoTenor, ChoraleTwoBass), [new(3, 7, true), new(7, 0, true)]),
        Block("Schubert: to the flat submediant and back, each by the common tone C held alone (block chords)",
            "0 5 7 0 | ~0h 8h 1 3 8 | 8 1 3:7 8 | ~0h 0h 5 7 0", [new(5, 8, true), new(13, 0, true)]),
    ];
}
