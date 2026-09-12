// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Analysis;

/// <summary>
/// A note or a chord as the key judge hears it: when it starts, when it stops, which pitch
/// classes it holds and — for a single note — its pitch, so that the judge can tell a step
/// from a leap along the line. <see cref="KeyProfiler.AnalyzeModulations"/> hands the judge
/// one of these per note; <see cref="ModulationDetector"/> hands it one per chord and one per
/// note of the line between the chords.
/// </summary>
/// <param name="Onset">When it starts.</param>
/// <param name="End">When it stops.</param>
/// <param name="PitchClasses">The pitch classes it holds, one bit each.</param>
/// <param name="Pitch">The MIDI pitch of a single note, or -1 for a chord.</param>
internal readonly record struct Sonority(Rational Onset, Rational End, ushort PitchClasses, int Pitch = -1);

/// <summary>
/// One change of key the judge heard: where the new key begins, what it left and what it
/// arrived at, and whether it held.
/// </summary>
/// <param name="Position">The start of the whole note in which the new key begins.</param>
/// <param name="From">The key the music was in.</param>
/// <param name="To">The key it moved to.</param>
/// <param name="Established">
/// Whether the new key held for a phrase — or to the end of the piece, closing on its tonic.
/// A change that did not hold is a tonicization: the music touched the key and came back.
/// </param>
/// <param name="Separation">How clearly the evidence chose <paramref name="To"/> over <paramref name="From"/>, 0-1.</param>
/// <param name="Stability">How much of the stretch that established the key belonged to it, 0-1.</param>
/// <param name="HeldUntil">
/// For an established key, where a note it does not own first sounds after <paramref name="Position"/>,
/// or the end of the music; for a tonicization, where the music is home again — the first
/// note the key does not own, or the first whole note after its tonic chord that neither is
/// that chord nor sounds a note of the key's own.
/// </param>
internal readonly record struct KeyChange(
    Rational Position,
    KeySignature From,
    KeySignature To,
    bool Established,
    float Separation,
    float Stability,
    Rational HeldUntil);

/// <summary>
/// What the judge heard: the key the music opens in, and every change of key after that.
/// </summary>
/// <param name="Opening">
/// The key the music opens in: the key given, unless the music was never in it — a change
/// placed at the first note is the opening key misjudged, not a modulation, and the key heard
/// there is the opening.
/// </param>
/// <param name="Changes">The changes of key, in order.</param>
internal readonly record struct Judgement(KeySignature Opening, List<KeyChange> Changes);

/// <summary>
/// The one place that decides where a piece changes key. Both public roads —
/// <see cref="KeyTrajectory.DetectModulations"/> over the notes of its fixed windows and
/// <see cref="ModulationDetector.Analyze(NoteBuffer, KeySignature)"/> over its chords — hand
/// their sonorities to <see cref="Judge"/>, and differ only in what a sonority is and in what
/// they layer on top of the answer.
/// </summary>
/// <remarks>
/// <para>
/// The judge applies a musician's rules rather than a window's point estimate:
/// </para>
/// <list type="bullet">
/// <item><description><b>A key holds for a phrase.</b> Evidence is read over <see cref="Phrase"/>
/// — four whole notes, a four-bar phrase in common time — never over a window shorter than
/// that. Two block chords in a two-bar window read as the key of the pitch class they share,
/// so I–V sounded as the dominant's key and IV–V7 as the subdominant's; a phrase reads as the
/// key it is in.</description></item>
/// <item><description><b>A chord is not a key.</b> A phrase whose material cannot decide a key
/// (<see cref="KeyDetectionResult.IsDecidable"/>) or does not separate its winner from the
/// runner-up says nothing; an arpeggiated IV chord read as the relative minor of its own
/// three notes.</description></item>
/// <item><description><b>A key owns its notes.</b> A phrase is named by the key that leaves the
/// least of it foreign — among keys that leave the same amount, by the Krumhansl profile. The
/// profile alone hears a dominant seventh as the key on its root: V7 I V7 I in G, half the
/// material D7, read as D major by a clear margin, though D major does not own the C natural
/// in every D7 and G owns every note.</description></item>
/// <item><description><b>A key owns its phrase.</b> Leaving the least of a phrase foreign is not
/// owning it: a phrase is a key's only when the notes the key lacks amount to less than a
/// quarter note in every bar of it — a stray note, not a chord tone (<see cref="StrayNote"/>
/// measures the passages that must pass and fail). An applied dominant is the key's chord: a
/// dominant seventh that resolves down a fifth into a chord the key owns leaves nothing
/// foreign, so I V7/V V I is owned by its key whole. Judged on what it left foreign relative
/// to the key the music was in, a passage that visits D and E for two bars each read as A
/// major, a key that was never there, because A leaves less of it foreign than D or E does; a
/// chromatic scale in eighths read as thirty-six keys one eighth apart; and D7 G C A7, the
/// phrase after the pivot of a pop verse, read as G major with the A7's C sharp called foreign
/// — an A7 that resolves to D minor, which G does not own, is no chord of G.</description></item>
/// <item><description><b>A key's chromatic chords are its own.</b> An applied chord — a major
/// triad as much as a dominant seventh, resolving down a fifth into a chord of the key that is
/// not the current key's tonic — and a borrowed chord — a major key's minor subdominant, flat
/// sixth or flat seventh, resolving into a chord of the key — leave nothing foreign, inside a
/// phrase the key frames with its tonic; and a minor key's tonic major triad closing the piece
/// is its Picardy third, a cadence of the minor key and no other key's note. A chord the key in
/// force owns is that key's, whatever another key might borrow it as, and a key cannot begin
/// on one of its own chromatic chords. With only dominant sevenths applied, C F G C | G E Am
/// D7 | G C D7 G and C F G C | G Cm D7 G | G C D7 G reached G two bars late, at the Am and the
/// D7, and Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭ G7 C never came home to C minor, ending in E flat
/// with the Picardy chord a tonicization of C major.</description></item>
/// <item><description><b>A passing tone is not a foreign note.</b> A note on its way — short,
/// approached and left by step — in a run that moves one way from a structural note the key
/// owns to another within a whole note, a semitone from the notes either side of it, is a
/// passing tone; a note that leaves an owned note by a semitone and returns is a neighbour;
/// a note approached by leap and resolved by step into the chord sounding under it is an
/// appoggiatura. None of them weighs anything toward what the key lacks, on either road: the
/// detector hands the judge the line between its chords for the purpose. Weighed as strays,
/// two chromatic passing eighths in a bar were a quarter of it, and a melody with two in every
/// bar of its new key named no key; a quarter-note neighbour under a D7 put G four bars late on
/// the trajectory road and at the bar on the detector's, which never saw it.</description></item>
/// <item><description><b>A minor key owns its leading tone in its dominant.</b> The raised
/// seventh belongs to V, V7 and the leading-tone seventh, sounds alone in the line, or leans on
/// the tonic chord as an appoggiatura; in any other chord it is a note the key lacks. Owned as
/// a free pitch class, it made E minor the owner of any G-major phrase with an E flat in it — a
/// C minor chord borrowed, a quarter-note neighbour — and E minor was named.</description></item>
/// <item><description><b>You cannot leave a key without sounding a note it lacks.</b> The new key
/// must be separated from the current one on the profile and some note the new key owns and
/// the current key does not must actually sound. A melody in C with a chromatic passing tone
/// in every bar read as G for four bars on the strength of one F sharp, while sounding F, A
/// flat, B flat and C sharp, none of which G owns.</description></item>
/// <item><description><b>A secondary dominant is not a modulation.</b> The change is only a
/// modulation if the new key still reads from where it began through a phrase; if the music is
/// back home by then, it was a tonicization. A phrase that cannot decide is extended by a
/// phrase at a time, to the end of the piece at most, and a stretch at the end shorter than a
/// phrase establishes a key only if the piece closes on that key's tonic having already sounded
/// it — in the pivot bar or in the stretch — so that V7/V–V closing a phrase is a half cadence,
/// not a modulation to the dominant. The pivot bar must itself sound the tonic: any bar the new
/// key owned used to count, and C Am F G | C Am D7 G modulated to G at the Am, a half cadence
/// read as a modulation because vi of the old key is ii of the new.</description></item>
/// <item><description><b>A key is entered when its own notes return.</b> The notes a key owns and
/// the key before it lacks — F sharp for G from C, G sharp for A minor from C — are what tell
/// the two apart, and one bar of them is an applied dominant. They must sound in a second bar
/// before a note of the old key's own is heard again, within two phrases of the change; or the
/// phrase that establishes the new key must be framed by its tonic chord, opening on it and
/// closing within it, which is a phrase in that chord's key whatever else it holds. So C F | E7
/// Am | F G | C C is a tonicization of vi, E7 Am Dm E7 Am is A minor, Am Dm E7 Am after four
/// bars of C is A minor too, and C F G C | G C D7 G | C F G C goes to the dominant and comes
/// back. Judged by whether the new key held for a phrase alone, the applied dominants of an
/// ordinary pop verse — C Am D7 G | C A7 Dm G7 — were three modulations, and C D7 G C | E7 Am
/// A7 Dm | G7 C B7 Em | F D7 G7 C, sixteen bars in C, went to G, A minor and back.</description></item>
/// <item><description><b>The relative major is reached when the leading tone stops.</b> The
/// relative major owns no note the minor lacks, so it is reached when a phrase reads as the
/// major with the minor's leading tone nowhere in it, and it begins at the bar where the
/// minor's tonic is left.</description></item>
/// <item><description><b>The change is placed where the new key begins.</b> A key cannot have
/// begun before the last note it does not own, so a tonicization is placed at the first
/// sonority after that which sounds a note the new key owns and the old does not, moved back
/// to the start of its whole note across sonorities both keys own — an arpeggiated D7 begins on
/// its D, not on the F sharp an eighth later; a modulation one bar earlier still when the new
/// key owns that whole bar, which is the pivot chord: C F G C | G D7 G modulates at the G, as a
/// musician writes it, not at the D7. And a key area begins with a phrase: when the new key's
/// own note falls later in the phrase — phrases counted from the first note, a phrase long —
/// and the new key owns every bar from the phrase's start, the key began there, provided a
/// phrase from there establishes it and it does not open on the old key's tonic chord, which is
/// still the old key. C F G C | G C D7 G | C F G C is heard as G from its fifth bar; measured
/// from the D7 in its seventh, the phrase ran into the return to C and the G area was a
/// tonicization. C F G C | D7 G C C | C F G C, whose D7 opens the phrase, stays in C.</description></item>
/// <item><description><b>A change at the first note is the opening key misjudged.</b> Whether the
/// opening key was read from the opening phrase or given by the caller, a change placed at the
/// first sonority means the music was never in the key it would leave; the key heard there is
/// the opening, and no modulation is reported. An A minor melody analyzed from C minor reported
/// a modulation to A minor at its first note. And an opening key guessed from the profile alone
/// — a melody opens on no chord — that never sounded a note of its own before another key was
/// read was never there either: two hundred random diatonic melodies in C, opened in A minor, E
/// minor or G by the profile, reported thirty-six modulations to C (two hundred more from
/// another seed, forty-five; now two).</description></item>
/// </list>
/// </remarks>
internal static class KeyAreaJudge
{
    /// <summary>
    /// How long a key must hold to be a key and not a chord: four whole notes, a four-bar
    /// phrase in common time. The evidence for a change is read over this length, and a change
    /// that does not hold for it is a tonicization.
    /// </summary>
    internal static readonly Rational Phrase = new(4, 1);

    // A phrase names a key when, among the keys that own most of it, the best separates from
    // the runner-up by this margin; the margin semantics are those of
    // KeyDetectionResult.Confidence, on which a clear detection sits around 0.1-0.35 and a
    // straddle of two keys near zero. Measured on the passages in
    // RealModulationsAreHeardWhereAMusicianHearsThemTests: every phrase that begins where a
    // new key begins clears it (0.12-0.37), and the phrases that straddle two keys fall below
    // it — C D♭ G♭ A♭ reads A♭ over D♭ at 0.030, and C D7 G C reads G over C at 0.025.
    private const float MinDecisive = 0.11f;

    // The new key must beat the key the music is in by this margin, measured the same way on
    // the pair that matters. Also the hysteresis that keeps a wobble from reading as a return.
    private const float MinSeparation = 0.11f;

    // A phrase that ties two keys within this margin establishes neither. Symmetric material —
    // a tritone pair of triads — scores two keys identically, and the tie is broken by key
    // index, which a transposition does not respect; refusing to build on a tie keeps the
    // judge's answer moving with the music.
    private const float MinTold = 0.01f;

    /// <summary>
    /// A key owns a bar when the notes it lacks amount to less than this in it — a quarter
    /// note of pitch-class weight, where a pitch class weighs the time it sounds. A stray note
    /// weighs an eighth; a chord tone sounds for its chord, a half bar or a bar, and is not
    /// stray. A passing tone, a neighbour tone or an appoggiatura of the key weighs nothing at
    /// all (<see cref="Evidence.IsNonHarmonic(int, KeySignature)"/>), nor does a note of the
    /// key's own chromatic chords (<see cref="Evidence.ChromaticChordOf"/>).
    /// </summary>
    /// <remarks>
    /// Measured in whole notes of pitch-class weight per bar, on the passages that must fail:
    /// the A7 of D7 G C A7, resolving to a D minor that G does not own, leaves G major 1.0 in
    /// its bar (the C sharp for a whole bar); the G chord of D G | A D, two bars of D and two of
    /// E read as A major, leaves A 0.5 in its bar and the B chord 0.5; the same passage
    /// arpeggiated in eighths leaves A 0.25 in the G chord's bar; a chromatic scale in eighths
    /// leaves every key 0.625 in every bar, none of its notes passing because none of them
    /// lands. And on what must pass: every modulation in the fixture leaves its new key 0.0 in
    /// every bar from where it begins; four bars of D flat in thirty-second-note arpeggios after
    /// four of C, whose last C notes quantize onto the D flat downbeat in the detector's
    /// eighth-note chords, leave D flat 0.125 in that bar.
    /// <para>
    /// The floor is <c>&gt;=</c> a quarter. It once weighed passing tones as it weighs strays:
    /// a chromatic quarter-note neighbour — a C sharp under the D7 of a G-major melody over
    /// chords — weighed exactly 0.25 in its bar, so the trajectory, which reads the melody,
    /// placed G four bars late (at 8 where a musician says 4) while the detector, which never
    /// saw the eighths, placed it at 4; two chromatic passing eighths in one bar weighed the
    /// same and a melody with two in every bar of its new key named no key at all; and a
    /// quarter E flat neighbour in G named E minor, whose raised seventh it is, on both roads.
    /// Measured with <c>&gt;</c> instead: the arpeggiated chain of secondary dominants names F
    /// sharp minor, and over two hundred random chromatic melodies the detector's modulations
    /// rise from 16 to 173 and the trajectory's from 3 to 59. The floor stays where it is; the
    /// passing tones weigh nothing, and those shapes are heard where a musician hears them.
    /// </para>
    /// </remarks>
    private const float StrayNote = 0.25f;

    /// <summary>
    /// The pitch classes a key owns: its scale, and for a minor key its raised leading tone,
    /// so that a minor key's dominant belongs to it.
    /// </summary>
    internal static ushort Owned(KeySignature key) => OwnedByKey[KeyIndex(key)];

    /// <summary>The index of <paramref name="key"/> among the twenty-four: its root, the minor keys after the major.</summary>
    private static int KeyIndex(KeySignature key) => key.Root + (key.IsMajor ? 0 : 12);

    private static readonly ushort[] OwnedByKey = BuildOwned();

    /// <summary>For each pitch class, the keys — one bit each, by <see cref="KeyIndex"/> — that do not own it.</summary>
    private static readonly uint[] KeysLacking = BuildKeysLacking();

    private static uint[] BuildKeysLacking()
    {
        var lacking = new uint[12];
        for (var pc = 0; pc < 12; pc++)
        {
            for (var k = 0; k < 24; k++)
            {
                if ((OwnedByKey[k] & (1 << pc)) == 0)
                    lacking[pc] |= 1u << k;
            }
        }

        return lacking;
    }

    private static ushort[] BuildOwned()
    {
        var owned = new ushort[24];
        for (var root = 0; root < 12; root++)
        {
            owned[root] = new KeySignature((byte)root, true).GetScaleMask();
            owned[root + 12] = (ushort)(new KeySignature((byte)root, false).GetScaleMask() | (1 << PitchMath.Fold(root + 11)));
        }

        return owned;
    }

    /// <summary>
    /// The key changes in <paramref name="sonorities"/>, judged at each of <paramref name="candidates"/>
    /// (ascending positions at which a phrase may be read), starting from <paramref name="startKey"/>
    /// or, when that is <see langword="null"/>, from the key the opening phrase is in.
    /// <paramref name="phrase"/> is the length a key must hold; callers pass <see cref="Phrase"/>
    /// or their own longer window.
    /// </summary>
    internal static Judgement Judge(
        IReadOnlyList<Sonority> sonorities,
        IReadOnlyList<Rational> candidates,
        KeySignature? startKey,
        Rational phrase)
    {
        var changes = new List<KeyChange>();
        if (sonorities.Count == 0)
            return new Judgement(startKey ?? new KeySignature(0, true), changes);

        var evidence = new Evidence(sonorities);
        var end = evidence.End;
        var firstOnset = evidence.FirstOnset;
        var openingByChord = false;
        var current = startKey ?? evidence.OpeningKey(phrase, out openingByChord);
        var opening = current;

        // An opening key read from the profile alone, with no chord to open on, is a guess.
        var openingGuessed = startKey is null && !openingByChord;

        // No change may be placed before this: the change before it, or the return home after a
        // tonicization, so that one excursion is counted once.
        var floor = Rational.Zero;

        foreach (var b in candidates)
        {
            var to = b + phrase < end ? b + phrase : end;
            if (to <= b || to <= floor)
                continue;

            var reading = evidence.Profile(b, to);
            if (!reading.Result.IsDecidable)
                continue;

            var named = evidence.Name(reading, b, to, current);
            if (named.Margin < MinDecisive)
                continue;

            var next = named.Key;
            if (next == current)
                continue;

            var separation = Separation(reading.Result, next, current);
            if (separation < MinSeparation)
                continue;

            // A key owns its phrase: the phrase that names the new key sounds nothing the new
            // key lacks beyond a stray note — not the old key's own notes, which would mean the
            // old key was never left, and not an applied dominant's altered note either, unless
            // it resolves into a chord the new key owns.
            if (!evidence.Owns(b, to, next, current))
                continue;

            // A key that owns nothing the old key lacks — the relative major, from its minor —
            // cannot announce itself with a note; it is heard when a phrase reads as it with no
            // note of the old key's own in it. Any other key must sound a note the old key lacks
            // — as its own note, not as the Picardy third that closes the old key's piece.
            var owned = Owned(next);
            var newNotes = (ushort)(owned & ~Owned(current));
            var ownsNothingNew = newNotes == 0;
            if (!ownsNothingNew && !evidence.Sounds(newNotes, b, to, next, current))
                continue;

            // A guessed opening key that never sounded a note of its own before another key was
            // read was wrong, and the music was in the other key from the start. A random
            // diatonic melody without a cadence is as much A minor as C major to the profile,
            // and read as A minor at its opening it modulated to C at the first phrase that read
            // the other way; V I IV V | I IV V I in C, opening on its dominant, read as G on the
            // profile and modulated to C at its second chord. A key that has no note of its own
            // against the new one — the major, against its relative minor — is not a guess this
            // can test.
            // A home note that is a non-harmonic tone of the other key was never the guessed
            // key's own: the F sharp in a C-major scale's E F F sharp G is no note of E minor's,
            // and a C-major melody with chromatic runs in it, opened in E minor by the profile,
            // reported a modulation to C at its fourth bar.
            var homeNotes = (ushort)(Owned(current) & ~owned);
            if (openingGuessed
                && changes.Count == 0
                && homeNotes != 0
                && !evidence.SoundsAsItsOwn(homeNotes, firstOnset, to, next, current))
            {
                opening = next;
                current = next;
                continue;
            }

            // Where does the new key begin? Not before the floor, not more than a phrase before
            // the evidence, and not before the last note the new key does not own: at the first
            // sonority after that which sounds a note of the new key the old one lacks; for a
            // key with no such note, where the phrase first leaves the old key's tonic chord.
            var scanFrom = b - phrase > floor ? b - phrase : floor;
            Rational first;
            if (ownsNothingNew)
            {
                var oldTonic = TonicTriad(current);
                if (!evidence.FirstOnsetSounding((ushort)(~oldTonic & 0x0FFF), b > floor ? b : floor, afterFrom: false, to, out first))
                    continue;
            }
            else
            {
                if (evidence.LastOnsetLacking(next, current, scanFrom, b, out var lastForeign))
                    scanFrom = evidence.NextOnsetAfter(lastForeign);
                if (!evidence.FirstOnsetOfTheKey(newNotes, scanFrom, to, next, current, out first))
                    continue;
            }

            var boundary = evidence.StartOfBar(first, next, current);
            if (ownsNothingNew && boundary.Denominator != 1)
            {
                // The relative major has no note of its own to begin on; it begins with a bar,
                // not on the note after the minor's last leading tone. In a melody every note
                // leaves the tonic triad, and the return read from the middle of the minor's
                // final cadence.
                boundary = new Rational((long)Math.Floor(boundary.ToDouble()) + 1, 1);
            }

            if (boundary < floor)
                boundary = floor;
            if (boundary >= to)
                continue;

            // Does the new key hold from there through a phrase? A phrase that cannot decide is
            // extended, to the end at most.
            var holdTo = (b > boundary ? b : boundary) + phrase;
            var (holds, hold, heldAs) = evidence.Holds(boundary, ref holdTo, next, current, phrase);

            // The phrase from the boundary reads a third key decisively: the evidence straddled
            // two keys and its compromise reading named neither. Nothing happened here — unless
            // the new key began with its phrase, judged below — and the third key is found at
            // its own candidate. C7 F B♭ F | C F G C is home at its ninth bar, but C's own note
            // is in the eleventh, and the two-bar stretch from there, G C, reads as G.
            var thirdKey = !holds
                && hold.Result.IsDecidable
                && heldAs.Key != next
                && heldAs.Key != current
                && heldAs.Margin >= MinDecisive
                && Separation(hold.Result, heldAs.Key, current) >= MinSeparation;

            // The bar before the first note the new key alone owns, when the new key owns that
            // whole bar outright: the pivot chord, a chord of both keys.
            var previousBar = boundary - Rational.Whole;
            var pivot = previousBar >= floor && evidence.BarIsOwned(previousBar, next, current, Exempt.Nothing);

            // A modulation is a change from the key the music is in: a stretch at the end can
            // only be a cadence in a key the music was in if the old key was still in force just
            // before it — in the pivot bar, or in the bar before the new key begins. Two bars of
            // F sharp closing a passage that left C four bars earlier for D and E is not a
            // modulation from C.
            // A bar both keys own but for a stray note — a chromatic passing eighth in the first
            // bar of a closing phrase — is the pivot bar still, and the old key was in force.
            var fromOldKey = pivot
                || evidence.BarIsOwned(previousBar, current, null)
                || (previousBar >= floor
                    && evidence.Owns(previousBar, boundary, next, current)
                    && evidence.Owns(previousBar, boundary, current, null));

            // A stretch at the end shorter than a phrase is a key only if the piece closes on
            // its tonic and that tonic was already heard — in the pivot bar, or in the stretch
            // before the close — so that the close is a cadence in a key the music was in.
            // V7/V–V closing a phrase is a half cadence, not a modulation to the dominant, and
            // a pivot bar without the tonic in it (vi of the old key as ii of the new) is not
            // where the tonic was heard.
            var fragment = holdTo == end && holdTo - boundary < phrase;
            var established = holds
                && (!fragment
                    || (fromOldKey && evidence.ClosesOn(next) && evidence.SoundsTonicBeforeClose(next, pivot ? previousBar : boundary)));

            // A key is entered when its own notes return: one bar of them is an applied
            // dominant, a second bar before the old key's own notes are heard again is the key
            // — unless the phrase is framed by the new tonic chord, which is a phrase in its
            // key. A key that owns nothing the old key lacks has no note of its own to return.
            var held = hold.Distribution;
            if (established
                && !ownsNothingNew
                && !evidence.Returns(newNotes, homeNotes, boundary, boundary + phrase + phrase, next, current)
                && !evidence.FramedBy(next, boundary, holdTo))
            {
                established = false;
            }

            // A modulation is written at its pivot chord.
            var position = boundary;
            if (established && pivot)
                position = previousBar;

            // A key area begins with a phrase. When the new key's own note falls later in its
            // phrase — the phrases counted from the first note — and the new key owns every bar
            // from the phrase's start, the key began there, if a phrase from there establishes
            // it: C F G C | G C D7 G | C F G C is G from its fifth bar, though the F sharp is in
            // the seventh and a phrase from the seventh runs into the return to C. Not a phrase
            // opening on the old key's tonic chord, which is still the old key: Am A7 Dm B♭ is A
            // minor with V7/iv and the Neapolitan, not D minor from its v.
            if (!ownsNothingNew)
            {
                var phraseStart = evidence.PhraseStart(first, phrase);
                if (phraseStart >= floor
                    && phraseStart < (established ? position : boundary)
                    && !evidence.OpensOn(current, phraseStart)
                    && evidence.BarIsOwned(phraseStart, next, current, Exempt.Nothing)
                    && evidence.BarsAreOwned(phraseStart, boundary, next, current))
                {
                    var startTo = phraseStart + phrase;
                    var (startHolds, earlier, _) = evidence.Holds(phraseStart, ref startTo, next, current, phrase);
                    var startFragment = startTo == end && startTo - phraseStart < phrase;
                    var barBefore = phraseStart - Rational.Whole;
                    var startFromOldKey = evidence.BarIsOwned(barBefore, current, null)
                        || evidence.BarIsOwned(barBefore, next, current);
                    if (startHolds
                        && (!startFragment || (startFromOldKey && evidence.ClosesOn(next) && evidence.SoundsTonicBeforeClose(next, phraseStart)))
                        && (evidence.Returns(newNotes, homeNotes, phraseStart, phraseStart + phrase + phrase, next, current)
                            || evidence.FramedBy(next, phraseStart, startTo)))
                    {
                        established = true;
                        position = phraseStart;
                        held = earlier.Distribution;
                    }
                }
            }

            if (thirdKey && !established)
                continue;

            // A change placed at the first note is the opening key misjudged, not a modulation:
            // the music was never in the key it would leave.
            if (established && position <= firstOnset)
            {
                opening = next;
                current = next;
                floor = evidence.NextOnsetAfter(boundary);
                continue;
            }

            var total = 0f;
            foreach (var weight in held)
                total += weight;
            var stability = total > 0f ? 1f - (Outside(held, next) / total) : 0f;

            // An established key holds until a note it does not own; an excursion lasts until
            // the music is home again — and an excursion that is home again before the new key's
            // tonic chord has sounded touched nothing: a single F sharp in an arpeggio is not a
            // tonicization of E major.
            Rational heldUntil;
            if (established)
            {
                heldUntil = evidence.FirstOnsetLacking(next, current, position, end);
            }
            else
            {
                heldUntil = evidence.HomeAgain(next, current, newNotes, homeNotes, boundary, out var tonicHeard);
                if (!tonicHeard)
                    continue;
            }

            changes.Add(new KeyChange(position, current, next, established, separation, stability, heldUntil));

            if (established)
            {
                current = next;
                floor = evidence.NextOnsetAfter(boundary);
            }
            else
            {
                // The same excursion is not counted twice: the next change waits for the music
                // coming home.
                floor = heldUntil > boundary ? heldUntil : evidence.NextOnsetAfter(boundary);
            }
        }

        return new Judgement(opening, changes);
    }

    /// <summary>A phrase's key, and how clearly it was named: the profile margin over the runner-up among the keys that own most of the phrase, 1 when no other key owns as much.</summary>
    private readonly record struct Named(KeySignature Key, float Margin);

    /// <summary>
    /// How much better the material fits <paramref name="next"/> than <paramref name="current"/>:
    /// the margin between their correlations relative to the better, clamped to 0-1 the way
    /// <see cref="KeyDetectionResult.Confidence"/> clamps its own, and zero when the better
    /// correlation is not positive.
    /// </summary>
    private static float Separation(KeyDetectionResult reading, KeySignature next, KeySignature current)
    {
        var nextCorrelation = CorrelationOf(reading, next);
        if (nextCorrelation <= 0f)
            return 0f;

        var separation = (nextCorrelation - CorrelationOf(reading, current)) / (nextCorrelation + 0.001f);
        return Math.Clamp(separation, 0f, 1f);
    }

    private static float CorrelationOf(KeyDetectionResult reading, KeySignature key)
    {
        foreach (var candidate in reading.AllCorrelations)
        {
            if (candidate.Key == key)
                return candidate.Correlation;
        }

        return 0f;
    }

    /// <summary>The pitch classes of <paramref name="key"/>'s tonic triad.</summary>
    private static ushort TonicTriad(KeySignature key) => TonicTriadByKey[KeyIndex(key)];

    private static readonly ushort[] TonicTriadByKey = BuildTonicTriads();

    private static ushort[] BuildTonicTriads()
    {
        var triads = new ushort[24];
        for (var root = 0; root < 12; root++)
        {
            triads[root] = (ushort)((1 << root) | (1 << PitchMath.Fold(root + 4)) | (1 << PitchMath.Fold(root + 7)));
            triads[root + 12] = (ushort)((1 << root) | (1 << PitchMath.Fold(root + 3)) | (1 << PitchMath.Fold(root + 7)));
        }

        return triads;
    }

    /// <summary>
    /// Whether <paramref name="pitchClasses"/> is <paramref name="key"/>'s tonic chord: it
    /// lies in the tonic triad and sounds the tonic. A lone G is a note of the C major triad,
    /// but a phrase beginning on G does not begin on the chord of C.
    /// </summary>
    private static bool IsTonicChord(KeySignature key, ushort pitchClasses) =>
        pitchClasses != 0
        && (pitchClasses & ~TonicTriad(key)) == 0
        && (pitchClasses & (1 << key.Root)) != 0;

    /// <summary>The weight of the material that <paramref name="key"/> does not own.</summary>
    private static float Outside(ReadOnlySpan<float> distribution, KeySignature key)
    {
        var owned = Owned(key);
        var sum = 0f;
        for (var pc = 0; pc < 12; pc++)
        {
            if ((owned & (1 << pc)) == 0)
                sum += distribution[pc];
        }

        return sum;
    }

    /// <summary>
    /// The pitch classes of a dominant seventh chord on <paramref name="root"/>: root, major
    /// third, fifth and minor seventh.
    /// </summary>
    private static ushort DominantSeventh(int root) =>
        (ushort)((1 << root)
            | (1 << PitchMath.Fold(root + 4))
            | (1 << PitchMath.Fold(root + 7))
            | (1 << PitchMath.Fold(root + 10)));

    /// <summary>
    /// The key profile of a span, with the pitch classes that sound in it and the weight of
    /// each pitch class over it.
    /// </summary>
    /// <param name="Result">The reading <see cref="KeyProfiler.DetectFromBuffer"/> would give the span.</param>
    /// <param name="Mask">The pitch classes that sound in the span.</param>
    /// <param name="Distribution">The weight of each pitch class over the span: the time it sounds.</param>
    private readonly record struct Reading(KeyDetectionResult Result, ushort Mask, float[] Distribution);

    /// <summary>
    /// How far a key's chromatic chords count as its own when its material is weighed: not at
    /// all — every note outside the scale is foreign, which measures how much chromatic harmony
    /// the key needs; its dominant sevenths and Picardy third, which name themselves; or all of
    /// them, its applied triads and borrowed chords too.
    /// </summary>
    private enum Exempt
    {
        Nothing,
        Sevenths,
        Chords,
    }

    /// <summary>The sonorities in onset order, with the lookups the judge needs.</summary>
    private sealed class Evidence
    {
        private readonly Sonority[] _sonorities;
        private readonly Rational _longest;
        private readonly int[] _long;

        // Sonorities that begin together are one chord. For each sonority, the chord's pitch
        // classes; the pitch classes of the next chord of three or more notes — a melody's
        // single notes between are not chords — which is what the chord resolves into; and,
        // when the chord is a major triad or a dominant seventh whose resolution is a triad on
        // the root a fifth below, that root, so that an applied chord can be told to belong to
        // the key that owns its resolution.
        private readonly ushort[] _chordPitchClasses;
        private readonly ushort[] _following;
        private readonly int[] _appliedRoot;

        // Whether each sonority is a single note of the line — a pitch, alone at its onset —
        // and whether the chord it belongs to could be one of some key's chromatic chords: an
        // applied chord, a borrowed chord (it holds a triad) or the last chord (a Picardy
        // third). Most sonorities in a melody are neither, and are weighed without asking.
        private readonly bool[] _lineNote;
        private readonly bool[] _maybeChromatic;

        // Whether each sonority lasts no longer than a quarter note.
        private readonly bool[] _short;

        // The keys — one bit each, by KeyIndex — a single note is a non-harmonic tone of:
        // passing, neighbour or appoggiatura. Decided once per note for all twenty-four keys,
        // because every phrase that holds the note asks the same question of it; the top bit
        // says the note has been asked.
        private uint[]? _nonHarmonic;
        private const uint Asked = 1u << 31;

        // Whether a single note is a note on its way — short, approached and left by step by
        // notes no shorter than itself — decided once per note; it does not depend on the key.
        private byte[]? _onItsWay;

        // What sounds either side of each note, and the run of notes on their way it is on,
        // found once per note: none of it depends on the key, and every key asks.
        private Neighbour[]? _before;
        private Neighbour[]? _after;
        private Run[]? _run;

        public Rational End { get; }

        /// <summary>The onset of the first sonority, or zero when there is none.</summary>
        public Rational FirstOnset => _sonorities.Length > 0 ? _sonorities[0].Onset : Rational.Zero;

        public Evidence(IReadOnlyList<Sonority> sonorities)
        {
            _sonorities = new Sonority[sonorities.Count];
            for (var i = 0; i < _sonorities.Length; i++)
                _sonorities[i] = sonorities[i];
            Array.Sort(_sonorities, static (a, b) => a.Onset.CompareTo(b.Onset));

            // The few sonorities far longer than the rest — a pedal held for the piece — are
            // kept aside, so that a scan for what sounds at a position reaches back over the
            // ordinary notes only and visits the pedal on its own: reaching back over the pedal's
            // length, every scan began at the first note, and a piece of four thousand chords
            // over a two-thousand-bar pedal took twenty times as long as the same chords alone.
            var end = Rational.Zero;
            var longest = Rational.Zero;
            var longOnes = new List<int>();
            for (var i = 0; i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.End > end)
                    end = s.End;
                var length = s.End - s.Onset;
                if (length > Phrase * 4)
                    longOnes.Add(i);
                else if (length > longest)
                    longest = length;
            }

            if (longOnes.Count > 16)
            {
                foreach (var i in longOnes)
                {
                    var length = _sonorities[i].End - _sonorities[i].Onset;
                    if (length > longest)
                        longest = length;
                }

                longOnes.Clear();
            }

            End = end;
            _longest = longest;
            _long = [.. longOnes];

            _chordPitchClasses = new ushort[_sonorities.Length];
            _following = new ushort[_sonorities.Length];
            _appliedRoot = new int[_sonorities.Length];
            _lineNote = new bool[_sonorities.Length];
            _maybeChromatic = new bool[_sonorities.Length];
            _short = new bool[_sonorities.Length];
            for (var i = 0; i < _sonorities.Length; i++)
                _short[i] = _sonorities[i].End - _sonorities[i].Onset <= Rational.Quarter;
            for (var i = 0; i < _sonorities.Length;)
            {
                var next = NextChord(i);
                ushort together = 0;
                for (var k = i; k < next; k++)
                    together |= _sonorities[k].PitchClasses;
                for (var k = i; k < next; k++)
                {
                    _chordPitchClasses[k] = together;
                    _appliedRoot[k] = -1;
                    _lineNote[k] = next - i == 1 && _sonorities[k].Pitch >= 0 && BitOperations.PopCount(together) == 1;
                    _maybeChromatic[k] = next == _sonorities.Length || HoldsATriad(together);
                }

                i = next;
            }

            // A chord resolves into the next chord — a melody's single notes between are not
            // chords; an applied chord is a major triad or a dominant seventh resolving into a
            // triad on the root a fifth below.
            for (var i = 0; i < _sonorities.Length; i = NextChord(i))
            {
                // Only a chord of three notes or more can be applied or borrowed; a melody's
                // notes have no resolution to look for, and looking took a melody of twenty
                // thousand notes twenty thousand scans to its end.
                if (BitOperations.PopCount(_chordPitchClasses[i]) < 3)
                    continue;

                var resolvesAt = NextChord(i);
                while (resolvesAt < _sonorities.Length && BitOperations.PopCount(_chordPitchClasses[resolvesAt]) < 3)
                    resolvesAt = NextChord(resolvesAt);
                if (resolvesAt >= _sonorities.Length)
                    continue;

                var following = _chordPitchClasses[resolvesAt];
                var root = AppliedRootOf(_chordPitchClasses[i]);
                if (root >= 0 && !ResolvesTo(root, following))
                    root = -1;

                for (var k = i; k < NextChord(i); k++)
                {
                    _following[k] = following;
                    _appliedRoot[k] = root;
                    _maybeChromatic[k] |= root >= 0;
                }
            }
        }

        /// <summary>Whether <paramref name="chord"/> holds a major or a minor triad on some root.</summary>
        private static bool HoldsATriad(ushort chord)
        {
            if (BitOperations.PopCount(chord) < 3)
                return false;

            for (var root = 0; root < 12; root++)
            {
                if ((chord & MajorTriad(root)) == MajorTriad(root) || (chord & MinorTriad(root)) == MinorTriad(root))
                    return true;
            }

            return false;
        }

        /// <summary>The index of the first sonority of the chord after the one <paramref name="index"/> belongs to.</summary>
        private int NextChord(int index)
        {
            var at = _sonorities[index].Onset;
            while (index < _sonorities.Length && _sonorities[index].Onset == at)
                index++;
            return index;
        }

        /// <summary>
        /// Whether <paramref name="chord"/> is a triad, major or minor, on the root a fifth below
        /// <paramref name="root"/> — whatever else sounds with it, so long as nothing sounds a
        /// third below that root. Containing the note is not enough: a C major chord contains
        /// E, and B7 followed by C is not V7 of E resolving. Containing the triad is not enough
        /// either: G sharp minor seventh holds B, D sharp and F sharp, and F sharp seventh
        /// followed by it is V7 falling to vi7, the deceptive cadence of B, not V7 of B
        /// resolving — a seventh chord on the sixth degree holds the tonic triad and is not the
        /// tonic chord. Read as V7/V of E resolving, it named E major for B B7 F sharp 7 G sharp
        /// minor 7, a phrase in B.
        /// </summary>
        private static bool ResolvesTo(int root, ushort chord)
        {
            var resolution = PitchMath.Fold(root + 5);
            return (chord & (1 << resolution)) != 0
                && (chord & (1 << PitchMath.Fold(resolution + 7))) != 0
                && ((chord & (1 << PitchMath.Fold(resolution + 3))) != 0 || (chord & (1 << PitchMath.Fold(resolution + 4))) != 0)
                && (chord & (1 << PitchMath.Fold(resolution - 3))) == 0
                && (chord & (1 << PitchMath.Fold(resolution - 4))) == 0;
        }

        /// <summary>
        /// The root of the applied chord <paramref name="pitchClasses"/> can be — a major triad
        /// and nothing else, or a dominant seventh with whatever sounds with it — or -1. A
        /// dominant seventh is a sonority of its own and names its root through a melody note
        /// struck with it; a plain triad with a stranger in it is not a plain triad. Only those
        /// two chords are applied: a major seventh or an added sixth on a degree rests there.
        /// </summary>
        /// <remarks>
        /// A plain triad is applied as a dominant seventh is: C F G C | G E Am D7 | G C D7 G is
        /// in G from its fifth bar, the E major triad being V/ii. With only dominant sevenths
        /// applied, the G sharp was a foreign bar and G began two bars late, at the Am.
        /// </remarks>
        private static int AppliedRootOf(ushort pitchClasses)
        {
            if (BitOperations.PopCount(pitchClasses) < 3)
                return -1;

            for (var root = 0; root < 12; root++)
            {
                var seventh = DominantSeventh(root);
                if ((pitchClasses & seventh) == seventh)
                    return root;
                var triad = MajorTriad(root);
                if (pitchClasses == triad)
                    return root;
            }

            return -1;
        }

        /// <summary>The pitch classes of the major triad on <paramref name="root"/>.</summary>
        private static ushort MajorTriad(int root) =>
            (ushort)((1 << root) | (1 << PitchMath.Fold(root + 4)) | (1 << PitchMath.Fold(root + 7)));

        /// <summary>The pitch classes of the minor triad on <paramref name="root"/>.</summary>
        private static ushort MinorTriad(int root) =>
            (ushort)((1 << root) | (1 << PitchMath.Fold(root + 3)) | (1 << PitchMath.Fold(root + 7)));

        /// <summary>
        /// The borrowed chord of the major key <paramref name="key"/> that the chord
        /// <paramref name="pitchClasses"/> is — the minor subdominant, the flat sixth or the flat
        /// seventh, whatever of the key's own sounds with it — as the triad's pitch classes, or 0.
        /// </summary>
        private static ushort BorrowedFrom(KeySignature key, ushort pitchClasses)
        {
            if (!key.IsMajor)
                return 0;

            var owned = Owned(key);
            var iv = MinorTriad(PitchMath.Fold(key.Root + 5));
            if ((pitchClasses & iv) == iv && (pitchClasses & ~iv & ~owned) == 0)
                return iv;
            var flatSixth = MajorTriad(PitchMath.Fold(key.Root + 8));
            if ((pitchClasses & flatSixth) == flatSixth && (pitchClasses & ~flatSixth & ~owned) == 0)
                return flatSixth;
            var flatSeventh = MajorTriad(PitchMath.Fold(key.Root + 10));
            if ((pitchClasses & flatSeventh) == flatSeventh && (pitchClasses & ~flatSeventh & ~owned) == 0)
                return flatSeventh;
            return 0;
        }

        /// <summary>
        /// The raised third of the Picardy third that closes the piece in the minor key
        /// <paramref name="key"/> — the chord sonority <paramref name="index"/> belongs to is the
        /// last, and is the key's tonic triad with its third raised — or 0.
        /// </summary>
        private ushort PicardyThird(int index, KeySignature key)
        {
            if (key.IsMajor || NextChord(index) < _sonorities.Length)
                return 0;

            var chord = _chordPitchClasses[index];
            var third = (ushort)(1 << PitchMath.Fold(key.Root + 4));
            var tonic = (ushort)((1 << key.Root) | third | (1 << PitchMath.Fold(key.Root + 7)));
            return (chord & ~tonic) == 0 && (chord & (1 << key.Root)) != 0 && (chord & third) != 0 ? third : (ushort)0;
        }

        /// <summary>
        /// The pitch classes sonority <paramref name="index"/> sounds that <paramref name="key"/>
        /// lacks — none for the notes of the key's own material that lies outside its scale: a
        /// chromatic chord of the key (<see cref="ChromaticChordOf"/>), as far as
        /// <paramref name="exempt"/> counts those as the key's, or a non-harmonic tone of the
        /// line (<see cref="IsNonHarmonic(int, KeySignature)"/>).
        /// </summary>
        private ushort Lacking(int index, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var lacking = Outright(index, key);
            if (lacking == 0)
                return 0;

            if (exempt != Exempt.Nothing && _maybeChromatic[index])
            {
                lacking &= (ushort)~ChromaticChordOf(index, key, current, exempt);
                if (lacking == 0)
                    return 0;
            }

            return IsNonHarmonic(index, key) ? (ushort)0 : lacking;
        }

        /// <summary>
        /// The notes outside <paramref name="key"/>'s scale that the chord sonority <paramref name="index"/>
        /// belongs to sounds as the key's own chromatic chord — an applied chord, a borrowed
        /// chord or the Picardy third that closes a minor piece — or 0 when the chord is none of
        /// those to the key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An applied chord — a major triad or a dominant seventh resolving down a fifth into a
        /// chord the key owns — is the key's, unless that chord is the tonic of
        /// <paramref name="current"/>, the key the music is in: a dominant resolving into the
        /// tonic is that key's dominant, not another key's applied chord. Am Dm E7 Am | A7 Dm B♭
        /// E7 is A minor with V7/iv and the Neapolitan; with its E7 taken for V7/v of D minor, D
        /// minor owned the second phrase. A borrowed chord — a major key's minor subdominant,
        /// flat sixth or flat seventh — resolving into a chord the key owns is the key's too: C F
        /// G C | G Cm D7 G | G C D7 G is in G from its fifth bar, and with the C minor chord a
        /// foreign bar G began two bars late.
        /// </para>
        /// <para>
        /// A dominant seventh names its own resolution; a plain triad or a borrowed chord is
        /// heard as applied or borrowed only against a key in force — <paramref name="current"/>
        /// given — never at the opening, where no key is yet: G | C F G C, a pickup on the
        /// dominant, opens in C, not in a G major that owns the F chord as its flat seventh. And
        /// a chord the key in force owns is that key's, whatever another key might borrow it as
        /// or apply it to: over a tonic pedal, C F G C A D G was owned by G major — the F its
        /// flat seventh, the A its V/V — where C major had to call the A chord foreign, and the
        /// piece went to the dominant. A key's applied triads and borrowed chords are heard,
        /// besides, only inside a phrase the key frames; that is <see cref="Owns"/>'s rule, which
        /// asks for them with <paramref name="exempt"/> at <see cref="Exempt.Chords"/> only then.
        /// </para>
        /// <para>
        /// The Picardy third: a minor key's tonic major triad closing the piece is that key's
        /// final cadence, its raised third owned as its raised seventh already is. Cm Fm G7 Cm |
        /// E♭ A♭ B♭ E♭ | Cm A♭ G7 C is home in C minor from its ninth bar; with the E natural a
        /// foreign bar, C minor never owned the closing phrase, the piece ended in E flat, and the
        /// Picardy chord was a tonicization of C major.
        /// </para>
        /// </remarks>
        private ushort ChromaticChordOf(int index, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var outside = (ushort)(_chordPitchClasses[index] & ~Owned(key));
            if (outside == 0)
                return 0;

            // A chord the key in force owns is that key's, whatever another key might borrow
            // it as or apply it to.
            if (current is { } inForce && OwnsChord(inForce, index))
                return 0;

            var owned = Owned(key);
            var following = _following[index];
            var resolvesIntoTheKey = following != 0 && (following & ~owned) == 0;
            var triads = exempt == Exempt.Chords && current is not null;
            ushort chromatic = 0;

            var root = _appliedRoot[index];
            if (root >= 0
                && resolvesIntoTheKey
                && (triads || (_chordPitchClasses[index] & DominantSeventh(root)) == DominantSeventh(root))
                && !(current is { } home && IsTonicChord(home, following)))
            {
                chromatic |= DominantSeventh(root);
            }

            if (resolvesIntoTheKey && triads)
                chromatic |= BorrowedFrom(key, _chordPitchClasses[index]);

            chromatic |= PicardyThird(index, key);
            return (ushort)(chromatic & outside);
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns outright every note of the chord sonority
        /// <paramref name="index"/> belongs to — for a minor key, its raised seventh only in a
        /// chord of the dominant's, as <see cref="Outright"/> reads it.
        /// </summary>
        private bool OwnsChord(KeySignature key, int index)
        {
            var chord = _chordPitchClasses[index];
            if ((chord & ~Owned(key)) != 0)
                return false;
            if (key.IsMajor)
                return true;

            var leadingTone = 1 << PitchMath.Fold(key.Root + 11);
            return (chord & leadingTone) == 0 || (chord & ~DominantChords(key)) == 0 || (chord & TonicTriad(key)) == TonicTriad(key);
        }

        /// <summary>
        /// The pitch classes sonority <paramref name="index"/> sounds that <paramref name="key"/>
        /// does not own outright: the notes outside its scale, and for a minor key its raised
        /// seventh wherever the chord it sounds in is not one of the dominant's — V, V7, the
        /// leading-tone seventh — nor holds the tonic triad, nor is the note alone. A minor key
        /// raises its seventh to make its dominant major; it does not own the note as a free
        /// pitch class of every chord. C E♭ G is no chord of E minor, and with the E flat taken
        /// for D sharp, E minor owned G Cm D7 G outright while G needed the borrowed chord, and
        /// was named for it. But the leading tone struck over the tonic chord — B natural on the
        /// downbeat over C minor, resolving to C — is the tonic chord with an appoggiatura, not a
        /// chord the key lacks: read as one, four bars of C minor with that cadence in them were
        /// no key at all.
        /// </summary>
        private ushort Outright(int index, KeySignature key)
        {
            var pitchClasses = _sonorities[index].PitchClasses;
            var lacking = (ushort)(pitchClasses & ~Owned(key));
            if (key.IsMajor)
                return lacking;

            var leadingTone = (ushort)(1 << PitchMath.Fold(key.Root + 11));
            var chord = _chordPitchClasses[index];
            if ((chord & leadingTone) != 0 && (chord & ~DominantChords(key)) != 0 && (chord & TonicTriad(key)) != TonicTriad(key))
                lacking |= (ushort)(pitchClasses & leadingTone);
            return lacking;
        }

        /// <summary>
        /// The pitch classes of a minor key's dominant chords — the dominant triad and seventh
        /// and the diminished seventh on the raised leading tone: degrees 5, 7 (raised), 2, 4 and
        /// flat 6.
        /// </summary>
        private static ushort DominantChords(KeySignature key) => DominantChordsByRoot[key.Root];

        private static readonly ushort[] DominantChordsByRoot = BuildDominantChords();

        private static ushort[] BuildDominantChords()
        {
            var chords = new ushort[12];
            for (var root = 0; root < 12; root++)
            {
                chords[root] = (ushort)((1 << PitchMath.Fold(root + 7))
                    | (1 << PitchMath.Fold(root + 11))
                    | (1 << PitchMath.Fold(root + 2))
                    | (1 << PitchMath.Fold(root + 5))
                    | (1 << PitchMath.Fold(root + 8)));
            }

            return chords;
        }

        /// <summary>
        /// Whether sonority <paramref name="index"/>, a note <paramref name="key"/> lacks, is a
        /// non-harmonic tone of the key — a passing tone, a neighbour tone or an appoggiatura —
        /// and so weighs nothing toward what the key lacks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A chromatic passing tone fills the whole step between two notes of the scale: it lies
        /// a semitone from the note either side of it and keeps the line's direction. A
        /// chromatic neighbour tone leaves a note of the scale by a semitone and returns to it.
        /// Both are notes on their way (<see cref="IsOnItsWay"/>) — short, approached and left by
        /// step by notes no shorter than themselves — and neither falls on the downbeat of its
        /// bar, where a note is a note of the line. A run of notes on their way — a scale, a
        /// chromatic run — is passing motion when it moves one way, from a structural note the
        /// key owns to another, within a whole note; the chromatic notes in it are the passing
        /// tones: B flat between B and A, F between E and F sharp. A chromatic scale has no
        /// structural note to set out from or land on within a bar — every note in it is on its
        /// way — so nothing in it passes, and the scale is what it always was to the judge: no
        /// key's. An appoggiatura is a structural note approached by leap from a note the key
        /// owns and resolved by a semitone into a tone of the chord sounding under it, sounding
        /// once in its bar: a leaning note needs a chord to lean on, so in a melody alone a
        /// chromatic note approached by leap is a note of the line, and a note that sounds again
        /// in its bar is a tone of the bar's harmony.
        /// </para>
        /// <para>
        /// Steps are read in pitch where the road has the note — a seventh is a leap — and in
        /// pitch class where it has only the chord: the detector folds a melody's downbeat note
        /// into the chord it strikes with, and the eighth after it is approached from that
        /// chord's tones.
        /// </para>
        /// <para>
        /// Weighed by duration alone, two chromatic passing eighths in a bar were a quarter of it
        /// and a melody with two in every bar of its new key named no key at all; a chromatic
        /// quarter-note neighbour under a D7 was a quarter of its bar and the trajectory road,
        /// which reads the melody, placed G four bars late while the detector, which never saw
        /// the eighths, placed it at the bar; and a passing F natural in E F F sharp G was the
        /// last note G major did not own, so G began three eighths into its bar. A passing tone
        /// is not a foreign note. The rules above are each the price of a looser reading,
        /// measured: read without direction, G F sharp A flat was a passing tone; read with steps
        /// of a tone, the F of a C-major scale's E F G was a passing tone in G major, and the
        /// scale's last bar became G's pivot bar; read on the downbeat, the F of a C-major
        /// melody's E F E sent it to G; read without the run's anchors, a chromatic scale in
        /// eighths was owned by every key and named one. Over two hundred random chromatic
        /// melodies, which have no key, the detector's modulations rose from 16 to 40 and the
        /// trajectory's from 3 to 15 with the first of those readings, and stand at 21 and 6 with
        /// the rules.
        /// </para>
        /// </remarks>
        private bool IsNonHarmonic(int index, KeySignature key) => (NonHarmonicKeys(index) >> KeyIndex(key) & 1) != 0;

        /// <summary>The keys, one bit each by their index, that single note <paramref name="index"/> is a non-harmonic tone of; 0 for a chord or a note longer than a quarter.</summary>
        private uint NonHarmonicKeys(int index)
        {
            if (!IsLineNote(index) || !IsShort(index))
                return 0;

            _nonHarmonic ??= new uint[_sonorities.Length];
            var keys = _nonHarmonic[index];
            if ((keys & Asked) == 0)
            {
                keys = Asked;
                for (var k = 0; k < 24; k++)
                {
                    if ((OwnedByKey[k] & _sonorities[index].PitchClasses) == 0 && IsNonHarmonic(index, OwnedByKey[k]))
                        keys |= 1u << k;
                }

                _nonHarmonic[index] = keys;
            }

            return keys & ~Asked;
        }

        private bool IsNonHarmonic(int index, ushort owned)
        {
            var before = Before(index);
            var after = After(index);
            if (!before.Found || !after.Found)
                return false;

            if (IsOnItsWay(index))
            {
                // A passing or neighbour tone is unaccented: on the downbeat of a bar, a note is
                // a note of the line.
                if (_sonorities[index].Onset.Denominator == 1)
                    return false;

                // Between two notes the key owns: back to the note it left — a neighbour — or
                // on in the same direction. G F sharp A flat is neither.
                var steps = before.StepsFrom(_sonorities[index], owned, forward: false, semitone: true);
                var resolutions = after.StepsFrom(_sonorities[index], owned, forward: true, semitone: true);
                if (!steps.Any || !resolutions.Any)
                    return false;
                if (steps.Returns(resolutions))
                    return true;

                // A passing tone is on a scale: the run it is on moves one way, from a
                // structural note the key owns to another, within a whole note. B B flat D flat
                // C B flat is a figure, not a scale, and its C passes nothing.
                var run = RunOf(index);
                var up = run.Rises && steps.Up && resolutions.Up;
                var down = run.Falls && steps.Down && resolutions.Down;
                if ((!up && !down) || !run.WithinAWholeNote)
                    return false;

                var setsOut = Before(run.First).StepsFrom(_sonorities[run.First], owned, forward: false);
                var lands = After(run.Last).StepsFrom(_sonorities[run.Last], owned, forward: true);
                return (up && setsOut.Up && lands.Up) || (down && setsOut.Down && lands.Down);
            }

            // An appoggiatura: approached by leap from a note the key owns, resolved by step into
            // a tone of the chord sounding under it, and sounding once in its bar.
            if (before.Pitch >= 0 ? !Contains(owned, before.Pitch) || IsStep(before.Pitch, _sonorities[index].Pitch) : (before.PitchClasses & owned) == 0)
                return false;

            var under = SoundingUnder(index);
            var resolution = after.StepsFrom(_sonorities[index], owned, forward: true, semitone: true);
            return BitOperations.PopCount(under) >= 2
                && (under & _sonorities[index].PitchClasses) == 0
                && (resolution.PitchClasses & under) != 0
                && !SoundsAgainInBar(index);
        }

        /// <summary>
        /// Whether the single note <paramref name="index"/> is a note on its way — short, and
        /// approached and left by step by notes no shorter than itself — rather than a
        /// structural note: one that is longer than a quarter, sounds in a chord, or is
        /// approached or left by a leap, a repetition, a rest or a note shorter than itself.
        /// Decided once per note; it does not depend on the key.
        /// </summary>
        private bool IsOnItsWay(int index)
        {
            if (!IsLineNote(index) || !IsShort(index))
                return false;

            _onItsWay ??= new byte[_sonorities.Length];
            if (_onItsWay[index] == 0)
            {
                var note = _sonorities[index];
                var before = Before(index);
                var after = After(index);
                var onItsWay = before.Found && after.Found
                    && before.NoShorter && after.NoShorter
                    && before.StepsFrom(note, 0x0FFF, forward: false).Any
                    && after.StepsFrom(note, 0x0FFF, forward: true).Any;
                _onItsWay[index] = (byte)(onItsWay ? 1 : 2);
            }

            return _onItsWay[index] == 1;
        }

        /// <summary>Whether sonority <paramref name="index"/> is a single note of the line: it has a pitch and nothing else begins with it.</summary>
        private bool IsLineNote(int index) => _lineNote[index];

        /// <summary>
        /// What sounds next to a note of the line: the single note of the line ending where it
        /// begins or beginning where it ends — in several voices, the nearest in pitch — or,
        /// when no note does, the harmony sounding into it or the chord struck where it ends.
        /// </summary>
        /// <param name="Index">The neighbouring note's sonority, or -1 for a harmony.</param>
        /// <param name="Pitch">The neighbouring note's pitch, or -1 for a harmony.</param>
        /// <param name="PitchClasses">The pitch classes sounding there.</param>
        /// <param name="NoShorter">Whether the neighbour lasts at least as long as the note; a harmony always does.</param>
        private readonly record struct Neighbour(int Index, int Pitch, ushort PitchClasses, bool NoShorter)
        {
            public static readonly Neighbour None = new(-1, -1, 0, false);

            // default(Neighbour) — Index 0 and no pitch classes — marks a neighbour not yet
            // found; None, with Index -1, marks one looked for and absent.

            public bool Found => PitchClasses != 0;

            /// <summary>
            /// The steps — a semitone or a tone, up or down; a semitone only when
            /// <paramref name="semitone"/> — from <paramref name="note"/> to this neighbour's
            /// notes that <paramref name="owned"/> holds: in pitch for a note, in pitch class for
            /// a harmony. <paramref name="forward"/> reads the interval from the note to the
            /// neighbour; otherwise from the neighbour to the note, so that a passing tone's two
            /// steps point the same way.
            /// </summary>
            public Steps StepsFrom(Sonority note, ushort owned, bool forward, bool semitone = false)
            {
                var reach = semitone ? 1 : 2;
                if (Pitch >= 0)
                {
                    if (!Contains(owned, Pitch) || !IsStep(Pitch, note.Pitch) || Math.Abs(Pitch - note.Pitch) > reach)
                        return default;
                    var interval = forward ? Pitch - note.Pitch : note.Pitch - Pitch;
                    return new Steps(Steps.Bit(interval), (ushort)(1 << PitchMath.Fold(Pitch)));
                }

                // A harmony holding the note's own pitch class is the note repeated — the
                // melody's D running into the D7 it strikes with — not a step to another of
                // its tones.
                var from = PitchMath.Fold(note.Pitch);
                if ((PitchClasses & (1 << from)) != 0)
                    return default;

                byte intervals = 0;
                ushort pitchClasses = 0;
                for (var interval = -reach; interval <= reach; interval++)
                {
                    if (interval == 0)
                        continue;
                    var pc = PitchMath.Fold(from + interval);
                    if ((PitchClasses & owned & (1 << pc)) != 0)
                    {
                        intervals |= Steps.Bit(forward ? interval : -interval);
                        pitchClasses |= (ushort)(1 << pc);
                    }
                }

                return new Steps(intervals, pitchClasses);
            }
        }

        /// <summary>
        /// The steps from a note to a neighbour, as a set of signed intervals (-2, -1, 1, 2)
        /// and the pitch classes stepped to.
        /// </summary>
        private readonly record struct Steps(byte Intervals, ushort PitchClasses)
        {
            public bool Any => Intervals != 0;

            /// <summary>Whether a step here rises.</summary>
            public bool Up => (Intervals & 0b1100) != 0;

            /// <summary>Whether a step here falls.</summary>
            public bool Down => (Intervals & 0b0011) != 0;

            public static byte Bit(int interval) => interval switch { -2 => 1, -1 => 2, 1 => 4, 2 => 8, _ => 0 };

            /// <summary>Whether the note returns to the pitch class it left: a neighbour tone.</summary>
            public bool Returns(Steps resolutions) => (PitchClasses & resolutions.PitchClasses) != 0;
        }

        /// <summary>
        /// A run of notes on their way: its first and last note, whether every step in it rises
        /// or every step falls, and whether it fits within a whole note. A note not on its way
        /// is a run of one.
        /// </summary>
        private readonly record struct Run(int First, int Last, bool Rises, bool Falls, bool WithinAWholeNote);

        /// <summary>The run note <paramref name="index"/> is on, found once.</summary>
        private Run RunOf(int index)
        {
            _run ??= new Run[_sonorities.Length];
            if (_run[index].First == 0 && _run[index].Last == 0 && !_run[index].Rises)
            {
                var rises = true;
                var falls = true;
                var first = index;
                while (Before(first) is { Pitch: >= 0 } previous && IsOnItsWay(previous.Index))
                {
                    var rising = _sonorities[first].Pitch > previous.Pitch;
                    rises &= rising;
                    falls &= !rising;
                    first = previous.Index;
                }

                var last = index;
                while (After(last) is { Pitch: >= 0 } following && IsOnItsWay(following.Index))
                {
                    var rising = following.Pitch > _sonorities[last].Pitch;
                    rises &= rising;
                    falls &= !rising;
                    last = following.Index;
                }

                _run[index] = new Run(first, last, rises, falls, _sonorities[last].End - _sonorities[first].Onset <= Rational.Whole);
            }

            return _run[index];
        }

        private Neighbour Before(int index)
        {
            _before ??= new Neighbour[_sonorities.Length];
            if (_before[index].Index == 0 && _before[index].PitchClasses == 0)
            {
                var note = _sonorities[index];
                var i = LineNoteEndingAt(note.Onset, note.Pitch);
                if (i >= 0)
                {
                    _before[index] = new Neighbour(i, _sonorities[i].Pitch, _sonorities[i].PitchClasses, _sonorities[i].End - _sonorities[i].Onset >= note.End - note.Onset);
                }
                else
                {
                    ushort sounding = 0;
                    foreach (var k in Reaching(note.Onset))
                    {
                        if (_sonorities[k].Onset >= note.Onset)
                            break;
                        if (_sonorities[k].End > note.Onset)
                            sounding |= _sonorities[k].PitchClasses;
                    }

                    _before[index] = sounding == 0 ? Neighbour.None : new Neighbour(-1, -1, sounding, true);
                }
            }

            return _before[index];
        }

        private Neighbour After(int index)
        {
            _after ??= new Neighbour[_sonorities.Length];
            if (_after[index].Index == 0 && _after[index].PitchClasses == 0)
            {
                var note = _sonorities[index];
                var i = LineNoteBeginningAt(note.End, note.Pitch);
                if (i >= 0)
                {
                    _after[index] = new Neighbour(i, _sonorities[i].Pitch, _sonorities[i].PitchClasses, _sonorities[i].End - _sonorities[i].Onset >= note.End - note.Onset);
                }
                else
                {
                    var k = FirstIndexAt(note.End);
                    _after[index] = k < _sonorities.Length && _sonorities[k].Onset == note.End
                        ? new Neighbour(-1, -1, _chordPitchClasses[k], true)
                        : Neighbour.None;
                }
            }

            return _after[index];
        }

        /// <summary>Whether <paramref name="pitchClasses"/> holds the pitch class of <paramref name="pitch"/>.</summary>
        private static bool Contains(ushort pitchClasses, int pitch) => (pitchClasses & (1 << PitchMath.Fold(pitch))) != 0;

        /// <summary>Whether two pitches are a step apart: a semitone or a tone.</summary>
        private static bool IsStep(int a, int b) => a != b && Math.Abs(a - b) <= 2;

        /// <summary>
        /// The single note of the line beginning at <paramref name="position"/> nearest in pitch
        /// to <paramref name="pitch"/>, or -1.
        /// </summary>
        private int LineNoteBeginningAt(Rational position, int pitch)
        {
            var nearest = -1;
            for (var i = FirstIndexAt(position); i < _sonorities.Length && _sonorities[i].Onset == position; i++)
            {
                if (_sonorities[i].Pitch >= 0 && (nearest < 0 || Math.Abs(_sonorities[i].Pitch - pitch) < Math.Abs(_sonorities[nearest].Pitch - pitch)))
                    nearest = i;
            }

            return nearest;
        }

        /// <summary>
        /// The single note of the line ending at <paramref name="position"/> nearest in pitch to
        /// <paramref name="pitch"/>, or -1.
        /// </summary>
        private int LineNoteEndingAt(Rational position, int pitch)
        {
            var nearest = -1;
            foreach (var i in Reaching(position))
            {
                if (_sonorities[i].Onset >= position)
                    break;
                if (_sonorities[i].End == position && _sonorities[i].Pitch >= 0
                    && (nearest < 0 || Math.Abs(_sonorities[i].Pitch - pitch) < Math.Abs(_sonorities[nearest].Pitch - pitch)))
                {
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The pitch classes of the chords sounding under sonority <paramref name="index"/>: the
        /// sonorities that began before it, in a group of two or more, and are still sounding
        /// when it begins.
        /// </summary>
        private ushort SoundingUnder(int index)
        {
            var onset = _sonorities[index].Onset;
            ushort under = 0;
            foreach (var k in Reaching(onset))
            {
                if (_sonorities[k].Onset >= onset)
                    break;
                if (_sonorities[k].End > onset && BitOperations.PopCount(_chordPitchClasses[k]) > 1)
                    under |= _sonorities[k].PitchClasses;
            }

            return under;
        }

        /// <summary>Whether sonority <paramref name="index"/> lasts no longer than a quarter note.</summary>
        private bool IsShort(int index) => _short[index];

        /// <summary>Whether another sonority beginning in the whole note of sonority <paramref name="index"/> sounds its pitch class.</summary>
        private bool SoundsAgainInBar(int index)
        {
            var bar = new Rational((long)Math.Floor(_sonorities[index].Onset.ToDouble()), 1);
            var to = bar + Rational.Whole;
            var pitchClasses = _sonorities[index].PitchClasses;
            for (var k = FirstIndexAt(bar); k < _sonorities.Length && _sonorities[k].Onset < to; k++)
            {
                if (k != index && (_sonorities[k].PitchClasses & pitchClasses) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a sonority in [<paramref name="from"/>, <paramref name="to"/>) sounds a pitch
        /// class in <paramref name="pitchClasses"/> as a note of <paramref name="key"/>'s own — in
        /// a sonority the key owns, and not as the Picardy third of <paramref name="current"/>,
        /// which is that key's cadence and no other key's note.
        /// </summary>
        public bool Sounds(ushort pitchClasses, Rational from, Rational to, KeySignature key, KeySignature current)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & ~PicardyThird(i, current) & pitchClasses) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The key the opening phrase is in: the key of the chord the piece opens on, when that
        /// key owns as much of the phrase as any key does (<paramref name="byChord"/>); else the
        /// key the phrase sounds like on the profile. When neither decides, the opening is
        /// extended by a phrase at a time, and the key the whole sounds like is the last resort.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A piece opens on its tonic, as a rule, and a musician takes the first chord for the
        /// tonic until the phrase says otherwise. The profile alone does not: it is weighted to
        /// the root and the fifth, so C G C G reads as G major by a margin of 0.24, and C Am D7 G
        /// | C A7 Dm G7 ties C and G over eight bars and sixteen, and the whole-piece fallback
        /// opened the verse in G — from which the trajectory reported a modulation to C at bar 7
        /// of a piece that never left it. The first chord decides among the keys that own the
        /// phrase, or leave the least of it foreign, an applied dominant counted as the key's:
        /// C G C G opens in C; C D7 G C, which G owns entire and C owns through its V7/V, opens
        /// in C; and the verse, whose first phrase G owns entire but whose first two phrases C
        /// leaves less of foreign than G does, opens in C once the phrase is extended.
        /// </para>
        /// <para>
        /// Ownership without the opening chord would name the wrong key: a change is judged
        /// against the key the music is in, and a key that has to call a recurring chord tone
        /// foreign loses to one that owns every note; the opening has nothing to be judged
        /// against. Ownership and the profile disagree on the blues: its tonic is a dominant
        /// seventh chord, which no major key owns — F major owns C7 — and by ownership alone a
        /// twelve-bar blues in C began in F and modulated home at its V7. F does not open on a
        /// C7, so the blues falls to the profile, which hears it in C. A note opens no key: a
        /// melody in C may begin on its sixth degree, and taken for the tonic of A minor it did.
        /// </para>
        /// </remarks>
        public KeySignature OpeningKey(Rational phrase, out bool byChord)
        {
            // An opening that cannot decide is extended as a change's hold is, a phrase at a
            // time: a piece that modulates sounds, as a whole, like a key it was never in at
            // the start, and read from that key the opening came back as a modulation at its
            // first note.
            byChord = false;
            var to = phrase < End ? phrase : End;
            var opening = Profile(Rational.Zero, to);
            while (true)
            {
                if (opening.Result.IsDecidable)
                {
                    if (OpensIn(opening.Result, to) is { } key)
                    {
                        byChord = true;
                        return key;
                    }

                    if (opening.Result.Confidence >= MinDecisive)
                        return opening.Result.Key;
                }

                if (to >= End)
                    break;

                to = to + phrase < End ? to + phrase : End;
                opening = Profile(Rational.Zero, to);
            }

            var whole = Profile(Rational.Zero, End);
            return whole.Result.IsDecidable ? whole.Result.Key : opening.Result.Key;
        }

        /// <summary>
        /// The key whose tonic chord the piece opens on — the profile's favourite among them
        /// when several qualify — provided it leaves as little of [0, <paramref name="to"/>)
        /// foreign as any key does, an applied dominant seventh counted as the key's. Failing
        /// that, when the piece opens on a triad and exactly one major key owns the phrase
        /// outright, that key: G C F G opens in C, on its dominant. <see langword="null"/> when
        /// the piece opens on a single note, or when nothing above decides.
        /// </summary>
        /// <remarks>
        /// The owner fallback is a major key or nothing: pitch classes cannot tell A flat from G
        /// sharp, so A minor, which owns its raised leading tone, owns C F Fm C outright — the
        /// borrowed iv's A flat taken for G sharp — and by ownership a phrase in C with a
        /// borrowed chord opened in A minor. And it is not for a dominant seventh: the blues
        /// opens on one, and F major owns C7. The profile decides those. No key is in force at
        /// the opening to apply a plain triad to or to borrow against, so here only a dominant
        /// seventh is a key's beyond its scale (<see cref="ChromaticChordOf"/>): G | C F G C, a
        /// pickup on the dominant, opens in C, not in a G major that owns the F chord as its
        /// flat seventh.
        /// </remarks>
        private KeySignature? OpensIn(KeyDetectionResult reading, Rational to)
        {
            var chord = _chordPitchClasses[0];
            if (BitOperations.PopCount(chord) < 2)
                return null;

            const float tolerance = 1e-4f;
            Span<float> foreign = stackalloc float[24];
            var least = float.MaxValue;
            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                foreign[i] = Foreign(Rational.Zero, to, reading.AllCorrelations[i].Key, current: null, Exempt.Chords);
                if (foreign[i] < least)
                    least = foreign[i];
            }

            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                var candidate = reading.AllCorrelations[i].Key;
                if (IsTonicChord(candidate, chord) && foreign[i] <= least + tolerance)
                    return candidate;
            }

            if (BitOperations.PopCount(chord) > 3 || least > tolerance)
                return null;

            KeySignature? owner = null;
            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                var candidate = reading.AllCorrelations[i].Key;
                if (!candidate.IsMajor || foreign[i] > tolerance)
                    continue;
                if (owner is not null)
                    return null;
                owner = candidate;
            }

            return owner;
        }

        /// <summary>
        /// Which of the keys in <paramref name="reading"/> own [<paramref name="from"/>,
        /// <paramref name="to"/>) as well as any key does: the keys that leave the least of it
        /// foreign — their applied and borrowed chords, Picardy third and non-harmonic tones
        /// counted as their own — and, among those, the keys that need the fewest chromatic
        /// chords to do it. A key that owns a phrase outright is a better owner than one that
        /// owns it through chromatic chords: C F G C is I IV V I in C, not IV ♭VII I IV in G, and
        /// C G C F is I V I IV in C, not V/V V I IV in F. Every key is weighed in one pass over
        /// the span: weighed one key at a time, twice, a piece of four thousand chords over a
        /// two-thousand-bar pedal took twenty times as long to judge.
        /// </summary>
        private void Owners(KeyDetectionResult reading, Rational from, Rational to, KeySignature? current, Span<bool> owns)
        {
            // Ownership is weighed to a small tolerance: the weights are note durations, and two
            // keys that own the same notes must tie exactly however the durations were summed.
            const float tolerance = 1e-4f;

            var candidates = reading.AllCorrelations;
            var count = Math.Min(candidates.Length, 24);
            Span<float> foreign = stackalloc float[24];
            Span<float> chromatic = stackalloc float[24];
            Span<Exempt> exempt = stackalloc Exempt[24];
            var opening = FirstIndexAt(from);
            var closing = FirstIndexAt(to) - 1;
            var spans = opening < _sonorities.Length && _sonorities[opening].Onset < to;
            Span<int> slotOf = stackalloc int[24];
            slotOf.Fill(-1);
            for (var k = 0; k < count; k++)
            {
                var framed = spans && (IsTonicChordOf(candidates[k].Key, opening) || IsTonicChordOf(candidates[k].Key, closing));
                exempt[k] = framed ? Exempt.Chords : Exempt.Sevenths;
                slotOf[KeyIndex(candidates[k].Key)] = k;
            }

            foreach (var i in Reaching(from))
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var start = s.Onset > from ? s.Onset : from;
                var stop = s.End < to ? s.End : to;
                if (stop <= start)
                    continue;

                var weight = (float)(stop - start).ToDouble();
                if (_lineNote[i])
                {
                    // A single note is foreign to the keys that do not own its pitch class, less
                    // the keys it is a non-harmonic tone of — one bit each, in one word.
                    var keys = KeysLacking[BitOperations.TrailingZeroCount(s.PitchClasses)] & ~NonHarmonicKeys(i);
                    while (keys != 0)
                    {
                        var k = slotOf[BitOperations.TrailingZeroCount(keys)];
                        keys &= keys - 1;
                        if (k >= 0)
                        {
                            foreign[k] += weight;
                            chromatic[k] += weight;
                        }
                    }

                    continue;
                }

                // A chord the key in force owns is its own to every other key; only a chord
                // it does not is asked whether it is another key's chromatic chord.
                var chromaticToSome = _maybeChromatic[i] && !(current is { } inForce && OwnsChord(inForce, i));
                for (var k = 0; k < count; k++)
                {
                    var key = candidates[k].Key;
                    var outright = Outright(i, key);
                    if (outright == 0)
                        continue;

                    chromatic[k] += BitOperations.PopCount(outright) * weight;
                    var lacking = chromaticToSome ? (ushort)(outright & ~ChromaticChordOf(i, key, current, exempt[k])) : outright;
                    if (lacking != 0)
                        foreign[k] += BitOperations.PopCount(lacking) * weight;
                }
            }

            var least = float.MaxValue;
            for (var k = 0; k < count; k++)
            {
                if (foreign[k] < least)
                    least = foreign[k];
            }

            var leastChromatic = float.MaxValue;
            for (var k = 0; k < count; k++)
            {
                if (foreign[k] <= least + tolerance && chromatic[k] < leastChromatic)
                    leastChromatic = chromatic[k];
            }

            for (var k = 0; k < count; k++)
                owns[k] = foreign[k] <= least + tolerance && chromatic[k] <= leastChromatic + tolerance;
        }

        /// <summary>
        /// Names the key of the phrase [<paramref name="from"/>, <paramref name="to"/>): of the
        /// keys that own it as well as any key does (<see cref="Owners"/>), the one the profile
        /// correlates best with. The margin is the profile's, taken over those keys only, so
        /// that a key no other key rivals in ownership is named outright.
        /// </summary>
        /// <remarks>
        /// <see cref="KeyDetectionResult.Key"/> is the profile's choice over all twenty-four keys,
        /// and the Krumhansl profile hears a dominant seventh as the key on its root: eight chords
        /// alternating D7 and G read as D major by a margin of 0.15, though D major does not own
        /// the C natural in every D7. A key that has to call a recurring chord tone foreign is
        /// not the phrase's key when another key owns every note. Ownership is measured as the
        /// key hears its material, not by scale membership: measured by scale membership, a minor
        /// key owned its relative major's scale and one note more — its raised seventh — so any
        /// phrase in G with an E flat neighbour in it, a quarter long, was named E minor outright,
        /// the passing note taken for the leading tone.
        /// </remarks>
        public Named Name(Reading reading, Rational from, Rational to, KeySignature? current)
        {
            Span<bool> owns = stackalloc bool[24];
            Owners(reading.Result, from, to, current, owns);

            // AllCorrelations is sorted strongest first, so the first two owners met are the best
            // and the runner-up among them.
            KeySignature? best = null;
            var bestCorrelation = 0f;
            var runnerUp = float.NaN;
            for (var i = 0; i < reading.Result.AllCorrelations.Length && i < 24; i++)
            {
                if (!owns[i])
                    continue;

                if (best is null)
                {
                    best = reading.Result.AllCorrelations[i].Key;
                    bestCorrelation = reading.Result.AllCorrelations[i].Correlation;
                }
                else
                {
                    runnerUp = reading.Result.AllCorrelations[i].Correlation;
                    break;
                }
            }

            if (best is not { } key)
                return new Named(reading.Result.Key, reading.Result.Confidence);

            if (float.IsNaN(runnerUp))
                return new Named(key, 1f);

            var margin = bestCorrelation > 0f
                ? Math.Clamp((bestCorrelation - runnerUp) / (bestCorrelation + 0.001f), 0f, 1f)
                : 0f;
            return new Named(key, margin);
        }

        /// <summary>
        /// The key profile of [<paramref name="from"/>, <paramref name="to"/>), each sonority
        /// weighed by how long it sounds inside the span — the reading
        /// <see cref="KeyProfiler.DetectFromBuffer"/> gives — with the pitch classes that sound
        /// there and the distribution itself.
        /// </summary>
        public Reading Profile(Rational from, Rational to)
        {
            var distribution = new float[12];
            ushort mask = 0;

            foreach (var i in Reaching(from))
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var start = s.Onset > from ? s.Onset : from;
                var stop = s.End < to ? s.End : to;
                if (stop <= start)
                    continue;

                var weight = (float)(stop - start).ToDouble();
                for (var pc = 0; pc < 12; pc++)
                {
                    if ((s.PitchClasses & (1 << pc)) != 0)
                        distribution[pc] += weight;
                }

                mask |= s.PitchClasses;
            }

            return new Reading(KeyProfiler.Detect(distribution), mask, distribution);
        }

        /// <summary>
        /// Whether <paramref name="key"/> holds from <paramref name="from"/> through
        /// <paramref name="holdTo"/>: the stretch — extended by a phrase at a time while it
        /// cannot decide, to the end at most — is decidable, names the key over any rival by
        /// <see cref="MinTold"/>, separates it from <paramref name="current"/>, and is owned by
        /// it. Returns the reading and the name the stretch was given, for the caller's use.
        /// </summary>
        public (bool Holds, Reading Hold, Named HeldAs) Holds(Rational from, ref Rational holdTo, KeySignature key, KeySignature current, Rational phrase)
        {
            if (holdTo > End)
                holdTo = End;

            var hold = Profile(from, holdTo);
            var heldAs = Name(hold, from, holdTo, current);
            while ((!hold.Result.IsDecidable || heldAs.Margin < MinDecisive) && holdTo < End)
            {
                holdTo = holdTo + phrase < End ? holdTo + phrase : End;
                hold = Profile(from, holdTo);
                heldAs = Name(hold, from, holdTo, current);
            }

            var holds = hold.Result.IsDecidable
                && heldAs.Margin >= MinTold
                && heldAs.Key == key
                && Separation(hold.Result, key, current) >= MinSeparation
                && Owns(from, holdTo, key, current);
            return (holds, hold, heldAs);
        }

        /// <summary>
        /// Whether <paramref name="key"/>'s tonic chord frames [<paramref name="from"/>,
        /// <paramref name="to"/>): the chord at its first onset or the chord at its last is the
        /// tonic triad, with or without a melody note above it — or, for a minor key at the
        /// close, the Picardy third.
        /// </summary>
        public bool Frames(KeySignature key, Rational from, Rational to)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length || _sonorities[i].Onset >= to)
                return false;

            var last = FirstIndexAt(to) - 1;
            return IsTonicChordOf(key, i) || IsTonicChordOf(key, last);
        }

        /// <summary>Whether the chord sonority <paramref name="index"/> belongs to is <paramref name="key"/>'s tonic chord, with or without a note above it, or its Picardy third.</summary>
        private bool IsTonicChordOf(KeySignature key, int index)
        {
            var chord = _chordPitchClasses[index];
            var tonic = TonicTriad(key);
            return IsTonicChord(key, chord)
                || (chord & tonic) == tonic
                || (chord & ~tonic & ~PicardyThird(index, key)) == 0 && (chord & (1 << key.Root)) != 0 && PicardyThird(index, key) != 0;
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns [<paramref name="from"/>, <paramref name="to"/>):
        /// in every whole-note bar of it, clipped to the span, the notes the key lacks amount to
        /// less than <see cref="StrayNote"/>, the key's chromatic chords counted as its own as
        /// far as <paramref name="exempt"/> says — and its applied triads and borrowed chords
        /// only inside a phrase the key frames (<see cref="Frames"/>).
        /// </summary>
        /// <remarks>
        /// A key's chromatic chords are heard inside a phrase of its own: C F G C | G E Am D7 is
        /// in G, whose tonic opens the phrase with the V/ii in it. Without the frame, a phrase
        /// every key owns a half of belonged to a third: D G A D | E A B E, two bars of D and two
        /// of E, was A major's — IV ♭VII V IV, V I V/V V — and A major was reported touched for
        /// four bars in a passage that never sounds its key.
        /// </remarks>
        public bool Owns(Rational from, Rational to, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            if (exempt == Exempt.Chords && !Frames(key, from, to))
                exempt = Exempt.Sevenths;

            var firstBar = (long)Math.Floor(from.ToDouble());
            var barCount = (int)Math.Max(0, (long)Math.Ceiling(to.ToDouble()) - firstBar);
            var foreign = new float[barCount];

            foreach (var i in Reaching(from))
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var lacking = Lacking(i, key, current, exempt);
                if (lacking == 0)
                    continue;

                var start = s.Onset > from ? s.Onset : from;
                var stop = s.End < to ? s.End : to;
                if (stop <= start)
                    continue;

                var count = BitOperations.PopCount(lacking);
                var bar = (long)Math.Floor(start.ToDouble());
                for (var at = start; at < stop; bar++)
                {
                    var barEnd = new Rational(bar + 1, 1);
                    var until = barEnd < stop ? barEnd : stop;
                    var index = (int)(bar - firstBar);
                    if (index >= 0 && index < barCount)
                    {
                        foreign[index] += count * (float)(until - at).ToDouble();
                        if (foreign[index] >= StrayNote)
                            return false;
                    }

                    at = until;
                }
            }

            return true;
        }

        /// <summary>
        /// The weight of the material in [<paramref name="from"/>, <paramref name="to"/>) that
        /// <paramref name="key"/> lacks (<see cref="Lacking"/>) — its chromatic chords counted
        /// as its own as far as <paramref name="exempt"/> says, its applied triads and borrowed
        /// chords only inside a phrase it frames (<see cref="Frames"/>); with nothing exempt the
        /// weight measures how much chromatic harmony the key needs to own the span. A
        /// non-harmonic tone weighs nothing either way.
        /// </summary>
        private float Foreign(Rational from, Rational to, KeySignature key, KeySignature? current, Exempt exempt)
        {
            if (exempt == Exempt.Chords && !Frames(key, from, to))
                exempt = Exempt.Sevenths;

            var sum = 0f;
            foreach (var i in Reaching(from))
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var lacking = Lacking(i, key, current, exempt);
                if (lacking == 0)
                    continue;

                var start = s.Onset > from ? s.Onset : from;
                var stop = s.End < to ? s.End : to;
                if (stop > start)
                    sum += BitOperations.PopCount(lacking) * (float)(stop - start).ToDouble();
            }

            return sum;
        }

        /// <summary>
        /// The onset of the first sonority in [<paramref name="from"/>, <paramref name="to"/>) —
        /// strictly after <paramref name="from"/> when <paramref name="afterFrom"/> — that sounds
        /// a pitch class in <paramref name="pitchClasses"/>.
        /// </summary>
        public bool FirstOnsetSounding(ushort pitchClasses, Rational from, bool afterFrom, Rational to, out Rational onset)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (afterFrom && s.Onset == from)
                    continue;

                if ((s.PitchClasses & pitchClasses) != 0)
                {
                    onset = s.Onset;
                    return true;
                }
            }

            onset = default;
            return false;
        }

        /// <summary>
        /// The onset of the first sonority in [<paramref name="from"/>, <paramref name="to"/>)
        /// that sounds a pitch class in <paramref name="pitchClasses"/> in a chord of
        /// <paramref name="key"/>'s own: a chord that is not one of the key's chromatic chords,
        /// or one that holds the key's tonic triad, however coloured — a blues opens on I7. A key
        /// begins on a chord of its own: an applied or a borrowed chord is heard as the key's
        /// only once the key is in force, so it cannot be where the key begins. In a
        /// descending-fifths sequence of major triads every four chords are V/V V I IV of the
        /// third one's key, and B♭ E♭ A♭ D♭ began an A flat major area on the B flat chord; the
        /// area that begins on the E flat runs into the G flat and is nothing. A chord with a
        /// stray note in it is not a chromatic chord: the detector's eighth-note grid rounds the
        /// last thirty-seconds of a bar of C onto the D flat downbeat, and D flat begins on that
        /// downbeat still — even where the strays make a D flat seventh of it.
        /// </summary>
        public bool FirstOnsetOfTheKey(ushort pitchClasses, Rational from, Rational to, KeySignature key, KeySignature current, out Rational onset)
        {
            var tonic = TonicTriad(key);
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                if ((s.PitchClasses & pitchClasses) != 0
                    && (ChromaticChordOf(i, key, current) == 0 || (_chordPitchClasses[i] & tonic) == tonic))
                {
                    onset = s.Onset;
                    return true;
                }
            }

            onset = default;
            return false;
        }

        /// <summary>
        /// The onset of the first sonority strictly after <paramref name="from"/> and before
        /// <paramref name="to"/> that sounds a note <paramref name="key"/> lacks, or <paramref name="to"/>.
        /// </summary>
        public Rational FirstOnsetLacking(KeySignature key, KeySignature current, Rational from, Rational to)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (s.Onset > from && Lacking(i, key, current) != 0)
                    return s.Onset;
            }

            return to;
        }

        /// <summary>
        /// The onset of the last sonority in [<paramref name="from"/>, <paramref name="to"/>)
        /// that sounds a note <paramref name="key"/> lacks.
        /// </summary>
        public bool LastOnsetLacking(KeySignature key, KeySignature current, Rational from, Rational to, out Rational onset)
        {
            var found = false;
            onset = default;
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (Lacking(i, key, current) != 0)
                {
                    onset = s.Onset;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// How many distinct whole notes in [<paramref name="from"/>, <paramref name="to"/>) hold
        /// a sonority beginning there that sounds a pitch class in <paramref name="pitchClasses"/>.
        /// </summary>
        public int BarsSounding(ushort pitchClasses, Rational from, Rational to)
        {
            var bars = 0;
            long? lastBar = null;
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & pitchClasses) == 0)
                    continue;

                var bar = (long)Math.Floor(s.Onset.ToDouble());
                if (bar != lastBar)
                {
                    bars++;
                    lastBar = bar;
                }
            }

            return bars;
        }

        /// <summary>
        /// Whether a sonority in [<paramref name="from"/>, <paramref name="to"/>) sounds a pitch
        /// class in <paramref name="homeNotes"/> — a note of <paramref name="current"/>'s own —
        /// as a note and not as a non-harmonic tone of <paramref name="key"/>: a passing F sharp
        /// in a C-major scale is not E minor sounding its own note.
        /// </summary>
        public bool SoundsAsItsOwn(ushort homeNotes, Rational from, Rational to, KeySignature key, KeySignature current)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & homeNotes) != 0 && Lacking(i, key, current, Exempt.Nothing) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether the new key's own notes, <paramref name="ownNotes"/>, sound in at least two
        /// whole notes from <paramref name="from"/> before <paramref name="within"/> — and before
        /// the old key's own notes, <paramref name="homeNotes"/>, are heard again as harmony: a
        /// second applied dominant after the old key has come home confirms nothing, and a chord
        /// of the old key is the old key back, whatever the new key might borrow it as; but a
        /// passing F natural in E F F♯ G is not C major coming home.
        /// </summary>
        public bool Returns(ushort ownNotes, ushort homeNotes, Rational from, Rational within, KeySignature key, KeySignature current)
        {
            if (within > End)
                within = End;
            if (homeNotes != 0)
            {
                for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
                {
                    var s = _sonorities[i];
                    if (s.Onset >= within)
                        break;
                    if (s.Onset > from && (s.PitchClasses & homeNotes) != 0 && Lacking(i, key, current, Exempt.Nothing) != 0)
                    {
                        within = s.Onset;
                        break;
                    }
                }
            }

            return BarsSounding(ownNotes, from, within) >= 2;
        }

        /// <summary>
        /// Whether [<paramref name="from"/>, <paramref name="to"/>) is framed by <paramref name="key"/>'s
        /// tonic chord: everything sounding at its first onset is that chord — or, in a melody,
        /// a single note of the tonic triad, as a tune may open on the third or the fifth — and
        /// everything sounding at its last lies within the tonic triad; an arpeggio may end on
        /// the third, and a minor key's piece may close on its Picardy third.
        /// </summary>
        public bool FramedBy(KeySignature key, Rational from, Rational to)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length || _sonorities[i].Onset >= to)
                return false;

            var opening = SoundingAt(_sonorities[i].Onset);
            var opensOnTheTonic = BitOperations.PopCount(opening) == 1
                ? (opening & ~TonicTriad(key)) == 0
                : IsTonicChord(key, opening);
            if (!opensOnTheTonic)
                return false;

            var last = FirstIndexAt(to) - 1;
            var closing = SoundingAt(_sonorities[last].Onset);
            return closing != 0 && (closing & ~TonicTriad(key) & ~PicardyThird(last, key)) == 0;
        }

        /// <summary>
        /// Whether everything sounding at the first onset at or after <paramref name="from"/> is
        /// <paramref name="key"/>'s tonic chord.
        /// </summary>
        public bool OpensOn(KeySignature key, Rational from)
        {
            var i = FirstIndexAt(from);
            return i < _sonorities.Length && IsTonicChord(key, SoundingAt(_sonorities[i].Onset));
        }

        /// <summary>The pitch classes of every sonority sounding at <paramref name="position"/>.</summary>
        private ushort SoundingAt(Rational position)
        {
            ushort together = 0;
            foreach (var i in Reaching(position))
            {
                if (_sonorities[i].Onset > position)
                    break;
                if (_sonorities[i].End > position)
                    together |= _sonorities[i].PitchClasses;
            }

            return together;
        }

        /// <summary>
        /// The start of the phrase holding <paramref name="position"/>: phrases of
        /// <paramref name="phrase"/> counted from the first note.
        /// </summary>
        public Rational PhraseStart(Rational position, Rational phrase)
        {
            var phrases = (long)Math.Floor(((position - FirstOnset) / phrase).ToDouble());
            return FirstOnset + (phrase * phrases);
        }

        /// <summary>
        /// Where the music is home again after an excursion to <paramref name="key"/> that began
        /// at <paramref name="from"/>: the first onset sounding a note the key does not own, or —
        /// once a whole note of the key's tonic chord has been heard — the first whole note that
        /// neither is the tonic nor sounds one of the key's own notes, <paramref name="ownNotes"/>;
        /// the end when neither comes. An excursion to a key with no note of its own — the
        /// relative major, from its minor — lasts until the old key's own notes,
        /// <paramref name="homeNotes"/>, return. <paramref name="tonicHeard"/> says whether the
        /// key's tonic chord sounded before the music was home — as a chord, or as a whole note
        /// of nothing but its notes.
        /// </summary>
        /// <remarks>
        /// A tonicization is an applied dominant and the chord it resolves to: C F | E7 Am | F G |
        /// C C touches A minor for the E7 and the Am, and is home at the F. Measured to the first
        /// note the new key did not own, the excursion ran to the end of the piece, seven bars
        /// for one E7 Am, because A minor owns every note of C major. The excursion is read by
        /// whole notes, from where it began, because an arpeggiated D7 sounds its A and C after
        /// its F sharp, and read note by note the excursion ended inside the chord that began it.
        /// </remarks>
        public Rational HomeAgain(KeySignature key, KeySignature current, ushort ownNotes, ushort homeNotes, Rational from, out bool tonicHeard)
        {
            var tonic = TonicTriad(key);
            tonicHeard = false;

            var until = End;
            if (ownNotes == 0 && homeNotes != 0 && FirstOnsetSounding(homeNotes, from, afterFrom: true, End, out var home))
                until = home;

            for (var bar = from; bar < until; bar += Rational.Whole)
            {
                var barTo = bar + Rational.Whole;
                ushort together = 0;
                for (var i = FirstIndexAt(bar); i < _sonorities.Length && _sonorities[i].Onset < barTo; i++)
                {
                    if (Lacking(i, key, current) != 0)
                        return _sonorities[i].Onset;
                    together |= _sonorities[i].PitchClasses;
                    if (BitOperations.PopCount(_chordPitchClasses[i]) >= 2 && IsTonicChord(key, _chordPitchClasses[i]))
                        tonicHeard = true;
                }

                if (together == 0)
                    continue;
                if (IsTonicChord(key, together))
                    tonicHeard = true;
                else if (ownNotes != 0 && (together & ownNotes) == 0 && tonicHeard)
                    return bar;
            }

            return until;
        }

        /// <summary>
        /// Whether the whole note starting at <paramref name="bar"/> holds at least one sonority
        /// beginning in it and every such sonority sounds only notes <paramref name="key"/> owns.
        /// A bar with a note the key lacks is not a chord of the key, however brief the note: the
        /// last bar of a C major scale is not the pivot into G for having only one F in it.
        /// </summary>
        public bool BarIsOwned(Rational bar, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var any = false;
            var to = bar + Rational.Whole;
            for (var i = FirstIndexAt(bar); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (Lacking(i, key, current, exempt) != 0)
                    return false;
                any = true;
            }

            return any;
        }

        /// <summary>Whether every whole note from <paramref name="from"/> to <paramref name="to"/> is <see cref="BarIsOwned"/> by <paramref name="key"/>.</summary>
        public bool BarsAreOwned(Rational from, Rational to, KeySignature key, KeySignature current)
        {
            for (var bar = from; bar < to; bar += Rational.Whole)
            {
                if (!BarIsOwned(bar, key, current))
                    return false;
            }

            return true;
        }

        /// <summary>The first onset strictly after <paramref name="position"/>, or the end.</summary>
        public Rational NextOnsetAfter(Rational position)
        {
            for (var i = FirstIndexAt(position); i < _sonorities.Length; i++)
            {
                if (_sonorities[i].Onset > position)
                    return _sonorities[i].Onset;
            }

            return End;
        }

        /// <summary>
        /// The start of the whole note holding <paramref name="onset"/>, or — when a sonority
        /// earlier in that whole note sounds a note <paramref name="key"/> lacks — the first
        /// onset after the last such sonority. The new key begins with the bar unless the bar
        /// began in the old.
        /// </summary>
        public Rational StartOfBar(Rational onset, KeySignature key, KeySignature current)
        {
            var bar = new Rational((long)Math.Floor(onset.ToDouble()), 1);
            var start = bar;
            var afterForeign = false;

            var i = FirstIndexAt(bar);
            while (i < _sonorities.Length && _sonorities[i].Onset <= onset)
            {
                var at = _sonorities[i].Onset;
                var lacking = false;
                while (i < _sonorities.Length && _sonorities[i].Onset == at)
                    lacking |= Lacking(i++, key, current) != 0;

                if (afterForeign)
                {
                    start = at;
                    afterForeign = false;
                }

                if (at < onset && lacking)
                    afterForeign = true;
            }

            return start;
        }

        /// <summary>
        /// Whether a sonority beginning at or after <paramref name="from"/> and before the last
        /// onset sounds <paramref name="key"/>'s tonic pitch class.
        /// </summary>
        public bool SoundsTonicBeforeClose(KeySignature key, Rational from)
        {
            if (_sonorities.Length == 0)
                return false;

            var last = _sonorities[^1].Onset;
            var tonic = (ushort)(1 << key.Root);
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= last)
                    break;
                if ((s.PitchClasses & tonic) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>Whether the music ends on <paramref name="key"/>'s tonic triad — a minor key's Picardy third included — and nothing else.</summary>
        public bool ClosesOn(KeySignature key)
        {
            if (_sonorities.Length == 0)
                return false;

            var final = _chordPitchClasses[^1];
            return final != 0 && (final & ~TonicTriad(key) & ~PicardyThird(_sonorities.Length - 1, key)) == 0;
        }

        /// <summary>The index of the first sonority whose onset is at or after <paramref name="position"/>.</summary>
        private int FirstIndexAt(Rational position)
        {
            var lo = 0;
            var hi = _sonorities.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (_sonorities[mid].Onset < position)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// The indices of the sonorities that may be sounding at <paramref name="position"/> or
        /// begin after it: the long ones kept aside that reach it, then every sonority from the
        /// first whose ordinary length could, in onset order. A loop over them stops itself where
        /// its onsets pass what it is looking for.
        /// </summary>
        private ReachingIndices Reaching(Rational position) => new(this, position);

        private struct ReachingIndices(Evidence evidence, Rational position)
        {
            private readonly int _mainStart = evidence.FirstIndexAt(position - evidence._longest);
            private int _long = 0;
            private int _main = -1;

            public int Current { get; private set; }

            public ReachingIndices GetEnumerator() => this;

            public bool MoveNext()
            {
                while (_long < evidence._long.Length)
                {
                    var i = evidence._long[_long++];
                    if (i < _mainStart && evidence._sonorities[i].End > position)
                    {
                        Current = i;
                        return true;
                    }
                }

                if (_main < 0)
                    _main = _mainStart;
                if (_main < evidence._sonorities.Length)
                {
                    Current = _main++;
                    return true;
                }

                return false;
            }
        }
    }
}
