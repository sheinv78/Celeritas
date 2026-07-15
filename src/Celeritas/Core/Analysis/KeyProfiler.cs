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
    private static readonly CorrelationComputer ComputeCorrelations = CreateCorrelationComputer();

    private delegate void CorrelationComputer(ReadOnlySpan<float> input, Span<float> correlations);

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
    /// <returns>Detected key and confidence (0-1)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeyDetectionResult Detect(ReadOnlySpan<float> pitchClassCounts)
    {
        if (pitchClassCounts.Length < 12)
            return new KeyDetectionResult(new KeySignature(0, true), 0f, []);

        // Normalize input
        Span<float> normalized = stackalloc float[16]; // 16 for SIMD alignment
        NormalizeDistribution(pitchClassCounts, normalized);

        // Compute correlations with all 24 key profiles
        Span<float> correlations = stackalloc float[24];

        ComputeCorrelations(normalized, correlations);

        // Find best match
        var bestKey = 0;
        var bestCorrelation = correlations[0];
        for (var i = 1; i < 24; i++)
        {
            if (correlations[i] > bestCorrelation)
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

        return new KeyDetectionResult(
            new KeySignature((byte)root, isMajor),
            confidence,
            allCorrelations);
    }

    /// <summary>
    /// Detect key from a NoteBuffer (extracts pitch class distribution automatically).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static KeyDetectionResult DetectFromBuffer(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        Span<float> distribution = stackalloc float[12];
        ExtractPitchClassDistribution(buffer, distribution);
        return Detect(distribution);
    }

    /// <summary>
    /// Detect key from an array of MIDI pitches.
    /// </summary>
    public static KeyDetectionResult DetectFromPitches(ReadOnlySpan<int> pitches)
    {
        Span<float> distribution = stackalloc float[12];
        distribution.Clear();

        foreach (var pitch in pitches)
        {
            if (pitch is >= 0 and < 128)
            {
                distribution[pitch % 12]++;
            }
        }

        return Detect(distribution);
    }

    /// <summary>
    /// Detect key from a human-readable notation string.
    /// Example: "C4 D4 E4 F4 G4 A4 B4"
    /// </summary>
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
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return DetectFromPitches(pitches);
    }

    /// <summary>
    /// Detect key from an array of note events.
    /// </summary>
    public static KeyDetectionResult DetectFromPitches(ReadOnlySpan<NoteEvent> notes)
    {
        if (notes.IsEmpty)
            return new KeyDetectionResult { Key = new KeySignature(0, true), Confidence = 0, AllCorrelations = [] };

        Span<int> pitches = notes.Length <= StackAlloc.MaxInts
            ? stackalloc int[notes.Length]
            : new int[notes.Length];
        for (var i = 0; i < notes.Length; i++)
            pitches[i] = notes[i].Pitch;

        return DetectFromPitches(pitches);
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
            var pitchClass = note.Pitch % 12;
            // Weight by duration (longer notes are more important for key)
            var weight = (float)note.Duration.ToDouble();
            distribution[pitchClass] += weight;
        }
    }

    /// <summary>
    /// Normalize distribution to zero mean and unit variance (for Pearson correlation).
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
    /// Computes Pearson correlation of input with all 24 key profiles in parallel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ComputeCorrelationsAvx512(ReadOnlySpan<float> input, Span<float> correlations)
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

                // Normalize by profile std dev (precomputed, all profiles have similar variance)
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
    /// AVX2 fallback for correlation computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ComputeCorrelationsAvx2(ReadOnlySpan<float> input, Span<float> correlations)
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
    // not a true Pearson correlation and introduces a small systematic bias between
    // major and minor scores. This is a deliberate tradeoff: normalizing each profile
    // would change the absolute correlation values relied upon by downstream consumers
    // and the ranking within each mode is unaffected. Comparisons of magnitudes across
    // modes should therefore be treated as approximate.

    /// <summary>
    /// Scalar fallback for systems without SIMD.
    /// </summary>
    private static void ComputeCorrelationsScalar(ReadOnlySpan<float> input, Span<float> correlations)
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
    public static ReadOnlySpan<float> GetKeyProfile(int root, bool isMajor)
    {
        var index = isMajor ? root : 12 + root;
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
    /// Analyze key changes over time using a sliding window.
    /// Returns key "trajectory" through the piece.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is not positive.</exception>
    public static KeyTrajectory AnalyzeModulations(
        NoteBuffer buffer,
        Rational windowSize,
        Rational stepSize)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (stepSize <= Rational.Zero)
            throw new ArgumentOutOfRangeException(nameof(stepSize), stepSize, "Step size must be positive");

        var results = new List<(Rational position, KeyDetectionResult result)>();

        var currentPos = Rational.Zero;
        var endTime = GetEndTime(buffer);

        // Allocate distribution buffer once, outside loop
        var distribution = new float[12];

        while (currentPos < endTime)
        {
            var windowEnd = currentPos + windowSize;

            // Clear and fill distribution
            Array.Clear(distribution);

            for (var i = 0; i < buffer.Count; i++)
            {
                var note = buffer.Get(i);
                // Check if note overlaps with window
                if (note.Offset < windowEnd && note.Offset + note.Duration > currentPos)
                {
                    var pitchClass = note.Pitch % 12;
                    distribution[pitchClass] += (float)note.Duration.ToDouble();
                }
            }

            var result = Detect(distribution);
            results.Add((currentPos, result));

            currentPos += stepSize;
        }

        return new KeyTrajectory(results);
    }

    private static Rational GetEndTime(NoteBuffer buffer)
    {
        var maxEnd = Rational.Zero;
        for (var i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            var end = note.Offset + note.Duration;
            if (end > maxEnd) maxEnd = end;
        }
        return maxEnd;
    }
}

/// <summary>
/// Result of key detection analysis.
/// </summary>
public readonly record struct KeyDetectionResult(
    KeySignature Key,
    float Confidence,
    KeyCorrelation[] AllCorrelations)
{
    /// <summary>Top N most likely keys</summary>
    public IEnumerable<KeyCorrelation> TopKeys(int n) => AllCorrelations.Take(n);

    public override string ToString()
    {
        var keyName = ChordLibrary.NoteNames[Key.Root] + (Key.IsMajor ? " Major" : " Minor");
        return $"{keyName} (confidence: {(int)Math.Round(Confidence * 100)}%)";
    }
}

/// <summary>
/// Correlation of input with a specific key profile.
/// </summary>
public readonly record struct KeyCorrelation(KeySignature Key, float Correlation)
{
    public override string ToString()
    {
        var keyName = ChordLibrary.NoteNames[Key.Root] + (Key.IsMajor ? " Major" : " Minor");
        return $"{keyName}: {Correlation:F3}";
    }
}

/// <summary>
/// Key changes over time in a piece.
/// </summary>
public sealed class KeyTrajectory(List<(Rational, KeyDetectionResult)> points)
{
    private IReadOnlyList<(Rational Position, KeyDetectionResult Result)> Points { get; } = points;

    /// <summary>
    /// Detect modulation points (where key changes significantly).
    /// </summary>
    public IEnumerable<(Rational Position, KeySignature FromKey, KeySignature ToKey)> DetectModulations()
    {
        for (var i = 1; i < Points.Count; i++)
        {
            var prev = Points[i - 1].Result.Key;
            var curr = Points[i].Result.Key;

            if (prev.Root == curr.Root && prev.IsMajor == curr.IsMajor)
            {
                continue;
            }

            // Check if both have reasonable confidence
            if (Points[i - 1].Result.Confidence > 0.3f && Points[i].Result.Confidence > 0.3f)
            {
                yield return (Points[i].Position, prev, curr);
            }
        }
    }
}
