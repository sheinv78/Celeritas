// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The roads-disagree lens as a test. Every chord-bearing passage of the fourteen tables in
/// <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/> is rebuilt in five
/// textures — every chord staccato for a quarter; a melody note held across every other chord
/// change; every chord struck twice in its bar; its second bar silent; every chord an eighth
/// off the beat — and asked of both roads in all twelve keys: from the same opening key they
/// must place the same modulations, within a bar of each other, and in the first two textures,
/// which change no harmony, the musician's plan must still be heard. Before the rules below,
/// the roads parted on seventeen of the six hundred and eighty-two cases of the first five
/// tables and missed the plan on forty-seven; the rules are each one passage here, with what
/// the roads answered before, measured on the library as it stood. Over the fourteen tables —
/// one thousand one hundred and forty-three cases — the roads part on five, the two residuals
/// named below in their textures, and miss the plan on three more, one of them named below too: the German sixth an eighth off the
/// beat, an eighth outside the bar's tolerance, and the pickup passage with its second bar
/// silent, where the given key is a guess nothing confirms and both roads hear no change, as
/// the fact below pins.
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
/// <para>
/// The seventh table brought the pop loop: Am F C G four times, told C, was no modulation on
/// the detector and C at bar 2 on the trajectory, which opens on the A minor chord — the roads
/// apart in every texture, from different opening keys — and told A minor was C at bar 2 on
/// both. The relative major is confirmed by its cadence or its frame, and a loop that returns
/// to its first chord is the key it started in.
/// </para>
/// <para>
/// The eighth table — the pop loops a musician plays every day, told either of their keys, the
/// same loop a tone up, silence around the new key's first phrase, the melody over the Picardy
/// chord — brought sixty cases and no disagreement: the roads alike on every one, and the
/// plan heard wherever the harmony is unchanged. The eighth iteration's four rows bring eight
/// more, alike on every one; before their rules the minor blues in sevenths parted the roads in
/// every texture — the trajectory opening in C major from its Am7 — and the phrases C Dm G Am
/// between A minor's cadences were C on both roads in every texture. The loop a tone up told
/// C is a fact below and no row: the trajectory, told nothing, opens on the A minor chord and
/// hears the told-A-minor answer, B minor, where the detector told C hears D major — the roads
/// alike from the same opening key, and a plan cannot speak for both.
/// </para>
/// <para>
/// The ninth table — pieces opening on a seventh chord or on ii or IV, the same loop moved a
/// fourth, a minor third, a tone and two tones, a circle of fifths, three silent bars, tremolo
/// eighths for a whole passage, a trill on the Picardy third, the relative major's cadence inside
/// a phrase and at its end — brought a hundred and four cases and one disagreement: the piece
/// opening on IV with its second bar silent, where the trajectory opened on the F chord. The
/// ninth iteration's rules — a seventh chord is the tonic of its root's key alone, a pause
/// between phrases is heard wherever it falls and a fermata closes its phrase, the closing
/// harmony is what the accompaniment spells, a phrase that comes to rest on a chord opens in its
/// key — bring the minor blues told C to the table, four cases alike on every one, and resolve
/// that disagreement; each is a fact below, with what the roads answered before.
/// </para>
/// </remarks>
public class TheTwoRoadsHearEveryTextureAlikeTests
{
    private static readonly string[] Names = ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly KeySignature CMajor = new(0, true);
    private static readonly KeySignature GMajor = new(7, true);

    /// <summary>Every passage of the fourteen tables that is built from chords — block chords, arpeggios, a melody over chords or an Alberti bass — by name.</summary>
    public static TheoryData<string> ChordBearingPassages => [.. AllPassages().Where(p => p.Chords.Length > 0).Select(p => p.Name)];

    private static IEnumerable<RealModulationPassages.Passage> AllPassages() =>
        RealModulationPassages.All
            .Concat(RealModulationPassages.HeldOut)
            .Concat(RealModulationPassages.ReviewerHeldOut)
            .Concat(RealModulationPassages.ThirdReviewerHeldOut)
            .Concat(RealModulationPassages.FourthReviewerHeldOut)
            .Concat(TexturesAndHomecomingsPassages.Table)
            .Concat(AccompanimentTexturesPassages.Table)
            .Concat(LoopsSilencesAndClosesPassages.Table)
            .Concat(OpeningsSequencesAndTremolosPassages.Table)
            .Concat(RefutedKeysPausesAndDecoratedClosesPassages.Table)
            .Concat(MetresPedalsAndSharedRegistersPassages.Table)
            .Concat(BarsFromTheMusicAndSharedRegistersPassages.Table)
            .Concat(OrdinaryMusicPassages.Table)
            .Concat(TheLastReadingPassages.Table);

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
    /// The two passages the lens still leaves the roads apart on — five cases of the theory —
    /// and why. The lead decides.
    /// <para>
    /// The D Dorian tune with its second bar silent, analyzed from D minor — a key that lacks the
    /// tune's B natural — is C major from bar 3 on the detector road, which is told D minor,
    /// and no modulation on the trajectory road, which opens in C major by the tune's notes.
    /// Given the same opening key the judge answers alike on both roads; the split is between
    /// the given key and the guessed one on a tune the judge has no mode for. With its second
    /// bar sounding, the C reading reaches the first note through the pivot bar and is the
    /// opening misjudged, so the tune is C throughout on both roads; the silence puts the
    /// pivot a bar later, and the reading stops there. It resisted the seventh iteration too,
    /// which taught the judge that a silent bar is neither key's: the given D minor is no
    /// guess — the piece opens on its tonic chord — so the music may not refute it, and the B
    /// natural of the third bar is a note D minor lacks, whatever D Dorian owns; a rule that
    /// reached the first note across the silence — every bar back to it the new key's, the old
    /// key's own notes never sounded — took C C | G C D7 G, told C, for G throughout. The ninth
    /// iteration tried the same shape's rule from the other side — a chord-opened key whose
    /// opening phrase another key owns as well is a guess — which did resolve it, and moved ten
    /// table rows and eighty-five cases of this theory with it (C G | D7 G C D7 G G told C
    /// became G throughout; every loop told the minor whose leading tone never sounds became the
    /// major): the given D minor stands, as a given key the piece opens on does. The piece
    /// opening on IV with its second bar silent, the other residual of the eighth table, is
    /// resolved: a phrase that comes to rest on a chord opens in that chord's key
    /// (<see cref="APhraseThatComesToRestOnAChordOpensInItsKey"/>). The eleventh iteration's
    /// rules do not reach it either: the tune has no chords, so its bar is the clock's as
    /// before; it has no pedal and no figure; and the case is not a pivot bar but a given key
    /// against a guessed one. It reads exactly as it did.
    /// </para>
    /// <para>
    /// The thirteenth table brought the second, of the same shape: F G Am E7 | Am Dm E7 Am | Am
    /// Dm E7 Am told A minor, with its second bar silent. Told A minor, the detector heard one
    /// key, the musician's answer; the trajectory, told nothing, opened on the F major chord that
    /// begins the piece, and with the G of bar 2 silenced nothing refuted F before the cadence,
    /// so it reported A minor at bar 2. That residual is gone: the thirteenth iteration reads a
    /// triad of EITHER mode opening a piece as a chord of its key before it is a key, so the F
    /// major chord names nothing, the phrase is A minor's — which owns it outright, sounds its own
    /// tonic chord and its own dominant seventh in it, and keeps the piece — and both roads hear
    /// one key in every texture.
    /// </para>
    /// <para>
    /// The same iteration leaves one passage in its place, in all four of its textures, and it is
    /// the same shape once more: a given key against a guessed one. C G Am E7 | Am Dm E7 Am | Am
    /// Dm E7 Am, told C. Told C, the detector hears the two opening bars as the key it was given
    /// and A minor from its dominant at bar 3 — the musician's answer to a musician who has been
    /// told the piece is in C. The trajectory is told nothing and opens where a musician opening
    /// cold opens: ten of the twelve bars are A minor's, the C is its III, and there is no
    /// modulation to hear — the musician's answer to a musician who has been told nothing. Both
    /// readings are right, and they cannot be made one: the same music told A minor is the
    /// thirteenth table's <c>told A minor, opening on its III</c>, whose plan is no modulation at
    /// all, and a road that is told nothing answers both rows alike. The fixture lets the
    /// trajectory hear no change on the told-C row for the same reason.
    /// </para>
    /// </summary>
    private static readonly HashSet<(string Passage, string Texture)> TheRoadsStillDifferOn =
    [
        ("a D Dorian folk tune, the raised sixth in every other bar (melody over chords)", "bar 2 silent"),
        ("a loop of the relative major cadencing into the minor: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told C — A minor from its dominant at bar 4, the Am before it the pivot (block chords)", "every chord staccato"),
        ("a loop of the relative major cadencing into the minor: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told C — A minor from its dominant at bar 4, the Am before it the pivot (block chords)", "every chord struck twice in its bar"),
        ("a loop of the relative major cadencing into the minor: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told C — A minor from its dominant at bar 4, the Am before it the pivot (block chords)", "bar 2 silent"),
        ("a loop of the relative major cadencing into the minor: C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told C — A minor from its dominant at bar 4, the Am before it the pivot (block chords)", "every chord an eighth off the beat"),
    ];

    /// <summary>
    /// The one case where a texture that changes no harmony still loses a modulation, and why.
    /// The lead decides. The jazz standard's AABA told C — eight bars home, eight in the
    /// subdominant, eight home again — with every chord struck staccato for a quarter: both roads
    /// hear the bridge at bar 9 and neither hears the return home at bar 17, where struck as
    /// written both hear both. The roads agree with each other, as the promise says; what is lost
    /// is the last section's evidence, three quarters of every bar being silence, so the home key
    /// has a quarter note a bar to be named by where the bridge had eight bars to establish
    /// itself. It is the shape of the judge's documented limitations rather than a broken
    /// promise, and it is named here so that no reader takes the theory for more than it asserts.
    /// </summary>
    private static readonly HashSet<(string Passage, string Texture)> TheRoadsStillMissThePlanOn =
    [
        ("a JAZZ STANDARD'S AABA, told C, the bridge a fourth away for eight bars (block chords)", "every chord staccato"),
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

                if (mustHearThePlan && !TheRoadsStillMissThePlanOn.Contains((name, texture)))
                {
                    // A passage the trajectory may hear no change on is one a road told nothing
                    // opens in another key than the caller named, and hears one key in: the plan
                    // is the told road's (RealModulationPassages.Passage.TrajectoryMayHearNoChange).
                    Assert.True(
                        IsHeard(passage.Plan, detector, tonic)
                        && (IsHeard(passage.Plan, trajectory, tonic) || (passage.TrajectoryMayHearNoChange && trajectory.Count == 0)),
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
        expected = [(new Rational(65, 8), GMajor), (new Rational(129, 8), CMajor)];
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
        Assert.Equal([(new Rational(4, 1), CMajor)], Detector(chain, aMinor));
        Assert.Equal([(new Rational(4, 1), CMajor)], Trajectory(chain));

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
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(12, 1), aMinor) };
        Assert.Equal(expected, Detector(picardy, aMinor));
        Assert.Equal(expected, Trajectory(picardy));
        Assert.DoesNotContain(ModulationDetector.Analyze(picardy, aMinor).Modulations, m => m.Type == ModulationType.Tonicization);

        var cMinor = new KeySignature(0, false);
        using var hymn = Twice(RealModulationPassages.ReviewerHeldOutNamed("a hymn: minor, its relative major, minor again closing on a Picardy third (block chords)"));
        expected = [(new Rational(4, 1), new KeySignature(3, true)), (new Rational(8, 1), cMinor)];
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
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        using var restruck = Blocks("9:m 2:m 4:7 9:m | 0 5 7 0 | 9:m 2:m 4:7 9 | 9 9");
        Assert.Equal(expected, Detector(restruck, aMinor));
        Assert.Equal(expected, Trajectory(restruck));
    }

    [Fact]
    public void ASilentBarInsideTheNewKeysFirstPhraseIsNeitherKeys()
    {
        // C F G C | G C R G | G C D7 G. The phrase that opens on the G of bar 5 is G major's —
        // its cadence D7 G confirms it — but it has no F sharp of its own to be named for, and
        // the frame that hears a key from where its own chords began reached only the phrase
        // that holds the F sharp: G began at bar 9 on both roads, with the seventh bar silent
        // and with a G chord struck in it alike. A phrase that opens on the new key's tonic
        // chord, every bar of it the new key's, running into the phrase the key is heard from,
        // is the key's too; and a silent bar is neither the old key's nor the new — it neither
        // ends the phrase nor splits its frame. Both are G from bar 5, on both roads. A key
        // begins where its music does, though: C F G C | R | G C D7 G is G from bar 6, not from
        // the silence — the roads had it at bar 7.
        using var silent = AccompanimentTexturesPassages.Table.Single(p => p.Name.StartsWith("a bar of silence inside the new key's first phrase: C F G C")).Build(0);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(silent, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(silent));

        using var struck = Blocks("0 5 7 0 | 7 0 7 7 | 7 0 2:7 7");
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(struck, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(struck));

        using var before = TexturesAndHomecomingsPassages.Table.Single(p => p.Name == "a bar of silence before the new key (block chords as notes)").Build(0);
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(before, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(before));

        // The phrase reached back to is framed by the new tonic — it closes on it as it opens
        // on it. C F G C | G C G C | G C D7 G: the V I V I closing on C is C's, and G begins at
        // bar 9 on both roads, as it did before the rule; asked only where the phrase opened,
        // the frame took it for G's and put G at bar 5.
        using var closingOnC = Blocks("0 5 7 0 | 7 0 7 0 | 7 0 2:7 7");
        Assert.Equal([(new Rational(8, 1), GMajor)], Detector(closingOnC, CMajor));
        Assert.Equal([(new Rational(8, 1), GMajor)], Trajectory(closingOnC));
    }

    [Fact]
    public void AMelodyMovingThroughTheTonesOfTheClosingChordIsThatChord()
    {
        // Am Dm E7 Am | C F G C | Am Dm E7 A, the tune arpeggiating C sharp, E, A over the final
        // A major chord. The last harmony was the last chord struck, and three melody notes were
        // struck after it, so the chord was no Picardy third but a chromatic chord: A major owned
        // the close outright, and the Picardy cadence was a modulation to A major at bar 11 on
        // both roads. A note of the line that is a tone of the chord under it is that chord's,
        // and a chord followed only by its own tones closes the piece: the close is A minor's
        // Picardy cadence, home at the pivot Am of bar 9, as in the block-chord hymn.
        var aMinor = new KeySignature(9, false);
        var passage = AccompanimentTexturesPassages.Table.Single(p => p.Name.StartsWith("A minor, its relative major, home to A minor closing on a Picardy third, the melody arpeggiating"));
        using var picardy = passage.Build(0);
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        Assert.Equal(expected, Detector(picardy, aMinor));
        Assert.Equal(expected, Trajectory(picardy));
        Assert.DoesNotContain(ModulationDetector.Analyze(picardy, aMinor).Modulations, m => m.ToKey == new KeySignature(9, true));

        // Passing and neighbour notes between the chord's tones are the melody's way through
        // it: a scale run E D C sharp B A down to the final note, and a turn A B A G sharp A on
        // it, end the piece on the Picardy chord too. Read for its tones alone, each left the
        // chord no last harmony, and A major was heard at bar 11 on both roads.
        foreach (var close in new[] { "the Picardy close under a scale run down to the final note", "the Picardy close under a turn on the final note" })
        {
            using var moving = AccompanimentTexturesPassages.Table.Single(p => p.Name.StartsWith(close)).Build(0);
            Assert.Equal(expected, Detector(moving, aMinor));
            Assert.Equal(expected, Trajectory(moving));
        }
    }

    [Fact]
    public void TheRelativeMajorIsConfirmedByItsCadenceOrItsFrame()
    {
        // Am F C G four times, the commonest pop loop. The relative major owns no note the minor
        // lacks, so it was reached when a phrase read as the major with the minor's leading tone
        // nowhere in it — and every phrase of the loop but the one opening on A minor read as C.
        // Told A minor, both roads modulated to C at bar 2 of a loop that never leaves its key;
        // told C, the detector heard none while the trajectory, which opens on the A minor
        // chord, heard C at bar 2 — the roads apart from different opening keys. The relative
        // major is confirmed by something the minor cannot own: a phrase framed by its tonic
        // chord, a loop that returns to its first chord, or its own cadence, V to I, at a
        // phrase end. The loop is one key either way — C with vi first, or A minor with III and
        // VII — and neither road reports a modulation from either. The relative major that is
        // framed (Am Dm E7 Am | C F G C), that loops on its tonic (C Am F G twice) or that
        // cadences (Dm G C C twice) is C at the pivot Am of bar 4 still, on both roads.
        var aMinor = new KeySignature(9, false);
        using var loop = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7");
        Assert.Empty(Detector(loop, CMajor));
        Assert.Empty(Detector(loop, aMinor));
        Assert.Empty(Trajectory(loop));
        Assert.Empty(ModulationDetector.Analyze(loop, aMinor).Modulations);

        foreach (var chords in new[] { "9:m 2:m 4:7 9:m | 0 5 7 0 | 0 5 7 0", "9:m 2:m 4:7 9:m | 0 9:m 5 7 | 0 9:m 5 7", "9:m 2:m 4:7 9:m | 2:m 7 0 0 | 2:m 7 0 0" })
        {
            using var confirmed = Blocks(chords);
            Assert.Equal([(new Rational(4, 1), CMajor)], Detector(confirmed, aMinor));
            Assert.Equal([(new Rational(4, 1), CMajor)], Trajectory(confirmed));
        }

        // The cadence closes a phrase. Am F C G | Am F C G | C F G C | C F G C: the G C at the
        // seam of the loop and the phrase after is no cadence, and C begins with the phrase
        // framed by its chord, at the G pivot before bar 9, on both roads — asked of a window
        // from where the minor's tonic was left, the seam was C's cadence and C began at bar 5.
        using var seam = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 0 5 7 0 | 0 5 7 0");
        Assert.Equal([(new Rational(7, 1), CMajor)], Detector(seam, aMinor));
        Assert.Equal([(new Rational(7, 1), CMajor)], Trajectory(seam));
    }

    [Fact]
    public void ASeventhChordOpensTheKeyOfItsRoot()
    {
        // A minor twelve-bar blues in sevenths: Am7 × 4 | Dm7 Dm7 Am7 Am7 | E7 Dm7 Am7 E7. The
        // piece opens on its first chord, and that chord is named A minor seventh — but read for
        // its pitch classes alone, A C E G was no key's tonic triad outright, so the opening fell
        // to the profile, which took it for C major (the same four notes are C6), and the
        // trajectory heard a modulation from C major to A minor at the E7 — A minor's own
        // dominant — where the detector, told A minor, heard none. A seventh chord names its
        // root as a triad does: Am7 opens A minor, Cmaj7 opens C. A dominant seventh does not:
        // the blues opens on its I7 and a piece may open on its V7, and the profile decides
        // those as before — a major blues in sevenths is its key on both roads, and a piece
        // opening on G7 in C is C throughout.
        var aMinor = new KeySignature(9, false);
        using var minorBlues = Blocks("9:m7 9:m7 9:m7 9:m7 | 2:m7 2:m7 9:m7 9:m7 | 4:7 2:m7 9:m7 4:7");
        Assert.Empty(Detector(minorBlues, aMinor));
        Assert.Empty(Trajectory(minorBlues));

        using var majorBlues = Blocks("0:7 0:7 0:7 0:7 | 5:7 5:7 0:7 0:7 | 7:7 5:7 0:7 7:7");
        Assert.Empty(Detector(majorBlues, CMajor));
        Assert.Empty(Trajectory(majorBlues));
        Assert.Equal(CMajor, ModulationDetector.Analyze(majorBlues, CMajor).StartKey);

        using var onTheDominant = Blocks("7:7 0 5 7 | 0 5 7 0");
        Assert.Empty(Detector(onTheDominant, CMajor));
        Assert.Empty(Trajectory(onTheDominant));

        using var majorSeventh = Blocks("0:maj7 5 7 0 | 9:m 2:m 7:7 0:maj7");
        Assert.Empty(Detector(majorSeventh, CMajor));
        Assert.Empty(Trajectory(majorSeventh));
    }

    [Fact]
    public void ASequenceKeepsTheReadingOfItsModel()
    {
        // Am F C G twice, then Bm G D A twice. A loop with no leading tone is either of its
        // relative keys, and the second loop is the first a tone up: the reading follows the
        // opening. Told A minor, the first loop is A minor's — i VI III VII — and the second is
        // B minor at the pivot G of bar 8, on both roads, as before. Told C, the first loop is C
        // major with vi first, and the same loop a tone up is D major with vi first; named by the
        // chord it opened on, it was B minor on the detector road too. A phrase that is the
        // phrase before it transposed is heard in the key in force moved by the same interval.
        // The trajectory is told nothing and opens on the A minor chord, so it hears the
        // told-A-minor answer — the roads read the same loop alike from the same opening key.
        // And a phrase repeated is its model at the unison: the copy's repeat keeps the copy's
        // key, judged from the phrase a candidate falls in. Judged from the span alone, Am Dm G C
        // twice then Bm Em A D twice, told A minor, was B minor at the copy and D major at the
        // copy's repeat — the repeat read from its second chord, Em A D, closing the piece with
        // the cadence A D — a modulation between two identical phrases; and Am Dm G C four times,
        // told A minor, changed to C at its fourth statement by the same closing cadence. One
        // loop is one key.
        var aMinor = new KeySignature(9, false);
        var bMinor = new KeySignature(11, false);
        var dMajor = new KeySignature(2, true);
        using var aToneUp = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 11:m 7 2 9 | 11:m 7 2 9");
        Assert.Equal([(new Rational(7, 1), bMinor)], Detector(aToneUp, aMinor));
        Assert.Equal([(new Rational(7, 1), dMajor)], Detector(aToneUp, CMajor));
        Assert.Equal([(new Rational(7, 1), bMinor)], Trajectory(aToneUp));

        using var cadencingCopy = Blocks("9:m 2:m 7 0 | 9:m 2:m 7 0 | 11:m 4:m 9 2 | 11:m 4:m 9 2");
        Assert.Equal([(new Rational(8, 1), bMinor)], Detector(cadencingCopy, aMinor));
        Assert.Equal([(new Rational(8, 1), bMinor)], Trajectory(cadencingCopy));
        Assert.Equal([(new Rational(8, 1), dMajor)], Detector(cadencingCopy, CMajor));

        using var oneLoop = Blocks("9:m 2:m 7 0 | 9:m 2:m 7 0 | 9:m 2:m 7 0 | 9:m 2:m 7 0");
        Assert.Empty(Detector(oneLoop, aMinor));
        Assert.Empty(Trajectory(oneLoop));

        // The same loop moved a fourth up, a minor third up, a tone up and a tone up again, and
        // moved once only: told C, the detector names each copy the major with vi first — F, E
        // flat, D then E, D — at the pivot; the trajectory, opening on the A minor chord, names
        // the minor a fourth, a minor third, a tone and two tones up. The eighth reviewer's rows,
        // which no one plan can pin for both roads.
        using var aFourthUp = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 2:m 10 5 0 | 2:m 10 5 0");
        Assert.Equal([(new Rational(8, 1), new KeySignature(5, true))], Detector(aFourthUp, CMajor));
        Assert.Equal([(new Rational(8, 1), new KeySignature(2, false))], Trajectory(aFourthUp));

        using var aMinorThirdUp = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 0:m 8 3 10 | 0:m 8 3 10");
        Assert.Equal([(new Rational(8, 1), new KeySignature(3, true))], Detector(aMinorThirdUp, CMajor));
        Assert.Equal([(new Rational(7, 1), new KeySignature(0, false))], Trajectory(aMinorThirdUp));

        using var twiceUp = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 11:m 7 2 9 | 11:m 7 2 9 | 1:m 9 4 11 | 1:m 9 4 11");
        Assert.Equal([(new Rational(7, 1), dMajor), (new Rational(15, 1), new KeySignature(4, true))], Detector(twiceUp, CMajor));
        Assert.Equal([(new Rational(7, 1), bMinor), (new Rational(15, 1), new KeySignature(1, false))], Trajectory(twiceUp));

        using var movedOnce = Blocks("9:m 5 0 7 | 11:m 7 2 9 | 11:m 7 2 9");
        Assert.Equal([(new Rational(3, 1), dMajor)], Detector(movedOnce, CMajor));
        Assert.Equal([(new Rational(3, 1), bMinor)], Trajectory(movedOnce));
    }

    [Fact]
    public void ALoopThatComesToRestOnTheOldTonicIsTheOldKeys()
    {
        // Am Dm E7 Am | C Dm G Am | C Dm G Am | Am Dm E7 Am, told A minor. Each middle phrase
        // opens on the relative major's chord and closes on the minor's tonic — I ii V vi twice,
        // or III iv VII i twice — and a loop that returns to its first chord confirmed the
        // relative major without asking where the loop came to rest: both roads went to C at bar
        // 4 and came home at bar 13. A phrase that closes on the old key's tonic is the old key's,
        // as the frame reaching back already says; between A minor's cadences a musician hears
        // one key with a lean to the relative major. The mirror loop C Am F G, closing on G, is
        // C from the pivot Am of bar 4 as before.
        var aMinor = new KeySignature(9, false);
        using var resting = Blocks("9:m 2:m 4:7 9:m | 0 2:m 7 9:m | 0 2:m 7 9:m | 9:m 2:m 4:7 9:m");
        Assert.Empty(Detector(resting, aMinor));
        Assert.Empty(Trajectory(resting));

        using var mirror = Blocks("9:m 2:m 4:7 9:m | 0 9:m 5 7 | 0 9:m 5 7 | 0 9:m 5 7");
        Assert.Equal([(new Rational(4, 1), CMajor)], Detector(mirror, aMinor));
        Assert.Equal([(new Rational(4, 1), CMajor)], Trajectory(mirror));
    }

    [Fact]
    public void AMusicianCountsTheNewPhraseFromTheReEntry()
    {
        // C F G C | R | R | G C D7 G. The phrases were counted from the first bar through the
        // silence, so the grid fell on the silent fifth bar and on the ninth, the D7: the G of bar
        // 7 opened no phrase, the D7's phrase was two bars to the end, and both roads heard a
        // two-bar tonicization of G — no modulation — where a musician hears G from bar 7, the
        // first sounding bar of its phrase. A whole bar or more of silence beginning where a
        // phrase would is a pause between phrases, and the count resumes with the music. A
        // silent bar inside a phrase is a rest in it, and the count stands: C F G C | G R R G |
        // G C D7 G is G from bar 5 as before.
        using var pause = LoopsSilencesAndClosesPassages.Table.Single(p => p.Name.StartsWith("two bars of silence before the new key")).Build(0);
        Assert.Equal([(new Rational(6, 1), GMajor)], Detector(pause, CMajor));
        Assert.Equal([(new Rational(6, 1), GMajor)], Trajectory(pause));

        using var rest = LoopsSilencesAndClosesPassages.Table.Single(p => p.Name.StartsWith("two silent bars inside the new key's first phrase")).Build(0);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(rest, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(rest));
    }

    [Fact]
    public void ATremoloIsTheChordItSpellsWhateverItsSpeed()
    {
        // The Picardy close with the final A major chord in tremolo eighths — A C sharp against
        // E A — under the melody arpeggiating C sharp, E, A. An arpeggiated chord was single
        // notes only, so the strokes stayed dyads: a chord with a C sharp in it followed by
        // another, no restrike and no Picardy third, and A major owned the close — a modulation
        // to A major at bar 11 on both roads. Strokes of two notes or more, none longer than an
        // eighth, that spell a triad together are that chord, and the tune's notes over them
        // that are its tones are the line: the close is A minor's Picardy cadence, home at the
        // pivot Am of bar 9, as with the chord struck once or held under a fermata.
        var aMinor = new KeySignature(9, false);
        using var tremolo = LoopsSilencesAndClosesPassages.Table.Single(p => p.Name.StartsWith("the Picardy close, the final chord in tremolo eighths")).Build(0);
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        Assert.Equal(expected, Detector(tremolo, aMinor));
        Assert.Equal(expected, Trajectory(tremolo));
        Assert.DoesNotContain(ModulationDetector.Analyze(tremolo, aMinor).Modulations, m => m.ToKey == new KeySignature(9, true));
    }

    [Fact]
    public void ASeventhChordIsTheTonicOfItsRootsKeyAlone()
    {
        // The minor blues in sevenths told C — the relative major. A seventh chord opened the key
        // of its root on the trajectory road, but the detector's test of a given key still took
        // A C E G for C major's tonic with a note above it: told C, the detector took the piece
        // for opened on C's tonic, so C was no guess, and heard C going to A minor at bar 8 —
        // the E7, A minor's own dominant — where told A minor it heard none and the trajectory
        // heard A minor from the first bar. A seventh chord is the tonic of its root's key
        // alone (a sixth chord is a voicing the judge cannot tell from it without a bass), and a
        // given major key whose tonic chord never sounds before its relative minor is read was
        // the caller's guess: told C, told A minor, and told nothing, the blues is A minor
        // throughout. The same sibling read G → Am7 as C's V–I6, so C Dm G Am7 twice between A
        // minor's cadences went to C at bar 4 and came home at bar 13 where the same loop with
        // a plain A minor triad is one key; both are one key now.
        var aMinor = new KeySignature(9, false);
        using var blues = Blocks("9:m7 9:m7 9:m7 9:m7 | 2:m7 2:m7 9:m7 9:m7 | 4:7 2:m7 9:m7 4:7");
        Assert.Empty(Detector(blues, CMajor));
        Assert.Equal(aMinor, ModulationDetector.Analyze(blues, CMajor).StartKey);
        Assert.Empty(Detector(blues, aMinor));
        Assert.Empty(Trajectory(blues));

        using var sevenths = Blocks("9:m 2:m 4:7 9:m | 0 2:m 7 9:m7 | 0 2:m 7 9:m7 | 9:m 2:m 4:7 9:m");
        Assert.Empty(Detector(sevenths, aMinor));
        Assert.Empty(Trajectory(sevenths));

        using var triads = Blocks("9:m 2:m 4:7 9:m | 0 2:m 7 9:m | 0 2:m 7 9:m | 9:m 2:m 4:7 9:m");
        Assert.Empty(Detector(triads, aMinor));
        Assert.Empty(Trajectory(triads));

        // The other side of the same four pitch classes: a piece that voices its tonic as a sixth
        // chord — A C E G under C F G, the judge's chord either way — and plays plain triads after
        // it is C major throughout. Named by the seventh chord, the trajectory road opened in A
        // minor and heard a modulation to C at bar 5; a seventh chord names the opening as a
        // reading of a voicing, and the music may refute it.
        using var sixth = Blocks("9:m7 5 7 9:m7 | 0 5 7 0 | 0 5 7:7 0");
        Assert.Empty(Detector(sixth, CMajor));
        Assert.Empty(Trajectory(sixth));
    }

    [Fact]
    public void APauseBetweenPhrasesIsHeardWhereverItFallsAndAFermataClosesItsPhrase()
    {
        // The count restarted only at a pause that began where a phrase of the count would. C F
        // G C | C | R | R | G C D7 G — a fifth bar of tonic, then two silent bars off the grid —
        // ran on to the C of bar 9, so the G of bar 8 opened no phrase and both roads heard a
        // two-bar tonicization of G and no modulation, where a musician hears G from bar 8. A
        // pause between phrases is heard wherever it falls once a phrase has sounded, and the
        // judge keeps both counts — the one from the first bar, which a rest inside a phrase
        // does not move, and the one from the re-entry — asking the second when the first makes
        // no phrase of the music after the silence: G is at bar 8. The frame reaches back across
        // the pause to the phrase it cut short, so C F G C | G | R | R | G C D7 G is G from the G
        // of bar 5, and C F G C | G R C G | G C D7 G — a rest inside G's phrase, the shape a
        // count restarted at every silence lost — is G from bar 5 as before. And a fermata
        // closes its phrase: C F G C | R | G held two bars | G C D7 G had its count restarted at
        // the fermata, so the D7 opened a two-bar phrase and G was a tonicization; the count
        // moves on with the G of bar 8, and G is heard from the fermata, whose second bar is the
        // held chord's own.
        using var offTheGrid = OpeningsSequencesAndTremolosPassages.Table.Single(p => p.Name.StartsWith("a fifth bar of tonic, two silent bars off the phrase grid")).Build(0);
        Assert.Equal([(new Rational(7, 1), GMajor)], Detector(offTheGrid, CMajor));
        Assert.Equal([(new Rational(7, 1), GMajor)], Trajectory(offTheGrid));

        using var cutShort = Buffer([.. OpeningsSequencesAndTremolosPassages.Block("0 5 7 0 | 7 | R | R | 7 0 2:7 7")]);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(cutShort, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(cutShort));

        using var restInside = Buffer([.. OpeningsSequencesAndTremolosPassages.Block("0 5 7 0 | 7 R 0 7 | 7 0 2:7 7")]);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(restInside, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(restInside));

        using var fermata = OpeningsSequencesAndTremolosPassages.Table.Single(p => p.Name.StartsWith("a silent bar, then the new key entering on a two-bar fermata")).Build(0);
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(fermata, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(fermata));
    }

    [Fact]
    public void TheClosingHarmonyIsWhatTheAccompanimentSpellsAndTheTuneOverItIsTheLine()
    {
        // The Picardy chord closing A minor's piece as an Alberti bass in eighths under the tune's
        // C sharp, E, A; restruck in quarters under E, D, B — the B an unresolved ninth; and in
        // tremolo eighths under C sharp, D, E — the D a passing tone. The tune's quarters over the
        // Alberti eighths defeated the arpeggio rule, which allowed longer notes over a tremolo
        // only; the tremolo rule allowed them only when they were the chord's own tones; and the
        // chord's second and third quarters, struck with the D and the B, were A C sharp E D and
        // A C sharp E B, no restrike of A major and no plain triad — so each time the chord was
        // no last harmony, A minor could not own the close, and the Picardy cadence was a
        // modulation to A major at bar 11 on both roads. The closing harmony is what the
        // accompaniment spells, and the tune over it is the line: quick single notes spelling a
        // chord below every longer note are that chord under the tune, a tremolo's strokes are
        // the chord whatever the tune does, and a triad struck again in its bar with one note
        // more is the triad restruck under the tune's note. Each close is A minor's homecoming
        // at the pivot Am of bar 9, as with the chord struck once.
        var aMinor = new KeySignature(9, false);
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        foreach (var prefix in new[]
        {
            "the Picardy close, the final chord as an Alberti bass in eighths",
            "the Picardy close, the final chord restruck in quarters under a melody ending on the second",
            "the Picardy close in tremolo eighths, a passing D in the tune over it",
        })
        {
            using var close = OpeningsSequencesAndTremolosPassages.Table.Single(p => p.Name.StartsWith(prefix)).Build(0);
            Assert.Equal(expected, Detector(close, aMinor));
            Assert.Equal(expected, Trajectory(close));
            Assert.DoesNotContain(ModulationDetector.Analyze(close, aMinor).Modulations, m => m.ToKey == new KeySignature(9, true));
        }
    }

    [Fact]
    public void APhraseThatComesToRestOnAChordOpensInItsKey()
    {
        // F G C C | F G C C | Dm G C C, told C, with its second bar silent: the trajectory, told
        // nothing, opened on the F chord in F major — the opening phrase F – C C being F's as
        // much as C's — and the G that refutes F fell in the second phrase, so it heard C at bar
        // 5 where the detector, told C, heard no change and a musician hears C throughout, IV V
        // I I; with the second bar sounding the G refuted F inside the phrase and both roads
        // opened in C. A phrase that comes to rest on a chord — one tonic chord through its last
        // two whole notes — opens in that chord's key when that key owns the phrase as well: the
        // rest is a reading of the phrase, refutable as a guessed opening is: C C G G | C F G C
        // opens in G by its rest until the F refutes G, and is C throughout as it was when the
        // C chord named it. A phrase resting on nothing opens on its chord as before: C G C G |
        // G D7 G G told C is C going to G at the pivot G of bar 4 on both roads.
        using var openingOnIV = Silent(Named("a piece opening on IV: F G C C | F G C C | Dm G C C, told C (block chords)"));
        Assert.Empty(Detector(openingOnIV, CMajor));
        Assert.Empty(Trajectory(openingOnIV));

        using var restingOnG = Blocks("0 0 7 7 | 0 5 7 0");
        Assert.Empty(Detector(restingOnG, CMajor));
        Assert.Empty(Trajectory(restingOnG));

        using var restingOnNothing = Blocks("0 7 0 7 | 7 2:7 7 7");
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(restingOnNothing, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(restingOnNothing));
    }

    [Fact]
    public void AGivenKeyIsRefutedWhereThePieceOpensAndNotByWhatComesLater()
    {
        // A given major key the piece does not open on is the caller's guess, and its tonic
        // chord is what confirms it, the relative minor owning no note the major lacks. The
        // chord was looked for over the window the minor was read in, which reaches past the
        // opening phrase, so the C of bar 5 answered for bar 1; and the refutation waited behind
        // the margin a change must clear, which is hysteresis against a wobble away from the key
        // the music is in and has nothing to guard at the opening — a relative pair separate by
        // almost nothing, 0.020 here. Told C, Am7 Dm7 E7 Am7 | C F G C | C F G7 C was C
        // throughout on the detector road and A minor going to C at bar 4 on the trajectory,
        // which is told nothing: the roads apart in all twelve keys, where a musician hears A
        // minor first and C from bar 5 on both.
        using var openingOnTheRelativeMinor = Blocks("9:m7 2:m7 4:7 9:m7 | 0 5 7 0 | 0 5 7:7 0");
        Assert.Equal([(new Rational(4, 1), CMajor)], Detector(openingOnTheRelativeMinor, CMajor));
        Assert.Equal([(new Rational(4, 1), CMajor)], Trajectory(openingOnTheRelativeMinor));

        // And the everyday shapes the rule must not fire on: a loop that opens on vi and sounds
        // its C inside the opening phrase is C's with vi first, and a minor blues told C that
        // never sounds a C major chord is A minor from bar 1, as before.
        using var viFirst = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7");
        Assert.Empty(Detector(viFirst, CMajor));
        Assert.Empty(Trajectory(viFirst));

        using var minorBlues = Blocks("9:m7 9:m7 9:m7 9:m7 | 2:m7 2:m7 9:m7 9:m7 | 4:7 2:m7 9:m7 4:7");
        Assert.Empty(Detector(minorBlues, CMajor));
        Assert.Empty(Trajectory(minorBlues));
    }

    [Fact]
    public void TheAccompanimentStoppingOverAHeldPedalIsAPauseBetweenPhrases()
    {
        // A bar in which nothing is struck is a bar of silence for the phrase count, whatever
        // one voice is still holding. Counted only where nothing sounded at all, C F G C | R |
        // G C D7 G over a held dominant pedal had no silent bar, so the pause between its
        // phrases was never heard: the detector heard a two-bar tonicization of G at bar 8 and
        // the trajectory no change at all, the roads apart in all twelve keys, where a musician
        // hears G from bar 6 on both — as both roads hear it when the pedal is lifted.
        using var pause = BlocksOver(43, new Rational(9, 1), ("0 5 7 0", 0), ("7 0 2:7 7", 5));
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(pause, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(pause));

        // Two voices of the fourth bar's chord tied over the rest are no more played than one:
        // counted a pause only where one pitch class at most sounded on, this read as no
        // modulation at all on both roads, where a musician hears the same G from bar 6.
        using var tied = BlocksUnder(
            [new NoteEvent(52, new Rational(3, 1), new Rational(2, 1)), new NoteEvent(55, new Rational(3, 1), new Rational(2, 1))],
            ("0 5 7 0", 0), ("7 0 2:7 7", 5));
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(tied, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(tied));

        // The same pedal with nothing silent under it is the shape the rule must not fire on,
        // and a fermata — a chord struck alone and held with nothing struck under it — is no
        // pause either: it closes its own phrase, and its chorale is read as it was.
        using var unbroken = BlocksOver(43, new Rational(12, 1), ("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7", 0));
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(unbroken, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(unbroken));

        using var chorale = Named("a chorale, C, G, and home to C in a two-bar phrase, fermatas on every phrase (four voices)").Build(0);
        var expected = new List<(Rational, KeySignature)> { (new Rational(5, 1), GMajor), (new Rational(10, 1), CMajor) };
        Assert.Equal(expected, Detector(chorale, CMajor));
        Assert.Equal(expected, Trajectory(chorale));
    }

    [Fact]
    public void ARestrikeCarriesTheChordsHarmonyOnUnderTheLine()
    {
        // The last harmony of the piece is the last chord struck, its restrikes and the notes of
        // the line sounding while its harmony holds looked through. The harmony was measured
        // from the first strike alone, so a trill in sixteenths over the Picardy chord restruck
        // in quarters ran past where the first quarter's harmony held: that first strike was no
        // last harmony, its C sharp was a note A minor lacks, and the Picardy cadence was a
        // modulation to A major at bar 11 on both roads. A restrike carries the harmony on.
        var aMinor = new KeySignature(9, false);
        using var trill = Named("the Picardy close restruck in quarters under a TRILL on the third: C sharp D C sharp D ... (four voices)").Build(0);
        var expected = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        Assert.Equal(expected, Detector(trill, aMinor));
        Assert.Equal(expected, Trajectory(trill));

        // A chord restruck under a tune that ends on a note it does not hold is heard as it was:
        // the seventh over the closing tonic triad is the line's, and the close is the key's.
        using var seventh = Named("a MAJOR piece home at the close, the final chord restruck in quarters under a tune ending on the SEVENTH, B (four voices)").Build(0);
        expected = [(new Rational(4, 1), GMajor), (new Rational(9, 1), CMajor)];
        Assert.Equal(expected, Detector(seventh, CMajor));
        Assert.Equal(expected, Trajectory(seventh));
    }

    [Fact]
    public void ABarIsAsLongAsTheMusicsOwnBars()
    {
        // The judge counted every bar as a whole note, no road passing it a metre. The same
        // passage — C ii V7 I, then two phrases in the dominant, one chord a bar — is heard in
        // the same bar of its own metre whatever that metre is: at bar 5 of 4/4, of 3/4, of 6/8
        // and of 12/8 alike. Written in 12/8, whose bars are a bar and a half of the clock, the
        // judge read four whole notes for its phrase and heard the dominant at 9, two of the
        // music's bars late, where a musician writes it at 6; written in bars of two whole
        // notes it heard nothing at all, its phrase being half the music's.
        var barOf = new (string Name, Rational Bar)[]
        {
            ("METRE 4/4: C Dm G7 C | G C D7 G | G C D7 G, one chord a bar of four quarters (block chords)", Rational.Whole),
            ("METRE 3/4: the same passage, one chord a bar of three quarters (block chords)", new Rational(3, 4)),
            ("METRE 6/8: the same passage, one chord a bar struck on both dotted quarters (block chords)", new Rational(3, 4)),
            ("METRE 12/8: the same passage, one chord a bar of a bar and a half (block chords)", new Rational(3, 2)),
            ("a SLOW piece of twelve two-bar chords: C F G C | G C D7 G | G C D7 G (block chords)", new Rational(2, 1)),
        };

        foreach (var (name, bar) in barOf)
        {
            using var buffer = Named(name).Build(0);
            var atTheFifthBar = bar * 4;
            Assert.Equal([(atTheFifthBar, GMajor)], Detector(buffer, CMajor));
            Assert.Equal([(atTheFifthBar, GMajor)], Trajectory(buffer));
        }

        // A harmony is what the music strikes together, and where it strikes three notes
        // together anywhere, two are no harmony: the bass of a broken accompaniment and the
        // tune's note over it are two voices. Counted as a harmony, they changed the harmony on
        // every beat of a waltz under a tune in quarters, no gap had the majority, and the bar
        // of three quarters was lost to the clock — the dominant heard at 4, a whole note after
        // the fifth bar of 3/4 a musician writes it at, and after the same waltz's left hand
        // alone was heard at 3.
        using var waltz = Named("a waltz accompaniment under a melody in quarters: C then G (3/4)").Build(0);
        Assert.Equal([(new Rational(3, 1), GMajor)], Detector(waltz, CMajor));
        Assert.Equal([(new Rational(3, 1), GMajor)], Trajectory(waltz));

        using var leftHand = Named("a waltz accompaniment, the bass on one and the chord on two and three: C then G (left hand alone, 3/4)").Build(0);
        Assert.Equal([(new Rational(3, 1), GMajor)], Detector(leftHand, CMajor));
        Assert.Equal([(new Rational(3, 1), GMajor)], Trajectory(leftHand));

        // The shape the rule must not fire on: chords that go a whole number to the whole note
        // are several to a bar, not bars of their own. Read as bars, C D E F sharp in half-bar
        // chords would give each key a phrase of four of them and name four keys, where a
        // musician hears a new chord every two bars and no key at all.
        using var halfBars = Named("a new key every two bars, C D E F sharp (half-bar chords)").Build(0);
        Assert.Empty(Detector(halfBars, CMajor));
        Assert.Empty(Trajectory(halfBars));
    }

    [Fact]
    public void APedalIsOneNoteUnderTheHarmonyNotTheHarmony()
    {
        // A pedal is struck once and rings under chord after chord: it weighs until the next
        // chord is struck, and is no part of the harmony after that. Weighed to the end of its
        // sound, the tonic C under C F G C | R | G C D7 G outweighed the dominant's whole
        // phrase — and stood in the chord the phrase opens on, so that G's phrase opened on
        // C E G plus a C — and neither road heard G at all, where a musician hears it from bar
        // 6 as both roads do over a dominant pedal, which was heard only because it happened to
        // spell the new key. The same passage in two-whole-note chords under the same pedal was
        // C throughout as well.
        using var tonicPedal = BlocksOver(36, new Rational(9, 1), ("0 5 7 0", 0), ("7 0 2:7 7", 5));
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(tonicPedal, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(tonicPedal));

        // The shape the rule must not fire on: a bar with a chord struck over the pedal is
        // still that chord's bar, and the pedal that changes with the key is heard as before.
        using var unbroken = BlocksOver(36, new Rational(12, 1), ("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7", 0));
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(unbroken, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(unbroken));

        using var changing = Named("a pedal that CHANGES with the key: C under the first phrase, G under the rest, no bar silent (four voices)").Build(0);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(changing, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(changing));
    }

    [Fact]
    public void TheFigureIsAVoiceOfItsOwnAboveTheTuneAsWellAsBelowIt()
    {
        // Quick single notes that spell one chord are that chord under — or over — the tune,
        // the figure being a voice of its own: all of it below every longer note struck with
        // it, or all of it above. Told from the tune by lying below it only, the closing chord
        // arpeggiated two octaves above the tune over a held bass was so many single notes, the
        // piece had no last harmony, and A minor's Picardy cadence went missing on both roads.
        var aMinor = new KeySignature(9, false);
        var home = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        using var above = Named("the Picardy close, the figure ABOVE the tune: the chord arpeggiated two octaves up over a held bass (four voices)").Build(0);
        Assert.Equal(home, Detector(above, aMinor));
        Assert.Equal(home, Trajectory(above));

        // The shape the rule must not fire on: a tune in quarters and eighths that crosses its
        // own register is a tune, not a figure with a tune over it, whatever its eighths spell.
        using var tune = Named("to the dominant with escape tones and anticipations, three of the escape tones chromatic (melody over chords)").Build(0);
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(tune, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(tune));
    }

    [Fact]
    public void ThePhraseTheOldKeyClosesOnIsNoPivotBar()
    {
        // A modulation is written at its pivot chord — the bar before the new key's own note,
        // when the new key owns that bar whole — unless that bar closes the phrase before on
        // the old key's own tonic chord, which is the old key's cadence. In 5/4, whose bars
        // hold the cadential C of the first phrase whole, C F G C | G C D7 G under a tune was
        // heard from that C, a bar before the phrase that leaves C; on the clock's bars the
        // same reading fell inside the C chord, a whole note before the dominant.
        using var fiveFour = Named("to the dominant in 5/4 (melody over chords)").Build(0);
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(fiveFour, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(fiveFour));

        // The shape the rule must not fire on: a pivot bar that is no cadence of the old key's
        // is the pivot as before — C F G C | G D7 G is written at the G, not at the D7.
        using var pivot = Blocks("0 5 7 0 | 7 2:7 7");
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(pivot, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(pivot));
    }

    [Fact]
    public void AMinorChordOpeningAPieceIsNoKeyUntilTheMusicMakesItOne()
    {
        // vi, iii and ii are chords of a key, not keys. Told C, the detector heard no change in
        // any of these three; the trajectory, told nothing, opened on the A minor or E minor
        // chord and reported C at bar 3 or bar 4 — the roads apart in all twelve keys and in
        // every texture, where a musician hears C throughout with a vi or a iii first.
        using var sixWithTheTonic = Blocks("9:m 2:m 7:7 0 | 0 5 7 0 | 0 5 7:7 0");
        Assert.Empty(Detector(sixWithTheTonic, CMajor));
        Assert.Empty(Trajectory(sixWithTheTonic));

        using var sixWithout = Blocks("9:m 2:m 5 7 | 0 5 7 0 | 0 5 7:7 0");
        Assert.Empty(Detector(sixWithout, CMajor));
        Assert.Empty(Trajectory(sixWithout));

        using var third = Blocks("4:m 9:m 0 7 | 0 5 7 0 | 0 5 7:7 0");
        Assert.Empty(Detector(third, CMajor));
        Assert.Empty(Trajectory(third));

        // The shapes the rule must not fire on. A phrase that makes the minor its own keeps it:
        // the loop Am F C G comes back to its Am every fourth bar and is A minor to a road told
        // nothing, so the relative major is heard where the fixture pins it, at the phrase its
        // own chord frames.
        var aMinor = new KeySignature(9, false);
        using var loop = Named("Am F C G twice, then C F G C twice: the relative major begins with the phrase framed by its chord, told A minor (block chords)").Build(0);
        Assert.Equal([(new Rational(7, 1), CMajor)], Detector(loop, aMinor));
        Assert.Equal([(new Rational(7, 1), CMajor)], Trajectory(loop));

        // And a phrase whose E7 no major key owns is A minor's own: Am Dm E7 Am | C F G C is
        // A minor to bar 5 on both roads.
        using var cadence = Blocks("9:m 2:m 4:7 9:m | 0 5 7 0");
        Assert.Equal([(new Rational(4, 1), CMajor)], Detector(cadence, aMinor));
        Assert.Equal([(new Rational(4, 1), CMajor)], Trajectory(cadence));
    }

    [Fact]
    public void APedalStruckAgainWithEveryChordIsStillOneNoteUnderTheHarmony()
    {
        // The guitar's and the keyboard's ordinary accompaniment: the tonic struck afresh at
        // every bar line. Struck with the chord and stopping with it, it is one sonority with
        // the chord on both roads — the detector road merges the notes that stop together into
        // one part with no pitch at all — so a tonic C under C Dm G7 C | G C D7 G | G C D7 G
        // made Dm7 of the ii and a G7 with an added fourth of the V, and both roads heard one
        // key where a musician hears the dominant from bar 5, as both roads hear it when the
        // pedal is held instead of struck.
        using var struckPedal = Named("a tonic pedal STRUCK AGAIN every bar, not held (four voices)").Build(0);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(struckPedal, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(struckPedal));

        // The shape the rule must not fire on: a bass that walks sounds no one pitch class
        // under the harmony, and its chords are the chords they are.
        var walking = new List<NoteEvent>();
        int[] roots = [0, 5, 7, 0, 7, 0, 2, 7];
        using (var blocks = Blocks("0 5 7 0 | 7 0 2:7 7"))
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                walking.Add(blocks.Get(i));
            }
        }

        for (var bar = 0; bar < roots.Length; bar++)
        {
            walking.Add(new NoteEvent(36 + roots[bar], new Rational(bar, 1), Rational.Whole));
        }

        using var bassLine = Buffer(walking);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(bassLine, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(bassLine));
    }

    [Fact]
    public void AChordSpreadIsThatChordStruckAtItsFirstNote()
    {
        // A chord rolled — its notes struck a sixteenth or a quarter apart and all held — is one
        // chord, not so many single notes. Rolled, A minor's Picardy close left the piece with
        // no chord in its last bar, so it closed on nothing and the return home at bar 9 was
        // heard on neither road: both stopped at the relative major of bar 5.
        var aMinor = new KeySignature(9, false);
        var home = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        using var rolled = Named("the Picardy close, the last chord ROLLED UPWARDS a sixteenth at a time and all held, under the tune (four voices)").Build(0);
        Assert.Equal(home, Detector(rolled, aMinor));
        Assert.Equal(home, Trajectory(rolled));

        using var spread = Named("the Picardy close, the last chord ROLLED OVER TWO BARS, its notes a quarter apart and all held (four voices)").Build(0);
        Assert.Equal(home, Detector(spread, aMinor));
        Assert.Equal(home, Trajectory(spread));

        // The shapes the rule must not fire on. An arpeggio is quick notes let go one by one,
        // and stays one; and a block chord with a note of the tune let go with it is no chord
        // spread — the notes must be struck one after another, three strokes or more.
        using var arpeggio = Named("the Picardy close, the melody arpeggiating the final chord ALONE in sixteenths (four voices)").Build(0);
        Assert.Equal(home, Detector(arpeggio, aMinor));
        Assert.Equal(home, Trajectory(arpeggio));

        using var ahead = Named("every chord pushed an eighth ahead of the bar under a melody on the beat: to the dominant (four voices)").Build(0);
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(ahead, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(ahead));
    }

    [Fact]
    public void TheFigureIsTheVoiceThatSpellsOneChordEvenInTheTunesOwnOctave()
    {
        // Where the figure lies in the tune's own octave, the register cannot tell them apart,
        // and the figure is the voice that spells one chord while the other voice moves: quick
        // notes running unbroken from the beginning of the bar to its end. A closing A major
        // figure — A E C sharp E in eighths — under a tune C sharp, B, A in the same octave over
        // a held bass was so many single notes, the last bar held no chord, and A minor's return
        // home at bar 9 went missing on both roads.
        var aMinor = new KeySignature(9, false);
        var home = new List<(Rational, KeySignature)> { (new Rational(4, 1), CMajor), (new Rational(8, 1), aMinor) };
        using var sameOctave = Named("the Picardy close, the figure in the TUNE'S OWN OCTAVE over a held bass (four voices)").Build(0);
        Assert.Equal(home, Detector(sameOctave, aMinor));
        Assert.Equal(home, Trajectory(sameOctave));

        // The shape the rule must not fire on: a tune in quarters and eighths is a tune, whose
        // eighths do not run the bar through, whatever they spell.
        using var tune = Named("to the dominant with escape tones and anticipations, three of the escape tones chromatic (melody over chords)").Build(0);
        Assert.Equal([(new Rational(5, 1), GMajor)], Detector(tune, CMajor));
        Assert.Equal([(new Rational(5, 1), GMajor)], Trajectory(tune));
    }

    [Fact]
    public void AChordThatSoundsOnceMoreIsNoLoopAndNoKey()
    {
        // The fence that keeps Am F C G a minor loop was the whole rest of the piece: the opening
        // minor triad named its key wherever that chord sounded again, so one later vi was enough.
        // The commonest pop loop of all — Am F C G7 three times over and then C F G7 C — opened in
        // A minor on the trajectory road, which is told nothing, and reported C major at bar 12,
        // where the detector told C heard nothing and a musician hears C throughout with a vi
        // first; and a piece told C that opens on vi, comes home to C at bar 4 and moves to a real
        // A minor at bar 9 was heard twice over on that road, C at bar 3 and A minor at bar 9. A
        // loop is a chord coming back at the music's own phrase length, at the head of a repeating
        // unit; a loop that runs to its last phrase and cadences there in the relative major was
        // the major's all along; and a phrase that closes on the major's own V7 – I is the major's
        // whatever it opened on.
        var aMinor = new KeySignature(9, false);
        using var popLoop = Blocks("9:m 5 0 7:7 | 9:m 5 0 7:7 | 9:m 5 0 7:7 | 0 5 7:7 0");
        Assert.Empty(Detector(popLoop, CMajor));
        Assert.Empty(Trajectory(popLoop));

        using var fence = Blocks("9:m 2:m 7:7 0 | 0 5 7 0 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m");
        Assert.Equal([(new Rational(8, 1), aMinor)], Detector(fence, CMajor));
        Assert.Equal([(new Rational(8, 1), aMinor)], Trajectory(fence));

        // The loops the fence must still keep: one that ends on its own fourth chord, and one that
        // breaks off into a section of its own rather than into the relative major's cadence.
        using var minorLoop = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7 | 9:m 5 0 7");
        Assert.Empty(Detector(minorLoop, aMinor));
        Assert.Empty(Trajectory(minorLoop));

        using var section = Blocks("9:m 5 0 7 | 9:m 5 0 7 | 0 5 7 0 | 0 5 7 0");
        Assert.Equal([(new Rational(7, 1), CMajor)], Detector(section, aMinor));
        Assert.Equal([(new Rational(7, 1), CMajor)], Trajectory(section));

        // And the phrase that closes on the major's own V7 - I is the major's only where the
        // piece does not turn straight back to the chord it opened on and end there. A minor song
        // whose first phrase reaches its relative major that way - i iv V7/III III, the commonest
        // way a minor tune reaches its III - and whose every other bar is the minor's is A minor
        // throughout: the seventh is A natural minor's VII7 as much as C's V7. Where the same
        // phrase is followed by the major's own, the major has it.
        using var reachesItsThird = Blocks("9:m 2:m 7:7 0 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m");
        Assert.Empty(Detector(reachesItsThird, aMinor));
        Assert.Empty(Trajectory(reachesItsThird));

        using var staysInTheMajor = Blocks("9:m 2:m 7:7 0 | 9:m 5 7 0 | 0 5 7:7 0");
        Assert.Empty(Detector(staysInTheMajor, CMajor));
        Assert.Empty(Trajectory(staysInTheMajor));
    }

    [Fact]
    public void ATriadOfEitherModeOpeningAPieceIsAChordOfItsKeyBeforeItIsAKey()
    {
        // vi, iii and ii are chords of a major key and not keys, and III, VI and VII are chords of
        // a minor key as much: the rule was the minor triad's alone, so a MAJOR triad opening a
        // minor piece named the major. C G Am E7 | Am Dm E7 Am | Am Dm E7 Am, told A minor, opened
        // in C major on the trajectory road, which is told nothing, and reported A minor at bar 3,
        // where the detector told A minor heard no change and a musician hears A minor throughout
        // with a III first. A minor key is heard from a phrase only where it sounds the chords that
        // are its own — its tonic chord and its dominant seventh, whose raised leading tone no
        // major key owns — and where the opening chord's key does not keep the piece.
        var aMinor = new KeySignature(9, false);
        using var openingOnIII = Blocks("0 7 9:m 4:7 | 9:m 2:m 4:7 9:m | 9:m 2:m 4:7 9:m");
        Assert.Empty(Detector(openingOnIII, aMinor));
        Assert.Empty(Trajectory(openingOnIII));

        // The shapes the rule must not fire on: a tonicization of vi inside a major piece, whose
        // own tonic chord comes back after the phrase, and a phrase in the major whose relative
        // minor sounds neither its tonic chord nor its dominant.
        using var tonicized = Blocks("0 5 | 4:7 9:m | 5 7 | 0 0");
        Assert.Empty(Detector(tonicized, CMajor));
        Assert.Empty(Trajectory(tonicized));

        using var restingOnNothing = Blocks("0 7 0 7 | 7 2:7 7 7");
        Assert.Equal([(new Rational(4, 1), GMajor)], Detector(restingOnNothing, CMajor));
        Assert.Equal([(new Rational(4, 1), GMajor)], Trajectory(restingOnNothing));
    }

    [Fact]
    public void ThePedalIsOneNoteUnderTheHarmonyHoweverOftenItIsStruck()
    {
        // A pedal struck again with every chord was taken out of the harmony; struck oftener than
        // once a bar it sounded between the chords, and those strokes weighed as material of their
        // own. A tonic struck on every beat, and one repeated in eighths, under C F Dm C | G D7 G
        // D7 | G D7 G G read as one key on both roads, where the same pedal held, or struck once a
        // bar, is heard and the dominant with it. The pedal is the pitch class that sounds in every
        // chord — as a note of the harmony or as a note heard over it — and every stroke of it
        // under the harmony weighs nothing.
        var toTheDominant = "0 5 2:m 0 | 7 2:7 7 2:7 | 7 2:7 7 7";
        var dominant = new List<(Rational, KeySignature)> { (new Rational(4, 1), GMajor) };
        foreach (var every in new[] { Rational.Quarter, Rational.Eighth })
        {
            var strokes = (int)(12 / every.ToDouble());
            using var blocks = Blocks(toTheDominant);
            var notes = new List<NoteEvent>();
            for (var i = 0; i < blocks.Count; i++)
            {
                notes.Add(blocks.Get(i));
            }

            for (var k = 0; k < strokes; k++)
            {
                notes.Add(new NoteEvent(36, every * k, every));
            }

            using var struck = Buffer(notes);
            Assert.Equal(dominant, Detector(struck, CMajor));
            Assert.Equal(dominant, Trajectory(struck));
        }

        // The shape the rule must not fire on: a bass that WALKS names no pedal, its note changing
        // with the harmony.
        using var walking = Named("THE FENCE: a WALKING BASS in quarters, a new note every beat, under the same twelve bars (four voices)").Build(0);
        Assert.Equal(dominant, Detector(walking, CMajor));
        Assert.Equal(dominant, Trajectory(walking));
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

    /// <summary>
    /// Stretches of block chords, each from the whole note it is written at, with one note held
    /// under them all from the first bar for <paramref name="length"/> — an organ pedal. The
    /// bars no stretch covers are silent in the chords and sound the pedal alone.
    /// </summary>
    private static NoteBuffer BlocksOver(int pedal, Rational length, params (string Chords, int At)[] parts) =>
        BlocksUnder([new NoteEvent(pedal, Rational.Zero, length)], parts);

    /// <summary>
    /// Stretches of block chords, each from the whole note it is written at, with
    /// <paramref name="held"/> sounding under or among them — a pedal, or voices of one chord
    /// tied over the rest that follows it. The bars no stretch covers strike nothing.
    /// </summary>
    private static NoteBuffer BlocksUnder(NoteEvent[] held, params (string Chords, int At)[] parts)
    {
        var notes = new List<NoteEvent>(held);
        foreach (var (chords, at) in parts)
        {
            using var blocks = Blocks(chords);
            for (var i = 0; i < blocks.Count; i++)
            {
                var note = blocks.Get(i);
                notes.Add(new NoteEvent(note.Pitch, note.Offset + new Rational(at, 1), note.Duration, note.Velocity));
            }
        }

        return Buffer(notes);
    }

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
