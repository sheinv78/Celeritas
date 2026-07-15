// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Named rhythm styles for pre-trained models.
/// </summary>
public enum RhythmStyle
{
    Classical,
    Jazz,
    Rock,
    Latin,
    Waltz
}

/// <summary>
/// Rhythm predictor using Markov chains and N-gram models.
/// </summary>
public sealed class RhythmPredictor
{
    // Stores RAW transition counts; probabilities are computed at query time.
    // This keeps repeated Train() calls accumulating correctly (normalizing in
    // place would mix probabilities with subsequent raw counts).
    private readonly Dictionary<string, Dictionary<Rational, float>> _transitions = [];
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
    /// <exception cref="ArgumentNullException"><paramref name="durations"/> is <see langword="null"/>.</exception>
    public void Train(IReadOnlyList<Rational> durations)
    {
        ArgumentNullException.ThrowIfNull(durations);

        if (durations.Count <= _order)
        {
            return;
        }

        for (int i = _order; i < durations.Count; i++)
        {
            var context = GetContext(durations, i - _order, _order);
            var next = durations[i];

            if (!_transitions.TryGetValue(context, out var dist))
            {
                dist = [];
                _transitions[context] = dist;
            }

            dist.TryGetValue(next, out var count);
            dist[next] = count + 1;
        }
    }

    /// <summary>
    /// Train from a NoteBuffer.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public void Train(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

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
    /// <exception cref="ArgumentNullException"><paramref name="recentDurations"/> is <see langword="null"/>.</exception>
    public RhythmPrediction Predict(IReadOnlyList<Rational> recentDurations)
    {
        ArgumentNullException.ThrowIfNull(recentDurations);

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

        var total = dist.Values.Sum();
        var sorted = dist.OrderByDescending(kv => kv.Value).ToList();
        var best = sorted[0];

        return new RhythmPrediction
        {
            MostLikely = best.Key,
            Confidence = best.Value / total,
            Alternatives = sorted.Skip(1).Take(4).Select(kv =>
                new RhythmAlternative { Duration = kv.Key, Probability = kv.Value / total }).ToList(),
            ContextFound = true
        };
    }

    /// <summary>
    /// Generate a rhythm sequence.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <see langword="null"/>.</exception>
    public List<Rational> Generate(IReadOnlyList<Rational> seed, int length)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var result = seed.ToList();

        for (int i = 0; i < length; i++)
        {
            var next = SampleNext(result);
            result.Add(next);
        }

        return [.. result.Skip(seed.Count)];
    }

    /// <summary>
    /// Generate a complete measure.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <see langword="null"/>.</exception>
    public List<Rational> GenerateMeasure(IReadOnlyList<Rational> seed, TimeSignature meter)
    {
        ArgumentNullException.ThrowIfNull(seed);

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
            {
                next = remaining;
            }

            if (next <= Rational.Zero)
            {
                break;
            }

            result.Add(next);
            context.Add(next);
            if (context.Count > _order)
            {
                context.RemoveAt(0);
            }

            current = current + next;
        }

        return result;
    }

    /// <summary>
    /// Get model statistics.
    /// </summary>
    public RhythmModelStatistics GetStats()
    {
        return new RhythmModelStatistics
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

    private static string GetContext(IReadOnlyList<Rational> durations, int start, int length)
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
                {
                    break;
                }
            }

            if (dist == null || dist.Count == 0)
            {
                return Rational.Quarter;
            }
        }

        // Sample from distribution (raw counts, so scale the random draw by the total)
        var total = dist.Values.Sum();
        var r = _random.NextDouble() * total;
        var cumulative = 0f;

        foreach (var (duration, count) in dist)
        {
            cumulative += count;
            if (r <= cumulative)
            {
                return duration;
            }
        }

        return dist.First().Key;
    }

    private RhythmPrediction FallbackPredict(IReadOnlyList<Rational> recentDurations)
    {
        // Try shorter contexts
        for (int o = _order - 1; o >= 1; o--)
        {
            if (recentDurations.Count < o)
            {
                continue;
            }

            var context = GetContext(recentDurations, recentDurations.Count - o, o);
            if (_transitions.TryGetValue(context, out var dist) && dist.Count > 0)
            {
                var total = dist.Values.Sum();
                var sorted = dist.OrderByDescending(kv => kv.Value).ToList();
                return new RhythmPrediction
                {
                    MostLikely = sorted[0].Key,
                    Confidence = sorted[0].Value / total * 0.8f, // Lower confidence for fallback
                    Alternatives = sorted.Skip(1).Take(4).Select(kv =>
                        new RhythmAlternative { Duration = kv.Key, Probability = kv.Value / total }).ToList(),
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
        var alts = string.Join(", ", Alternatives.Select(a => $"{a.Duration} ({(int)Math.Round(a.Probability * 100)}%)"));
        return $"Predicted: {MostLikely} ({(int)Math.Round(Confidence * 100)}%){(alts.Length > 0 ? $" | Alternatives: {alts}" : "")}";
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
public sealed class RhythmModelStatistics
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
    public static RhythmPredictor GetStyleModel(RhythmStyle style)
    {
        return style switch
        {
            RhythmStyle.Classical => GetStyleModel("classical"),
            RhythmStyle.Jazz => GetStyleModel("jazz"),
            RhythmStyle.Rock => GetStyleModel("rock"),
            RhythmStyle.Latin => GetStyleModel("latin"),
            RhythmStyle.Waltz => GetStyleModel("waltz"),
            _ => GetStyleModel("classical")
        };
    }

    /// <summary>
    /// Get a pre-trained model for a style.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="style"/> is <see langword="null"/>.</exception>
    public static RhythmPredictor GetStyleModel(string style)
    {
        ArgumentNullException.ThrowIfNull(style);

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
