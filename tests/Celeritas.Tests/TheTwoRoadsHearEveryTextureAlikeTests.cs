// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The roads-disagree lens as a test. Every chord-bearing passage of the six tables in
/// <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/> is rebuilt in five
/// textures — every chord staccato for a quarter; a melody note held across every other chord
/// change; every chord struck twice in its bar; its second bar silent; every chord an eighth
/// off the beat — and asked of both roads in all twelve keys: from the same opening key they
/// must place the same modulations, within a bar of each other, and in the first two textures,
/// which change no harmony, the musician's plan must still be heard. Before the rules below,
/// the roads parted on seventeen of the six hundred and eighty-two cases of the first five
/// tables and missed the plan on forty-seven; the rules are each one passage here, with what
/// the roads answered before, measured on the library as it stood.
/// </summary>
/// <remarks>
/// <para>
/// The seventeen were of four shapes, none of them the judge's rules failing on music it had
/// seen: a chord struck twice in its bar resolved into its own restrike and was no applied
/// chord, so the trajectory's opening key changed; sixteenths struck one after another were
/// rounded onto the detector's eighth grid into two-note chords of neighbouring tones; a bar of
/// silence left a given key nothing to refute it with and a guessed one nothing to confirm it
/// with, and the roads opened in different keys; and chords an eighth late spilt a tail into the
/// next clock bar, where the old key owned it, or left the trajectory opening on a lone melody
/// note. A musician hears one harmony however often it is struck, a line in sixteenths, a key
/// from the first chord — struck, arpeggiated, or an eighth behind the tune — and bars from the
/// music's accents, not from the clock.
/// </para>
/// </remarks>
public class TheTwoRoadsHearEveryTextureAlikeTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature GMajor = new(7, true);

    /// <summary>Every passage of the six tables that is built from chords — block chords, arpeggios, a melody over chords or an Alberti bass — by name.</summary>
    public static TheoryData<string> ChordBearingPassages => [.. AllPassages().Where(p => p.Chords.Length > 0).Select(p => p.Name)];

    private static IEnumerable<RealModulationPassages.Passage> AllPassages() =>
        RealModulationPassages.All
            .Concat(RealModulationPassages.HeldOut)
            .Concat(RealModulationPassages.ReviewerHeldOut)
            .Concat(RealModulationPassages.ThirdReviewerHeldOut)
            .Concat(RealModulationPassages.FourthReviewerHeldOut)
            .Concat(TexturesAndHomecomingsPassages.Table);

    private static RealModulationPassages.Passage Named(string name) => AllPassages().Single(p => p.Name == name);

    /// <summary>The five textures, by name: each rebuilds a passage's chords and melody, in the tonic given.</summary>
    private static readonly (string Name, Func<List<NoteEvent>, List<NoteEvent>, List<NoteEvent>> Make)[] Textures =
    [
        ("every chord staccato", Staccato),
        ("a melody note held across every other chord change", Held),
        ("every chord struck twice in its bar", Twice),
        ("bar 2 silent", Silent),
        ("every chord an eighth off the beat", OffBeat),
    ];

    /// <summary>
    /// The one case the lens still leaves the roads apart on, and why. The lead decides: the
    /// D Dorian tune with its second bar silent, analyzed from D minor — a key that lacks the
    /// tune's B natural — is C major from bar 3 on the detector road, which is told D minor,
    /// and no modulation on the trajectory road, which opens in C major by the tune's notes.
    /// Given the same opening key the judge answers alike on both roads; the split is between
    /// the given key and the guessed one on a tune the judge has no mode for. With its second
    /// bar sounding, the C reading reaches the first note through the pivot bar and is the
    /// opening misjudged, so the tune is C throughout on both roads; the silence puts the
    /// pivot a bar later, and the reading stops there.
    /// </summary>
    private static readonly HashSet<(string Passage, string Texture)> TheRoadsStillDifferOn =
    [
        ("a D Dorian folk tune, the raised sixth in every other bar (melody over chords)", "bar 2 silent"),
    ];

    [Theory]
    [MemberData(nameof(ChordBearingPassages))]
    public void BothRoadsPlaceTheSameModulationsInEveryTextureOfEveryPassage(string name)
    {
        var passage = Named(name);
        var plan = passage.Plan.Length == 0 ? "none" : string.Join(", ", passage.Plan);
        foreach (var (texture, make) in Textures)
        {
            if (texture.StartsWith("a melody note held") && passage.Melody.Length == 0)
            {
                continue;
            }

            var mustHearThePlan = texture.StartsWith("every chord staccato") || texture.StartsWith("a melody note held");
            var readings = new HashSet<string>();
            for (var tonic = 0; tonic < 12; tonic++)
            {
                var (chords, melody) = Parts(passage, tonic);
                using var buffer = Buffer(make(chords, melody));
                var opening = new KeySignature((byte)((tonic + passage.OpeningRoot) % 12), passage.OpeningIsMajor);
                var detector = Detector(buffer, opening);
                var trajectory = Trajectory(buffer, passage.TrajectoryWindow);
                readings.Add($"detector [{Describe(detector, tonic)}] trajectory [{Describe(trajectory, tonic)}]");

                if (!TheRoadsStillDifferOn.Contains((name, texture)))
                {
                    Assert.True(
                        Agree(detector, trajectory),
                        $"{name}, {texture}, in {Names[tonic]}: the detector heard [{Describe(detector, tonic)}] and the trajectory [{Describe(trajectory, tonic)}]");
                }

                if (mustHearThePlan)
                {
                    Assert.True(
                        IsHeard(passage.Plan, detector, tonic) && IsHeard(passage.Plan, trajectory, tonic),
                        $"{name}, {texture}, in {Names[tonic]}: the roads heard [{Describe(detector, tonic)}] and [{Describe(trajectory, tonic)}] where a musician hears [{plan}]");
                }
            }

            // The same music in twelve keys is one reading.
            Assert.True(readings.Count == 1, $"{name}, {texture}: {string.Join(" | ", readings)}");
        }
    }

    [Fact]
    public void AChordStruckTwiceInItsBarIsOneHarmony()
    {
        // Every chord as two half notes. A chord resolved into its own restrike, so D7 D7 G was
        // no V7/V: C F G C | G C D7 G lost its applied chord and C owned nothing of it. The
        // trajectory, guessing its opening key, opened C G | D7 G C D7 G G in G — the one key
        // that owned the opening phrase — and heard no modulation where the detector, told C,
        // heard G at bar 2; and I vi V/V V | I IV V7 I opened in G and modulated to C at bar 5.
        // Struck twice within a bar or once, a chord is one harmony: it resolves where its
        // restrike does, no phrase begins on the second strike, and the key in force stands at
        // the restrike as it stood at the chord — asked afresh, the restruck C of B♭ E♭ C C F F
        // found C major standing again and was C's own chord, not B flat's V/V, and B flat
        // began at bar 8 on both roads where a musician hears it at bar 5.
        using var twoBars = Twice(RealModulationPassages.HeldOutNamed("two bars of C, then G for six (block chords)"));
        Assert.Equal([(new Rational(1, 1), GMajor)], Detector(twoBars, CMajor));
        Assert.Equal([(new Rational(1, 1), GMajor)], Trajectory(twoBars));

        using var halfCadence = Twice(RealModulationPassages.HeldOutNamed("I vi V/V V | I IV V7 I (block chords)"));
        Assert.Empty(Detector(halfCadence, CMajor));
        Assert.Empty(Trajectory(halfCadence));

        using var flatSeventh = Twice(RealModulationPassages.ThirdReviewerHeldOutNamed("to the flat seventh with V/V in the new key's first phrase: C F G C | Bb Eb C F | Bb Eb F7 Bb (block chords)"));
        var bFlat = new KeySignature(10, true);
        Assert.Equal([(new Rational(4, 1), bFlat)], Detector(flatSeventh, CMajor));
        Assert.Equal([(new Rational(4, 1), bFlat)], Trajectory(flatSeventh));
    }

    [Fact]
    public void SixteenthsStruckOneAfterAnotherAreALineNotChords()
    {
        // The arpeggio texture with every eighth struck twice: sixteenths. The detector rounded
        // every onset onto its eighth grid, so pairs of neighbouring sixteenths — E with G, G
        // with C — became two-note chords, no arpeggiated chord was heard, and G began at bar 7
        // on the detector road where the trajectory, which never rounds, placed it at bar 4. A
        // note moved by half its own length or more is at another place in time, not jittered.
        using var arpeggios = Twice(RealModulationPassages.ThirdReviewerHeldOutNamed("to the dominant with V/ii as a plain triad in its second bar (arpeggios)"));
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(arpeggios, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(arpeggios));
    }

    [Fact]
    public void TheBarsAreHeardFromTheMusicsAccentsNotFromTheClock()
    {
        // Every chord an eighth late, nothing else sounding: the bars are late by an eighth. Read
        // on the clock, the last eighth of each F major chord fell into the first whole note of
        // G's, the key that owned the tail owned the phrase outright, and four bars each of C, F,
        // G and C was C at bar 8 on both roads — after which the roads parted over where G began.
        // Arpeggiated, each clock bar held the last eighth of one chord and seven of the next,
        // no chord at all, and eight bars of C then eight of G went to E minor at the G.
        var fMajor = new KeySignature(5, true);
        using var blocks = OffBeat(RealModulationPassages.HeldOutNamed("four bars each of C, F, G and C (block chords)"));
        var expected = new List<(Rational, KeySignature)> { (new Rational(33, 8), fMajor), (new Rational(65, 8), GMajor), (new Rational(97, 8), CMajor) };
        Assert.Equal(expected, Detector(blocks, CMajor));
        Assert.Equal(expected, Trajectory(blocks));

        using var arpeggios = OffBeat(RealModulationPassages.Named("eight bars home, eight in the dominant, eight home (arpeggios)"));
        expected = [(new Rational(65, 8), GMajor), (new Rational(121, 8), CMajor)];
        Assert.Equal(expected, Detector(arpeggios, CMajor));
        Assert.Equal(expected, Trajectory(arpeggios));
    }

    [Fact]
    public void AChordReleasedLateIsTheBarBefores()
    {
        // A melody on the beat, its chords an eighth behind: the bars are the melody's, and each
        // chord's last eighth sounds into the next bar. Weighed where it sounded, the eighth of C
        // major under the G sharp that begins E major's first bar was two foreign pitch classes
        // for an eighth — a quarter note of weight, the most a bar may lack — so E major owned
        // nothing, and the passage to the chromatic mediant was a tonicization on one road and
        // no change on the other. A chord struck in the bar before that stops within an eighth
        // of the bar line is the harmony leaving, and weighs in the bar it was struck in.
        var eMajor = new KeySignature(4, true);
        using var mediant = OffBeat(RealModulationPassages.Named("to the chromatic mediant (melody over chords)"));
        Assert.Equal([(new Rational(4, 1), eMajor)], Detector(mediant, CMajor));
        Assert.Equal([(new Rational(4, 1), eMajor)], Trajectory(mediant));
    }

    [Fact]
    public void AGivenKeyThePieceDoesNotOpenOnIsAGuessTheMusicMayRefute()
    {
        // The detector is told a key; the trajectory guesses one. With its second bar silent —
        // the F chord gone — C F G C | C F G7 C, told G, has nothing left to refute G with at the
        // first note, and the detector heard a modulation to C at bar 5 where the trajectory,
        // opening in C, heard none; and the pickup passage G | C F G C | G C D7 G G, told C, lost
        // the F that confirmed C and the detector heard G at bar 5 where the trajectory, opening
        // in G, heard none. A musician told the key takes the first chord for its tonic until the
        // music says otherwise: a given key the piece does not open on is a guess, and a guess
        // that never sounds a note of its own before another key is read was wrong from the
        // start. Both passages are one key throughout on both roads.
        using var toldG = Silent(RealModulationPassages.ReviewerHeldOutNamed("in C throughout, the detector told the piece opens in G (block chords)"));
        Assert.Empty(Detector(toldG, GMajor));
        Assert.Empty(Trajectory(toldG));

        using var pickup = Silent(RealModulationPassages.ReviewerHeldOutNamed("to the dominant after a quarter-note pickup chord (block chords)"));
        Assert.Empty(Detector(pickup, CMajor));
        Assert.Empty(Trajectory(pickup));
    }

    [Fact]
    public void ThePieceOpensOnItsFirstChordStruckArpeggiatedOrAnEighthBehindTheTune()
    {
        // The opening chord had to be struck at the very first onset. Arpeggiated, C F G C | G E
        // Am D7 | G C D7 G with its second bar silent was so many single notes to the trajectory,
        // whose profile read C – G C as G and heard no modulation; and with the chords an eighth
        // late under a held dominant pedal, the piece opened on the pedal alone and the profile
        // guessed G. The detector, told C, heard G at bar 5 both times. The chord the piece
        // opens on is the first chord, struck or arpeggiated, within a quarter of the first note.
        using var arpeggios = Silent(RealModulationPassages.ThirdReviewerHeldOutNamed("to the dominant with V/ii as a plain triad in its second bar (arpeggios)"));
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(arpeggios, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(arpeggios));

        using var pedal = OffBeat(RealModulationPassages.ReviewerHeldOutNamed("to the dominant over a dominant pedal held throughout (melody over chords)"));
        Assert.Equal([(new Rational(33, 8), GMajor)], Detector(pedal, CMajor));
        Assert.Equal([(new Rational(33, 8), GMajor)], Trajectory(pedal));
    }

    [Fact]
    public void AChainOfAppliedDominantsIsTheKeys()
    {
        // Am Dm E7 Am | A7 D7 G7 C | C F G C: a chain of dominants into C. An applied chord had to
        // resolve into a chord the key owns outright, so the A7, resolving into D7, was a foreign
        // bar to C, and C was written at the D7 — a bar late, on both roads. An applied chord
        // resolving into another applied chord of the key resolves into the key: the chain is
        // C's from its first link, and C is written at the pivot Am before it, as the fixture's
        // pivots are written. And the arpeggiated chain of the second table, its second bar
        // silent, opens in C on both roads — the one key that owns C – A7 D7 with its chain, where
        // G major needed only one applied chord and was guessed by the trajectory instead.
        var aMinor = new KeySignature(9, false);
        using var chain = Blocks("9:m 2:m 4:7 9:m | 9:7 2:7 7:7 0 | 0 5 7 0");
        Assert.Equal([(new Rational(3, 1), CMajor)], Detector(chain, aMinor));
        Assert.Equal([(new Rational(3, 1), CMajor)], Trajectory(chain));

        using var arpeggiated = Silent(RealModulationPassages.HeldOutNamed("a chain of secondary dominants (arpeggios)"));
        Assert.Empty(Detector(arpeggiated, CMajor));
        Assert.Empty(Trajectory(arpeggiated));
    }

    [Fact]
    public void ANoteStruckWithAChordAndLetGoBeforeItIsTheLines()
    {
        // The melody with two chromatic passing eighths in each bar of G, its chords struck twice:
        // the B flat of C5 B4 B♭4 A4 fell on the G chord's restrike and was grouped into it, a
        // chord tone G major lacked, so the bar was no bar of G's and G was a one-bar tonicization
        // on the detector and nothing on the trajectory. A note struck with a chord and let go
        // before it, no tone of the chord that outlasts it, is the line's — a passing tone here —
        // and G begins at bar 5 on both roads.
        using var passing = Twice(RealModulationPassages.ReviewerHeldOutNamed("to the dominant, the melody with two chromatic passing eighths in each bar of the new key (melody over chords)"));
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(passing, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(passing));
    }

    [Fact]
    public void AnAppoggiaturaMayAnticipateItsChordAndAPhraseOpensOnTheChordUnderItsFirstNote()
    {
        // A melody on the beat with its chords an eighth behind. The appoggiatura struck on every
        // downbeat of G sounded alone for an eighth before the chord it leans on, so it leaned on
        // nothing and was a foreign quarter; and every phrase began on that lone note, no chord of
        // any key's, so nothing framed G's applied chords and G was never named — on either road.
        // A chord struck under a note within an eighth of its onset is the harmony the note leans
        // on, and a phrase opens on the chord under its first note: G begins at bar 5, as it does
        // with the 4-3 suspension and the chord tone struck over V/ii, which the roads had placed
        // at bar 7.
        foreach (var name in new[]
        {
            "to the dominant, a chromatic appoggiatura struck on every downbeat of the new key (melody over chords)",
            "to the dominant with V/ii as a triad, a 4-3 suspension struck over it (melody over chords)",
            "to the dominant with V/ii as a triad, a chord tone struck over it (melody over chords)",
        })
        {
            using var late = OffBeat(RealModulationPassages.ThirdReviewerHeldOutNamed(name));
            Assert.Equal([(new Rational(4, 1), GMajor)], Detector(late, CMajor));
            Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(late));
        }
    }

    [Fact]
    public void ThePicardyThirdNamesTheMinorKeyItCloses()
    {
        // Am Dm E7 Am | C F G C | C F G C | E7 A. The two-bar close was named A major by the
        // profile — A major owns E7 A outright, A minor only with the Picardy third counted as its
        // own — and since the music had never been in A major the close was a tonicization of it
        // on both roads. The Picardy third is the minor key's cadence: a span closing the piece on
        // the tonic major triad of a minor key the music has been in is that key's, not its
        // parallel major's, and the return home is written at the pivot C before the E7. And a
        // Picardy third struck twice in its final bar closes the piece as one struck once: the
        // hymn Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭ G7 C, every chord struck twice, ended in C major at
        // bar 11 on both roads, and is home in C minor at bar 9.
        var aMinor = new KeySignature(9, false);
        var passage = TexturesAndHomecomingsPassages.Table.Single(p => p.Name == "A minor, its relative major, and home to A minor closing on a Picardy third (block chords)");
        using var picardy = passage.Build(0);
        var expected = new List<(Rational, KeySignature)> { (new Rational(3, 1), CMajor), (new Rational(11, 1), aMinor) };
        Assert.Equal(expected, Detector(picardy, aMinor));
        Assert.Equal(expected, Trajectory(picardy));
        Assert.DoesNotContain(ModulationDetector.Analyze(picardy, aMinor).Modulations, m => m.Type == ModulationType.Tonicization);

        var cMinor = new KeySignature(0, false);
        using var hymn = Twice(RealModulationPassages.ReviewerHeldOutNamed("a hymn: minor, its relative major, minor again closing on a Picardy third (block chords)"));
        expected = [(new Rational(3, 1), new KeySignature(3, true)), (new Rational(8, 1), cMinor)];
        Assert.Equal(expected, Detector(hymn, cMinor));
        Assert.Equal(expected, Trajectory(hymn));
    }

    [Fact]
    public void AChainOfDominantsIsTheKeysWhoseCadenceItReaches()
    {
        // C F G C | B7 E7 A7 D7 | G7 C F C | C F G C: sixteen bars that never leave C, a chain of
        // dominants driving to the cadence in bar 9. Given to any key that owned a link of the
        // chain outright, the chain was E minor's — B7 its V7, D7 its natural minor's VII7, two
        // chromatic chords where C needed four — and the piece went to E minor at bar 4 and came
        // home at bar 9, on both roads. A chain of dominants is the key's whose tonic or dominant
        // it lands on, past its last seventh; and landing on G, C's dominant, the same chain before
        // G C D7 G | G C D7 G is C's until G is established at the G, as before.
        using var home = Blocks("0 5 7 0 | 11:7 4:7 9:7 2:7 | 7:7 0 5 0 | 0 5 7 0");
        Assert.Empty(Detector(home, CMajor));
        Assert.Empty(Trajectory(home));

        using var toTheDominant = Blocks("0 5 7 0 | 11:7 4:7 9:7 2:7 | 7 0 2:7 7 | 7 0 2:7 7");
        Assert.Equal([(new Rational(7, 1), GMajor)], Detector(toTheDominant, CMajor));
        Assert.Equal([(new Rational(7, 1), GMajor)], Trajectory(toTheDominant));
    }

    [Fact]
    public void TheBarsAreCountedFromTheFirstBarWhereverThePieceBegins()
    {
        // The pickup passage G(q) C F G C | G C D7 G G after a count-in bar of silence: the bars
        // begin a quarter after the clock, and so do the phrases. Anchored on the bars' phase
        // alone, a piece that began a bar in counted its phrases from the clock, and G moved from
        // the G chord at 21/4 to the last quarter of the C before it, at 5; started at zero the
        // same music placed G at 17/4. Shifting the music by a bar shifts every answer by a bar.
        var passage = RealModulationPassages.ReviewerHeldOutNamed("to the dominant after a quarter-note pickup chord (block chords)");
        var (chords, melody) = Parts(passage, 0);
        using var atZero = Buffer([.. chords, .. melody]);
        using var aBarIn = Buffer([.. chords.Concat(melody).Select(n => new NoteEvent(n.Pitch, n.Offset + Rational.Whole, n.Duration, n.Velocity))]);
        Assert.Equal([(new Rational(17, 4), GMajor)], Detector(atZero, CMajor));
        Assert.Equal([(new Rational(17, 4), GMajor)], Trajectory(atZero));
        Assert.Equal([(new Rational(21, 4), GMajor)], Detector(aBarIn, CMajor));
        Assert.Equal([(new Rational(21, 4), GMajor)], Trajectory(aBarIn));
    }

    [Fact]
    public void APicardyThirdRepeatedToTheEndClosesThePiece()
    {
        // A hymn's last chord restruck under the fermata, bar after bar: Am Dm E7 Am | C F G C |
        // Am Dm E7 A | A A. Only a restrike within the final bar closed the piece, so the A of bar
        // 12 was a chromatic chord and the close was a modulation to A major at bar 13 on both
        // roads; struck once, the same close was A minor's Picardy cadence. A final chord repeated
        // to the end with nothing between is the close, however many bars it is held for.
        var aMinor = new KeySignature(9, false);
        var expected = new List<(Rational, KeySignature)> { (new Rational(3, 1), CMajor), (new Rational(8, 1), aMinor) };
        using var restruck = Blocks("9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7 9 | 9 9");
        Assert.Equal(expected, Detector(restruck, aMinor));
        Assert.Equal(expected, Trajectory(restruck));
    }

    // ---------- the textures ----------

    /// <summary>The passage's chords and its melody, built in <paramref name="tonic"/>, apart.</summary>
    private static (List<NoteEvent> Chords, List<NoteEvent> Melody) Parts(RealModulationPassages.Passage passage, int tonic)
    {
        var chords = new List<NoteEvent>();
        using (var built = (passage with { Melody = [] }).Build(tonic))
        {
            for (var i = 0; i < built.Count; i++)
            {
                chords.Add(built.Get(i));
            }
        }

        var melody = passage.Melody
            .Where(n => n.Pitch != MusicNotation.RestPitch)
            .Select(n => new NoteEvent(n.Pitch + tonic, n.Offset + passage.Anacrusis, n.Duration, n.Velocity))
            .ToList();
        return (chords, melody);
    }

    /// <summary>Every chord cut to a quarter — struck, then silence — under the melody as written.</summary>
    private static List<NoteEvent> Staccato(List<NoteEvent> chords, List<NoteEvent> melody) =>
        [.. chords.Select(n => new NoteEvent(n.Pitch, n.Offset, n.Duration < Rational.Quarter ? n.Duration : Rational.Quarter, n.Velocity)), .. melody];

    /// <summary>At every other chord change, the melody note ending there is held through the note beginning there.</summary>
    private static List<NoteEvent> Held(List<NoteEvent> chords, List<NoteEvent> melody)
    {
        var result = melody.ToList();
        var k = 0;
        foreach (var at in chords.Select(c => c.Offset).Distinct().OrderBy(x => x).Skip(1))
        {
            if (k++ % 2 == 1)
            {
                continue;
            }

            var ending = result.FindIndex(n => n.Offset + n.Duration == at);
            var beginning = result.FindIndex(n => n.Offset == at);
            if (ending < 0 || beginning < 0)
            {
                continue;
            }

            var held = result[ending];
            result[ending] = new NoteEvent(held.Pitch, held.Offset, held.Duration + result[beginning].Duration, held.Velocity);
            result.RemoveAt(beginning);
        }

        return [.. chords, .. result];
    }

    /// <summary>Every chord struck twice: two halves of its length.</summary>
    private static List<NoteEvent> Twice(List<NoteEvent> chords, List<NoteEvent> melody) =>
        [.. chords.SelectMany(n => new[] { new NoteEvent(n.Pitch, n.Offset, n.Duration / 2, n.Velocity), new NoteEvent(n.Pitch, n.Offset + (n.Duration / 2), n.Duration / 2, n.Velocity) }), .. melody];

    /// <summary>Bar 2 silent: nothing begins in it, and a note reaching into it stops at its start.</summary>
    private static List<NoteEvent> Silent(List<NoteEvent> chords, List<NoteEvent> melody)
    {
        var from = new Rational(1, 1);
        var to = new Rational(2, 1);
        var result = new List<NoteEvent>();
        foreach (var n in chords.Concat(melody))
        {
            if (n.Offset >= from && n.Offset < to)
            {
                continue;
            }

            result.Add(n.Offset < from && n.Offset + n.Duration > from ? new NoteEvent(n.Pitch, n.Offset, from - n.Offset, n.Velocity) : n);
        }

        return result;
    }

    /// <summary>Every chord an eighth late; the melody as written.</summary>
    private static List<NoteEvent> OffBeat(List<NoteEvent> chords, List<NoteEvent> melody) =>
        [.. chords.Select(n => new NoteEvent(n.Pitch, n.Offset + Rational.Eighth, n.Duration, n.Velocity)), .. melody];

    private static NoteBuffer Twice(RealModulationPassages.Passage passage) => Build(passage, Twice);

    private static NoteBuffer Silent(RealModulationPassages.Passage passage) => Build(passage, Silent);

    private static NoteBuffer OffBeat(RealModulationPassages.Passage passage) => Build(passage, OffBeat);

    private static NoteBuffer Build(RealModulationPassages.Passage passage, Func<List<NoteEvent>, List<NoteEvent>, List<NoteEvent>> make)
    {
        var (chords, melody) = Parts(passage, 0);
        return Buffer(make(chords, melody));
    }

    /// <summary>Block chords in close root position from C3, one whole note each: <c>root[:quality]</c> as the fixture writes them; bar lines are ignored.</summary>
    private static NoteBuffer Blocks(string chords) =>
        new RealModulationPassages.Passage("blocks", RealModulationPassages.Texture.BlockChords, true, chords, [], []).Build(0);

    private static NoteBuffer Buffer(List<NoteEvent> notes)
    {
        var buffer = new NoteBuffer(Math.Max(1, notes.Count));
        buffer.AddRange(notes.OrderBy(n => n.Offset).ThenBy(n => n.Pitch).ToArray());
        return buffer;
    }

    // ---------- the roads ----------

    private static List<(Rational Position, KeySignature ToKey)> Detector(NoteBuffer buffer, KeySignature opening) =>
        ModulationDetector.Analyze(buffer, opening).Modulations
            .Where(m => m.Type != ModulationType.Tonicization)
            .Select(m => (m.Offset, m.ToKey))
            .ToList();

    private static List<(Rational Position, KeySignature ToKey)> Trajectory(NoteBuffer buffer, Rational? window = null) =>
        KeyProfiler.AnalyzeModulations(buffer, window ?? new Rational(2, 1), new Rational(1, 1))
            .DetectModulations()
            .Select(m => (m.Position, m.ToKey))
            .ToList();

    /// <summary>The roads agree when they report the same keys in the same order, each within one whole note of the other.</summary>
    private static bool Agree(List<(Rational Position, KeySignature ToKey)> a, List<(Rational Position, KeySignature ToKey)> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.ToKey == pair.Second.ToKey && Math.Abs((pair.First.Position - pair.Second.Position).ToDouble()) <= 1.0);

    private static bool IsHeard(RealModulationPassages.PlannedModulation[] plan, List<(Rational Position, KeySignature ToKey)> heard, int tonic) =>
        heard.Count == plan.Length
        && plan.All(p => heard.Any(h => h.ToKey.Root == (tonic + p.ToRoot) % 12 && h.ToKey.IsMajor == p.ToMajor && Math.Abs((h.Position - p.Position).ToDouble()) <= 1.0));

    /// <summary>The reading relative to the tonic it was built in, so twelve keys can be compared.</summary>
    private static string Describe(List<(Rational Position, KeySignature ToKey)> heard, int tonic) =>
        heard.Count == 0 ? "none" : string.Join(", ", heard.Select(h => $"{Names[PitchMath.Fold(h.ToKey.Root - tonic)]}{(h.ToKey.IsMajor ? "M" : "m")}@{h.Position}"));
}
