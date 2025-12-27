// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Harmonization;

/// <summary>
/// Harmonizes melodies using dynamic programming (Viterbi-style).
/// Fully extensible via strategy interfaces.
/// </summary>
public sealed class MelodyHarmonizer
{
    private readonly IChordCandidateProvider _candidateProvider;
    private readonly ITransitionScorer _transitionScorer;
    private readonly IMelodyFitScorer _fitScorer;
    private readonly IHarmonicRhythmStrategy _rhythmStrategy;

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
    /// Create a harmonizer with custom strategies.
    /// </summary>
    public MelodyHarmonizer(
        IChordCandidateProvider candidateProvider,
        ITransitionScorer transitionScorer,
        IMelodyFitScorer fitScorer,
        IHarmonicRhythmStrategy rhythmStrategy)
    {
        _candidateProvider = candidateProvider ?? throw new ArgumentNullException(nameof(candidateProvider));
        _transitionScorer = transitionScorer ?? throw new ArgumentNullException(nameof(transitionScorer));
        _fitScorer = fitScorer ?? throw new ArgumentNullException(nameof(fitScorer));
        _rhythmStrategy = rhythmStrategy ?? throw new ArgumentNullException(nameof(rhythmStrategy));
    }

    /// <summary>
    /// Harmonize a melody, automatically detecting the key.
    /// </summary>
    public HarmonizationResult Harmonize(ReadOnlySpan<NoteEvent> melody)
    {
        if (melody.IsEmpty)
            return new HarmonizationResult { Key = new KeySignature(0, true), TotalCost = 0 };

        // Detect key from melody pitches
        Span<int> pitches = stackalloc int[melody.Length];
        for (var i = 0; i < melody.Length; i++)
            pitches[i] = melody[i].Pitch;

        var key = KeyAnalyzer.IdentifyKey(pitches);
        return Harmonize(melody, key);
    }

    /// <summary>
    /// Harmonize a melody in a specified key.
    /// </summary>
    public HarmonizationResult Harmonize(ReadOnlySpan<NoteEvent> melody, KeySignature key)
    {
        if (melody.IsEmpty)
            return new HarmonizationResult { Key = key, TotalCost = 0 };

        // 1. Segment melody into time slices
        var slices = _rhythmStrategy.Segment(melody);
        if (slices.Count == 0)
            return new HarmonizationResult { Key = key, TotalCost = 0 };

        // 2. Generate candidates for each slice
        var candidatesPerSlice = new List<List<ChordCandidate>>(slices.Count);
        var context = new HarmonizationContext { Key = key };

        foreach (var slice in slices)
        {
            var candidates = _candidateProvider
                .GetCandidates(slice.Pitches, key, context)
                .ToList();

            // Ensure at least one candidate (fallback to tonic)
            if (candidates.Count == 0)
            {
                var tonicPitches = new[] { 60 + key.Root, 60 + (key.Root + (key.IsMajor ? 4 : 3)) % 12, 60 + (key.Root + 7) % 12 };
                var tonicChord = ChordAnalyzer.Identify(tonicPitches);
                candidates.Add(new ChordCandidate(tonicChord, tonicPitches, 1.0f, "fallback"));
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
                candidate.Pitches,
                costs[i][path[i]],
                candidate.Rationale);
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
    public HarmonizationResult Harmonize(NoteBuffer buffer)
    {
        var notes = new NoteEvent[buffer.Count];
        for (var i = 0; i < buffer.Count; i++)
            notes[i] = buffer.Get(i);
        return Harmonize(notes);
    }

    /// <summary>
    /// Harmonize from a NoteBuffer with a specified key.
    /// </summary>
    public HarmonizationResult Harmonize(NoteBuffer buffer, KeySignature key)
    {
        var notes = new NoteEvent[buffer.Count];
        for (var i = 0; i < buffer.Count; i++)
            notes[i] = buffer.Get(i);
        return Harmonize(notes, key);
    }

    /// <summary>
    /// Harmonize from a NoteEvent array (convenience overload).
    /// </summary>
    public HarmonizationResult Harmonize(NoteEvent[] melody) => Harmonize(melody.AsSpan());

    /// <summary>
    /// Harmonize from a NoteEvent array with a specified key (convenience overload).
    /// </summary>
    public HarmonizationResult Harmonize(NoteEvent[] melody, KeySignature key) => Harmonize(melody.AsSpan(), key);
}
