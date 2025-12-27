// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Collections.Frozen;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Direction of melodic movement.
/// </summary>
public enum MelodicDirection
{
    Ascending,
    Descending,
    Static
}

/// <summary>
/// Overall shape/contour of a melody.
/// </summary>
public enum MelodicContour
{
    /// <summary>Generally ascending throughout.</summary>
    Ascending,
    /// <summary>Generally descending throughout.</summary>
    Descending,
    /// <summary>Rises then falls (arch shape).</summary>
    Arch,
    /// <summary>Falls then rises (inverted arch/bowl).</summary>
    Bowl,
    /// <summary>Alternating rises and falls.</summary>
    Wave,
    /// <summary>Stays mostly level.</summary>
    Static,
    /// <summary>No clear pattern.</summary>
    Complex
}

/// <summary>
/// Classification of melodic motion between two notes.
/// </summary>
public enum MelodicMotionType
{
    /// <summary>Same pitch (unison/repetition).</summary>
    Repetition,
    /// <summary>Step motion (1-2 semitones).</summary>
    Step,
    /// <summary>Small leap (3-4 semitones - minor/major 3rd).</summary>
    SmallLeap,
    /// <summary>Medium leap (5-7 semitones - 4th/5th).</summary>
    MediumLeap,
    /// <summary>Large leap (8+ semitones - 6th or more).</summary>
    LargeLeap
}

/// <summary>
/// A single melodic interval with context.
/// </summary>
public readonly record struct MelodicInterval(
    int FromPitch,
    int ToPitch,
    int Semitones,
    MelodicDirection Direction,
    MelodicMotionType Motion,
    Rational StartTime
);

/// <summary>
/// A detected melodic motif (recurring pattern).
/// </summary>
public sealed class Motif
{
    public required int[] IntervalPattern { get; init; }
    public required IReadOnlyList<Rational> Occurrences { get; init; }
    public required int Length { get; init; }
    public required double Significance { get; init; } // 0-1, based on frequency and length

    /// <summary>
    /// Human-readable description of the interval pattern.
    /// </summary>
    public string PatternDescription => string.Join(" ", IntervalPattern.Select(i =>
        i > 0 ? $"+{i}" : i.ToString()));
}

/// <summary>
/// Statistics about interval distribution.
/// </summary>
public sealed class MelodicIntervalStats
{
    public required int TotalIntervals { get; init; }
    public required double AverageInterval { get; init; }
    public required int LargestLeap { get; init; }
    public required double StepPercent { get; init; }
    public required double LeapPercent { get; init; }
    public required double RepetitionPercent { get; init; }
    public required IReadOnlyDictionary<int, int> IntervalHistogram { get; init; }
    public required IReadOnlyDictionary<MelodicMotionType, int> MotionHistogram { get; init; }
}

/// <summary>
/// Complete melody analysis result.
/// </summary>
public sealed class MelodyAnalysisResult
{
    public required MelodicContour Contour { get; init; }
    public required string ContourDescription { get; init; }
    public required int LowestPitch { get; init; }
    public required int HighestPitch { get; init; }
    public required int Ambitus { get; init; } // range in semitones
    public required string AmbitusDescription { get; init; }
    public required IReadOnlyList<MelodicInterval> Intervals { get; init; }
    public required MelodicIntervalStats Statistics { get; init; }
    public required IReadOnlyList<Motif> Motifs { get; init; }
    public required double Conjunctness { get; init; } // 0-1, how stepwise
    public required double Complexity { get; init; } // 0-1, variety of intervals
    public required string CharacterDescription { get; init; }
}

/// <summary>
/// Melody analysis engine - analyzes contour, intervals, and detects motifs.
/// </summary>
public static class MelodyAnalyzer
{
    /// <summary>
    /// Named intervals for display.
    /// </summary>
    public static readonly FrozenDictionary<int, string> IntervalNames = new Dictionary<int, string>
    {
        [0] = "Unison",
        [1] = "m2",
        [2] = "M2",
        [3] = "m3",
        [4] = "M3",
        [5] = "P4",
        [6] = "TT",
        [7] = "P5",
        [8] = "m6",
        [9] = "M6",
        [10] = "m7",
        [11] = "M7",
        [12] = "P8"
    }.ToFrozenDictionary();

    /// <summary>
    /// Analyze a melody from a NoteBuffer.
    /// </summary>
    public static MelodyAnalysisResult Analyze(NoteBuffer buffer)
    {
        if (buffer.Count == 0)
        {
            return EmptyResult();
        }

        // Extract pitches in time order
        var notes = new List<(int Pitch, Rational Time)>();
        for (int i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            notes.Add((note.Pitch, note.Offset));
        }
        notes.Sort((a, b) => a.Time.CompareTo(b.Time));

        var pitches = notes.Select(n => n.Pitch).ToArray();
        var times = notes.Select(n => n.Time).ToArray();

        return Analyze(pitches, times);
    }

    /// <summary>
    /// Analyze a melody from pitch array.
    /// </summary>
    public static MelodyAnalysisResult Analyze(int[] pitches, Rational[]? times = null)
    {
        if (pitches.Length == 0)
        {
            return EmptyResult();
        }

        times ??= Enumerable.Range(0, pitches.Length)
            .Select(i => new Rational(i, 1))
            .ToArray();

        // Calculate intervals
        var intervals = new List<MelodicInterval>();
        for (int i = 1; i < pitches.Length; i++)
        {
            var from = pitches[i - 1];
            var to = pitches[i];
            var semitones = to - from;
            var absSemitones = Math.Abs(semitones);

            var direction = semitones > 0 ? MelodicDirection.Ascending :
                           semitones < 0 ? MelodicDirection.Descending :
                           MelodicDirection.Static;

            var motion = ClassifyMotion(absSemitones);

            intervals.Add(new MelodicInterval(from, to, semitones, direction, motion, times[i - 1]));
        }

        // Calculate statistics
        var stats = CalculateStatistics(intervals);

        // Detect contour
        var contour = DetectContour(pitches);
        var contourDesc = DescribeContour(contour, pitches);

        // Calculate range
        var lowest = pitches.Min();
        var highest = pitches.Max();
        var ambitus = highest - lowest;
        var ambitusDesc = DescribeAmbitus(ambitus, lowest, highest);

        // Detect motifs
        var motifs = DetectMotifs(intervals.Select(i => i.Semitones).ToArray());

        // Calculate conjunctness (how stepwise the melody is)
        var conjunctness = stats.TotalIntervals > 0
            ? (stats.StepPercent + stats.RepetitionPercent) / 100.0
            : 1.0;

        // Calculate complexity (variety of intervals)
        var complexity = CalculateComplexity(stats.IntervalHistogram);

        // Character description
        var character = DescribeCharacter(contour, conjunctness, complexity, ambitus, stats);

        return new MelodyAnalysisResult
        {
            Contour = contour,
            ContourDescription = contourDesc,
            LowestPitch = lowest,
            HighestPitch = highest,
            Ambitus = ambitus,
            AmbitusDescription = ambitusDesc,
            Intervals = intervals,
            Statistics = stats,
            Motifs = motifs,
            Conjunctness = conjunctness,
            Complexity = complexity,
            CharacterDescription = character
        };
    }

    /// <summary>
    /// Classify the type of melodic motion based on interval size.
    /// </summary>
    public static MelodicMotionType ClassifyMotion(int absSemitones) => absSemitones switch
    {
        0 => MelodicMotionType.Repetition,
        1 or 2 => MelodicMotionType.Step,
        3 or 4 => MelodicMotionType.SmallLeap,
        5 or 6 or 7 => MelodicMotionType.MediumLeap,
        _ => MelodicMotionType.LargeLeap
    };

    /// <summary>
    /// Get a human-readable name for an interval.
    /// </summary>
    public static string GetIntervalName(int semitones)
    {
        var abs = Math.Abs(semitones);
        var direction = semitones >= 0 ? "↑" : "↓";

        if (abs <= 12)
        {
            return $"{direction}{IntervalNames[abs]}";
        }

        var octaves = abs / 12;
        var remainder = abs % 12;
        return $"{direction}{octaves}oct+{IntervalNames[remainder]}";
    }

    private static MelodicContour DetectContour(int[] pitches)
    {
        if (pitches.Length < 3)
        {
            return MelodicContour.Static;
        }

        // Find turning points
        var turningPoints = new List<int>();
        for (int i = 1; i < pitches.Length - 1; i++)
        {
            var prev = pitches[i - 1];
            var curr = pitches[i];
            var next = pitches[i + 1];

            // Local maximum
            if (curr > prev && curr > next)
            {
                turningPoints.Add(i);
            }
            // Local minimum
            else if (curr < prev && curr < next)
            {
                turningPoints.Add(-i);
            }
        }

        // Overall direction
        var first = pitches[0];
        var last = pitches[^1];
        var overallChange = last - first;

        // Find the peak/trough positions
        var maxIdx = Array.IndexOf(pitches, pitches.Max());
        var minIdx = Array.IndexOf(pitches, pitches.Min());

        var totalLen = pitches.Length;
        var maxPos = (double)maxIdx / totalLen;
        var minPos = (double)minIdx / totalLen;

        // Classify based on shape
        if (turningPoints.Count == 0)
        {
            // Monotonic
            if (Math.Abs(overallChange) <= 2)
            {
                return MelodicContour.Static;
            }

            return overallChange > 0 ? MelodicContour.Ascending : MelodicContour.Descending;
        }

        if (turningPoints.Count == 1)
        {
            // Single turn - arch or bowl
            if (turningPoints[0] > 0)
            {
                return MelodicContour.Arch; // peak in middle
            }
            else
            {
                return MelodicContour.Bowl; // trough in middle
            }
        }

        if (turningPoints.Count >= 2 && turningPoints.Count <= 4)
        {
            return MelodicContour.Wave;
        }

        return MelodicContour.Complex;
    }

    private static string DescribeContour(MelodicContour contour, int[] pitches)
    {
        var first = pitches[0];
        var last = pitches[^1];
        var change = last - first;

        return contour switch
        {
            MelodicContour.Ascending => $"Rising melody (net +{change} semitones)",
            MelodicContour.Descending => $"Falling melody (net {change} semitones)",
            MelodicContour.Arch => "Arch shape - rises to peak then descends",
            MelodicContour.Bowl => "Bowl shape - descends to trough then rises",
            MelodicContour.Wave => "Undulating/wave-like contour",
            MelodicContour.Static => "Level/static melody with little movement",
            MelodicContour.Complex => "Complex contour with multiple direction changes",
            _ => "Unknown contour"
        };
    }

    private static string DescribeAmbitus(int ambitus, int lowest, int highest)
    {
        var lowNote = MusicMath.MidiToNoteName(lowest);
        var highNote = MusicMath.MidiToNoteName(highest);

        var rangeDesc = ambitus switch
        {
            <= 5 => "narrow",
            <= 12 => "moderate",
            <= 19 => "wide",
            _ => "very wide"
        };

        var intervalDesc = ambitus <= 12
            ? IntervalNames.GetValueOrDefault(ambitus, $"{ambitus} semitones")
            : $"{ambitus / 12} octave(s) + {IntervalNames.GetValueOrDefault(ambitus % 12, "")}";

        return $"{rangeDesc} range: {lowNote} to {highNote} ({intervalDesc})";
    }

    private static MelodicIntervalStats CalculateStatistics(List<MelodicInterval> intervals)
    {
        if (intervals.Count == 0)
        {
            return new MelodicIntervalStats
            {
                TotalIntervals = 0,
                AverageInterval = 0,
                LargestLeap = 0,
                StepPercent = 0,
                LeapPercent = 0,
                RepetitionPercent = 0,
                IntervalHistogram = new Dictionary<int, int>(),
                MotionHistogram = new Dictionary<MelodicMotionType, int>()
            };
        }

        var intervalHist = new Dictionary<int, int>();
        var motionHist = new Dictionary<MelodicMotionType, int>();

        foreach (var interval in intervals)
        {
            var abs = Math.Abs(interval.Semitones);
            intervalHist[abs] = intervalHist.GetValueOrDefault(abs, 0) + 1;
            motionHist[interval.Motion] = motionHist.GetValueOrDefault(interval.Motion, 0) + 1;
        }

        var total = intervals.Count;
        var steps = motionHist.GetValueOrDefault(MelodicMotionType.Step, 0);
        var reps = motionHist.GetValueOrDefault(MelodicMotionType.Repetition, 0);
        var leaps = total - steps - reps;

        return new MelodicIntervalStats
        {
            TotalIntervals = total,
            AverageInterval = intervals.Average(i => Math.Abs(i.Semitones)),
            LargestLeap = intervals.Max(i => Math.Abs(i.Semitones)),
            StepPercent = 100.0 * steps / total,
            LeapPercent = 100.0 * leaps / total,
            RepetitionPercent = 100.0 * reps / total,
            IntervalHistogram = intervalHist,
            MotionHistogram = motionHist
        };
    }

    private static List<Motif> DetectMotifs(int[] intervals, int minLength = 2, int maxLength = 6)
    {
        if (intervals.Length < minLength * 2)
        {
            return [];
        }

        var motifs = new List<Motif>();
        var seenPatterns = new HashSet<string>();

        // Search for repeating patterns of various lengths
        for (int len = minLength; len <= Math.Min(maxLength, intervals.Length / 2); len++)
        {
            for (int start = 0; start <= intervals.Length - len; start++)
            {
                var pattern = intervals.Skip(start).Take(len).ToArray();
                var patternKey = string.Join(",", pattern);

                if (seenPatterns.Contains(patternKey))
                {
                    continue;
                }

                // Find all occurrences
                var occurrences = new List<int> { start };
                for (int search = start + len; search <= intervals.Length - len; search++)
                {
                    bool match = true;
                    for (int k = 0; k < len; k++)
                    {
                        if (intervals[search + k] != pattern[k])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        occurrences.Add(search);
                        search += len - 1; // skip overlapping matches
                    }
                }

                if (occurrences.Count >= 2)
                {
                    seenPatterns.Add(patternKey);

                    // Significance based on length and frequency
                    var significance = (len * occurrences.Count) / (double)intervals.Length;
                    significance = Math.Min(1.0, significance);

                    motifs.Add(new Motif
                    {
                        IntervalPattern = pattern,
                        Occurrences = occurrences.Select(i => new Rational(i, 1)).ToList(),
                        Length = len,
                        Significance = significance
                    });
                }
            }
        }

        // Sort by significance
        return motifs.OrderByDescending(m => m.Significance).Take(5).ToList();
    }

    private static double CalculateComplexity(IReadOnlyDictionary<int, int> histogram)
    {
        if (histogram.Count == 0)
        {
            return 0;
        }

        // Entropy-based complexity
        var total = histogram.Values.Sum();
        var entropy = 0.0;

        foreach (var count in histogram.Values)
        {
            var p = (double)count / total;
            if (p > 0)
            {
                entropy -= p * Math.Log2(p);
            }
        }

        // Normalize to 0-1 (max entropy for 12 different intervals)
        var maxEntropy = Math.Log2(12);
        return Math.Min(1.0, entropy / maxEntropy);
    }

    private static string DescribeCharacter(
        MelodicContour contour,
        double conjunctness,
        double complexity,
        int ambitus,
        MelodicIntervalStats stats)
    {
        var parts = new List<string>();

        // Motion character
        if (conjunctness > 0.8)
        {
            parts.Add("Smooth, stepwise");
        }
        else if (conjunctness > 0.5)
        {
            parts.Add("Mixed stepwise and leaping");
        }
        else if (stats.LeapPercent > 60)
        {
            parts.Add("Angular, leaping");
        }
        else
        {
            parts.Add("Moderately conjunct");
        }

        // Range character
        parts.Add(ambitus switch
        {
            <= 5 => "narrow-range",
            <= 12 => "moderate-range",
            _ => "wide-range"
        });

        // Complexity
        if (complexity > 0.7)
        {
            parts.Add("complex");
        }
        else if (complexity < 0.3)
        {
            parts.Add("simple");
        }

        // Style hints
        if (conjunctness > 0.7 && ambitus <= 12)
        {
            parts.Add("(vocal-style)");
        }
        else if (stats.LeapPercent > 50 && ambitus > 15)
        {
            parts.Add("(instrumental-style)");
        }

        return string.Join(" ", parts) + " melody";
    }

    private static MelodyAnalysisResult EmptyResult() => new()
    {
        Contour = MelodicContour.Static,
        ContourDescription = "Empty melody",
        LowestPitch = 0,
        HighestPitch = 0,
        Ambitus = 0,
        AmbitusDescription = "No range",
        Intervals = [],
        Statistics = new MelodicIntervalStats
        {
            TotalIntervals = 0,
            AverageInterval = 0,
            LargestLeap = 0,
            StepPercent = 0,
            LeapPercent = 0,
            RepetitionPercent = 0,
            IntervalHistogram = new Dictionary<int, int>(),
            MotionHistogram = new Dictionary<MelodicMotionType, int>()
        },
        Motifs = [],
        Conjunctness = 1.0,
        Complexity = 0,
        CharacterDescription = "Empty"
    };
}
