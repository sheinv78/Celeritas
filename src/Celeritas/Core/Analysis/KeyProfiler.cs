// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Celeritas.Core.Simd;

namespace Celeritas.Core.Analysis;

/// <summary>
/// High-performance key detection using the Krumhansl-Schmuckler algorithm.
/// Optimized with SIMD (AVX-512/AVX2/SSE2) for real-time analysis.
///
/// The algorithm correlates pitch-class distributions with psychological key profiles
/// derived from empirical studies of tonal perception.
/// </summary>
public static class KeyProfiler
{
    /// <summary>
    /// How far apart two correlations must be before one counts as better than the other.
    /// </summary>
    /// <remarks>
    /// The AVX-512, AVX2 and scalar kernels sum the same products in different orders, so they
    /// agree to about a part in ten million rather than bit for bit. A clear detection separates
    /// its winner by 0.1 to 0.35, which is four orders of magnitude above this, so nothing a
    /// listener could hear is lost by treating a smaller difference as no difference at all.
    /// </remarks>
    private const float ScoreNoise = 1e-5f;

    private static readonly CorrelationComputer ComputeCorrelations = CreateCorrelationComputer();

    // internal, not private, so a test can drive the whole detection through each kernel the
    // host offers (see Detect's kernel overload): only one of them runs on any given machine.
    internal delegate void CorrelationComputer(ReadOnlySpan<float> input, Span<float> correlations);

    /// <summary>
    /// A key's position in the 24-profile table, majors C..B at 0-11 and minors C..B at 12-23 —
    /// the order the winner loop walks and the order a tie is listed in.
    /// </summary>
    private static int KeyIndex(KeySignature key) => key.IsMajor ? key.Root : 12 + key.Root;

    private static readonly Comparer<KeyCorrelation> ByKeyIndex =
        Comparer<KeyCorrelation>.Create((a, b) => KeyIndex(a.Key).CompareTo(KeyIndex(b.Key)));

    // Krumhansl-Kessler key profiles (from cognitive musicology research)
    // These represent the psychological "weight" of each pitch class in a key

    /// <summary>Major key profile (C major as reference)</summary>
    private static readonly float[] MajorProfile =
    [
        6.35f,  // C  - tonic
        2.23f,  // C#
        3.48f,  // D  - supertonic
        2.33f,  // D#
        4.38f,  // E  - mediant
        4.09f,  // F  - subdominant
        2.52f,  // F#
        5.19f,  // G  - dominant
        2.39f,  // G#
        3.66f,  // A  - submediant
        2.29f,  // A#
        2.88f   // B  - leading tone
    ];

    /// <summary>Minor key profile (A minor as reference, rotated to C)</summary>
    private static readonly float[] MinorProfile =
    [
        6.33f,  // C  - tonic (for C minor)
        2.68f,  // C#
        3.52f,  // D  - supertonic
        5.38f,  // D# - minor third
        2.60f,  // E
        3.53f,  // F  - subdominant
        2.54f,  // F#
        4.75f,  // G  - dominant
        3.98f,  // G# - minor sixth
        2.69f,  // A
        3.34f,  // A# - minor seventh
        3.17f   // B
    ];

    // Precomputed rotated profiles for all 24 keys (12 major + 12 minor)
    // Stored as aligned arrays for SIMD access
    private static readonly float[][] AllKeyProfiles;

    // For SIMD: 16-element aligned profiles (12 notes + 4 padding)
    private static readonly float[] AlignedProfiles; // 24 keys * 16 floats = 384 floats

    static KeyProfiler()
    {
        AllKeyProfiles = new float[24][];
        AlignedProfiles = new float[24 * 16];

        // Generate rotated profiles for all keys
        for (var root = 0; root < 12; root++)
        {
            // Major key
            AllKeyProfiles[root] = RotateProfile(MajorProfile, root);
            // Minor key
            AllKeyProfiles[12 + root] = RotateProfile(MinorProfile, root);
        }

        // Copy to aligned buffer for SIMD
        for (var key = 0; key < 24; key++)
        {
            for (var i = 0; i < 12; i++)
            {
                AlignedProfiles[(key * 16) + i] = AllKeyProfiles[key][i];
            }
            // Padding with zeros
            for (var i = 12; i < 16; i++)
            {
                AlignedProfiles[(key * 16) + i] = 0f;
            }
        }
    }

    private static CorrelationComputer CreateCorrelationComputer()
    {
        return SimdInfo.GetBest() switch
        {
            SimdInstructionSet.Avx512F => ComputeCorrelationsAvx512,
            SimdInstructionSet.Avx2 => ComputeCorrelationsAvx2,
            _ => ComputeCorrelationsScalar
        };
    }

    /// <summary>
    /// Rotate a profile to a different root note.
    /// </summary>
    private static float[] RotateProfile(float[] profile, int semitones)
    {
        var rotated = new float[12];
        for (var i = 0; i < 12; i++)
        {
            rotated[i] = profile[(i - semitones + 12) % 12];
        }
        return rotated;
    }

    /// <summary>
    /// Detect the key of a piece from pitch class distribution.
    /// Uses SIMD-accelerated correlation with Krumhansl-Schmuckler profiles.
    /// </summary>
    /// <param name="pitchClassCounts">Array of 12 floats representing normalized pitch class frequencies</param>
    /// <returns>
    /// Detected key and confidence — the margin between the best key's score and the runner-up's,
    /// relative to the best. It measures how cleanly the winner separates from the field, not how
    /// well the music fits the key, so it is not a goodness-of-fit: a clear detection typically
    /// lands around 0.1-0.35, and a value below 0.5 is not "low confidence".
    /// </returns>
    // internal, not private, so KeyAreaJudge can profile the phrase-length spans it judges with
    // the same kernel and the same margins as every public reading.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static KeyDetectionResult Detect(ReadOnlySpan<float> pitchClassCounts) =>
        Detect(pitchClassCounts, ComputeCorrelations);

    /// <summary>
    /// <see cref="Detect(ReadOnlySpan{float})"/> through a chosen kernel rather than the one the
    /// CPU selected. Internal for the tests that run every kernel the host can execute over the
    /// same music and require the key and the order of the list to agree (the correlations and
    /// the confidence agree only to the kernels' noise); the public entry points always use the
    /// selected kernel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static KeyDetectionResult Detect(ReadOnlySpan<float> pitchClassCounts, CorrelationComputer computeCorrelations)
    {
        if (pitchClassCounts.Length < 12)
            return new KeyDetectionResult(new KeySignature(0, true), 0f, []);

        // Normalize input
        Span<float> normalized = stackalloc float[16]; // 16 for SIMD alignment
        NormalizeDistribution(pitchClassCounts, normalized);

        // Compute correlations with all 24 key profiles
        Span<float> correlations = stackalloc float[24];

        computeCorrelations(normalized, correlations);

        // Find best match. A later key has to beat the incumbent by more than the noise the
        // kernels differ by, not merely by a bit.
        //
        // Music that is symmetric under transposition gives two keys the same correlation
        // exactly — a 7b5 chord maps onto itself a tritone away, so C major and F# major score
        // identically for it, and an augmented triad ties three ways. The kernels sum in
        // different orders, so "identically" comes out as 0.28461015 against 0.28461018, and
        // comparing those exactly let the winner be decided by which kernel the CPU chose: the
        // natively compiled bindings reported C major for a chord the test build called F#
        // major. Within this margin the incumbent stands, which makes the lower key index win —
        // the same answer an exact tie already produced, on every machine.
        var bestKey = 0;
        var bestCorrelation = correlations[0];
        for (var i = 1; i < 24; i++)
        {
            if (correlations[i] > bestCorrelation + ScoreNoise)
            {
                bestCorrelation = correlations[i];
                bestKey = i;
            }
        }

        // Convert to KeySignature
        var root = bestKey % 12;
        var isMajor = bestKey < 12;

        // Compute confidence (how much better is best vs second best).
        // When the best correlation is not positive the ratio is meaningless
        // (division by a non-positive value), so report zero confidence.
        var sortedCorrelations = correlations.ToArray();
        Array.Sort(sortedCorrelations);
        Array.Reverse(sortedCorrelations);

        var confidence = sortedCorrelations[0] > 0f
            ? (sortedCorrelations[0] - sortedCorrelations[1]) / (sortedCorrelations[0] + 0.001f)
            : 0f;
        confidence = Math.Clamp(confidence, 0f, 1f);

        // Return all correlations for advanced analysis
        var allCorrelations = new KeyCorrelation[24];
        for (var i = 0; i < 24; i++)
        {
            allCorrelations[i] = new KeyCorrelation(
                new KeySignature((byte)(i % 12), i < 12),
                correlations[i]);
        }
        Array.Sort(allCorrelations, (a, b) => b.Correlation.CompareTo(a.Correlation));
        OrderTiesByKeyIndex(allCorrelations);

        // The list leads with the key that was chosen. The winner loop above is chained — each
        // key is measured against the incumbent, not against the strongest — so a run of scores
        // each a fraction of the noise apart can hand it a key that is not the first of its tie
        // in the list. None of the 4095 pitch-class subsets produces such a run (their ties are
        // exact or a few 1e-7 apart, their closest non-tie is 6e-4 apart), and a duration-weighted
        // distribution would have to land two keys each within 1e-5 of a third yet more than 1e-5
        // apart from each other by chance, but the list's promise is unconditional.
        var chosen = Array.FindIndex(allCorrelations, c => c.Key.Root == root && c.Key.IsMajor == isMajor);
        if (chosen > 0)
        {
            var winner = allCorrelations[chosen];
            Array.Copy(allCorrelations, 0, allCorrelations, 1, chosen);
            allCorrelations[0] = winner;
        }

        // Counted here rather than at each call site: Detect is the one place that always has
        // the distribution, so no path can forget to report its evidence.
        var distinctPitchClasses = 0;
        for (var i = 0; i < 12; i++)
        {
            if (pitchClassCounts[i] > 0f)
                distinctPitchClasses++;
        }

        return new KeyDetectionResult(
            new KeySignature((byte)root, isMajor),
            confidence,
            allCorrelations,
            distinctPitchClasses);
    }

    /// <summary>
    /// Puts each tie in a list already sorted strongest-first into key-index order. A tie is a
    /// run of keys whose correlations all sit within <see cref="ScoreNoise"/> of the run's
    /// strongest; the keys in it are listed majors C..B before minors C..B, the rule the winner
    /// loop already applies to the top of the list.
    /// </summary>
    /// <remarks>
    /// Sorting on the raw correlation alone left a tie in whichever order the kernel's rounding
    /// put it. The tritone C–F# scores C minor and F# minor identically by symmetry, but the
    /// kernels sum in different orders and one of them comes out a few 1e-7 larger: AVX-512 and
    /// AVX2 listed C minor third and F# minor fourth, the scalar kernel the other way round. Over
    /// the 4095 pitch-class subsets the kernels ordered 358 lists differently, 111 of them within
    /// the top five, so a caller's Top-5 named a different runner-up on a different CPU.
    /// </remarks>
    private static void OrderTiesByKeyIndex(KeyCorrelation[] sorted)
    {
        var runStart = 0;
        for (var i = 1; i <= sorted.Length; i++)
        {
            if (i < sorted.Length && sorted[runStart].Correlation - sorted[i].Correlation <= ScoreNoise)
                continue;

            if (i - runStart > 1)
                Array.Sort(sorted, runStart, i - runStart, ByKeyIndex);

            runStart = i;
        }
    }

    /// <summary>
    /// Detect key from a NoteBuffer (extracts pitch class distribution automatically), weighing
    /// each note by how long it is held.
    /// </summary>
    /// <remarks>
    /// This is the only reading in the library that weighs duration, and how long a note is held
    /// is strong evidence about the tonal centre: a four-bar pedal should not count the same as a
    /// passing sixteenth. It therefore disagrees with
    /// <see cref="DetectFromPitches(ReadOnlySpan{NoteEvent})"/> on the same notes whenever their
    /// durations differ — over 600 random mixed-duration passages the two named a different key
    /// in 390 — and agrees with it exactly when every note is the same length. Choose by which
    /// question you are asking: this one for "what key does this music sound like", that one for
    /// "what key do these notes belong to".
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static KeyDetectionResult DetectFromBuffer(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        Span<float> distribution = stackalloc float[12];
        ExtractPitchClassDistribution(buffer, distribution);
        return Detect(distribution);
    }

    /// <summary>
    /// Detect key from an array of MIDI pitches. Pitches outside the MIDI range are folded to
    /// their pitch class rather than skipped: the engine produces them itself, and a key is a
    /// question about pitch classes.
    /// </summary>
    public static KeyDetectionResult DetectFromPitches(ReadOnlySpan<int> pitches)
    {
        Span<float> distribution = stackalloc float[12];
        distribution.Clear();

        foreach (var pitch in pitches)
        {
            // Dropping out-of-range pitches instead of folding them meant a melody transposed
            // below zero — which MusicMath.Transpose does without clamping, by documented design —
            // contributed nothing at all, and an entirely out-of-range one was answered as C major
            // at 0% confidence rather than in the key it plainly was.
            distribution[PitchMath.Fold(pitch)]++;
        }

        return Detect(distribution);
    }

    /// <summary>
    /// Detect key from a human-readable notation string, counting each note once however long
    /// the notation says it is held.
    /// Example: "C4 D4 E4 F4 G4 A4 B4"
    /// </summary>
    /// <remarks>
    /// The durations written in the notation are parsed but not weighed; see
    /// <see cref="DetectFromPitches(ReadOnlySpan{NoteEvent})"/> for what that means and
    /// <see cref="DetectFromBuffer"/> for the reading that does weigh them.
    /// A notation string containing no notes returns the empty-input sentinel: C major with
    /// <see cref="KeyDetectionResult.Confidence"/> of 0 and an empty
    /// <see cref="KeyDetectionResult.AllCorrelations"/> array. Check the confidence (or that
    /// the correlations are non-empty) before treating the key as a real detection.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="notation"/> is <see langword="null"/>.</exception>
    public static KeyDetectionResult DetectFromPitches(string notation)
    {
        // Parse treats null like blank text and hands back no notes, so an unguarded null
        // would reach the empty-input branch and be answered as C Major at 0% confidence.
        ArgumentNullException.ThrowIfNull(notation);

        var notes = MusicNotation.Parse(notation);
        if (notes.Length == 0)
            return new KeyDetectionResult { Key = new KeySignature(0, true), Confidence = 0, AllCorrelations = [] };

        // The note count here is driven by the caller's string content, so it is unbounded.
        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];

        // Skip rests. MusicNotation.Parse marks them with RestPitch (-1), and folding that
        // into a pitch class made every rest a B: "C4/4 R/4 E4/4 G4/4" came back as E minor
        // on four pitch classes instead of C major on three.
        var count = 0;
        for (var i = 0; i < notes.Length; i++)
        {
            if (notes[i].Pitch == MusicNotation.RestPitch)
                continue;

            pitches[count++] = notes[i].Pitch;
        }

        if (count == 0)
            return new KeyDetectionResult { Key = new KeySignature(0, true), Confidence = 0, AllCorrelations = [] };

        return DetectFromPitches(pitches[..count]);
    }

    /// <summary>
    /// Detect key from an array of note events, counting each note once however long it is held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The durations on the notes are not read — the name is exact, this detects from their
    /// pitches — so a note held for four bars counts as much as a passing sixteenth and no more.
    /// For the duration-weighted reading use <see cref="DetectFromBuffer"/>, which is the same
    /// algorithm over a distribution built from how long each note sounds; the two agree exactly
    /// when every note is the same length and disagreed on 390 of 600 random mixed-duration
    /// passages when they were not.
    /// </para>
    /// <para>
    /// An empty span returns the empty-input sentinel: C major with
    /// <see cref="KeyDetectionResult.Confidence"/> of 0 and an empty
    /// <see cref="KeyDetectionResult.AllCorrelations"/> array. Check the confidence (or that
    /// the correlations are non-empty) before treating the key as a real detection.
    /// </para>
    /// </remarks>
    public static KeyDetectionResult DetectFromPitches(ReadOnlySpan<NoteEvent> notes)
    {
        if (notes.IsEmpty)
            return new KeyDetectionResult { Key = new KeySignature(0, true), Confidence = 0, AllCorrelations = [] };

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];

        // Skip rests. MusicNotation.Parse marks them with RestPitch (-1), and folding that
        // into a pitch class made every rest a B: "C4/4 R/4 E4/4 G4/4" came back as E minor
        // on four pitch classes instead of C major on three.
        var count = 0;
        for (var i = 0; i < notes.Length; i++)
        {
            if (notes[i].Pitch == MusicNotation.RestPitch)
                continue;

            pitches[count++] = notes[i].Pitch;
        }

        if (count == 0)
            return new KeyDetectionResult { Key = new KeySignature(0, true), Confidence = 0, AllCorrelations = [] };

        return DetectFromPitches(pitches[..count]);
    }

    /// <summary>
    /// Extract pitch class distribution from a NoteBuffer.
    /// Weights notes by duration for more accurate key detection.
    /// </summary>
    private static void ExtractPitchClassDistribution(NoteBuffer buffer, Span<float> distribution)
    {
        distribution.Clear();

        for (var i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            // Silence carries no pitch class: folding RestPitch (-1) put its whole duration on
            // pitch class 11, and a phrase in C major with rests in it read as E minor.
            if (Rests.IsRest(note.Pitch)) continue;
            // Fold: `%` keeps the sign in C#, so a pitch below zero indexed backwards out of the
            // distribution. Its sibling DetectFromPitches guards this same loop and its cousin
            // ChordAnalyzer.GetMask folds it — one distribution, computed three ways.
            var pitchClass = PitchMath.Fold(note.Pitch);
            // Weight by duration (longer notes are more important for key)
            var weight = (float)note.Duration.ToDouble();
            distribution[pitchClass] += weight;
        }
    }

    /// <summary>
    /// Normalize distribution to zero mean and unit variance — the z-scoring the pseudo-correlation
    /// below expects (see the NOTE on correlation bias).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void NormalizeDistribution(ReadOnlySpan<float> input, Span<float> output)
    {
        // Compute mean
        var sum = 0f;
        for (var i = 0; i < 12; i++)
            sum += input[i];
        var mean = sum / 12f;

        // Compute std dev
        var variance = 0f;
        for (var i = 0; i < 12; i++)
        {
            var diff = input[i] - mean;
            variance += diff * diff;
        }
        var stdDev = MathF.Sqrt(variance / 12f);
        stdDev = stdDev switch
        {
            < 0.0001f => 1f,
            _ => stdDev
        };

        // Normalize
        for (var i = 0; i < 12; i++)
        {
            output[i] = (input[i] - mean) / stdDev;
        }

        // Zero padding for SIMD
        for (var i = 12; i < output.Length; i++)
            output[i] = 0f;
    }

    /// <summary>
    /// AVX-512 optimized correlation computation.
    /// Scores the z-scored input against all 24 raw key profiles in parallel. This is a
    /// pseudo-correlation, not a true Pearson correlation — see the NOTE on correlation bias below.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void ComputeCorrelationsAvx512(ReadOnlySpan<float> input, Span<float> correlations)
    {
        fixed (float* pInput = input)
        fixed (float* pProfiles = AlignedProfiles)
        fixed (float* pCorrelations = correlations)
        {
            // Load input vector (12 values + 4 padding)
            var vInput = Avx512F.LoadVector512(pInput);

            for (var key = 0; key < 24; key++)
            {
                // Load key profile
                var vProfile = Avx512F.LoadVector512(pProfiles + (key * 16));

                // Multiply element-wise
                var vProduct = Avx512F.Multiply(vInput, vProfile);

                // Horizontal sum (dot product)
                // AVX-512 doesn't have direct horizontal add, so we reduce
                var sum = HorizontalSumAvx512(vProduct);

                pCorrelations[key] = sum / 12f;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumAvx512(Vector512<float> v)
    {
        // Reduce 512-bit to 256-bit
        var lo = v.GetLower();
        var hi = v.GetUpper();
        var sum256 = Avx.Add(lo, hi);

        // Reduce 256-bit to 128-bit
        var lo128 = sum256.GetLower();
        var hi128 = sum256.GetUpper();
        var sum128 = Sse.Add(lo128, hi128);

        return HorizontalSumSse(sum128);
    }

    /// <summary>
    /// AVX2 fallback for the same pseudo-correlation (see the NOTE on correlation bias below).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void ComputeCorrelationsAvx2(ReadOnlySpan<float> input, Span<float> correlations)
    {
        fixed (float* pInput = input)
        fixed (float* pProfiles = AlignedProfiles)
        fixed (float* pCorrelations = correlations)
        {
            // Load input as two 256-bit vectors (8 + 8 = 16, but we only use 12)
            var vInput0 = Avx.LoadVector256(pInput);      // [0..7]
            var vInput1 = Avx.LoadVector256(pInput + 8);  // [8..15] (only 4 used)

            for (var key = 0; key < 24; key++)
            {
                var pProfile = pProfiles + (key * 16);
                var vProfile0 = Avx.LoadVector256(pProfile);
                var vProfile1 = Avx.LoadVector256(pProfile + 8);

                // Multiply
                var vProduct0 = Avx.Multiply(vInput0, vProfile0);
                var vProduct1 = Avx.Multiply(vInput1, vProfile1);

                // Sum
                var vSum = Avx.Add(vProduct0, vProduct1);
                var sum = HorizontalSumAvx2(vSum);

                pCorrelations[key] = sum / 12f;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumAvx2(Vector256<float> v)
    {
        // Reduce 256-bit to 128-bit
        var lo = v.GetLower();
        var hi = v.GetUpper();
        var sum = Sse.Add(lo, hi);

        return HorizontalSumSse(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HorizontalSumSse(Vector128<float> v)
    {
        v = Sse.Add(v, Sse.Shuffle(v, v, 0b10_11_00_01));
        v = Sse.Add(v, Sse.Shuffle(v, v, 0b00_01_10_11));
        return v.ToScalar();
    }

    // NOTE on correlation bias: the "correlation" computed here is a dot product of the
    // z-scored input with the RAW (non-normalized) key profiles, divided by 12. Because
    // the major and minor profiles have different standard deviations (sigma), this is
    // not a true Pearson correlation. The ranking WITHIN each mode is unaffected (all 12
    // rotations of a profile share its sigma), but the cross-mode argmax in Detect IS
    // affected: the major profile's larger sigma inflates major scores by roughly 9%
    // relative to minor ones, so borderline major-vs-minor decisions lean major. This is
    // a documented, deliberate tradeoff: normalizing each profile would change the
    // absolute correlation values relied upon by downstream consumers. Comparisons of
    // magnitudes across modes should therefore be treated as approximate and slightly
    // major-biased.

    /// <summary>
    /// Scalar fallback for systems without SIMD. Computes the same pseudo-correlation
    /// (see the NOTE on correlation bias above).
    /// </summary>
    // internal, not private, so a test can run all three kernels side by side on whatever
    // hardware it finds and check they agree: a SIMD kernel that drifts from the scalar
    // reference would otherwise only be caught on the machines that select it.
    internal static void ComputeCorrelationsScalar(ReadOnlySpan<float> input, Span<float> correlations)
    {
        for (var key = 0; key < 24; key++)
        {
            var profile = AllKeyProfiles[key];
            var sum = 0f;

            for (var i = 0; i < 12; i++)
            {
                sum += input[i] * profile[i];
            }

            correlations[key] = sum / 12f;
        }
    }

    /// <summary>
    /// Get the key profile for visualization or advanced analysis.
    /// </summary>
    /// <param name="root">Pitch class of the tonic, 0=C .. 11=B. Folded into that range, as in
    /// <see cref="KeyAnalyzer.GetScaleMask"/>, so -1 is B and 12 is C.</param>
    /// <param name="isMajor">Whether to return the major or the minor profile.</param>
    public static ReadOnlySpan<float> GetKeyProfile(int root, bool isMajor)
    {
        // Fold before indexing, not after. The 24 profiles are a single array — majors then
        // minors — so an unfolded root silently overrides isMajor rather than failing: root 12
        // with isMajor:true landed on index 12 and returned the C *minor* profile, a well-formed
        // answer in the mode the caller did not ask for.
        var pitchClass = PitchMath.Fold(root);
        var index = isMajor ? pitchClass : 12 + pitchClass;
        return AllKeyProfiles[index];
    }

    /// <summary>
    /// Compute how well a chord fits in a given key context.
    /// Returns dot product of chord mask with key profile (higher = better fit).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ChordKeyFit(ushort chordMask, KeySignature key)
    {
        var profile = AllKeyProfiles[key.IsMajor ? key.Root : 12 + key.Root];
        var fit = 0f;

        for (var i = 0; i < 12; i++)
        {
            if ((chordMask & (1 << i)) != 0)
            {
                fit += profile[i];
            }
        }

        return fit;
    }

    /// <summary>
    /// Analyze key changes over time using a sliding window: the key profile of every window of
    /// <paramref name="windowSize"/> whole notes, one window every <paramref name="stepSize"/>,
    /// as a key "trajectory" through the piece. The trajectory's
    /// <see cref="KeyTrajectory.Points"/> are those per-window readings; its
    /// <see cref="KeyTrajectory.DetectModulations"/> says where the key actually changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the statistical road to a piece's keys, and it needs no starting key: every
    /// window is profiled from its notes alone, weighed by duration as
    /// <see cref="DetectFromBuffer"/> weighs them, so the points can be plotted and the opening
    /// key is read from the music. <see cref="ModulationDetector.Analyze(NoteBuffer, KeySignature)"/>
    /// is the harmonic road: it starts from a key the caller knows, reads chords and the line
    /// between them rather than windows, and tells a tonicization from a modulation, names the
    /// type and finds the pivot chord. Both decide where the key changes by the same rules — a
    /// key holds for a phrase, a chord is not a key, a key owns its phrase, its chromatic chords
    /// and non-harmonic tones are its own, an arpeggiated chord is that chord, a secondary
    /// dominant is not a modulation, a key is entered when its own notes return and heard from
    /// where its own chords began — so from the same opening key they place the same
    /// modulations, each at the positions it reads at (this one at its window positions, the
    /// detector at every chord); use this one to see how the key reading moves, and the
    /// detector to have the changes classified.
    /// </para>
    /// <para>
    /// The window is the resolution of the trajectory, not the length a key must hold:
    /// <see cref="KeyTrajectory.DetectModulations"/> judges a change over a phrase (four whole
    /// notes, or the window when that is longer), whatever the window. A two-bar window over
    /// block chords holds two chords and reads as the key of the pitch class they share, which
    /// is why the modulations are no longer read off the points one window at a time.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="windowSize"/> or
    /// <paramref name="stepSize"/> is not positive.</exception>
    public static KeyTrajectory AnalyzeModulations(
        NoteBuffer buffer,
        Rational windowSize,
        Rational stepSize)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (windowSize <= Rational.Zero)
            throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "Window size must be positive");

        if (stepSize <= Rational.Zero)
            throw new ArgumentOutOfRangeException(nameof(stepSize), stepSize, "Step size must be positive");

        var results = new List<(Rational position, KeyDetectionResult result)>();

        var currentPos = Rational.Zero;
        var endTime = GetEndTime(buffer);

        // How many windows this asks for. A step far smaller than the music is long produces a
        // number of them no machine will get through — a step of 1/long.MaxValue over a couple
        // of bars is about 2^62 windows — and the call simply never returned. Refuse it the way
        // a non-positive step is refused, rather than appearing to hang.
        if (endTime > Rational.Zero)
        {
            var windows = (Int128)endTime.Numerator * stepSize.Denominator
                / ((Int128)endTime.Denominator * stepSize.Numerator);
            if (windows > 1_000_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepSize),
                    stepSize,
                    $"A step of {stepSize} across {endTime} of music asks for {windows} windows; " +
                    "use a step in proportion to the passage.");
            }
        }

        // Copy the notes once, sorted by onset, so each window scans only its own slice
        // instead of rescanning the whole buffer for every step (O(windows x notes)).
        var sorted = new NoteEvent[buffer.Count];
        for (var i = 0; i < buffer.Count; i++)
            sorted[i] = buffer.Get(i);
        Array.Sort(sorted, static (a, b) => a.Offset.CompareTo(b.Offset));
        var start = 0;

        // Allocate distribution buffer once, outside loop
        var distribution = new float[12];

        while (currentPos < endTime)
        {
            var windowEnd = currentPos + windowSize;

            // Leading notes that ended at or before this window's start can never overlap
            // any later window either (windows only move forward), so drop them for good.
            while (start < sorted.Length && sorted[start].Offset + sorted[start].Duration <= currentPos)
                start++;

            // Clear and fill distribution
            Array.Clear(distribution);

            for (var i = start; i < sorted.Length; i++)
            {
                var note = sorted[i];
                // Sorted by onset: once a note starts at/after the window end, all later ones do too.
                if (note.Offset >= windowEnd)
                    break;
                // Check if note overlaps with window. Fold like the sibling paths in
                // ExtractPitchClassDistribution and DetectFromPitches: `%` keeps the sign in C#,
                // so a pitch below zero indexed backwards out of the distribution.
                if (note.Offset + note.Duration > currentPos && !Rests.IsRest(note.Pitch))
                {
                    var pitchClass = PitchMath.Fold(note.Pitch);
                    distribution[pitchClass] += (float)note.Duration.ToDouble();
                }
            }

            var result = Detect(distribution);
            results.Add((currentPos, result));

            currentPos += stepSize;
        }

        // The notes go with the points: DetectModulations judges phrases of the music, not the
        // windows' point estimates, and a rest carries no pitch class. Each note keeps its
        // pitch, so that the judge can tell a passing tone — approached and left by step —
        // from a note of the harmony.
        var sonorities = new List<Sonority>(sorted.Length);
        foreach (var note in sorted)
        {
            if (Rests.IsRest(note.Pitch))
                continue;
            sonorities.Add(new Sonority(note.Offset, note.Offset + note.Duration, (ushort)(1 << PitchMath.Fold(note.Pitch)), note.Pitch));
        }

        return new KeyTrajectory(results, sonorities, windowSize);
    }

    /// <summary>
    /// When the music stops sounding. Trailing rests do not extend it: a key trajectory has no
    /// point to make about silence, and windows past the last note reported a key detected from
    /// an empty distribution — a fabricated answer at the end of every passage that ended quietly.
    /// </summary>
    private static Rational GetEndTime(NoteBuffer buffer)
    {
        var maxEnd = Rational.Zero;
        for (var i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            if (Rests.IsRest(note.Pitch)) continue;
            var end = note.Offset + note.Duration;
            if (end > maxEnd) maxEnd = end;
        }
        return maxEnd;
    }
}

/// <summary>
/// Result of key detection analysis.
/// </summary>
/// <param name="Key">The most likely key.</param>
/// <param name="Confidence">
/// Margin between the best key's score and the runner-up's, relative to the best (clamped to 0-1).
/// It is a separation measure, not a goodness-of-fit: it says how cleanly the winner beat the
/// field, not how well the music fits the key. A clear detection typically lands around 0.1-0.35,
/// so a value below 0.5 is not "low confidence".
/// </param>
/// <param name="AllCorrelations">
/// All 24 key correlations, sorted most likely first. A run of keys whose correlations all lie
/// within 1e-5 of the run's strongest is a tie, and a tie is listed in key order — majors C..B,
/// then minors C..B — the same rule that chooses <see cref="Key"/> from a tie; the list leads
/// with <see cref="Key"/>. The order is therefore the same on every machine: the SIMD kernels
/// differ by about 1e-7, so sorting on the raw value alone let the CPU decide which of two
/// symmetric keys came first.
/// </param>
/// <param name="DistinctPitchClasses">
/// How many of the twelve pitch classes the analyzed material actually sounded, 0-12. This is
/// the evidence behind <see cref="Confidence"/>, reported separately because the two answer
/// different questions: a margin can be wide on almost no evidence. Two notes a fifth apart
/// separate their winner from the field about as cleanly as a whole phrase does, because a clean
/// separation among candidates is not the same thing as enough music to decide a key. Fewer than
/// about five distinct pitch classes cannot single out a seven-note scale, whatever the margin
/// reads.
/// </param>
public readonly record struct KeyDetectionResult(
    KeySignature Key,
    float Confidence,
    KeyCorrelation[] AllCorrelations,
    int DistinctPitchClasses = 0)
{
    /// <summary>
    /// Whether the material carries enough distinct pitch classes for a key to be decidable at
    /// all. A seven-note scale cannot be singled out by fewer than five of them, however clean
    /// the <see cref="Confidence"/> margin looks.
    /// </summary>
    /// <remarks>
    /// This is a necessary condition and not a sufficient one — it counts pitch classes and
    /// looks at nothing else. The chromatic aggregate has all twelve and reports
    /// <see langword="true"/> here while its <see cref="Confidence"/> is exactly 0, because
    /// twelve notes fit every key equally. Check both before treating <see cref="Key"/> as a
    /// real detection.
    /// </remarks>
    public bool IsDecidable => DistinctPitchClasses >= 5;

    /// <summary>
    /// The first <paramref name="n"/> keys of <see cref="AllCorrelations"/>: the most likely
    /// keys, a tie among them listed in key order (majors C..B, then minors C..B), so the same
    /// <paramref name="n"/> keys in the same order on every machine.
    /// </summary>
    /// <remarks>
    /// Ties were listed in whichever order the CPU's kernel rounded them into: the augmented
    /// triad C, E, G# ties C# minor, F minor and A minor three ways, and its top five ended in
    /// F minor and A minor on an AVX machine but A minor and C# minor on a scalar one. Over the
    /// 4095 pitch-class subsets the AVX kernels and the scalar one disagreed on 111 top-five
    /// lists. The winner itself was already chosen by key order within a tie; the list now
    /// follows the same rule, so that triad's top five ends in C# minor and F minor everywhere.
    /// </remarks>
    public IEnumerable<KeyCorrelation> TopKeys(int n) => AllCorrelations.Take(n);

    /// <summary>Returns the detected key and confidence percentage (e.g. "C Major (confidence: 82%)").</summary>
    public override string ToString()
    {
        // The key's own name — "Bb Major", not "A# Major" — rather than a sharp table of this
        // class's own, which this renderer and the one below both kept.
        return $"{Key} (confidence: {(int)Math.Round(Confidence * 100)}%)";
    }
}

/// <summary>
/// Correlation of input with a specific key profile.
/// </summary>
/// <param name="Key">The key whose profile was correlated.</param>
/// <param name="Correlation">The correlation score for that key.</param>
public readonly record struct KeyCorrelation(KeySignature Key, float Correlation)
{
    /// <summary>Returns the key name and correlation score (e.g. "C Major: 0.812").</summary>
    public override string ToString()
    {
        // Invariant, so the example in the summary is what a caller gets on every machine: under
        // a locale with a comma for a decimal separator this returned "C Major: 0,812".
        return FormattableString.Invariant($"{Key}: {Correlation:F3}");
    }
}

/// <summary>
/// Key changes over time in a piece: the per-window key readings of
/// <see cref="KeyProfiler.AnalyzeModulations"/>, and the modulations judged from the music
/// behind them.
/// </summary>
public sealed class KeyTrajectory
{
    private readonly IReadOnlyList<Sonority> _sonorities;
    private readonly Rational _windowSize;

    // Produced by key-trajectory analysis; not constructible by consumers (#18 API freeze).
    internal KeyTrajectory(List<(Rational, KeyDetectionResult)> points, IReadOnlyList<Sonority> sonorities, Rational windowSize)
    {
        Points = points;
        _sonorities = sonorities;
        _windowSize = windowSize;
    }

    /// <summary>
    /// The per-window detection results: the start position of each analysis window
    /// together with the key detected in it, in chronological order.
    /// </summary>
    public IReadOnlyList<(Rational Position, KeyDetectionResult Result)> Points { get; }

    /// <summary>
    /// Detect modulation points: where the music settles in a new key. Each is reported at the
    /// start of the whole note in which the new key begins — the first note the old key does
    /// not own, moved back to the bar line across notes both keys own — with the key left and
    /// the key arrived at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A change is a modulation when a phrase — four whole notes, or the analysis window when
    /// that is longer — read from where the new key begins is decidable, names the new key
    /// clearly, fits it better than the key the music was in, sounds a note the new key owns and
    /// the old does not, and is owned by the new key — the notes it lacks amounting to less than
    /// a quarter note in any bar, its own chromatic chords counted as the key's (an applied
    /// chord, a major triad or a dominant seventh resolving down a fifth into a chord of the key;
    /// a borrowed chord, a major key's minor subdominant, flat sixth or flat seventh resolving
    /// into a chord of the key, both inside a phrase the key's tonic frames; a minor key's
    /// Picardy third closing the piece) and its passing tones, neighbour tones and
    /// appoggiaturas weighing nothing; when the new key still reads from there through the
    /// phrase, or to the end of the piece closing on its tonic from a key the music was still
    /// in; and when the new key's own notes return in a second bar before the old key's are
    /// heard again, or the phrase is framed by the new tonic chord — in a melody, by a note of
    /// the tonic triad at its start and the tonic at its close. A key change that does not hold
    /// that long is a tonicization — an applied dominant, a borrowed chord — and is not reported
    /// here; <see cref="ModulationDetector"/> reports those, as <see cref="ModulationType.Tonicization"/>.
    /// The new key begins after the last note it does not own, on a chord of its own — not on
    /// one of its applied or borrowed chords: at its pivot chord when the bar before is its, or
    /// at the start of the phrase — phrases counted from the first note — in which its own note
    /// first sounds, when it owns every bar from there. The opening key is
    /// the key of the chord the piece opens on when that key owns the opening phrase as well as
    /// any key does, else the key the opening phrase sounds like, extended a phrase at a time
    /// while it cannot decide, or the whole piece as a last resort; a change placed at the first
    /// note is that key heard better, not a modulation, and so is a change from an opening key
    /// read off the profile alone that never sounded a note of its own.
    /// </para>
    /// <para>
    /// This used to report a modulation at every confident, decidable window whose key differed
    /// from the previous such window's — a decision made on one window at a time. At the two-bar
    /// window a block-chord passage is read at, a window holds two chords, and two chords read
    /// as the key of the pitch class they share: C–F read C, F–G7 read F, G–C read G, D7–G read
    /// D. Four bars of I IV V I in C followed by four in D flat came back as four modulations
    /// (C to G, G to F minor, F minor to D flat, D flat to A flat); a twelve-bar blues, a minor
    /// phrase with its raised leading tone and I V7/V V I came back with three or four each,
    /// none of them a modulation; and a bare melody restated a semitone higher came back with
    /// none. Judged on forty passages a musician wrote — nursery tunes and textbook
    /// modulations in block chords, arpeggios, melody alone and melody over chords — it was
    /// wrong on thirty-four; judged by phrase it agrees with the musician on all forty, in
    /// every key.
    /// </para>
    /// <para>
    /// Judged by phrase alone, on sixty-four further passages a reviewer wrote — a pop verse
    /// of applied dominants, keys visited for two bars each, a chromatic scale, alternating
    /// four-bar areas, real tunes with their chords — it was wrong on eight: a key that left
    /// less of a phrase foreign than the key the music was in was taken for the phrase's key,
    /// so C Am D7 G | C A7 Dm G7 opened in G and came home at its Dm, a passage visiting D and
    /// E for two bars each read as E then F sharp, a chromatic scale in eighths turned to C
    /// minor, and C F G C | G C D7 G | C F G C, measured from the D7, held G for two bars and
    /// was no modulation at all. A key must own the phrase it is named for, be entered by its
    /// own notes returning or by a phrase framed by its tonic, and begin with the phrase in
    /// which it is heard; both roads agree with the musician on all sixty-four. Two hundred
    /// random diatonic melodies in C, which the profile opens in A minor, E minor or G as
    /// readily as in C, reported thirty-six modulations to C; they report none, and two
    /// hundred more from another seed, which reported forty-five, report two (melodies whose
    /// second half never sounds F, so that G major owns it outright).
    /// </para>
    /// <para>
    /// With only dominant sevenths counted as a key's applied chords and every note weighed
    /// alike, on thirty-eight passages a second reviewer wrote it was wrong on seven, four of
    /// them melodies: two chromatic passing eighths in each bar of a G-major melody weighed a
    /// quarter of the bar, so a melody alone or over chords with that in every bar named no key;
    /// a chromatic quarter-note neighbour under a D7 put G at bar 8 where a musician hears 4,
    /// the detector — which never saw the melody's eighths — at 4; a passing F natural in E F F
    /// sharp G placed G three eighths into its bar; C F G C | G E Am D7 | G C D7 G and C F G C |
    /// G Cm D7 G | G C D7 G reached G two bars late; and Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭ G7 C,
    /// closing on a Picardy third, never came home to C minor. A passing tone is not a foreign
    /// note, a key's chromatic chords are its own, the Picardy third is the minor key's cadence,
    /// and the detector now hears the line its chords carry; both roads agree with the musician
    /// on all thirty-eight, and with each other on every passage of the three tables.
    /// </para>
    /// <para>
    /// On thirty-one passages a third reviewer wrote it was wrong on twelve: a chromatic
    /// appoggiatura struck with the chord on every downbeat of the new key, a passing tone
    /// inside an arch (G A A♭ G) and a half-note passing tone each named no key; the German
    /// sixth in the return's second bar put C minor two bars late; a bar of B flat quoted inside
    /// a G phrase put G two bars late; the V/V of a new key that the old key owns (the G of F B♭
    /// G C7, the C of B♭ E♭ C F) and a tonic seventh closing a phrase (B♭ E♭ F B♭7) put the new
    /// key a phrase late or named E flat; a 4-3 suspension or a chord tone struck over G's V/ii
    /// made it no plain triad; and the V/ii and the borrowed iv arpeggiated in eighths were three
    /// foreign eighths a bar. An appoggiatura is defined by its resolution, whether the chord
    /// was struck before it or with it; a note where the line turns is structural; a note that
    /// leans on a sounding harmony may be long; the augmented sixth and the tonic seventh are a
    /// key's chromatic chords; a chord the key in force owns is that key's only while the key
    /// stands and the chord's resolution is its own; a key is heard from where its own chords
    /// began, a bar of a foreign key inside it a parenthesis; a line note that is a tone of the
    /// chord under it is that chord's, and a chord struck as several notes weighs as one; and an
    /// arpeggiated chord is that chord. Both roads agree with the musician on all thirty-one, and
    /// with each other on every passage of the four tables.
    /// </para>
    /// <para>
    /// On twenty-three passages a fourth reviewer wrote it was wrong on one: a chorale's two-bar
    /// return home, Dm G7 C, sounded no C before its last chord, and a stretch at the end
    /// shorter than a phrase had to — so the homecoming was heard as nothing. A return to a key
    /// the music has been in is a homecoming, and needs no such confirmation. And on this road
    /// a melody over staccato chords — each chord a quarter, then silence — lost its
    /// appoggiaturas, the harmony they leaned on having stopped sounding before they resolved,
    /// so G began four bars late or not at all where the detector, whose chords then sounded
    /// until the next chord, placed it at the bar: a chord's harmony holds until the next chord,
    /// a whole note past its notes at most, and a note of the line leans on that. Both roads
    /// agree with the musician on all twenty-three, and with each other on every passage of the
    /// five tables.
    /// </para>
    /// <para>
    /// On thirty-three passages a sixth reviewer wrote — accompaniment textures, and the pop
    /// loop told either of its keys — it was wrong on five: a bar of silence inside the new
    /// key's first phrase put G at bar 9, where a musician hears it from bar 5; the Picardy
    /// close with the melody arpeggiating up through the final chord was a modulation to A
    /// major; and Am F C G four times was a modulation to C at bar 2 told A minor, and told C
    /// too on this road, which opens on the A minor chord where the detector was told C and
    /// heard none — the relative major named by the profile with no cadence and no frame. A
    /// key is heard from where its phrases began and a silent bar is neither key's, a melody
    /// moving through the tones of the closing chord is that chord, and the relative major is
    /// confirmed by its cadence or its frame. Both roads agree with the musician on all
    /// thirty-three, and with each other on every passage of the seven tables.
    /// </para>
    /// <para>
    /// On thirty-five passages a seventh reviewer wrote — the pop loops a musician plays every
    /// day, told either of their keys; the same loop a tone up; a loop turning into a real move to
    /// the relative key; silence around the new key's first phrase; the melody over the Picardy
    /// chord — the frame reaching back had swallowed a phrase of V I V I closing on the old tonic (G
    /// at bar 5 for bar 9), the cadence that confirms the relative major had been found at the seam
    /// of two phrases (C at bar 5 for bar 9), and a scale run or a turn over the closing chord had
    /// kept it from ending the piece (a modulation to A major again). A reached-back phrase is
    /// framed by the new tonic, opening and closing on it; a confirming cadence closes a phrase of
    /// the piece's count; notes on their way by step between the closing chord's tones are that
    /// chord's. Both roads agree with the musician on the twenty-four that join the fixture as an
    /// eighth table, and with each other on every passage of the eight tables.
    /// </para>
    /// <para>
    /// The window no longer limits what is heard: a one-bar window over arpeggiated triads has
    /// undecidable points, and the modulation is still found, because the phrase is read from
    /// the notes. The points remain the per-window readings they always were.
    /// </para>
    /// </remarks>
    public IEnumerable<(Rational Position, KeySignature FromKey, KeySignature ToKey)> DetectModulations()
    {
        if (Points.Count == 0)
            yield break;

        var candidates = new Rational[Points.Count];
        for (var i = 0; i < candidates.Length; i++)
            candidates[i] = Points[i].Position;

        var phrase = _windowSize > KeyAreaJudge.Phrase ? _windowSize : KeyAreaJudge.Phrase;

        foreach (var change in KeyAreaJudge.Judge(_sonorities, candidates, startKey: null, phrase).Changes)
        {
            if (change.Established)
                yield return (change.Position, change.From, change.To);
        }
    }
}
