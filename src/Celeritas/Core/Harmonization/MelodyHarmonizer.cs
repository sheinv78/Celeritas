// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Harmonizes melodies using dynamic programming (Viterbi-style).
/// Fully extensible via strategy interfaces.
/// </summary>
/// <remarks>
/// Create a harmonizer with custom strategies.
/// </remarks>
public sealed class MelodyHarmonizer(
    IChordCandidateProvider candidateProvider,
    ITransitionScorer transitionScorer,
    IMelodyFitScorer fitScorer,
    IHarmonicRhythmStrategy rhythmStrategy)
{
    private readonly IChordCandidateProvider _candidateProvider = candidateProvider ?? throw new ArgumentNullException(nameof(candidateProvider));
    private readonly ITransitionScorer _transitionScorer = transitionScorer ?? throw new ArgumentNullException(nameof(transitionScorer));
    private readonly IMelodyFitScorer _fitScorer = fitScorer ?? throw new ArgumentNullException(nameof(fitScorer));
    private readonly IHarmonicRhythmStrategy _rhythmStrategy = rhythmStrategy ?? throw new ArgumentNullException(nameof(rhythmStrategy));

    /// <summary>
    /// Create a harmonizer with default strategies.
    /// </summary>
    public MelodyHarmonizer()
        : this(new DefaultChordCandidateProvider(),
               new DefaultTransitionScorer(),
               new DefaultTransitionScorer(), // implements both interfaces
               new DefaultHarmonicRhythmStrategy())
    {
    }

    /// <summary>
    /// Harmonize a melody, automatically detecting the key.
    /// </summary>
    public HarmonizationResult Harmonize(ReadOnlySpan<NoteEvent> melody)
    {
        if (melody.IsEmpty)
        {
            return new HarmonizationResult { Key = new KeySignature(0, true), TotalCost = 0 };
        }

        // Detect key from melody pitches, skipping rests: RestPitch (-1) folds to a B and
        // would vote for a note that is not in the melody at all.
        Span<int> pitches = melody.Length <= StackAlloc.MaxInts
            ? stackalloc int[melody.Length]
            : new int[melody.Length];
        var pitchCount = 0;
        for (var i = 0; i < melody.Length; i++)
        {
            if (melody[i].Pitch == MusicNotation.RestPitch)
                continue;

            pitches[pitchCount++] = melody[i].Pitch;
        }

        if (pitchCount == 0)
        {
            return new HarmonizationResult { Key = new KeySignature(0, true), TotalCost = 0 };
        }

        var key = KeyAnalyzer.IdentifyKey(pitches[..pitchCount]);
        return Harmonize(melody, key);
    }

    /// <summary>
    /// Harmonize a melody in a specified key.
    /// </summary>
    public HarmonizationResult Harmonize(ReadOnlySpan<NoteEvent> melody, KeySignature key)
    {
        if (melody.IsEmpty)
        {
            return new HarmonizationResult { Key = key, TotalCost = 0 };
        }

        // Rests are not harmonized. MusicNotation.Parse marks them with RestPitch (-1), which
        // folds to a B, so a rest used to take a slice of its own and be given a chord built
        // on a note nobody played.
        var sounding = WithoutRests(melody);
        if (sounding.Length == 0)
        {
            return new HarmonizationResult { Key = key, TotalCost = 0 };
        }

        melody = sounding;

        // 1. Segment melody into time slices
        var slices = _rhythmStrategy.Segment(melody);
        if (slices.Count == 0)
        {
            return new HarmonizationResult { Key = key, TotalCost = 0 };
        }

        // 2. Generate candidates for each slice
        var candidatesPerSlice = new List<List<ChordCandidate>>(slices.Count);
        var context = new HarmonizationContext();

        foreach (var slice in slices)
        {
            var candidates = _candidateProvider
                .GetCandidates(slice.Pitches, key, context)
                .ToList();

            // Ensure at least one candidate (fallback to tonic)
            if (candidates.Count == 0)
            {
                // Root-position tonic: third and fifth stacked above the root (folding
                // each tone into octave 4 independently produced arbitrary inversions).
                var tonicRoot = 60 + key.Root;
                var tonicPitches = new[] { tonicRoot, tonicRoot + (key.IsMajor ? 4 : 3), tonicRoot + 7 };
                var tonicChord = ChordAnalyzer.Identify(tonicPitches);
                candidates.Add(new ChordCandidate(tonicChord, tonicPitches, 1.0f));
            }

            candidatesPerSlice.Add(candidates);
            context.StepIndex++;
        }

        // 3. Dynamic programming (Viterbi)
        var n = slices.Count;
        var costs = new float[n][];
        var backpointers = new int[n][];

        // Initialize first slice
        costs[0] = new float[candidatesPerSlice[0].Count];
        backpointers[0] = new int[candidatesPerSlice[0].Count];
        for (var j = 0; j < candidatesPerSlice[0].Count; j++)
        {
            var candidate = candidatesPerSlice[0][j];
            costs[0][j] = _fitScorer.ScoreFit(candidate, slices[0].Pitches, slices[0].IsStrongBeat);
            backpointers[0][j] = -1;
        }

        // Forward pass
        for (var i = 1; i < n; i++)
        {
            var prevCandidates = candidatesPerSlice[i - 1];
            var currCandidates = candidatesPerSlice[i];

            costs[i] = new float[currCandidates.Count];
            backpointers[i] = new int[currCandidates.Count];

            for (var j = 0; j < currCandidates.Count; j++)
            {
                var currCandidate = currCandidates[j];
                var bestCost = float.MaxValue;
                var bestPrev = 0;

                for (var k = 0; k < prevCandidates.Count; k++)
                {
                    var prevCandidate = prevCandidates[k];
                    var transitionCost = _transitionScorer.ScoreTransition(prevCandidate, currCandidate, key);
                    var totalCost = costs[i - 1][k] + transitionCost;

                    if (totalCost < bestCost)
                    {
                        bestCost = totalCost;
                        bestPrev = k;
                    }
                }

                var fitCost = _fitScorer.ScoreFit(currCandidate, slices[i].Pitches, slices[i].IsStrongBeat);
                costs[i][j] = bestCost + fitCost;
                backpointers[i][j] = bestPrev;
            }
        }

        // 4. Backtrack to find best path
        var path = new int[n];
        var minFinalCost = float.MaxValue;
        for (var j = 0; j < candidatesPerSlice[n - 1].Count; j++)
        {
            if (costs[n - 1][j] < minFinalCost)
            {
                minFinalCost = costs[n - 1][j];
                path[n - 1] = j;
            }
        }

        for (var i = n - 1; i > 0; i--)
        {
            path[i - 1] = backpointers[i][path[i]];
        }

        // 5. Build result
        var assignments = new ChordAssignment[n];
        for (var i = 0; i < n; i++)
        {
            var candidate = candidatesPerSlice[i][path[i]];
            assignments[i] = new ChordAssignment(
                slices[i].Start,
                slices[i].End,
                candidate.Chord,
                candidate.Pitches);
        }

        return new HarmonizationResult
        {
            Key = key,
            Chords = assignments,
            TotalCost = minFinalCost
        };
    }

    /// <summary>
    /// Harmonize from a NoteBuffer.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public HarmonizationResult Harmonize(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var notes = new NoteEvent[buffer.Count];
        for (var i = 0; i < buffer.Count; i++)
        {
            notes[i] = buffer.Get(i);
        }

        return Harmonize(notes);
    }

    /// <summary>
    /// Harmonize from a NoteBuffer with a specified key.
    /// </summary>
    /// <summary>
    /// Returns the melody with rests removed, or the original span when it has none (the
    /// common case, which then costs no allocation).
    /// </summary>
    private static ReadOnlySpan<NoteEvent> WithoutRests(ReadOnlySpan<NoteEvent> melody)
    {
        var rests = 0;
        for (var i = 0; i < melody.Length; i++)
        {
            if (melody[i].Pitch == MusicNotation.RestPitch)
                rests++;
        }

        if (rests == 0)
            return melody;

        var sounding = new NoteEvent[melody.Length - rests];
        var next = 0;
        for (var i = 0; i < melody.Length; i++)
        {
            if (melody[i].Pitch != MusicNotation.RestPitch)
                sounding[next++] = melody[i];
        }

        return sounding;
    }

    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public HarmonizationResult Harmonize(NoteBuffer buffer, KeySignature key)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var notes = new NoteEvent[buffer.Count];
        for (var i = 0; i < buffer.Count; i++)
        {
            notes[i] = buffer.Get(i);
        }

        return Harmonize(notes, key);
    }

    /// <summary>
    /// Harmonize from a NoteEvent array (convenience overload).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="melody"/> is <see langword="null"/>.</exception>
    public HarmonizationResult Harmonize(NoteEvent[] melody)
    {
        // AsSpan() turns null into an empty span rather than throwing, which the span
        // overload then answers with a well-formed "C major, cost 0, no chords" result —
        // a null melody reported as successfully harmonized, indistinguishable from a
        // legitimately empty one.
        ArgumentNullException.ThrowIfNull(melody);
        return Harmonize(melody.AsSpan());
    }

    /// <summary>
    /// Harmonize from a NoteEvent array with a specified key (convenience overload).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="melody"/> is <see langword="null"/>.</exception>
    public HarmonizationResult Harmonize(NoteEvent[] melody, KeySignature key)
    {
        ArgumentNullException.ThrowIfNull(melody);
        return Harmonize(melody.AsSpan(), key);
    }
}
