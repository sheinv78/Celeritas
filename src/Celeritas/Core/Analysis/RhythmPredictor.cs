// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Rhythm predictor using Markov chains and N-gram models.
/// </summary>
public sealed class RhythmPredictor
{
    private readonly Dictionary<string, Dictionary<Rational, float>> _transitions = new();
    private readonly int _order;
    private readonly Random _random;

    /// <summary>
    /// Create a new rhythm predictor.
    /// </summary>
    /// <param name="order">Markov chain order (1 = first-order, 2 = second-order, etc.)</param>
    /// <param name="seed">Random seed for reproducibility (null = random)</param>
    public RhythmPredictor(int order = 2, int? seed = null)
    {
        _order = Math.Max(1, order);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Train the predictor on a sequence of durations.
    /// </summary>
    public void Train(IReadOnlyList<Rational> durations)
    {
        if (durations.Count <= _order)
            return;

        for (int i = _order; i < durations.Count; i++)
        {
            var context = GetContext(durations, i - _order, _order);
            var next = durations[i];

            if (!_transitions.TryGetValue(context, out var dist))
            {
                dist = new Dictionary<Rational, float>();
                _transitions[context] = dist;
            }

            dist.TryGetValue(next, out var count);
            dist[next] = count + 1;
        }

        // Normalize probabilities
        foreach (var (_, dist) in _transitions)
        {
            var total = dist.Values.Sum();
            foreach (var key in dist.Keys.ToList())
            {
                dist[key] /= total;
            }
        }
    }

    /// <summary>
    /// Train from a NoteBuffer.
    /// </summary>
    public void Train(NoteBuffer buffer)
    {
        var durations = new List<Rational>();
        for (int i = 0; i < buffer.Count; i++)
        {
            durations.Add(buffer.GetDuration(i));
        }
        Train(durations);
    }

    /// <summary>
    /// Predict the next duration given recent context.
    /// </summary>
    public RhythmPrediction Predict(IReadOnlyList<Rational> recentDurations)
    {
        if (recentDurations.Count < _order)
        {
            return new RhythmPrediction
            {
                MostLikely = Rational.Quarter,
                Confidence = 0,
                Alternatives = [],
                ContextFound = false
            };
        }

        var context = GetContext(recentDurations, recentDurations.Count - _order, _order);

        if (!_transitions.TryGetValue(context, out var dist) || dist.Count == 0)
        {
            // Fall back to shorter context
            return FallbackPredict(recentDurations);
        }

        var sorted = dist.OrderByDescending(kv => kv.Value).ToList();
        var best = sorted[0];

        return new RhythmPrediction
        {
            MostLikely = best.Key,
            Confidence = best.Value,
            Alternatives = sorted.Skip(1).Take(4).Select(kv =>
                new RhythmAlternative { Duration = kv.Key, Probability = kv.Value }).ToList(),
            ContextFound = true
        };
    }

    /// <summary>
    /// Generate a rhythm sequence.
    /// </summary>
    public List<Rational> Generate(IReadOnlyList<Rational> seed, int length)
    {
        var result = seed.ToList();

        for (int i = 0; i < length; i++)
        {
            var next = SampleNext(result);
            result.Add(next);
        }

        return result.Skip(seed.Count).ToList();
    }

    /// <summary>
    /// Generate a complete measure.
    /// </summary>
    public List<Rational> GenerateMeasure(IReadOnlyList<Rational> seed, TimeSignature meter)
    {
        var result = new List<Rational>();
        var target = meter.MeasureDuration;
        var current = Rational.Zero;
        var context = seed.ToList();

        while (current < target)
        {
            var next = SampleNext(context);

            // Ensure we don't exceed measure
            var remaining = target - current;
            if (next > remaining)
                next = remaining;

            if (next <= Rational.Zero)
                break;

            result.Add(next);
            context.Add(next);
            if (context.Count > _order)
                context.RemoveAt(0);

            current = current + next;
        }

        return result;
    }

    /// <summary>
    /// Get model statistics.
    /// </summary>
    public RhythmModelStats GetStats()
    {
        return new RhythmModelStats
        {
            Order = _order,
            UniqueContexts = _transitions.Count,
            TotalTransitions = _transitions.Values.Sum(d => d.Count),
            MostCommonDurations = _transitions
                .SelectMany(kv => kv.Value)
                .GroupBy(kv => kv.Key)
                .Select(g => (g.Key, g.Sum(x => x.Value)))
                .OrderByDescending(x => x.Item2)
                .Take(5)
                .Select(x => x.Key)
                .ToList()
        };
    }

    private string GetContext(IReadOnlyList<Rational> durations, int start, int length)
    {
        var parts = new List<string>();
        for (int i = 0; i < length && start + i < durations.Count; i++)
        {
            parts.Add(durations[start + i].ToString());
        }
        return string.Join("|", parts);
    }

    private Rational SampleNext(IReadOnlyList<Rational> context)
    {
        if (context.Count < _order)
        {
            // Not enough context, return common duration
            return Rational.Quarter;
        }

        var key = GetContext(context, context.Count - _order, _order);

        if (!_transitions.TryGetValue(key, out var dist) || dist.Count == 0)
        {
            // Try shorter context
            for (int o = _order - 1; o >= 1; o--)
            {
                key = GetContext(context, context.Count - o, o);
                if (_transitions.TryGetValue(key, out dist) && dist.Count > 0)
                    break;
            }

            if (dist == null || dist.Count == 0)
                return Rational.Quarter;
        }

        // Sample from distribution
        var r = _random.NextDouble();
        var cumulative = 0f;

        foreach (var (duration, prob) in dist)
        {
            cumulative += prob;
            if (r <= cumulative)
                return duration;
        }

        return dist.First().Key;
    }

    private RhythmPrediction FallbackPredict(IReadOnlyList<Rational> recentDurations)
    {
        // Try shorter contexts
        for (int o = _order - 1; o >= 1; o--)
        {
            if (recentDurations.Count < o) continue;

            var context = GetContext(recentDurations, recentDurations.Count - o, o);
            if (_transitions.TryGetValue(context, out var dist) && dist.Count > 0)
            {
                var sorted = dist.OrderByDescending(kv => kv.Value).ToList();
                return new RhythmPrediction
                {
                    MostLikely = sorted[0].Key,
                    Confidence = sorted[0].Value * 0.8f, // Lower confidence for fallback
                    Alternatives = sorted.Skip(1).Take(4).Select(kv =>
                        new RhythmAlternative { Duration = kv.Key, Probability = kv.Value }).ToList(),
                    ContextFound = true
                };
            }
        }

        // Final fallback: return most common duration overall
        var allDurations = _transitions
            .SelectMany(kv => kv.Value)
            .GroupBy(kv => kv.Key)
            .Select(g => (g.Key, g.Sum(x => x.Value)))
            .OrderByDescending(x => x.Item2)
            .FirstOrDefault();

        return new RhythmPrediction
        {
            MostLikely = allDurations.Key != default ? allDurations.Key : Rational.Quarter,
            Confidence = 0.3f,
            Alternatives = [],
            ContextFound = false
        };
    }
}

/// <summary>
/// Result of rhythm prediction.
/// </summary>
public sealed class RhythmPrediction
{
    public required Rational MostLikely { get; init; }
    public required float Confidence { get; init; }
    public required IReadOnlyList<RhythmAlternative> Alternatives { get; init; }
    public required bool ContextFound { get; init; }

    public override string ToString()
    {
        var alts = string.Join(", ", Alternatives.Select(a => $"{a.Duration} ({a.Probability:P0})"));
        return $"Predicted: {MostLikely} ({Confidence:P0}){(alts.Length > 0 ? $" | Alternatives: {alts}" : "")}";
    }
}

/// <summary>
/// Alternative prediction.
/// </summary>
public sealed class RhythmAlternative
{
    public required Rational Duration { get; init; }
    public required float Probability { get; init; }
}

/// <summary>
/// Statistics about the rhythm model.
/// </summary>
public sealed class RhythmModelStats
{
    public int Order { get; init; }
    public int UniqueContexts { get; init; }
    public int TotalTransitions { get; init; }
    public IReadOnlyList<Rational> MostCommonDurations { get; init; } = [];
}

/// <summary>
/// Pre-trained rhythm models for common styles.
/// </summary>
public static class RhythmModels
{
    /// <summary>
    /// Get a pre-trained model for a style.
    /// </summary>
    public static RhythmPredictor GetStyleModel(string style)
    {
        var predictor = new RhythmPredictor(order: 2, seed: 42);

        var durations = style.ToLowerInvariant() switch
        {
            "classical" => ClassicalDurations(),
            "jazz" => JazzDurations(),
            "rock" => RockDurations(),
            "latin" => LatinDurations(),
            "waltz" => WaltzDurations(),
            _ => ClassicalDurations()
        };

        predictor.Train(durations);
        return predictor;
    }

    private static List<Rational> ClassicalDurations() =>
    [
        // Bach-like quarter/eighth patterns
        new(1,4), new(1,4), new(1,4), new(1,4),
        new(1,8), new(1,8), new(1,4), new(1,8), new(1,8), new(1,4),
        new(1,4), new(1,8), new(1,8), new(1,4), new(1,4),
        new(1,2), new(1,4), new(1,4),
        new(1,4), new(1,4), new(1,2),
        new(1,8), new(1,8), new(1,8), new(1,8), new(1,4), new(1,4),
        new(1,4), new(3,8), new(1,8),
        new(3,8), new(1,8), new(1,4), new(1,4),
        new(1,1) // whole note
    ];

    private static List<Rational> JazzDurations() =>
    [
        // Swing/syncopated patterns
        new(3,8), new(1,8), new(3,8), new(1,8),
        new(1,4), new(1,8), new(1,8), new(1,4), new(1,4),
        new(1,8), new(1,4), new(1,8), new(1,4), new(1,4),
        new(3,8), new(1,8), new(1,4), new(3,8), new(1,8),
        new(1,4), new(3,8), new(1,8), new(1,4),
        new(1,2), new(1,4), new(1,4),
        new(1,8), new(3,8), new(1,4), new(1,4),
        new(1,4), new(1,4), new(1,8), new(1,4), new(1,8)
    ];

    private static List<Rational> RockDurations() =>
    [
        // Driving eighths with quarter accents
        new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8), new(1,8),
        new(1,4), new(1,4), new(1,4), new(1,4),
        new(1,8), new(1,8), new(1,4), new(1,8), new(1,8), new(1,4),
        new(1,4), new(1,8), new(1,8), new(1,4), new(1,4),
        new(1,2), new(1,2),
        new(1,4), new(1,4), new(1,8), new(1,8), new(1,8), new(1,8)
    ];

    private static List<Rational> LatinDurations() =>
    [
        // Tresillo and clave patterns
        new(3,8), new(3,8), new(2,8),
        new(3,8), new(3,8), new(2,8),
        new(1,4), new(1,4), new(1,4), new(1,4),
        new(3,8), new(1,8), new(1,4), new(1,4),
        new(1,8), new(1,8), new(3,8), new(1,8), new(1,4),
        new(3,8), new(3,8), new(2,8), new(2,8), new(2,8)
    ];

    private static List<Rational> WaltzDurations() =>
    [
        // 3/4 patterns
        new(1,4), new(1,4), new(1,4),
        new(1,2), new(1,4),
        new(1,4), new(1,2),
        new(3,4),
        new(1,8), new(1,8), new(1,4), new(1,4),
        new(1,4), new(1,8), new(1,8), new(1,4),
        new(1,4), new(1,4), new(1,8), new(1,8)
    ];
}
