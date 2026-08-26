// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using System.Runtime.CompilerServices;
using Celeritas.Core.Analysis;

namespace Celeritas.Core;

/// <summary>
/// Functional harmony analyzer using bitwise operations for performance
/// </summary>
public static class KeyAnalyzer
{
    // Scale masks for quick degree identification
    /// <summary>12-bit pitch-class mask of the C major scale (bits 0,2,4,5,7,9,11).</summary>
    public const ushort MajorScaleMask = 0b101010110101; // C D E F G A B = bits 0,2,4,5,7,9,11

    /// <summary>12-bit pitch-class mask of the C natural-minor scale (bits 0,2,3,5,7,8,10).</summary>
    public const ushort MinorScaleMask = 0b010110101101; // C D Eb F G Ab Bb = bits 0,2,3,5,7,8,10

    private static readonly ushort[] MajorScaleMasksByRoot;
    private static readonly ushort[] MinorScaleMasksByRoot;

    // Standard deviation of the Krumhansl-Kessler profile of each mode. Every one of the 12
    // rotations of a profile is a permutation of the same 12 weights, so one root is
    // representative of the mode. Dividing by these turns the profile dot product in
    // ProfileScore into a true Pearson correlation, which is what makes a major candidate and
    // its relative minor comparable: the two profiles have different sigmas (1.26 vs 1.15), and
    // an unscaled dot product would hand every relative-pair tie to the major by construction.
    private static readonly float MajorProfileSigma;
    private static readonly float MinorProfileSigma;

    static KeyAnalyzer()
    {
        MajorScaleMasksByRoot = new ushort[12];
        MinorScaleMasksByRoot = new ushort[12];

        for (var root = 0; root < 12; root++)
        {
            // Transposing a scale UP by `root` semitones moves bit k to bit (k+root) mod 12,
            // i.e. a LEFT rotation of the 12-bit mask.
            MajorScaleMasksByRoot[root] = RotateLeft(MajorScaleMask, root);
            MinorScaleMasksByRoot[root] = RotateLeft(MinorScaleMask, root);
        }

        MajorProfileSigma = ProfileSigma(true);
        MinorProfileSigma = ProfileSigma(false);
    }

    /// <summary>
    /// Standard deviation of a mode's Krumhansl-Kessler weights, read from
    /// <see cref="KeyProfiler.GetKeyProfile"/> so the two analyzers share one set of constants.
    /// </summary>
    private static float ProfileSigma(bool isMajor)
    {
        var profile = KeyProfiler.GetKeyProfile(0, isMajor);

        var sum = 0f;
        for (var i = 0; i < 12; i++)
            sum += profile[i];
        var mean = sum / 12f;

        var variance = 0f;
        for (var i = 0; i < 12; i++)
        {
            var deviation = profile[i] - mean;
            variance += deviation * deviation;
        }

        // The profiles are not constant, so this is strictly positive and safe to divide by.
        return MathF.Sqrt(variance / 12f);
    }

    /// <summary>
    /// Pitch-class mask of the major or natural-minor scale rooted at <paramref name="root"/> (0=C…11=B).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetScaleMask(int root, bool isMajor)
    {
        var index = PitchMath.Fold(root);
        return isMajor ? MajorScaleMasksByRoot[index] : MinorScaleMasksByRoot[index];
    }

    /// <summary>
    /// Analyze chord in the context of a key signature
    /// Uses cyclic rotation (ROR) to find scale degree
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RomanNumeralChord Analyze(ReadOnlySpan<int> pitches, KeySignature key)
    {
        if (pitches.IsEmpty)
            return RomanNumeralChord.Invalid;

        // First, identify the chord quality independently
        var chord = ChordAnalyzer.Identify(pitches);
        if (chord.Quality == ChordQuality.Unknown)
            return RomanNumeralChord.Invalid;

        // Get chord root pitch class
        var chordRoot = ChordLibrary.GetPitchClass(chord.Root);

        // Calculate interval from key root to chord root
        var interval = (chordRoot - key.Root + 12) % 12;

        // Map interval to scale degree and function
        return key.IsMajor
            ? AnalyzeInMajorKey(interval, chord.Quality)
            : AnalyzeInMinorKey(interval, chord.Quality);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (array overload)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RomanNumeralChord Analyze(int[] pitches, KeySignature key)
    {
        // Guard before the span conversion, not after: new ReadOnlySpan<int>(null) is legal
        // and yields an *empty* span rather than throwing, so an unguarded null would reach
        // the empty-input branch and be answered as if the caller had passed no notes.
        ArgumentNullException.ThrowIfNull(pitches);
        return Analyze(new ReadOnlySpan<int>(pitches), key);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (NoteEvent array overload)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RomanNumeralChord Analyze(ReadOnlySpan<NoteEvent> notes, KeySignature key)
    {
        if (notes.IsEmpty)
            return RomanNumeralChord.Invalid;

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        var sounding = Rests.SoundingInto(notes, pitches);
        if (sounding == 0)
            return RomanNumeralChord.Invalid;

        return Analyze(pitches[..sounding], key);
    }

    /// <summary>
    /// Analyze chord in the context of a key signature (NoteEvent array overload)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RomanNumeralChord Analyze(NoteEvent[] notes, KeySignature key)
    {
        // AsSpan() is null-safe and returns an empty span, so null would be answered
        // as RomanNumeralChord.Invalid rather than reported as the mistake it is.
        ArgumentNullException.ThrowIfNull(notes);
        return Analyze(notes.AsSpan(), key);
    }

    private static RomanNumeralChord AnalyzeInMajorKey(int interval, ChordQuality quality)
    {
        return interval switch
        {
            0 => new RomanNumeralChord(ScaleDegree.I, quality, HarmonicFunction.Tonic),      // I
            2 => new RomanNumeralChord(ScaleDegree.Ii, quality, HarmonicFunction.Subdominant), // ii
            4 => new RomanNumeralChord(ScaleDegree.Iii, quality, HarmonicFunction.Tonic),    // iii
            5 => new RomanNumeralChord(ScaleDegree.Iv, quality, HarmonicFunction.Subdominant), // IV
            7 => new RomanNumeralChord(ScaleDegree.V, quality, HarmonicFunction.Dominant),   // V
            9 => new RomanNumeralChord(ScaleDegree.Vi, quality, HarmonicFunction.Tonic),     // vi
            11 => new RomanNumeralChord(ScaleDegree.Vii, quality, HarmonicFunction.Dominant), // vii°
            _ => RomanNumeralChord.Invalid
        };
    }

    private static RomanNumeralChord AnalyzeInMinorKey(int interval, ChordQuality quality)
    {
        return interval switch
        {
            0 => new RomanNumeralChord(ScaleDegree.I, quality, HarmonicFunction.Tonic),      // i
            2 => new RomanNumeralChord(ScaleDegree.Ii, quality, HarmonicFunction.Subdominant), // ii°
            3 => new RomanNumeralChord(ScaleDegree.Iii, quality, HarmonicFunction.Tonic),    // III
            5 => new RomanNumeralChord(ScaleDegree.Iv, quality, HarmonicFunction.Subdominant), // iv
            7 => new RomanNumeralChord(ScaleDegree.V, quality, HarmonicFunction.Dominant),   // V (or v)
            8 => new RomanNumeralChord(ScaleDegree.Vi, quality, HarmonicFunction.Tonic),     // VI
            10 => new RomanNumeralChord(ScaleDegree.Vii, quality, HarmonicFunction.Dominant), // VII
            _ => RomanNumeralChord.Invalid
        };
    }

    /// <summary>
    /// Identify the key of a collection of pitches: the 24 major and natural-minor scales are
    /// first ranked by how many of the input's pitch classes they contain, and the candidates
    /// that tie for that best overlap are then separated by how heavily the input weights each
    /// of their scale degrees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scale overlap alone cannot answer this question. A key and its relative (G major and E
    /// minor) have <em>identical</em> pitch-class sets, so they always tie; so does any key whose
    /// scale merely happens to contain the notes played (a melody on G B D A C sits inside C
    /// major, G major, A minor and E minor alike). Which of those a listener hears is decided by
    /// <em>emphasis</em> — how often the tonic and the other structural degrees actually sound —
    /// and that is why this overload reads a multiset of pitches rather than a set. Repeating a
    /// note is evidence, and it is counted.
    /// </para>
    /// <para>
    /// The tie-break scores each surviving candidate by correlating the input's pitch-class
    /// counts against that key's Krumhansl-Kessler profile, the same weights
    /// <see cref="KeyProfiler"/> uses, read from <see cref="KeyProfiler.GetKeyProfile"/> so the
    /// two analyzers cannot drift apart. Keeping the overlap prefilter in front of it preserves
    /// a guarantee <see cref="KeyProfiler"/> does not make: where some scale contains every pitch
    /// class sounded, the key returned is one of those scales — <see cref="KeyProfiler"/> may
    /// name a key whose scale omits a note that is plainly sounding. The two therefore agree on
    /// material that decides the key, but can differ on material that does not: this method also
    /// divides out the major bias documented in <see cref="KeyProfiler"/>, which is enough to tip
    /// a near-tie between relatives.
    /// </para>
    /// <para>
    /// Note counts are the weighting; note durations are not. A caller who wants a held whole
    /// note to outweigh a passing sixteenth wants <see cref="KeyProfiler.DetectFromBuffer"/>,
    /// which weights by duration.
    /// </para>
    /// <para>
    /// <strong>Documented conventions where the input cannot decide.</strong> These are fixed
    /// answers, not artifacts of iteration order:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Empty input returns <c>C major</c>.</description></item>
    /// <item><description>A bare scale — every pitch class of one diatonic set sounded equally
    /// often — returns the <em>relative major</em> (a plain G-major scale is G major, not E
    /// minor). The Krumhansl-Kessler weights lean that way for an evenly-weighted diatonic set;
    /// the margin is thin, and callers who need the distinction must supply material that
    /// emphasizes a tonic.</description></item>
    /// <item><description>An input that leaves several candidates scoring identically — a
    /// diminished seventh, an augmented triad, all twelve pitch classes sounded equally often —
    /// is settled by the bass: the tied key rooted nearest above the lowest sounding note wins,
    /// major before minor at equal distance. That keeps the answer <em>equivariant</em>, so
    /// transposing the passage transposes the answer with it. An earlier rule took the lowest
    /// root, which made the answer depend on absolute position: a diminished seventh moved up a
    /// semitone reported a key a fourth away. A chromatic run beginning on C still returns
    /// <c>C major</c>.</description></item>
    /// </list>
    /// <para>
    /// This method returns a bare <see cref="KeySignature"/> and so cannot report how thin the
    /// winning margin was, and it will answer a genuinely undecided input as confidently as a
    /// decided one. Callers who slide a window across music and must not mistake an ambiguous
    /// window for a key change — modulation detection above all — need the margin as well as the
    /// answer: use <see cref="KeyProfiler.DetectFromPitches(ReadOnlySpan{int})"/> and gate on its
    /// <see cref="KeyDetectionResult.Confidence"/>, as
    /// <see cref="KeyTrajectory.DetectModulations"/> does.
    /// </para>
    /// </remarks>
    public static KeySignature IdentifyKey(ReadOnlySpan<int> pitches)
    {
        if (pitches.IsEmpty)
            return new KeySignature(0, true);

        // Build the mask and the multiset in one pass. ChordAnalyzer.GetMask would give the
        // mask alone, and the multiplicities it drops are precisely the evidence that separates
        // a key from its relative.
        Span<int> counts = stackalloc int[12];
        counts.Clear();
        ushort mask = 0;

        var lowestPitch = int.MaxValue;

        foreach (var pitch in pitches)
        {
            // Fold rather than `%`: `%` keeps the sign in C#, so a pitch below zero would index
            // backwards out of `counts`. Folding also keeps the answer octave-invariant, which a
            // question about pitch classes must be.
            var pitchClass = PitchMath.Fold(pitch);
            counts[pitchClass]++;
            mask |= (ushort)(1 << pitchClass);

            if (pitch < lowestPitch)
                lowestPitch = pitch;
        }

        // The bass only settles a tie, but it has to be carried this far to do it: the
        // mask and the counts have both forgotten which note was lowest.
        return IdentifyKey(mask, counts, PitchMath.Fold(lowestPitch));
    }

    /// <summary>
    /// Identify a key from a pitch-class mask plus the pitch-class counts behind it.
    /// The mask selects the candidates; the counts choose between them.
    /// </summary>
    private static KeySignature IdentifyKey(ushort mask, ReadOnlySpan<int> counts, int bassPitchClass)
    {
        // Pass 1: the historical prefilter. How many of the input's pitch classes does the best
        // scale contain? Every candidate reaching that number stays in the running -- the old
        // code instead kept whichever of them the loop happened to reach first, which is how a
        // diatonic G-major melody came back as E minor.
        var bestOverlap = 0;

        for (var root = 0; root < 12; root++)
        {
            var majorOverlap = PopCount((ushort)(mask & MajorScaleMasksByRoot[root]));
            if (majorOverlap > bestOverlap)
                bestOverlap = majorOverlap;

            var minorOverlap = PopCount((ushort)(mask & MinorScaleMasksByRoot[root]));
            if (minorOverlap > bestOverlap)
                bestOverlap = minorOverlap;
        }

        // Pass 2: separate the tied candidates by how the input actually weights their degrees.
        var total = 0;
        for (var i = 0; i < 12; i++)
            total += counts[i];
        var meanCount = total / 12f;

        // Roots ascending, major before minor at each root, compared with a strict `>`: an exact
        // tie therefore resolves to the lowest root and to major, the documented convention.
        var bestRoot = 0;
        var bestIsMajor = true;
        var bestScore = float.NegativeInfinity;
        var bestDistance = int.MaxValue;

        // Scores are profile correlations of order 1-20; anything closer than this is the
        // same score arrived at by a different rounding path, not a real preference.
        const float TieEpsilon = 1e-4f;

        for (var root = 0; root < 12; root++)
        {
            for (var mode = 0; mode < 2; mode++)
            {
                var isMajor = mode == 0;
                var scaleMask = isMajor ? MajorScaleMasksByRoot[root] : MinorScaleMasksByRoot[root];

                if (PopCount((ushort)(mask & scaleMask)) != bestOverlap)
                    continue;

                var score = ProfileScore(counts, meanCount, root, isMajor);

                // A symmetric set -- a diminished seventh, an augmented triad, a whole-tone
                // scale -- correlates identically with several keys, so the profile settles
                // nothing and the tie-break becomes the answer. Taking the lowest root there made
                // that answer depend on absolute position: the same chord moved up a semitone
                // reported a key a fourth away, cycling with the symmetry instead of transposing
                // with the music.
                //
                // Distance from the bass is the same for every transposition of a passage, so
                // preferring the tied candidate nearest above the bass keeps the answer
                // equivariant. It is also the musical reading: with nothing else to go on, the
                // lowest sounding note is the likeliest tonic. Inputs the profile can separate
                // never reach this.
                var distance = PitchMath.Fold(root - bassPitchClass);

                var beatsBest = score > bestScore + TieEpsilon;
                var tiesBest = !beatsBest && score > bestScore - TieEpsilon;

                if (beatsBest || (tiesBest && distance < bestDistance))
                {
                    bestScore = MathF.Max(score, bestScore);
                    bestDistance = distance;
                    bestRoot = root;
                    bestIsMajor = isMajor;
                }
            }
        }

        return new KeySignature((byte)bestRoot, bestIsMajor);
    }

    /// <summary>
    /// Correlate a pitch-class count vector with one key's Krumhansl-Kessler profile.
    /// </summary>
    /// <remarks>
    /// Pearson's r between the counts and the profile is
    /// <c>sum((c_i - c_mean)(p_i - p_mean)) / (12 * sigma_c * sigma_p)</c>. The centred counts sum
    /// to zero, so the <c>p_mean</c> term drops out entirely; <c>sigma_c</c> and the 12 are the
    /// same for every candidate and cannot change a ranking, so they are omitted. What must
    /// <em>not</em> be omitted is <c>sigma_p</c>: it differs between the two modes, and it is the
    /// only thing standing between this and the major bias documented in <see cref="KeyProfiler"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ProfileScore(ReadOnlySpan<int> counts, float meanCount, int root, bool isMajor)
    {
        var profile = KeyProfiler.GetKeyProfile(root, isMajor);

        var sum = 0f;
        for (var i = 0; i < 12; i++)
            sum += (counts[i] - meanCount) * profile[i];

        return sum / (isMajor ? MajorProfileSigma : MinorProfileSigma);
    }

    /// <summary>
    /// Identify key signature from a collection of pitches (array overload).
    /// See <see cref="IdentifyKey(ReadOnlySpan{int})"/> for the algorithm and for the
    /// conventions used where the input cannot decide the key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature IdentifyKey(int[] pitches)
    {
        // Without this, null becomes an empty span and is reported as C major.
        ArgumentNullException.ThrowIfNull(pitches);
        return IdentifyKey(new ReadOnlySpan<int>(pitches));
    }

    /// <summary>
    /// Identify key signature from a human-readable notation string.
    /// Example: "C4 D4 E4 F4 G4 A4 B4" -> C major
    /// </summary>
    private static KeySignature IdentifyKey(string notation)
    {
        var notes = MusicNotation.Parse(notation);
        if (notes.Length == 0)
            return new KeySignature(0, true);

        // The note count here is driven by the caller's string content, so it is unbounded.
        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return IdentifyKey(pitches);
    }

    /// <summary>
    /// Identify key signature from note events.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeySignature IdentifyKey(ReadOnlySpan<NoteEvent> notes)
    {
        if (notes.IsEmpty)
            return new KeySignature(0, true);

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        var sounding = Rests.SoundingInto(notes, pitches);
        if (sounding == 0)
            return new KeySignature(0, true);

        return IdentifyKey(pitches[..sounding]);
    }

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// Example: <c>DetectKey("G4 B4 D5 G5 D5 B4 G4")</c> -&gt; G major.
    /// </summary>
    /// <remarks>
    /// How often a note appears in <paramref name="notation"/> is evidence and is counted: see
    /// <see cref="IdentifyKey(ReadOnlySpan{int})"/> for the algorithm and for what is returned
    /// when the notation cannot decide the key (blank text, a bare scale, a chromatic run).
    /// Note <em>durations</em> written in the notation are ignored; only the notes themselves are
    /// weighed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="notation"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(string notation)
    {
        // null used to parse to zero notes and be answered as C major. The guard belongs on
        // this public entry point so the exception names `notation`, not Parse's own `input`.
        ArgumentNullException.ThrowIfNull(notation);
        return IdentifyKey(notation);
    }

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// </summary>
    /// <remarks>
    /// Each note counts once towards the key, however long it is held; see
    /// <see cref="IdentifyKey(ReadOnlySpan{int})"/> for the algorithm and its documented
    /// answers for undecidable input. For duration-weighted detection use
    /// <see cref="KeyProfiler.DetectFromPitches(ReadOnlySpan{NoteEvent})"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(ReadOnlySpan<NoteEvent> notes) => IdentifyKey(notes);

    /// <summary>
    /// Alias for IdentifyKey for more intuitive API.
    /// </summary>
    /// <remarks>
    /// Each note in the buffer counts once towards the key, however long it is held; see
    /// <see cref="IdentifyKey(ReadOnlySpan{int})"/> for the algorithm and its documented
    /// answers for undecidable input. For duration-weighted detection use
    /// <see cref="KeyProfiler.DetectFromBuffer"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature DetectKey(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Rests are silence, not a pitch class. Counting RestPitch (-1) as a B made a phrase in
        // C major with two half-bar rests come back as E minor.
        Span<int> pitches = buffer.Count <= StackAlloc.MaxInts
            ? stackalloc int[buffer.Count]
            : new int[buffer.Count];
        var sounding = 0;
        for (var i = 0; i < buffer.Count; i++)
        {
            var pitch = buffer.PitchAt(i);
            if (!Rests.IsRest(pitch)) pitches[sounding++] = pitch;
        }

        return IdentifyKey(pitches[..sounding]);
    }

    /// <summary>
    /// Cyclic right rotation (ROR) for 12-bit mask. Moves bit k to bit (k-shift) mod 12,
    /// i.e. transposes a pitch-class mask DOWN by <paramref name="shift"/> semitones.
    /// A negative <paramref name="shift"/> rotates the other way, so
    /// <c>RotateRight(v, -1) == RotateLeft(v, 1)</c>.
    /// To transpose a scale to a root, use <see cref="GetScaleMask"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort RotateRight(ushort value, int shift)
    {
        shift = NormalizeShift(shift);
        return (ushort)(((value >> shift) | (value << (12 - shift))) & 0xFFF);
    }

    /// <summary>
    /// Cyclic left rotation for 12-bit mask. Moves bit k to bit (k+shift) mod 12,
    /// i.e. transposes a pitch-class mask UP by <paramref name="shift"/> semitones.
    /// A negative <paramref name="shift"/> rotates the other way, so
    /// <c>RotateLeft(v, -1) == RotateRight(v, 1)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort RotateLeft(ushort value, int shift)
    {
        shift = NormalizeShift(shift);
        return (ushort)(((value << shift) | (value >> (12 - shift))) & 0xFFF);
    }

    /// <summary>
    /// Fold an arbitrary semitone count into [0, 12), the way <see cref="GetScaleMask"/> folds a root.
    /// </summary>
    /// <remarks>
    /// A bare <c>shift %= 12</c> leaves a negative shift negative, and C# then masks the shift count
    /// of <c>&gt;&gt;</c>/<c>&lt;&lt;</c> to 5 bits rather than rejecting it — so both halves of the
    /// rotation shifted past the end of the mask and OR'd to zero. A caller asking to transpose down
    /// by one semitone silently got back an empty scale instead of a rotated one.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeShift(int shift) => ((shift % 12) + 12) % 12;

    /// <summary>
    /// Population count (number of set bits) - Hamming weight
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount(ushort value)
    {
        return BitOperations.PopCount(value);
    }
}
