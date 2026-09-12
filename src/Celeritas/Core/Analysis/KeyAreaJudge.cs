// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Analysis;

/// <summary>
/// A note or a chord as the key judge hears it: when it starts, when it stops, and which pitch
/// classes it holds. <see cref="KeyProfiler.AnalyzeModulations"/> hands the judge one of these
/// per note; <see cref="ModulationDetector"/> hands it one per chord.
/// </summary>
internal readonly record struct Sonority(Rational Onset, Rational End, ushort PitchClasses);

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
/// quarter note in every bar of it — a passing tone, not a chord tone (<see cref="StrayNote"/>
/// measures the passages that must pass and fail). An applied dominant is the key's chord: a
/// dominant seventh that resolves down a fifth into a chord the key owns leaves nothing
/// foreign, so I V7/V V I is owned by its key whole. Judged on what it left foreign relative
/// to the key the music was in, a passage that visits D and E for two bars each read as A
/// major, a key that was never there, because A leaves less of it foreign than D or E does; a
/// chromatic scale in eighths read as thirty-six keys one eighth apart; and D7 G C A7, the
/// phrase after the pivot of a pop verse, read as G major with the A7's C sharp called foreign
/// — an A7 that resolves to D minor, which G does not own, is no chord of G.</description></item>
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
    /// note of pitch-class weight, where a pitch class weighs the time it sounds. A passing
    /// tone weighs an eighth; a chord tone sounds for its chord, a half bar or a bar, and is not
    /// stray.
    /// </summary>
    /// <remarks>
    /// Measured in whole notes of pitch-class weight per bar, on the passages that must fail:
    /// the A7 of D7 G C A7, resolving to a D minor that G does not own, leaves G major 1.0 in
    /// its bar (the C sharp for a whole bar); the G chord of D G | A D, two bars of D and two of
    /// E read as A major, leaves A 0.5 in its bar and the B chord 0.5; the same passage
    /// arpeggiated in eighths leaves A 0.25 in the G chord's bar; a chromatic scale in eighths
    /// leaves every key 0.625 in every bar. And on what must pass: every modulation in the
    /// fixture leaves its new key 0.0 in every bar from where it begins; a melody with a
    /// chromatic passing eighth in every bar leaves C 0.125 per bar; four bars of D flat in
    /// thirty-second-note arpeggios after four of C, whose last C note quantizes onto the D flat
    /// downbeat in the detector's eighth-note chords, leave D flat 0.125 in that bar.
    /// <para>
    /// The floor is <c>&gt;=</c> a quarter, and that has a measured price. A chromatic
    /// quarter-note neighbour — a C sharp under the D7 of a G-major melody over chords — weighs
    /// exactly 0.25 in its bar, so the trajectory, which reads the melody's grid, places G four
    /// bars late (at 8 where a musician says 4) while the detector, which never sees the
    /// eighths, places it at 4; two chromatic passing eighths in one bar weigh the same and a
    /// melody with two in every bar of its new key names no key at all; and a quarter E flat
    /// neighbour in G names E minor, whose raised seventh it is, on both roads. Measured with
    /// <c>&gt;</c> instead: the arpeggiated chain of secondary dominants names F sharp minor, and
    /// over two hundred random chromatic melodies the detector's modulations rise from 16 to 173
    /// and the trajectory's from 3 to 59. The floor stays where it is; these shapes are what it
    /// still gets wrong.
    /// </para>
    /// </remarks>
    private const float StrayNote = 0.25f;

    /// <summary>
    /// The pitch classes a key owns: its scale, and for a minor key its raised leading tone,
    /// so that a minor key's dominant belongs to it.
    /// </summary>
    internal static ushort Owned(KeySignature key)
    {
        var mask = key.GetScaleMask();
        if (!key.IsMajor)
            mask |= (ushort)(1 << PitchMath.Fold(key.Root + 11));
        return mask;
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

            var named = Name(reading.Result, reading.Distribution);
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
            // note of the old key's own in it. Any other key must sound a note the old key lacks.
            var owned = Owned(next);
            var newNotes = (ushort)(owned & ~Owned(current));
            var ownsNothingNew = newNotes == 0;
            if (!ownsNothingNew && (reading.Mask & newNotes) == 0)
                continue;

            // A guessed opening key that never sounded a note of its own before another key was
            // read was wrong, and the music was in the other key from the start. A random
            // diatonic melody without a cadence is as much A minor as C major to the profile,
            // and read as A minor at its opening it modulated to C at the first phrase that read
            // the other way; V I IV V | I IV V I in C, opening on its dominant, read as G on the
            // profile and modulated to C at its second chord. A key that has no note of its own
            // against the new one — the major, against its relative minor — is not a guess this
            // can test.
            var homeNotes = (ushort)(Owned(current) & ~owned);
            if (openingGuessed
                && changes.Count == 0
                && homeNotes != 0
                && !evidence.FirstOnsetSounding(homeNotes, firstOnset, afterFrom: false, to, out _))
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
                if (!evidence.FirstOnsetSounding(newNotes, scanFrom, afterFrom: false, to, out first))
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
            // whole bar: the pivot chord.
            var previousBar = boundary - Rational.Whole;
            var pivot = previousBar >= floor && evidence.BarIsOwned(previousBar, next, current);

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
                && !evidence.Returns(newNotes, homeNotes, boundary, boundary + phrase + phrase)
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
                        && (evidence.Returns(newNotes, homeNotes, phraseStart, phraseStart + phrase + phrase)
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
    /// Names the key of a phrase: of the keys that leave the least of its material foreign, the
    /// one the profile correlates best with. The margin is the profile's, taken over those keys
    /// only, so that a key no other key rivals in ownership is named outright.
    /// </summary>
    /// <remarks>
    /// <see cref="KeyDetectionResult.Key"/> is the profile's choice over all twenty-four keys,
    /// and the Krumhansl profile hears a dominant seventh as the key on its root: eight chords
    /// alternating D7 and G read as D major by a margin of 0.15, though D major does not own the
    /// C natural in every D7. A key that has to call a recurring chord tone foreign is not the
    /// phrase's key when another key owns every note.
    /// </remarks>
    private static Named Name(KeyDetectionResult reading, ReadOnlySpan<float> distribution)
    {
        // Ownership is weighed to a small tolerance: the weights are note durations, and two
        // keys that own the same notes must tie exactly however the durations were summed.
        const float tolerance = 1e-4f;

        var least = float.MaxValue;
        foreach (var candidate in reading.AllCorrelations)
        {
            var foreign = Outside(distribution, candidate.Key);
            if (foreign < least)
                least = foreign;
        }

        // AllCorrelations is sorted strongest first, so the first two owners met are the best
        // and the runner-up among them.
        KeySignature? best = null;
        var bestCorrelation = 0f;
        var runnerUp = float.NaN;
        foreach (var candidate in reading.AllCorrelations)
        {
            if (Outside(distribution, candidate.Key) > least + tolerance)
                continue;

            if (best is null)
            {
                best = candidate.Key;
                bestCorrelation = candidate.Correlation;
            }
            else
            {
                runnerUp = candidate.Correlation;
                break;
            }
        }

        if (best is not { } key)
            return new Named(reading.Key, reading.Confidence);

        if (float.IsNaN(runnerUp))
            return new Named(key, 1f);

        var margin = bestCorrelation > 0f
            ? Math.Clamp((bestCorrelation - runnerUp) / (bestCorrelation + 0.001f), 0f, 1f)
            : 0f;
        return new Named(key, margin);
    }

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
    private static ushort TonicTriad(KeySignature key) =>
        (ushort)((1 << key.Root)
            | (1 << PitchMath.Fold(key.Root + (key.IsMajor ? 4 : 3)))
            | (1 << PitchMath.Fold(key.Root + 7)));

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

    /// <summary>The sonorities in onset order, with the lookups the judge needs.</summary>
    private sealed class Evidence
    {
        private readonly Sonority[] _sonorities;
        private readonly Rational _longest;

        // Sonorities that begin together are one chord. For each sonority, the chord's pitch
        // classes and — when the chord is a dominant seventh followed by a chord on the root a
        // fifth below — that root and the resolving chord's pitch classes, so that an applied
        // dominant can be told to belong to the key that owns its resolution.
        private readonly ushort[] _chordPitchClasses;
        private readonly int[] _dominantRoot;
        private readonly ushort[] _resolution;

        public Rational End { get; }

        /// <summary>The onset of the first sonority, or zero when there is none.</summary>
        public Rational FirstOnset => _sonorities.Length > 0 ? _sonorities[0].Onset : Rational.Zero;

        public Evidence(IReadOnlyList<Sonority> sonorities)
        {
            _sonorities = new Sonority[sonorities.Count];
            for (var i = 0; i < _sonorities.Length; i++)
                _sonorities[i] = sonorities[i];
            Array.Sort(_sonorities, static (a, b) => a.Onset.CompareTo(b.Onset));

            var end = Rational.Zero;
            var longest = Rational.Zero;
            foreach (var s in _sonorities)
            {
                if (s.End > end)
                    end = s.End;
                var length = s.End - s.Onset;
                if (length > longest)
                    longest = length;
            }

            End = end;
            _longest = longest;

            _chordPitchClasses = new ushort[_sonorities.Length];
            _dominantRoot = new int[_sonorities.Length];
            _resolution = new ushort[_sonorities.Length];
            for (var i = 0; i < _sonorities.Length;)
            {
                var next = NextChord(i);
                ushort together = 0;
                for (var k = i; k < next; k++)
                    together |= _sonorities[k].PitchClasses;
                for (var k = i; k < next; k++)
                {
                    _chordPitchClasses[k] = together;
                    _dominantRoot[k] = -1;
                }

                i = next;
            }

            // An applied dominant resolves into the next chord — a melody's single notes between
            // are not chords — a triad on the root a fifth below.
            for (var i = 0; i < _sonorities.Length; i = NextChord(i))
            {
                var root = DominantRootOf(_chordPitchClasses[i]);
                if (root < 0)
                    continue;

                var resolvesAt = NextChord(i);
                while (resolvesAt < _sonorities.Length && BitOperations.PopCount(_chordPitchClasses[resolvesAt]) < 3)
                    resolvesAt = NextChord(resolvesAt);
                if (resolvesAt >= _sonorities.Length)
                    continue;

                var resolution = _chordPitchClasses[resolvesAt];
                if (!ResolvesTo(root, resolution))
                    continue;

                for (var k = i; k < NextChord(i); k++)
                {
                    _dominantRoot[k] = root;
                    _resolution[k] = resolution;
                }
            }
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
        /// <paramref name="root"/> — whatever else sounds with it. Containing that note is not
        /// enough: a C major chord contains E, and B7 followed by C is not V7 of E resolving.
        /// </summary>
        private static bool ResolvesTo(int root, ushort chord)
        {
            var resolution = PitchMath.Fold(root + 5);
            return (chord & (1 << resolution)) != 0
                && (chord & (1 << PitchMath.Fold(resolution + 7))) != 0
                && ((chord & (1 << PitchMath.Fold(resolution + 3))) != 0 || (chord & (1 << PitchMath.Fold(resolution + 4))) != 0);
        }

        /// <summary>The root of the dominant seventh chord <paramref name="pitchClasses"/> contains, or -1.</summary>
        private static int DominantRootOf(ushort pitchClasses)
        {
            if (BitOperations.PopCount(pitchClasses) < 4)
                return -1;

            for (var root = 0; root < 12; root++)
            {
                if ((pitchClasses & DominantSeventh(root)) == DominantSeventh(root))
                    return root;
            }

            return -1;
        }

        /// <summary>
        /// The pitch classes sonority <paramref name="index"/> sounds that <paramref name="key"/>
        /// lacks — none for the notes of an applied dominant that resolves into a chord the key
        /// owns, unless that chord is the tonic of <paramref name="current"/>, the key the music
        /// is in: a dominant seventh resolving into the tonic is that key's dominant, not another
        /// key's applied chord. Am Dm E7 Am | A7 Dm B♭ E7 is A minor with V7/iv and the
        /// Neapolitan; with its E7 taken for V7/v of D minor, D minor owned the second phrase.
        /// </summary>
        private ushort Lacking(int index, KeySignature key, KeySignature? current)
        {
            var owned = Owned(key);
            var lacking = (ushort)(_sonorities[index].PitchClasses & ~owned);
            if (lacking == 0)
                return 0;

            var root = _dominantRoot[index];
            if (root >= 0
                && (_resolution[index] & ~owned) == 0
                && !(current is { } home && IsTonicChord(home, _resolution[index])))
            {
                lacking &= (ushort)~DominantSeventh(root);
            }

            return lacking;
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
        /// foreign as any key does, an applied dominant counted as the key's. Failing that, when
        /// the piece opens on a triad and exactly one major key owns the phrase outright, that
        /// key: G C F G opens in C, on its dominant. <see langword="null"/> when the piece opens
        /// on a single note, or when nothing above decides.
        /// </summary>
        /// <remarks>
        /// The owner fallback is a major key or nothing: pitch classes cannot tell A flat from G
        /// sharp, so A minor, which owns its raised leading tone, owns C F Fm C outright — the
        /// borrowed iv's A flat taken for G sharp — and by ownership a phrase in C with a
        /// borrowed chord opened in A minor. And it is not for a dominant seventh: the blues
        /// opens on one, and F major owns C7. The profile decides those.
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
                foreign[i] = Foreign(Rational.Zero, to, reading.AllCorrelations[i].Key);
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
        /// The key profile of [<paramref name="from"/>, <paramref name="to"/>), each sonority
        /// weighed by how long it sounds inside the span — the reading
        /// <see cref="KeyProfiler.DetectFromBuffer"/> gives — with the pitch classes that sound
        /// there and the distribution itself.
        /// </summary>
        public Reading Profile(Rational from, Rational to)
        {
            var distribution = new float[12];
            ushort mask = 0;

            for (var i = FirstIndexReaching(from); i < _sonorities.Length; i++)
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
            var heldAs = Name(hold.Result, hold.Distribution);
            while ((!hold.Result.IsDecidable || heldAs.Margin < MinDecisive) && holdTo < End)
            {
                holdTo = holdTo + phrase < End ? holdTo + phrase : End;
                hold = Profile(from, holdTo);
                heldAs = Name(hold.Result, hold.Distribution);
            }

            var holds = hold.Result.IsDecidable
                && heldAs.Margin >= MinTold
                && heldAs.Key == key
                && Separation(hold.Result, key, current) >= MinSeparation
                && Owns(from, holdTo, key, current);
            return (holds, hold, heldAs);
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns [<paramref name="from"/>, <paramref name="to"/>):
        /// in every whole-note bar of it, clipped to the span, the notes the key lacks amount to
        /// less than <see cref="StrayNote"/>. An applied dominant that resolves into a chord the
        /// key owns lacks nothing.
        /// </summary>
        public bool Owns(Rational from, Rational to, KeySignature key, KeySignature? current)
        {
            var firstBar = (long)Math.Floor(from.ToDouble());
            var barCount = (int)Math.Max(0, (long)Math.Ceiling(to.ToDouble()) - firstBar);
            var foreign = new float[barCount];

            for (var i = FirstIndexReaching(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var lacking = Lacking(i, key, current);
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
        /// <paramref name="key"/> lacks, an applied dominant that resolves into a chord the key
        /// owns counted as the key's.
        /// </summary>
        private float Foreign(Rational from, Rational to, KeySignature key)
        {
            var sum = 0f;
            for (var i = FirstIndexReaching(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                var lacking = Lacking(i, key, current: null);
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
        /// Whether the new key's own notes, <paramref name="ownNotes"/>, sound in at least two
        /// whole notes from <paramref name="from"/> before <paramref name="within"/> — and before
        /// the old key's own notes, <paramref name="homeNotes"/>, are heard again: a second
        /// applied dominant after the old key has come home confirms nothing.
        /// </summary>
        public bool Returns(ushort ownNotes, ushort homeNotes, Rational from, Rational within)
        {
            if (within > End)
                within = End;
            if (homeNotes != 0 && FirstOnsetSounding(homeNotes, from, afterFrom: true, within, out var home))
                within = home;
            return BarsSounding(ownNotes, from, within) >= 2;
        }

        /// <summary>
        /// Whether [<paramref name="from"/>, <paramref name="to"/>) is framed by <paramref name="key"/>'s
        /// tonic chord: everything sounding at its first onset is that chord, and everything
        /// sounding at its last lies within the tonic triad — an arpeggio may end on the third.
        /// </summary>
        public bool FramedBy(KeySignature key, Rational from, Rational to)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length || _sonorities[i].Onset >= to || !OpensOn(key, from))
                return false;

            var closing = SoundingAt(_sonorities[FirstIndexAt(to) - 1].Onset);
            return closing != 0 && (closing & ~TonicTriad(key)) == 0;
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
            for (var i = FirstIndexReaching(position); i < _sonorities.Length && _sonorities[i].Onset <= position; i++)
            {
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
        public bool BarIsOwned(Rational bar, KeySignature key, KeySignature? current)
        {
            var any = false;
            var to = bar + Rational.Whole;
            for (var i = FirstIndexAt(bar); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (Lacking(i, key, current) != 0)
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

        /// <summary>Whether the music ends on <paramref name="key"/>'s tonic triad and nothing else.</summary>
        public bool ClosesOn(KeySignature key)
        {
            if (_sonorities.Length == 0)
                return false;

            var final = _chordPitchClasses[^1];
            return final != 0 && (final & ~TonicTriad(key)) == 0;
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

        /// <summary>The index from which a sonority may still be sounding at <paramref name="position"/>.</summary>
        private int FirstIndexReaching(Rational position) => FirstIndexAt(position - _longest);
    }
}
