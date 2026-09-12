// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// The rules both modulation roads now decide by, one passage each, beside the forty and the
/// sixty-four in <see cref="RealModulationsAreHeardWhereAMusicianHearsThemTests"/>: a key holds
/// for a phrase, a chord is not a key, a key owns its phrase, a secondary dominant is not a
/// modulation, a key is entered when its own notes return, a key area begins with a phrase, the
/// relative minor is reached when its dominant returns and left when its leading tone stops, a
/// change at the first note is the opening key misjudged, a key's chromatic chords are its own,
/// the Picardy third is the minor key's cadence, a passing tone is not a foreign note, the
/// resolution chain decides whose chord a chord is, an arpeggiated chord is that chord, a
/// leaning note is the line's and not the chord's, a key is heard from where its own chords
/// began, and the two roads place the same modulations. Each passage names what the roads
/// answered before the rules, measured on the library as it stood.
/// </summary>
public class AKeyHoldsForAPhraseTests
{
    private static readonly KeySignature CMajor = new(0, true);

    /// <summary>
    /// Block chords in close root position from C3, one whole note each unless suffixed
    /// <c>h</c> (a half note) or <c>q</c> (a quarter): <c>root[:quality]</c> with the root in
    /// semitones above C and the quality one of <c>m 7 m7 dim</c>; bar lines are ignored.
    /// </summary>
    private static NoteBuffer Chords(string tokens)
    {
        var qualities = new Dictionary<string, int[]> { [""] = [0, 4, 7], ["m"] = [0, 3, 7], ["7"] = [0, 4, 7, 10], ["m7"] = [0, 3, 7, 10], ["dim"] = [0, 3, 6] };
        var notes = new List<NoteEvent>();
        var time = Rational.Zero;
        foreach (var raw in tokens.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "|")
            {
                continue;
            }

            var token = raw;
            var duration = Rational.Whole;
            if (token.EndsWith('h') || token.EndsWith('q'))
            {
                duration = token.EndsWith('h') ? Rational.Half : Rational.Quarter;
                token = token[..^1];
            }

            var colon = token.IndexOf(':');
            var root = int.Parse(colon >= 0 ? token[..colon] : token);
            foreach (var interval in qualities[colon >= 0 ? token[(colon + 1)..] : ""])
            {
                notes.Add(new NoteEvent(48 + root + interval, time, duration));
            }

            time += duration;
        }

        var buffer = new NoteBuffer(notes.Count);
        buffer.AddRange(notes.ToArray());
        return buffer;
    }

    private static List<(Rational Position, KeySignature ToKey)> Modulations(NoteBuffer buffer, KeySignature start) =>
        ModulationDetector.Analyze(buffer, start).Modulations
            .Where(m => m.Type != ModulationType.Tonicization)
            .Select(m => (m.Offset, m.ToKey))
            .ToList();

    private static List<(Rational Position, KeySignature ToKey)> Trajectory(NoteBuffer buffer) =>
        KeyProfiler.AnalyzeModulations(buffer, new Rational(2, 1), new Rational(1, 1))
            .DetectModulations()
            .Select(m => (m.Position, m.ToKey))
            .ToList();

    [Fact]
    public void FourBarsOfCThenFourOfDFlatModulateOnceWhereDFlatBegins()
    {
        // The detector's window was half the piece's chords and its loop never judged the last
        // window, so the simplest textbook passage — I IV V I in C, then in D flat — reported no
        // modulation at all, while the trajectory reported four.
        using var buffer = Chords("0 5 7 0 | 1 6 8 1");

        var heard = Assert.Single(Modulations(buffer, CMajor));
        Assert.Equal(new KeySignature(1, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
        Assert.Equal([heard], Trajectory(buffer));
    }

    [Fact]
    public void AnArpeggiatedChordIsNotAKey()
    {
        // I IV V I twice, each chord arpeggiated in eighths. A one-bar window of the detector's
        // pseudo-chords held one triad, whose three pitch classes are not enough to decide a
        // key, and the IV chord's F A C was reported as a modulation to A minor and back.
        var passage = RealModulationPassages.Named("I V7/V V I (arpeggios)");
        using var applied = passage.Build(0);
        using var plain = Arpeggiate("0 5 7 0 | 0 5 7 0");

        Assert.Empty(ModulationDetector.Analyze(plain, CMajor).Modulations);
        Assert.Empty(Trajectory(plain));

        // With a V7/V in bars 2 and 6 the passage is I V7/V V I in C, and an arpeggiated chord is
        // that chord: it reads as its block-chord twin does — no change of key at all, the D7
        // being C's own applied dominant. Heard note by note, the D7's F sharps were foreign
        // eighths, and the excursions to G were reported as Direct modulations — one to G, one
        // to E minor — then, once judged by phrase, as tonicizations that the block chords of
        // the same music never reported.
        var block = RealModulationPassages.Named("I V7/V V I (block chords)");
        using var struck = block.Build(0);

        Assert.Empty(ModulationDetector.Analyze(struck, CMajor).Modulations);
        Assert.Empty(ModulationDetector.Analyze(applied, CMajor).Modulations);
        Assert.Empty(Trajectory(applied));
    }

    [Fact]
    public void TheRelativeMinorIsReachedThroughItsDominant()
    {
        // C F G C | E7 Am Dm E7 Am. A minor's scale is C major's, so no note of the natural
        // minor tells the two apart and the detector's separation gate never cleared; the G sharp
        // of E7 does tell them apart, and a minor key owns its raised leading tone.
        using var buffer = Chords("0 5 7 0 | 4:7 9:m 2:m 4:7 9:m");

        var heard = Assert.Single(Modulations(buffer, CMajor));
        Assert.Equal(new KeySignature(9, false), heard.ToKey);
        Assert.InRange(heard.Position.ToDouble(), 3.0, 5.0);
        Assert.Equal([heard], Trajectory(buffer));
    }

    [Fact]
    public void OneAppliedDominantOfViIsATonicization()
    {
        // C F | E7 Am | F G | C C: V7/vi and vi, then IV V I. The phrase from the E7 is owned by
        // A minor to the last note, and the profile reads it as A minor too; what makes it a
        // tonicization is that A minor's one distinguishing note, the G sharp, never returns.
        // Both roads reported a modulation to A minor here, the detector adding one back to C.
        using var buffer = Chords("0 5 | 4:7 9:m | 5 7 | 0 0");

        Assert.Empty(Modulations(buffer, CMajor));
        Assert.Empty(Trajectory(buffer));
        Assert.Contains(ModulationDetector.Analyze(buffer, CMajor).Modulations, e =>
            e.Type == ModulationType.Tonicization && e.ToKey == new KeySignature(9, false));
    }

    [Fact]
    public void TheRelativeMajorIsReachedWhenTheLeadingToneStops()
    {
        // Eight bars in C, eight in A minor through E7, eight in C. The way back owns no note
        // A minor lacks, so it is heard when a phrase reads as C major with no G sharp in it, and
        // it begins where A minor's tonic is left. The detector placed the return three bars
        // early; the trajectory heard thirteen modulations in the twenty-four bars.
        using var buffer = Chords("0 5 7 0 0 5 7 0 | 4:7 9:m 2:m 4:7 9:m 2:m 4:7 9:m | 0 5 7 0 0 5 7 0");

        var heard = Modulations(buffer, CMajor);
        Assert.Equal(2, heard.Count);
        Assert.Equal(new KeySignature(9, false), heard[0].ToKey);
        Assert.InRange(heard[0].Position.ToDouble(), 7.0, 9.0);
        Assert.Equal(CMajor, heard[1].ToKey);
        Assert.InRange(heard[1].Position.ToDouble(), 15.0, 17.0);
        Assert.Equal(heard, Trajectory(buffer));
    }

    [Fact]
    public void SixteenBarsOfIIVVIStayHomeOnBothRoads()
    {
        // The trajectory heard I–V as the dominant's key and V–I as the tonic's, at every turn:
        // seven modulations in sixteen bars that never leave C.
        using var buffer = Chords("0 5 7 0 0 5 7 0 0 5 7 0 0 5 7 0");

        Assert.Empty(Trajectory(buffer));
        Assert.Empty(ModulationDetector.Analyze(buffer, CMajor).Modulations);
    }

    [Fact]
    public void AModulationIsPlacedAtItsPivotChord()
    {
        // C F G C | G D7 G C D7 G: the G in bar 5 is V in C and I in G, and a musician writes the
        // modulation there, not at the D7 that first sounds the F sharp. The same passage with a
        // half-bar harmonic rhythm is placed at the same bar.
        using var whole = Chords("0 5 7 0 | 7 2:7 7 0 2:7 7");
        using var half = Chords("0h 5h 7:7h 0h 0h 5h 7:7h 0h | 7h 0h 2:7h 7h 7h 0h 2:7h 7h");

        var heard = Assert.Single(Modulations(whole, CMajor));
        Assert.Equal(new KeySignature(7, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
        Assert.Equal(new Rational(4, 1), Assert.Single(Modulations(half, CMajor)).Position);
    }

    [Fact]
    public void TheTwoRoadsPlaceTheSameModulationsOnEveryPassage()
    {
        // One judge behind both roads: on every passage a musician wrote, the trajectory's
        // modulations and the detector's are the same keys at the same positions. Before, the
        // trajectory was wrong on thirty-four of the forty and the detector on sixteen, and they
        // disagreed with each other on almost every one.
        foreach (var passage in RealModulationPassages.All)
        {
            using var buffer = passage.Build(0);
            var opening = new KeySignature(0, passage.OpeningIsMajor);

            Assert.Equal(Modulations(buffer, opening), Trajectory(buffer));
        }

        // The held-out passages too, and the second reviewer's, to the same keys within a bar
        // of each other: the roads differ in what a sonority is, and the detector's chords sound
        // until the next where the trajectory's notes last their own length. Before the detector
        // heard the line between its chords they were a bar or more apart on a melody over
        // chords — a chromatic quarter-note neighbour under a D7 put G at bar 8 on the
        // trajectory and at 4 on the detector, and a melody with two chromatic passing eighths
        // in each bar of its G was G on the detector and no key on the trajectory.
        foreach (var passage in RealModulationPassages.HeldOut.Concat(RealModulationPassages.ReviewerHeldOut))
        {
            using var buffer = passage.Build(0);
            var opening = new KeySignature((byte)passage.OpeningRoot, passage.OpeningIsMajor);
            var detector = Modulations(buffer, opening);
            var trajectory = KeyProfiler.AnalyzeModulations(buffer, passage.TrajectoryWindow, new Rational(1, 1))
                .DetectModulations()
                .Select(m => (m.Position, m.ToKey))
                .ToList();

            if (passage.TrajectoryMayHearNoChange && trajectory.Count == 0)
            {
                continue;
            }

            Assert.True(detector.Count == trajectory.Count, passage.Name);
            for (var i = 0; i < detector.Count; i++)
            {
                Assert.Equal(detector[i].ToKey, trajectory[i].ToKey);
                Assert.True(Math.Abs((detector[i].Position - trajectory[i].Position).ToDouble()) <= 1.0, passage.Name);
            }
        }
    }

    [Fact]
    public void AKeyMustOwnThePhraseItIsNamedFor()
    {
        // C Am D7 G | C A7 Dm G7, twice: a pop verse whose V7/V–V is a half cadence and whose
        // A7 is V7/ii. The phrase after the Am pivot, D7 G C A7, left less of itself foreign to
        // G than to C — one C sharp against a C sharp and an F sharp — and that was enough: the
        // detector went to G at the Am, home at the Dm and to G again, and the trajectory,
        // opening in G, came home at bar 7. A key owns its phrase only when the notes it lacks
        // amount to less than a quarter note in every bar, and an A7 resolving to a D minor
        // that G does not own is no chord of G.
        using var verse = Chords("0 9:m 2:7 7 | 0 9:7 2:m 7:7 | 0 9:m 2:7 7 | 0 9:7 2:m 7:7");

        Assert.Empty(Modulations(verse, CMajor));
        Assert.Empty(Trajectory(verse));
        Assert.Equal(CMajor, ModulationDetector.Analyze(verse, CMajor).StartKey);

        // I IV V I in C, D, E and F sharp, two bars each: no key owns two of those areas, and
        // both roads named one that was never there — A major, which leaves less of D G | A D E
        // A B E foreign than D or E does — and F sharp from it. A wrong key is worse than none.
        using var everyTwoBars = Chords("0h 5h 7h 0h | 2h 7h 9h 2h | 4h 9h 11h 4h | 6h 11h 1h 6h");

        Assert.Empty(Modulations(everyTwoBars, CMajor));
        Assert.Empty(Trajectory(everyTwoBars));

        // A chromatic scale in eighths: every key lacks five of every eight notes. The detector
        // reported thirty-six Direct modulations one eighth apart at confidence 0.67, the
        // trajectory a turn to C minor; nothing is heard, not even a tonicization.
        using var chromatic = new NoteBuffer(64);
        for (var i = 0; i < 64; i++)
        {
            chromatic.AddNote(60 + (i % 12), Rational.Eighth * i, Rational.Eighth);
        }

        Assert.Empty(ModulationDetector.Analyze(chromatic, CMajor).Modulations);
        Assert.Empty(Trajectory(chromatic));
    }

    [Fact]
    public void AnAppliedDominantIsTheChordOfTheKeyItResolvesInto()
    {
        // C F G C | G A7 D7 G | C D7 G G: the second phrase is I V7/V V7 I in G, framed by its
        // tonic, and a musician writes the modulation at its G. The A7's C sharp is no note of
        // G, and counted foreign it kept the phrase from being G's: the modulation was placed
        // at the D7, two bars late. A dominant seventh that resolves down a fifth into a chord
        // the key owns is the key's own applied dominant.
        using var buffer = Chords("0 5 7 0 | 7 9:7 2:7 7 | 0 2:7 7 7");

        var heard = Assert.Single(Modulations(buffer, CMajor));
        Assert.Equal(new KeySignature(7, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
        Assert.Equal([heard], Trajectory(buffer));

        // Not when it resolves into the tonic of the key the music is in: Am Dm E7 Am | A7 Dm
        // Bb E7 | Am F Dm E7 | Am Am is A minor with V7/iv and the Neapolitan, and its E7 is A
        // minor's dominant, not V7/v of a D minor that owns the A minor chord it resolves to.
        using var neapolitan = Chords("9:m 2:m 4:7 9:m | 9:7 2:m 10 4:7 | 9:m 5 2:m 4:7 | 9:m 9:m");

        Assert.Empty(Modulations(neapolitan, new KeySignature(9, false)));
        Assert.Empty(Trajectory(neapolitan));
    }

    [Fact]
    public void AKeyIsEnteredWhenItsOwnNotesReturnOrItsPhraseIsFramed()
    {
        // C F G C | D7 G C C | C F G C: V7/V V I I is a tonicization of the dominant inside a
        // phrase of C. The phrase from the D7 held G — G owns D7 G C C — and G was a modulation
        // at the C before it. One bar of the new key's own note is an applied dominant; a second
        // bar before the old key's own note returns is the key, or a phrase framed by the new
        // tonic chord.
        using var tonicized = Chords("0 5 7 0 | 2:7 7 0 0 | 0 5 7 0");

        Assert.Empty(Modulations(tonicized, CMajor));
        Assert.Empty(Trajectory(tonicized));
        Assert.Contains(ModulationDetector.Analyze(tonicized, CMajor).Modulations, e =>
            e.Type == ModulationType.Tonicization && e.ToKey == new KeySignature(7, true));

        // C F G C | Am Dm E7 Am | F G C C: i iv V7 i framed by A minor's tonic is a phrase in A
        // minor though its G sharp sounds once, and the music comes home at the F.
        using var framed = Chords("0 5 7 0 | 9:m 2:m 4:7 9:m | 5 7 0 0");

        var heard = Modulations(framed, CMajor);
        Assert.Equal(2, heard.Count);
        Assert.Equal(new KeySignature(9, false), heard[0].ToKey);
        Assert.Equal(new Rational(4, 1), heard[0].Position);
        Assert.Equal(CMajor, heard[1].ToKey);
        Assert.InRange(heard[1].Position.ToDouble(), 7.0, 8.0);
        Assert.Equal(heard, Trajectory(framed));
    }

    [Fact]
    public void AKeyAreaBeginsWithItsPhrase()
    {
        // C F G C | G C D7 G | C F G C | G C D7 G: four-bar areas alternating between C and G,
        // each I IV V I in its key. The G area's F sharp falls in its third bar, and a phrase
        // measured from the D7 ran into the return to C: the first G area was a tonicization and
        // the second, reaching the end, a modulation at bar 14. The key began where its phrase
        // did, at the G it owns from.
        using var alternating = Chords("0 5 7 0 | 7 0 2:7 7 | 0 5 7:7 0 | 7 0 2:7 7");

        var heard = Modulations(alternating, CMajor);
        Assert.Equal(
            [(new Rational(4, 1), new KeySignature(7, true)), (new Rational(8, 1), CMajor), (new Rational(12, 1), new KeySignature(7, true))],
            heard);
        Assert.Equal(heard, Trajectory(alternating));

        // Not a phrase that opens on the old key's tonic chord, which is still the old key: C Am
        // F G | C Am D7 G ends on a half cadence, and the Am before the D7 owns no G to be the
        // pivot the tonic was heard in. Both roads modulated to G at the Am.
        using var halfCadence = Chords("0 9:m 5 7 | 0 9:m 2:7 7");

        Assert.Empty(Modulations(halfCadence, CMajor));
        Assert.Empty(Trajectory(halfCadence));
    }

    [Fact]
    public void AChangeAtTheFirstNoteIsTheOpeningKeyMisjudgedOnBothRoads()
    {
        // An A minor melody with its G sharp, then a C major melody, analyzed from C minor — a
        // key the music was never in. The detector reported a modulation from C minor to A
        // minor at the first note and kept C minor as the start; the trajectory, with no key
        // given, already took a change at the first note for the opening key. The music opens
        // in A minor on both roads and moves to C once.
        var melody = MusicNotation.Parse(
            "A4/4 B4/4 C5/4 D5/4 | E5/2 D5/4 C5/4 | B4/4 G#4/4 B4/4 D5/4 | C5/2 A4/2 | "
            + "C5/4 D5/4 E5/4 F5/4 | G5/2 F5/4 E5/4 | D5/4 B4/4 D5/4 F5/4 | E5/2 C5/2");
        using var buffer = new NoteBuffer(melody.Length);
        buffer.AddRange(melody);

        var result = ModulationDetector.Analyze(buffer, new KeySignature(0, false));
        Assert.Equal(new KeySignature(9, false), result.StartKey);
        var heard = Assert.Single(result.Modulations);
        Assert.Equal(new KeySignature(9, false), heard.FromKey);
        Assert.Equal(CMajor, heard.ToKey);
        Assert.Equal(CMajor, result.EndKey);
        Assert.Equal([(heard.Offset, heard.ToKey)], Trajectory(buffer));

        // G C F G | C F G C opens on its dominant. The profile read the first phrase as G, and
        // the trajectory modulated to C at the second chord; a guessed opening key that never
        // sounded a note of its own before another key was read — no F sharp anywhere — was
        // never there.
        using var onTheDominant = Chords("7 0 5 7 | 0 5 7 0");

        Assert.Empty(Trajectory(onTheDominant));
        Assert.Empty(Modulations(onTheDominant, CMajor));
    }

    [Fact]
    public void ATonicizationLastsUntilTheMusicIsHomeAgain()
    {
        // C F | E7 Am | F G | C C: the tonicization of vi is the E7 and the Am, two bars. Its
        // Duration ran to the end of the piece — six bars — because it was measured to the first
        // note A minor does not own, and A minor owns every note of C major. It lasts until the
        // music is home: the first whole note after the new tonic that neither is that chord nor
        // sounds a note of the new key's own.
        using var buffer = Chords("0 5 | 4:7 9:m | 5 7 | 0 0");

        var excursion = Assert.Single(ModulationDetector.Analyze(buffer, CMajor).Modulations);
        Assert.Equal(ModulationType.Tonicization, excursion.Type);
        Assert.Equal(new KeySignature(9, false), excursion.ToKey);
        Assert.Equal(new Rational(2, 1), excursion.Offset);
        Assert.Equal(new Rational(2, 1), excursion.Duration);

        // The same excursion arpeggiated in eighths is the same excursion: the E7 and the Am,
        // two bars, read by whole notes — an arpeggiated E7 sounds its B and D after its G sharp,
        // and read note by note the excursion ended inside the chord that began it. And an
        // arpeggiated I V7/V V I is I V7/V V I: no excursion at all, as its block chords never
        // reported one. Heard note by note, the D7's F sharps made a two-bar tonicization of G
        // that the struck chords of the same music did not have.
        using var arpeggiated = Arpeggiate("0 5 | 4:7 9:m | 5 7 | 0 0");

        var applied = Assert.Single(ModulationDetector.Analyze(arpeggiated, CMajor).Modulations);
        Assert.Equal(ModulationType.Tonicization, applied.Type);
        Assert.Equal(new KeySignature(9, false), applied.ToKey);
        Assert.Equal(new Rational(2, 1), applied.Offset);
        Assert.Equal(new Rational(2, 1), applied.Duration);

        using var appliedDominant = Arpeggiate("0 2:7 7 0 | 0 2:7 7:7 0");
        Assert.Empty(ModulationDetector.Analyze(appliedDominant, CMajor).Modulations);
    }

    [Fact]
    public void TheTrajectoryOpensInTheKeyItHearsFirst()
    {
        // Ode to Joy in C, then in D. Its first phrase has no leading tone and cannot decide a
        // key on its own; read from the whole piece instead, the opening came back as D major
        // and the trajectory reported a modulation from D to C at the first note — from a key
        // the music was never in. The opening is extended until it decides, and a change at the
        // first note is the opening key, not a modulation.
        const string odeToJoy =
            "E4/4 E4/4 F4/4 G4/4 | G4/4 F4/4 E4/4 D4/4 | C4/4 C4/4 D4/4 E4/4 | E4/4. D4/8 D4/2 | "
            + "E4/4 E4/4 F4/4 G4/4 | G4/4 F4/4 E4/4 D4/4 | C4/4 C4/4 D4/4 E4/4 | D4/4. C4/8 C4/2";
        var tune = MusicNotation.Parse(odeToJoy);
        var notes = new List<NoteEvent>(tune);
        notes.AddRange(tune.Select(n => new NoteEvent(n.Pitch + 2, n.Offset + new Rational(8, 1), n.Duration, n.Velocity)));
        using var buffer = new NoteBuffer(notes.Count);
        buffer.AddRange(notes.ToArray());

        var heard = Assert.Single(KeyProfiler.AnalyzeModulations(buffer, new Rational(2, 1), new Rational(1, 1)).DetectModulations());
        Assert.Equal(CMajor, heard.FromKey);
        Assert.Equal(new KeySignature(2, true), heard.ToKey);
        Assert.Equal(new Rational(8, 1), heard.Position);
    }

    [Fact]
    public void AHalfCadenceThroughVOfVIsNotAModulation()
    {
        // C F G7 C | C F D7 G: an antecedent phrase closing on the dominant through V7/V. The
        // two chords at the end are owned by G and the piece closes on G, and that alone made
        // them a modulation to the dominant on both roads; a stretch shorter than a phrase at
        // the end is a key only if its tonic was already heard before the close.
        using var buffer = Chords("0 5 7:7 0 | 0 5 2:7 7");

        Assert.Empty(Modulations(buffer, CMajor));
        Assert.Empty(Trajectory(buffer));
        Assert.Contains(ModulationDetector.Analyze(buffer, CMajor).Modulations, e =>
            e.Type == ModulationType.Tonicization && e.ToKey == new KeySignature(7, true));
    }

    [Fact]
    public void TheRelativeMajorBeginsWithABar()
    {
        // An A minor melody with its G sharp in the cadence of bar 3, then a C major melody from
        // bar 5. The relative major owns no note of its own to begin on, and read at every note
        // the detector began it on the B after the last G sharp — in the middle of A minor's
        // final cadence, a bar and a half before the musician. It begins with a bar, on both roads.
        var melody = MusicNotation.Parse(
            "A4/4 B4/4 C5/4 D5/4 | E5/2 D5/4 C5/4 | B4/4 G#4/4 B4/4 D5/4 | C5/2 A4/2 | "
            + "C5/4 D5/4 E5/4 F5/4 | G5/2 F5/4 E5/4 | D5/4 B4/4 D5/4 F5/4 | E5/2 C5/2");
        using var buffer = new NoteBuffer(melody.Length);
        buffer.AddRange(melody);

        var heard = Assert.Single(Modulations(buffer, new KeySignature(9, false)));
        Assert.Equal(CMajor, heard.ToKey);
        Assert.InRange(heard.Position.ToDouble(), 3.0, 4.0);
        Assert.Equal(1, heard.Position.Denominator);
        Assert.Equal([heard], Trajectory(buffer));
    }

    [Fact]
    public void AKeysChromaticChordsAreItsOwn()
    {
        // C F G C | G E Am D7 | G C D7 G: the E major triad is V/ii of G, and a musician hears G
        // from its fifth bar. Only a dominant seventh was an applied chord, so the G sharp was
        // a foreign bar and both roads reached G two bars late, at the Am.
        using var appliedTriad = Chords("0 5 7 0 | 7 4 9:m 2:7 | 7 0 2:7 7");

        var heard = Assert.Single(Modulations(appliedTriad, CMajor));
        Assert.Equal(new KeySignature(7, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
        Assert.Equal([heard], Trajectory(appliedTriad));

        // C F G C | G Cm D7 G | G C D7 G: the C minor chord is G's borrowed iv, resolving into
        // its D7. It was a foreign bar too, and G began at the D7.
        using var borrowed = Chords("0 5 7 0 | 7 0:m 2:7 7 | 7 0 2:7 7");

        heard = Assert.Single(Modulations(borrowed, CMajor));
        Assert.Equal(new KeySignature(7, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
        Assert.Equal([heard], Trajectory(borrowed));

        // A chord the key in force owns is that key's, whatever another key might borrow it as:
        // C F G C A D G, round and round, is C with a chain of secondary dominants, not G with
        // F as its flat seventh and A as its V/V. G owned every bar of it once its applied
        // triads and borrowed chords counted, and the detector went to the dominant.
        using var chain = Chords("0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h");

        Assert.Empty(Modulations(chain, CMajor));
        Assert.Empty(Trajectory(chain));

        // A chord resolves into the chord on the root a fifth below, not into a seventh chord
        // that holds that triad: F sharp seventh falling to G sharp minor seventh is the
        // deceptive cadence of B, not V7 of B resolving. With G sharp minor seventh taken for a
        // B chord, F sharp 7 was V7/V of E and B B7 F sharp 7 G sharp m7 was named E major.
        using var deceptive = Chords("0 5 7 0 | 11 11:7 6:7 8:m7 | 0 5 7 0");

        Assert.DoesNotContain(ModulationDetector.Analyze(deceptive, CMajor).Modulations, m => m.ToKey == new KeySignature(4, true));
        Assert.DoesNotContain(Trajectory(deceptive), m => m.ToKey == new KeySignature(4, true));
    }

    [Fact]
    public void ThePicardyThirdIsTheMinorKeysCadence()
    {
        // Cm Fm G7 Cm | Eb Ab Bb Eb | Cm Ab G7 C: a hymn in C minor that visits its relative
        // major and comes home, closing on a Picardy third. The E natural of the final chord
        // was a whole bar C minor did not own, so C minor never owned its closing phrase: both
        // roads ended the piece in E flat, and the detector called the Picardy chord a
        // tonicization of C major. A minor key's tonic major triad closing the piece is its
        // cadence, its raised third owned as its raised seventh already is.
        using var hymn = Chords("0:m 5:m 7:7 0:m | 3 8 10 3 | 0:m 8 7:7 0");
        var cMinor = new KeySignature(0, false);

        var heard = Modulations(hymn, cMinor);
        Assert.Equal(2, heard.Count);
        Assert.Equal(new KeySignature(3, true), heard[0].ToKey);
        Assert.InRange(heard[0].Position.ToDouble(), 3.0, 4.0);
        Assert.Equal(cMinor, heard[1].ToKey);
        Assert.Equal(new Rational(8, 1), heard[1].Position);
        Assert.Equal(heard, Trajectory(hymn));
        Assert.DoesNotContain(ModulationDetector.Analyze(hymn, cMinor).Modulations, m => m.ToKey == CMajor);

        // Cm Fm G7 Cm | Ab Fm G7 C: the Picardy third alone was a tonicization of C major too.
        using var picardy = Chords("0:m 5:m 7:7 0:m | 8 5:m 7:7 0");

        Assert.Empty(ModulationDetector.Analyze(picardy, cMinor).Modulations);
        Assert.Empty(Trajectory(picardy));

        // Four bars of C major after four of C minor are still the parallel major: the raised
        // third is the minor key's only in the chord that closes the piece.
        using var parallel = Chords("0:m 5:m 7:7 0:m | 0 5 7:7 0");

        heard = Modulations(parallel, cMinor);
        Assert.Equal([(new Rational(4, 1), CMajor)], heard);
        Assert.Equal(heard, Trajectory(parallel));
    }

    [Fact]
    public void APassingToneIsNotAForeignNote()
    {
        // Four bars in C, then four in G whose melody has two chromatic passing eighths in each
        // of its first three bars — D C B Bb A Ab G, G A Bb B C C# D, D E F F# G Ab A — and
        // closes B A G. Weighed as strays, the two eighths were a quarter of each bar, the most
        // a key may lack of a bar it owns, and neither road named any key for the melody alone;
        // over its chords, the detector — which never saw the eighths — said G and the
        // trajectory said nothing. A passing tone weighs nothing.
        var G = new KeySignature(7, true);
        using var alone = RealModulationPassages.ReviewerHeldOutNamed("to the dominant, the melody with two chromatic passing eighths in each bar of the new key (melody alone)").Build(0);
        using var overChords = RealModulationPassages.ReviewerHeldOutNamed("to the dominant, the melody with two chromatic passing eighths in each bar of the new key (melody over chords)").Build(0);

        Assert.Equal([(new Rational(4, 1), G)], Modulations(alone, CMajor));
        Assert.Equal([(new Rational(4, 1), G)], Trajectory(alone));
        Assert.Equal([(new Rational(4, 1), G)], Modulations(overChords, CMajor));
        Assert.Equal([(new Rational(4, 1), G)], Trajectory(overChords));

        // A quarter-note C sharp under the D7 of a G-major melody over chords, approached by
        // leap from A and resolved into D: an appoggiatura. It weighed exactly a quarter, and
        // the trajectory placed G four bars late, at 8, where the detector said 4.
        using var appoggiatura = RealModulationPassages.ReviewerHeldOutNamed("to the dominant, the melody with one chromatic quarter-note neighbour in the new key (melody over chords)").Build(0);

        Assert.Equal([(new Rational(4, 1), G)], Modulations(appoggiatura, CMajor));
        Assert.Equal([(new Rational(4, 1), G)], Trajectory(appoggiatura));

        // E F F sharp G in the sixth bar of a passage that goes to G at its fifth: the F is a
        // chromatic passing tone, and it was the last note G did not own, so the trajectory
        // began G three eighths into the bar, at 43/8.
        using var run = RealModulationPassages.ReviewerHeldOutNamed("to the dominant through a deceptive cadence, with a chromatic run E F F sharp G in bar 6 (melody over chords)").Build(0);

        Assert.Equal([(new Rational(4, 1), G)], Modulations(run, CMajor));
        Assert.Equal([(new Rational(4, 1), G)], Trajectory(run));

        // A quarter E flat neighbour of D in a G-major melody. Weighed as a stray it kept G
        // from owning its bar, and E minor — which owns E flat as its raised seventh, D sharp
        // — was the only key that could; neither road heard the modulation at all.
        var melody = MusicNotation.Parse(
            "E4/4 G4/4 C5/2 | A4/4 F4/4 C5/2 | D5/4 B4/4 G4/2 | E4/4 D4/4 C4/2 | "
            + "D5/4 C5/4 B4/4 A4/4 | G4/4 F#4/4 G4/4 A4/4 | B4/4 D5/4 Eb5/4 D5/4 | F#5/4 A5/4 G5/2");
        using var neighbour = new NoteBuffer(melody.Length);
        neighbour.AddRange(melody);

        Assert.Equal([(new Rational(4, 1), G)], Modulations(neighbour, CMajor));
        Assert.Equal([(new Rational(4, 1), G)], Trajectory(neighbour));

        // Not every note between two steps is a passing tone. A passing or neighbour tone is
        // unaccented: a melody in C whose F falls on a downbeat between two E's is in C, not in
        // a G major that hears the F as a chromatic neighbour; read with the neighbour exempt,
        // the trajectory went to G at the F. And a chromatic scale is what it always was to the
        // judge — no key's — though every note in it is approached and left by a semitone: a
        // run of notes on their way must set out from a structural note and land on one within
        // a whole note, and a chromatic scale has neither.
        var downbeat = MusicNotation.Parse(
            "C4/4 E4/4 G4/4 G4/4 | G4/4 D4/4 E4/4 E4/4 | F4/4 F4/4 A4/4 A4/4 | G4/4 G4/4 D4/4 D4/4 | "
            + "D4/4 A4/4 E4/4 E4/4 | F4/4 E4/4 D4/4 C4/4 | E4/4 B4/4 G4/4 D4/4 | C4/4 B4/4 B4/4 B4/4");
        using var accented = new NoteBuffer(downbeat.Length);
        accented.AddRange(downbeat);

        Assert.Empty(Modulations(accented, CMajor));
        Assert.Empty(Trajectory(accented));

        using var chromatic = new NoteBuffer(64);
        for (var i = 0; i < 64; i++)
        {
            chromatic.AddNote(60 + (i % 12), Rational.Eighth * i, Rational.Eighth);
        }

        Assert.Empty(ModulationDetector.Analyze(chromatic, CMajor).Modulations);
        Assert.Empty(Trajectory(chromatic));
    }

    [Fact]
    public void AKeyBeginsOnAChordOfItsOwn()
    {
        // In a descending-fifths sequence of major triads every four chords are V/V V I IV of
        // the third one's key, and with plain triads applied B flat E flat A flat D flat began
        // an A flat major area on the B flat chord. An applied or borrowed chord is heard as
        // the key's only once the key is in force, so it cannot be where the key begins.
        using var fifths = Chords("0 5 10 3 8 1 6 11 4 9 2 7 0");

        Assert.Empty(Modulations(fifths, CMajor));
        Assert.Empty(Trajectory(fifths));

        // But a chord with a stray note in it is a chord of the key: the detector's eighth-note
        // grid rounds the last thirty-seconds of a bar of C onto the D flat downbeat, and D flat
        // begins on that downbeat — not an eighth later, where the first clean chord is.
        var passage = RealModulationPassages.HeldOutNamed("up a semitone in thirty-second-note arpeggios (thirty-second arpeggios)");
        using var thirtySeconds = passage.Build(0);

        var heard = Assert.Single(Modulations(thirtySeconds, CMajor));
        Assert.Equal(new KeySignature(1, true), heard.ToKey);
        Assert.Equal(new Rational(4, 1), heard.Position);
    }

    // ---------- the third reviewer's two findings on iteration three ----------

    [Fact]
    public void AGuessedOpeningIsNotConfirmedByAPassingTone()
    {
        // A C-major melody whose third bar rises F F# G G# A A# and whose sixth falls G Gb F E
        // Eb D Db. The profile opens it in E minor (E and B weigh most in the first phrase), and
        // the judge drops a guessed opening only if the guessed key never sounded a note of its
        // own before another key was read. With the chromatic notes weighed as passing tones for
        // C, C owned every phrase — but the F sharp in the run counted as E minor's own note, so
        // the guess stood and the trajectory reported a modulation to C at bar 4 in a piece that
        // never left C. A home note that is a passing tone of the other key is no note of the
        // guessed key's own: the same rule the patch already applied in Returns().
        using var melody = RealModulationPassages.ThirdReviewerHeldOut
            .Single(p => p.Name.StartsWith("a C major melody with a chromatic scale fragment")).Build(0);

        Assert.Empty(Trajectory(melody));
        Assert.Empty(Modulations(melody, CMajor));
    }

    [Fact]
    public void TheLeadingToneLeansOnTheMinorTonic()
    {
        // Four bars of C, then eight of C minor over Cm Fm G7 Cm; in bars 8 and 12 the melody
        // strikes B natural on the downbeat over the tonic chord and resolves to C. Owned only in
        // the dominant's chords or alone in the line, the leading tone struck with the tonic was a
        // note C minor lacked for a quarter — the floor — so C minor owned neither phrase, and
        // both roads heard no modulation at all (HEAD: C minor at 4). The tonic chord with the
        // leading tone leaning on it is the tonic chord, and C E flat G is still no chord of E
        // minor's, so the borrowed-iv row the rule was written for is unchanged.
        var cMinor = new KeySignature(0, false);
        using var melody = RealModulationPassages.ThirdReviewerHeldOut
            .Single(p => p.Name.StartsWith("to the parallel minor, the leading tone struck on the downbeat")).Build(0);

        Assert.Equal([(new Rational(4, 1), cMinor)], Modulations(melody, CMajor));
        Assert.Equal([(new Rational(4, 1), cMinor)], Trajectory(melody));
    }

    [Fact]
    public void TheResolutionChainDecidesWhoseChordItIs()
    {
        // C F G C | F B♭ G C7 | F B♭ C7 F: the G is V/V of F, because it resolves into C7, no plain
        // chord of C's. A chord the key in force owned was that key's whatever it resolved into,
        // so F could not count the G as its own and began at bar 8 (position 7), a phrase after
        // the musician's bar 5.
        var fMajor = new KeySignature(5, true);
        using var toTheSubdominant = Chords("0 5 7 0 | 5 10 7 0:7 | 5 10 0:7 5");

        Assert.Equal([(new Rational(4, 1), fMajor)], Modulations(toTheSubdominant, CMajor));
        Assert.Equal([(new Rational(4, 1), fMajor)], Trajectory(toTheSubdominant));

        // C F G C | B♭ E♭ C F | B♭ E♭ F7 B♭: the C major chord is V/V of B flat, because C major
        // was left at the E flat before its tonic chord came round. Guarded on ownership alone,
        // F was named at bar 4 and B flat at bar 9.
        var bFlat = new KeySignature(10, true);
        using var toTheFlatSeventh = Chords("0 5 7 0 | 10 3 0 5 | 10 3 5:7 10");

        Assert.Equal([(new Rational(4, 1), bFlat)], Modulations(toTheFlatSeventh, CMajor));
        Assert.Equal([(new Rational(4, 1), bFlat)], Trajectory(toTheFlatSeventh));

        // The guard still holds where it must: C F G C A D G, round and round, is C with a chain
        // of secondary dominants, not G with F as its flat seventh — the F resolves into G, C's
        // own chord, and C stands; and a borrowed chord the key in force owns is its own whatever
        // follows, so a new key every two bars, C D E F sharp, names none.
        using var chain = Chords("0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h 0h 5h 7h 0h 9h 2h 7h");
        using var everyTwoBars = Chords("0h 5h 7h 0h 2h 7h 9h 2h 4h 9h 11h 4h 6h 11h 1h 6h");

        Assert.Empty(Modulations(chain, CMajor));
        Assert.Empty(Trajectory(chain));
        Assert.Empty(Modulations(everyTwoBars, CMajor));
        Assert.Empty(Trajectory(everyTwoBars));
    }

    [Fact]
    public void TheAugmentedSixthAndTheTonicSeventhAreTheKeysChords()
    {
        // Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭7 G7 Cm | Cm Fm G7 Cm: the A♭7 is C minor's German sixth,
        // resolving into its dominant. Enharmonically a dominant seventh on A flat that resolves
        // nowhere near a fifth below, it was no chord of C minor's, and the return came home at
        // bar 11 (position 10), on the G7, two bars after the C minor chord that begins it.
        var cMinor = new KeySignature(0, false);
        using var germanSixth = Chords("0:m 5:m 7:7 0:m | 3 8 10 3 | 0:m 8:7 7:7 0:m | 0:m 5:m 7:7 0:m");

        var heard = Modulations(germanSixth, cMinor);
        Assert.Equal(2, heard.Count);
        Assert.Equal(new KeySignature(3, true), heard[0].ToKey);
        Assert.Equal(cMinor, heard[1].ToKey);
        Assert.Equal(new Rational(8, 1), heard[1].Position);
        Assert.Equal(heard, Trajectory(germanSixth));

        // C F G C | B♭ E♭ F B♭7 | B♭ E♭ F7 B♭: the B♭7 closing the first B flat phrase is that
        // tonic coloured, I7, not V7 of E flat. Read as an applied chord of E flat's, the phrase
        // went to E flat at bar 8 and B flat at bar 9.
        var bFlat = new KeySignature(10, true);
        using var tonicSeventh = Chords("0 5 7 0 | 10 3 5 10:7 | 10 3 5:7 10");

        Assert.Equal([(new Rational(4, 1), bFlat)], Modulations(tonicSeventh, CMajor));
        Assert.Equal([(new Rational(4, 1), bFlat)], Trajectory(tonicSeventh));
    }

    [Fact]
    public void AnArpeggiatedChordIsThatChord()
    {
        // C F G C | G E Am D7 | G C D7 G and C F G C | G Cm D7 G | G C D7 G, each chord R 3 5 8 5
        // 3 R 3 in eighths: the E major and the C minor are G's V/ii and borrowed iv, as they are
        // struck. Heard note by note they were three G sharps and three E flats a bar, and G
        // began two bars late on both roads, at bar 7.
        var gMajor = new KeySignature(7, true);
        using var appliedTriad = Arpeggiate("0 5 7 0 | 7 4 9:m 2:7 | 7 0 2:7 7");
        using var borrowed = Arpeggiate("0 5 7 0 | 7 0:m 2:7 7 | 7 0 2:7 7");

        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(appliedTriad, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(appliedTriad));
        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(borrowed, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(borrowed));

        // The detector's candidates are every onset; a window beginning on the borrowed chord's
        // last eighth — an E flat, with D7 G G C after it — read as E minor at 23/4 and nothing
        // else. A phrase does not begin in the middle of a chord.
        Assert.DoesNotContain(ModulationDetector.Analyze(borrowed, CMajor).Modulations, m => m.ToKey == new KeySignature(4, false));
    }

    [Fact]
    public void ALeaningNoteIsTheLinesNotTheChords()
    {
        // A chromatic appoggiatura struck with the chord on every downbeat of G's four bars: A
        // sharp with G B D, resolving to B. Heard only when struck after its chord, the commonest
        // appoggiatura was a chord tone, and a melody with one on every downbeat named no key on
        // either road. And the reading is the same in all twelve keys: with the seventh of a D7
        // and the C sharp struck on it both a step from the D that follows, the one chosen by bit
        // order changed with the transposition.
        var gMajor = new KeySignature(7, true);
        var passages = RealModulationPassages.ThirdReviewerHeldOut;
        foreach (var name in new[]
        {
            "to the dominant, a chromatic appoggiatura struck on every downbeat",
            "to the dominant with V/ii as a triad, a 4-3 suspension struck over it",
            "to the dominant with V/ii as a triad, a chord tone struck over it",
        })
        {
            var passage = passages.Single(p => p.Name.StartsWith(name));
            for (var tonic = 0; tonic < 12; tonic += 11)
            {
                using var melody = passage.Build(tonic);
                var expected = (new Rational(4, 1), new KeySignature((byte)((tonic + 7) % 12), true));
                Assert.Equal([expected], Modulations(melody, new KeySignature((byte)tonic, true)));
                Assert.Equal([expected], Trajectory(melody));
            }
        }

        // A struck over E G♯ B — a 4-3 suspension — and the melody's G sharp quarter after it:
        // the first made the chord no plain triad and so no V/ii, the second was a foreign
        // quarter neither the chord's exemption nor the non-harmonic rule reached, and G began
        // at bar 7 with A minor touched on the way. A line note that is a tone of the chord
        // under it is that chord's, and adds no chromatic weight the chord has not already.
        using var suspension = passages.Single(p => p.Name.StartsWith("to the dominant with V/ii as a triad, a 4-3 suspension")).Build(0);
        Assert.DoesNotContain(ModulationDetector.Analyze(suspension, CMajor).Modulations, m => m.ToKey == new KeySignature(9, false));
        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(suspension, CMajor));
    }

    [Fact]
    public void APassingToneMayTurnOrLeanLong()
    {
        // G A A♭ G | D E E♭ D in the new key's first two bars: the A flat passes from the A down
        // to the G, and the A, though approached from below, is where the line turns and the run
        // sets out. With the run made to move one way through every note on its way, the A flat
        // was a foreign note and the melody named no key.
        var gMajor = new KeySignature(7, true);
        var passages = RealModulationPassages.ThirdReviewerHeldOut;
        using var arch = passages.Single(p => p.Name.StartsWith("to the dominant, a chromatic passing tone inside an arch")).Build(0);

        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(arch, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(arch));

        // C C♯ D over a D7, the C sharp a half note: it leans on the chord and resolves into it
        // while the chord still sounds. Limited to a quarter, it was half its bar against G and
        // killed the modulation on both roads.
        using var halfNote = passages.Single(p => p.Name.StartsWith("to the dominant, a half-note chromatic passing tone")).Build(0);

        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(halfNote, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(halfNote));
    }

    [Fact]
    public void AKeyIsHeardFromWhereItsOwnChordsBegan()
    {
        // C F G C | G B♭ E♭ F B♭ D7 G | G C D7 G: a bar of B flat major quoted inside G. The key
        // is heard from its fifth bar, where its own chords began, the quotation a parenthesis;
        // measured from the D7 after it, G began at bar 7.
        var gMajor = new KeySignature(7, true);
        using var quotation = Chords("0 5 7 0 | 7 10q 3q 5q 10q 2:7 7 | 7 0 2:7 7");

        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(quotation, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(quotation));

        // The same music without the quotation is G from its fifth bar too.
        using var plain = Chords("0 5 7 0 | 7 0 2:7 7 | 7 0 2:7 7");
        Assert.Equal([(new Rational(4, 1), gMajor)], Modulations(plain, CMajor));
        Assert.Equal([(new Rational(4, 1), gMajor)], Trajectory(plain));
    }

    [Fact]
    public void TheKeyInForceHearsItsOwnChromaticChordsWithoutAFrame()
    {
        // Cm Fm G7 Cm | A♭ Fm G7 C | Fm C: a hymn in C minor closing on a Picardy third with a
        // minor plagal Amen after it. Once the guard let a G7 go where it resolved into the
        // Picardy C major — a chord C minor does not own outright — F minor owned Fm G7 C Fm as
        // i V7/V V i, while C minor, framed like any rival, could not count the C major triad
        // as its V/iv, and the detector wrote a three-bar tonicization of F minor from the
        // Picardy chord to the end. A key in force hears its own chromatic chords wherever it
        // stands, frame or no frame; the passage is C minor throughout on both roads, with no
        // excursion at all.
        var cMinor = new KeySignature(0, false);
        using var amen = Chords("0:m 5:m 7:7 0:m | 8 5:m 7:7 0 | 5:m 0");

        var result = ModulationDetector.Analyze(amen, cMinor);
        Assert.Equal(cMinor, result.StartKey);
        Assert.Empty(result.Modulations);
        Assert.Empty(Trajectory(amen));
    }

    [Fact]
    public void AStrayNoteDoesNotTakeTheOpeningFromTheChordItOpensOn()
    {
        // C F G7 C | G C D7 G with escape tones — approached by step, left by leap — and
        // anticipations in the melody, three of the escape tones chromatic: a B flat eighth in
        // bar 2, a C sharp in bar 5, a B flat in bar 7. The piece opens on a C major chord and
        // cadences V7 I in C, and moves to G at bar 5. Once a melody note over the G7 counted as
        // that chord's, F major — which hears C's G7 as its own V7/V — left nothing of the first
        // phrase foreign while C left the B flat eighth, so the piece opened in F; from F, G was
        // a tonicization and the trajectory road heard no modulation. The key whose tonic chord
        // opens the piece opens it when it owns the phrase, a stray note a bar allowed.
        var gMajor = new KeySignature(7, true);
        var passage = new RealModulationPassages.Passage(
            "escape tones",
            RealModulationPassages.Texture.MelodyOverChords,
            true,
            "0 5 7:7 0 | 7 0 2:7 7",
            MusicNotation.Parse(
                "E4/4 F4/8 C4/8 G4/4 A4/8 E4/8 | A4/4 Bb4/8 F4/8 C5/4 D5/8 A4/8 | D5/4 E5/8 B4/8 G4/4 F4/8 E4/8 | E4/4 D4/8 C4/8 C4/2 | "
                + "B4/4 C#5/8 G4/8 D5/4 E5/8 B4/8 | E5/4 F#5/8 C5/8 G4/4 A4/8 F#4/8 | F#4/4 G4/8 D4/8 A4/4 Bb4/8 G4/8 | G4/4 F#4/8 G4/8 G4/2"),
            []);
        using var escapeTones = passage.Build(0);

        var result = ModulationDetector.Analyze(escapeTones, CMajor);
        Assert.Equal(CMajor, result.StartKey);
        var heard = Assert.Single(Modulations(escapeTones, CMajor));
        Assert.Equal(gMajor, heard.ToKey);
        Assert.InRange(heard.Position.ToDouble(), 4.0, 5.0);
        var trajectory = Assert.Single(Trajectory(escapeTones));
        Assert.Equal(gMajor, trajectory.ToKey);
        Assert.InRange(trajectory.Position.ToDouble(), 4.0, 5.0);
    }

    /// <summary>Each chord as R 3 5 8 5 3 R 3 in eighths, as the fixture's arpeggio texture is built.</summary>
    private static NoteBuffer Arpeggiate(string tokens)
    {
        using var block = Chords(tokens);
        var byOnset = new SortedDictionary<Rational, List<int>>();
        for (var i = 0; i < block.Count; i++)
        {
            var note = block.Get(i);
            if (!byOnset.TryGetValue(note.Offset, out var pitches))
            {
                byOnset[note.Offset] = pitches = [];
            }

            pitches.Add(note.Pitch);
        }

        int[] rise = [0, 1, 2, 3, 2, 1, 0, 1];
        var notes = new List<NoteEvent>();
        foreach (var (onset, pitches) in byOnset)
        {
            pitches.Sort();
            int[] tones = [.. pitches, pitches[0] + 12];
            for (var k = 0; k < 8; k++)
            {
                notes.Add(new NoteEvent(tones[rise[k]], onset + (Rational.Eighth * k), Rational.Eighth));
            }
        }

        var buffer = new NoteBuffer(notes.Count);
        buffer.AddRange(notes.ToArray());
        return buffer;
    }
}
