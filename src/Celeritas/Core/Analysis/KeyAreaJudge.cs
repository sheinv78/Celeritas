// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

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
/// <param name="HeldUntil">Where a note the new key does not own first sounds after <paramref name="Position"/>, or the end of the music.</param>
internal readonly record struct KeyChange(
    Rational Position,
    KeySignature From,
    KeySignature To,
    bool Established,
    float Separation,
    float Stability,
    Rational HeldUntil);

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
/// <item><description><b>You cannot leave a key without sounding a note it lacks.</b> The new key
/// must be separated from the current one on the profile, some note the new key owns and the
/// current key does not must actually sound, and the phrase must leave fewer notes foreign to
/// the new key than to the old one. A melody in C with a chromatic passing tone in every bar
/// read as G for four bars on the strength of one F sharp, while sounding F, A flat, B flat and
/// C sharp, none of which G owns.</description></item>
/// <item><description><b>A secondary dominant is not a modulation.</b> The change is only a
/// modulation if the new key still reads from where it began through a phrase; if the music is
/// back home by then, it was a tonicization. A phrase that cannot decide is extended by a
/// phrase at a time, to the end of the piece at most, and a stretch at the end shorter than a
/// phrase establishes a key only if the piece closes on that key's tonic having already sounded
/// it — in the pivot bar or in the stretch — so that V7/V–V closing a phrase is a half cadence,
/// not a modulation to the dominant.</description></item>
/// <item><description><b>The relative minor is reached when its dominant returns.</b> A minor
/// key owns its raised leading tone as well as its natural scale, so it owns everything its
/// relative major does and the raised seventh is the one note that tells them apart — and that
/// note is also the applied dominant of vi, the commonest chromatic chord in a major key. So
/// where the new key owns everything the old one did, the note must sound in two bars within
/// two phrases of the change: C F | E7 Am | F G | C C is a tonicization of vi, and E7 Am Dm E7
/// Am is A minor. The way back is the mirror image: the relative major owns no note the minor
/// lacks, so it is reached when a phrase reads as the major with the minor's leading tone
/// nowhere in it, and it begins at the bar where the minor's tonic is left.</description></item>
/// <item><description><b>The change is placed where the new key begins.</b> A tonicization at
/// the first sonority sounding a note the new key owns and the old does not, moved back to the
/// start of its whole note across sonorities both keys own — an arpeggiated D7 begins on its D,
/// not on the F sharp an eighth later — and never across one the new key does not own; a
/// modulation one bar earlier still when the new key owns that whole bar, which is the pivot
/// chord: C F G C | G D7 G modulates at the G, as a musician writes it, not at the
/// D7.</description></item>
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
    internal static List<KeyChange> Judge(
        IReadOnlyList<Sonority> sonorities,
        IReadOnlyList<Rational> candidates,
        KeySignature? startKey,
        Rational phrase)
    {
        var changes = new List<KeyChange>();
        if (sonorities.Count == 0)
            return changes;

        var evidence = new Evidence(sonorities);
        var end = evidence.End;
        var firstOnset = evidence.FirstOnset;
        var current = startKey ?? evidence.OpeningKey(phrase);

        // No change may be placed before this: the change before it, or the return home after a
        // tonicization, so that one excursion is counted once.
        var floor = Rational.Zero;

        foreach (var b in candidates)
        {
            var to = b + phrase < end ? b + phrase : end;
            if (to <= b || to <= floor)
                continue;

            var (reading, mask, distribution) = evidence.Profile(b, to);
            if (!reading.IsDecidable)
                continue;

            var named = Name(reading, distribution);
            if (named.Margin < MinDecisive)
                continue;

            var next = named.Key;
            if (next == current)
                continue;

            var separation = Separation(reading, next, current);
            if (separation < MinSeparation)
                continue;

            // A key that owns nothing the old key lacks — the relative major, from its minor —
            // cannot announce itself with a note; it is heard when a phrase reads as it with no
            // note of the old key's own in it. Any other key must sound a note the old key lacks,
            // and leave less of the phrase foreign than the old key did.
            var newNotes = (ushort)(Owned(next) & ~Owned(current));
            var ownsNothingNew = newNotes == 0;
            if (ownsNothingNew)
            {
                if (Outside(distribution, next) > Outside(distribution, current))
                    continue;
            }
            else
            {
                if ((mask & newNotes) == 0)
                    continue;

                if (!(Outside(distribution, next) < Outside(distribution, current)))
                    continue;
            }

            // Where does the new key begin? At the first sonority within a phrase before the
            // evidence — never before the floor — or inside it that sounds a note of the new key
            // the old one lacks; for a key with no such note, where the phrase first leaves the
            // old key's tonic chord.
            var scanFrom = b - phrase > floor ? b - phrase : floor;
            Rational first;
            if (ownsNothingNew)
            {
                var oldTonic = TonicTriad(current);
                if (!evidence.FirstOnsetSounding((ushort)(~oldTonic & 0x0FFF), b > floor ? b : floor, afterFrom: false, to, out first))
                    continue;
            }
            else if (!evidence.FirstOnsetSounding(newNotes, scanFrom, afterFrom: false, to, out first))
            {
                continue;
            }

            var boundary = evidence.StartOfBar(first, Owned(next));
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
            if (holdTo > end)
                holdTo = end;

            var (hold, _, held) = evidence.Profile(boundary, holdTo);
            var heldAs = Name(hold, held);
            while ((!hold.IsDecidable || heldAs.Margin < MinDecisive) && holdTo < end)
            {
                holdTo = holdTo + phrase < end ? holdTo + phrase : end;
                (hold, _, held) = evidence.Profile(boundary, holdTo);
                heldAs = Name(hold, held);
            }

            var holdSeparation = Separation(hold, next, current);
            var holds = hold.IsDecidable
                && heldAs.Margin >= MinTold
                && heldAs.Key == next
                && holdSeparation >= MinSeparation
                && (ownsNothingNew
                    ? Outside(held, next) <= Outside(held, current)
                    : Outside(held, next) < Outside(held, current));

            // The phrase from the boundary reads a third key decisively: the evidence straddled
            // two keys and its compromise reading named neither. Nothing happened here; the
            // third key is found at its own candidate.
            if (!holds
                && hold.IsDecidable
                && heldAs.Key != next
                && heldAs.Key != current
                && heldAs.Margin >= MinDecisive
                && Separation(hold, heldAs.Key, current) >= MinSeparation)
            {
                continue;
            }

            // The bar before the first note the new key alone owns, when the new key owns that
            // whole bar: the pivot chord.
            var previousBar = boundary - Rational.Whole;
            var pivot = previousBar >= floor && evidence.BarIsOwned(previousBar, Owned(next));

            // A stretch at the end shorter than a phrase is a key only if the piece closes on
            // its tonic and that tonic was already heard — in the pivot bar, or in the stretch
            // before the close — so that the close is a cadence in a key the music was in.
            // V7/V–V closing a phrase is a half cadence, not a modulation to the dominant.
            var fragment = holdTo == end && holdTo - boundary < phrase;
            var established = holds
                && (!fragment
                    || (evidence.ClosesOn(next) && (pivot || evidence.SoundsTonicBeforeClose(next, boundary))));

            // A key that owns everything the old key did is told from it by one note. That note
            // must return: once is the applied dominant of vi, twice is the relative minor.
            if (established && (Owned(current) & ~Owned(next)) == 0)
            {
                var within = boundary + phrase + phrase;
                if (within > end)
                    within = end;
                established = evidence.BarsSounding(newNotes, boundary, within) >= 2;
            }

            // A modulation is written at its pivot chord.
            var position = boundary;
            if (established && pivot)
                position = previousBar;

            // With no key given, a change placed at the first note is the opening key
            // misjudged, not a modulation: the music was never in the key it would leave.
            if (startKey is null && established && position <= firstOnset)
            {
                current = next;
                floor = evidence.NextOnsetAfter(boundary);
                continue;
            }

            var total = 0f;
            foreach (var weight in held)
                total += weight;
            var stability = total > 0f ? 1f - (Outside(held, next) / total) : 0f;

            var foreignNotes = (ushort)(~Owned(next) & 0x0FFF);
            var heldUntil = evidence.FirstOnsetSounding(foreignNotes, boundary, afterFrom: true, end, out var contradiction)
                ? contradiction
                : end;

            changes.Add(new KeyChange(position, current, next, established, separation, stability, heldUntil));

            if (established)
            {
                current = next;
                floor = evidence.NextOnsetAfter(boundary);
            }
            else
            {
                // The same excursion is not counted twice: the next change waits for a note the
                // key we are in owns and the excursion's key does not — the music coming home.
                var homeNotes = (ushort)(Owned(current) & ~Owned(next));
                floor = homeNotes != 0 && evidence.FirstOnsetSounding(homeNotes, boundary, afterFrom: true, end, out var home)
                    ? home
                    : boundary + phrase;
            }
        }

        return changes;
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

    /// <summary>The sonorities in onset order, with the lookups the judge needs.</summary>
    private sealed class Evidence
    {
        private readonly Sonority[] _sonorities;
        private readonly Rational _longest;

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
        }

        /// <summary>
        /// The key the opening phrase sounds like on the profile; when that phrase cannot decide,
        /// the opening is extended by a phrase at a time, and the key the whole sounds like is
        /// the last resort.
        /// </summary>
        /// <remarks>
        /// The opening is not named by ownership as a change is. A change is judged against the
        /// key the music is in, and a key that has to call a recurring chord tone foreign loses
        /// to one that owns every note; the opening has nothing to be judged against, and the
        /// profile's root-weighted reading is what it sounds like. The two disagree on the
        /// blues: its tonic is a dominant seventh chord, which no major key owns — F major owns
        /// C7 — and by ownership a twelve-bar blues in C began in F and modulated home at its V7.
        /// </remarks>
        public KeySignature OpeningKey(Rational phrase)
        {
            // An opening that cannot decide is extended as a change's hold is, a phrase at a
            // time: a piece that modulates sounds, as a whole, like a key it was never in at
            // the start, and read from that key the opening came back as a modulation at its
            // first note.
            var to = phrase < End ? phrase : End;
            var (opening, _, _) = Profile(Rational.Zero, to);
            while ((!opening.IsDecidable || opening.Confidence < MinDecisive) && to < End)
            {
                to = to + phrase < End ? to + phrase : End;
                (opening, _, _) = Profile(Rational.Zero, to);
            }

            if (opening.IsDecidable && opening.Confidence >= MinDecisive)
                return opening.Key;

            var (whole, _, _) = Profile(Rational.Zero, End);
            return whole.IsDecidable ? whole.Key : opening.Key;
        }

        /// <summary>
        /// The key profile of [<paramref name="from"/>, <paramref name="to"/>), each sonority
        /// weighed by how long it sounds inside the span — the reading
        /// <see cref="KeyProfiler.DetectFromBuffer"/> gives — with the pitch classes that sound
        /// there and the distribution itself.
        /// </summary>
        public (KeyDetectionResult Reading, ushort Mask, float[] Distribution) Profile(Rational from, Rational to)
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

            return (KeyProfiler.Detect(distribution), mask, distribution);
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
        /// Whether the whole note starting at <paramref name="bar"/> holds at least one sonority
        /// beginning in it and every such sonority sounds only pitch classes in <paramref name="owned"/>.
        /// </summary>
        public bool BarIsOwned(Rational bar, ushort owned)
        {
            var any = false;
            var to = bar + Rational.Whole;
            for (var i = FirstIndexAt(bar); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & ~owned) != 0)
                    return false;
                any = true;
            }

            return any;
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
        /// earlier in that whole note sounds a note the key does not own — the first onset after
        /// the last such sonority. The new key begins with the bar unless the bar began in the old.
        /// </summary>
        public Rational StartOfBar(Rational onset, ushort owned)
        {
            var bar = new Rational((long)Math.Floor(onset.ToDouble()), 1);
            var start = bar;
            var afterForeign = false;

            var i = FirstIndexAt(bar);
            while (i < _sonorities.Length && _sonorities[i].Onset <= onset)
            {
                var at = _sonorities[i].Onset;
                ushort together = 0;
                while (i < _sonorities.Length && _sonorities[i].Onset == at)
                    together |= _sonorities[i++].PitchClasses;

                if (afterForeign)
                {
                    start = at;
                    afterForeign = false;
                }

                if (at < onset && (together & ~owned) != 0)
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

            var last = _sonorities[^1].Onset;
            ushort final = 0;
            for (var i = _sonorities.Length - 1; i >= 0 && _sonorities[i].Onset == last; i--)
                final |= _sonorities[i].PitchClasses;

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
