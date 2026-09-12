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
/// change at the first note is the opening key misjudged, and the two roads place the same
/// modulations. Each passage names what the roads answered before the rules, measured on the
/// library as it stood.
/// </summary>
public class AKeyHoldsForAPhraseTests
{
    private static readonly KeySignature CMajor = new(0, true);

    /// <summary>
    /// Block chords in close root position from C3, one whole note each unless suffixed
    /// <c>h</c> (a half note): <c>root[:quality]</c> with the root in semitones above C and the
    /// quality one of <c>m 7 dim</c>; bar lines are ignored.
    /// </summary>
    private static NoteBuffer Chords(string tokens)
    {
        var qualities = new Dictionary<string, int[]> { [""] = [0, 4, 7], ["m"] = [0, 3, 7], ["7"] = [0, 4, 7, 10], ["dim"] = [0, 3, 6] };
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
            if (token.EndsWith('h'))
            {
                duration = Rational.Half;
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

        // With a V7/V in bars 2 and 6 the excursions to G are heard, and heard as what they are:
        // a two-bar applied dominant is a tonicization, not a modulation. Both were reported as
        // Direct modulations — one to G, one to E minor.
        var events = ModulationDetector.Analyze(applied, CMajor).Modulations;
        Assert.NotEmpty(events);
        Assert.All(events, e =>
        {
            Assert.Equal(ModulationType.Tonicization, e.Type);
            Assert.Equal(new KeySignature(7, true), e.ToKey);
        });
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

        // The held-out passages too, to the same keys within a bar of each other: the roads
        // differ in what a sonority is, and a note held alone for a bar — the common tone of a
        // common-tone modulation — is a sonority to the trajectory and no chord to the detector,
        // which places the new key at the chord after it where the trajectory places it at the
        // note.
        foreach (var passage in RealModulationPassages.HeldOut)
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

        // Arpeggiated I V7/V V I twice: the excursion to G is the D7 and the G, read by whole
        // notes, because an arpeggiated D7 sounds its A and C after its F sharp and read note by
        // note the excursion ended inside the chord that began it.
        using var arpeggiated = Arpeggiate("0 2:7 7 0 | 0 2:7 7:7 0");

        var applied = Assert.Single(ModulationDetector.Analyze(arpeggiated, CMajor).Modulations);
        Assert.Equal(ModulationType.Tonicization, applied.Type);
        Assert.Equal(new KeySignature(7, true), applied.ToKey);
        Assert.Equal(new Rational(1, 1), applied.Offset);
        Assert.Equal(new Rational(2, 1), applied.Duration);
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
