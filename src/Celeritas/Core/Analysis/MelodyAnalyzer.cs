// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Collections.Frozen;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Direction of melodic movement.
/// </summary>
public enum MelodicDirection
{
    /// <summary>Upward motion (pitch increases).</summary>
    Ascending,
    /// <summary>Downward motion (pitch decreases).</summary>
    Descending,
    /// <summary>No pitch change (repeated pitch).</summary>
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
    int Semitones,
    MelodicDirection Direction,
    MelodicMotionType Motion
);

/// <summary>
/// A detected melodic motif (recurring pattern).
/// </summary>
public sealed class Motif
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal Motif() { }

    /// <summary>The recurring pattern as consecutive melodic intervals in semitones.</summary>
    public required int[] IntervalPattern { get; init; }

    /// <summary>
    /// Onset of each occurrence's first note. When the melody was analyzed without timing, these
    /// are sequential note positions (index <c>i</c> as <c>i/1</c>) rather than musical times.
    /// </summary>
    public required IReadOnlyList<Rational> Occurrences { get; init; }
    /// <summary>Number of intervals in the pattern.</summary>
    public required int Length { get; init; }
    /// <summary>Prominence score in 0-1, derived from pattern length and occurrence count.</summary>
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
public sealed class MelodicIntervalStatistics
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal MelodicIntervalStatistics() { }

    /// <summary>Number of intervals analyzed (one fewer than the note count).</summary>
    public required int TotalIntervals { get; init; }
    /// <summary>Mean absolute interval size in semitones.</summary>
    public required double AverageInterval { get; init; }
    /// <summary>Largest absolute interval in semitones.</summary>
    public required int LargestLeap { get; init; }
    /// <summary>Percentage of intervals that are steps (1-2 semitones).</summary>
    public required double StepPercent { get; init; }
    /// <summary>Percentage of intervals that are leaps (3+ semitones).</summary>
    public required double LeapPercent { get; init; }
    /// <summary>Percentage of intervals that are repetitions (unison).</summary>
    public required double RepetitionPercent { get; init; }
    /// <summary>Count of each absolute interval size, keyed by semitones.</summary>
    public required IReadOnlyDictionary<int, int> IntervalHistogram { get; init; }
    /// <summary>Count of each motion type.</summary>
    public required IReadOnlyDictionary<MelodicMotionType, int> MotionHistogram { get; init; }
}

/// <summary>
/// Complete melody analysis result.
/// </summary>
public sealed class MelodyAnalysisResult
{
    // Produced by MelodyAnalyzer; not constructible by consumers (#18 API freeze).
    internal MelodyAnalysisResult() { }

    /// <summary>Overall melodic contour classification.</summary>
    public required MelodicContour Contour { get; init; }
    /// <summary>Human-readable description of the contour.</summary>
    public required string ContourDescription { get; init; }
    /// <summary>Lowest MIDI pitch in the melody.</summary>
    public required int LowestPitch { get; init; }
    /// <summary>Highest MIDI pitch in the melody.</summary>
    public required int HighestPitch { get; init; }
    /// <summary>Pitch range in semitones (highest minus lowest).</summary>
    public required int Ambitus { get; init; } // range in semitones
    /// <summary>Human-readable description of the range.</summary>
    public required string AmbitusDescription { get; init; }
    /// <summary>Consecutive melodic intervals in order.</summary>
    public required IReadOnlyList<MelodicInterval> Intervals { get; init; }
    /// <summary>Aggregate interval statistics.</summary>
    public required MelodicIntervalStatistics Statistics { get; init; }
    /// <summary>Detected recurring motifs, most significant first (up to 5).</summary>
    public required IReadOnlyList<Motif> Motifs { get; init; }
    /// <summary>Stepwise-ness in 0-1 (fraction of steps and repetitions).</summary>
    public required double Conjunctness { get; init; } // 0-1, how stepwise
    /// <summary>Interval variety in 0-1 (normalized entropy of the interval histogram).</summary>
    public required double Complexity { get; init; } // 0-1, variety of intervals
    /// <summary>Human-readable summary of the melody's character.</summary>
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
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static MelodyAnalysisResult Analyze(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Count == 0)
        {
            return EmptyResult();
        }

        // Extract pitches in time order. A rest is silence, not a note in the line: kept, its
        // RestPitch (-1) reached the interval naming and threw "MIDI pitch must be 0-127".
        var notes = new List<(int Pitch, Rational Time)>();
        for (int i = 0; i < buffer.Count; i++)
        {
            var note = buffer.Get(i);
            if (Rests.IsRest(note.Pitch)) continue;
            notes.Add((note.Pitch, note.Offset));
        }

        if (notes.Count == 0)
        {
            return EmptyResult();
        }
        // Pitch tie-break: chord notes share an onset, and an offset-only sort would
        // leave their order (and thus the interval sequence) insertion-order dependent.
        notes.Sort((a, b) =>
        {
            var cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Pitch.CompareTo(b.Pitch);
        });

        var pitches = notes.Select(n => n.Pitch).ToArray();
        var times = notes.Select(n => n.Time).ToArray();

        return Analyze(pitches, times);
    }

    /// <summary>
    /// Analyze a melody from pitch array. When <paramref name="times"/> is supplied, each note's
    /// onset is used for <see cref="Motif.Occurrences"/>; otherwise occurrences are reported as
    /// sequential note positions (index <c>i</c> as <c>i/1</c>), since no timing is known.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pitches"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="times"/> is non-null and its length differs from <paramref name="pitches"/>.
    /// </exception>
    public static MelodyAnalysisResult Analyze(int[] pitches, Rational[]? times = null)
    {
        ArgumentNullException.ThrowIfNull(pitches);

        // A times array of the wrong length was silently accepted before, because times was never
        // read past this point — Analyze(3 pitches, 1 time) "succeeded". If a caller supplies
        // timing, it has to line up with the notes it times.
        if (times is not null && times.Length != pitches.Length)
            throw new ArgumentException(
                $"times length ({times.Length}) must match pitches length ({pitches.Length}).",
                nameof(times));

        if (pitches.Length == 0)
        {
            return EmptyResult();
        }

        times ??= [.. Enumerable.Range(0, pitches.Length).Select(i => new Rational(i, 1))];

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

            intervals.Add(new MelodicInterval(semitones, direction, motion));
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

        // Detect motifs. times lines up with pitches (and so with the note each interval starts
        // on), so an occurrence at interval index i is reported at that note's onset, times[i].
        var motifs = DetectMotifs([.. intervals.Select(i => i.Semitones)], times);

        // Calculate conjunctness (how stepwise the melody is)
        var conjunctness = stats.TotalIntervals > 0
            ? (stats.StepPercent + stats.RepetitionPercent) / 100.0
            : 1.0;

        // Calculate complexity (variety of intervals)
        var complexity = CalculateComplexity(stats.IntervalHistogram);

        // Character description
        var character = DescribeCharacter(conjunctness, complexity, ambitus, stats);

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

        // Find turning points, comparing each pitch against the nearest DIFFERENT
        // neighboring pitches so plateau peaks/troughs (C D E E D C) are detected —
        // strict immediate-neighbor comparison saw no turn at either E and called
        // the whole arch "static". A plateau is evaluated once, at its first note.
        var turningPoints = new List<int>();
        for (int i = 1; i < pitches.Length - 1; i++)
        {
            var curr = pitches[i];
            if (curr == pitches[i - 1])
            {
                continue; // Interior of a plateau: already evaluated at its start.
            }

            var prev = pitches[i - 1];

            // Next different pitch (skip over the plateau, if any).
            int j = i + 1;
            while (j < pitches.Length && pitches[j] == curr)
            {
                j++;
            }

            if (j >= pitches.Length)
            {
                break; // Plateau extends to the end: no turn.
            }

            var next = pitches[j];

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


        return turningPoints.Count switch
        {
            // Classify based on shape
            // Monotonic
            0 when Math.Abs(overallChange) <= 2 => MelodicContour.Static,
            0 => overallChange > 0 ? MelodicContour.Ascending : MelodicContour.Descending,
            // Single turn - arch or bowl
            1 when turningPoints[0] > 0 => MelodicContour.Arch,
            1 => MelodicContour.Bowl,
            >= 2 and <= 4 => MelodicContour.Wave,
            _ => MelodicContour.Complex
        };
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

    private static MelodicIntervalStatistics CalculateStatistics(List<MelodicInterval> intervals)
    {
        if (intervals.Count == 0)
        {
            return new MelodicIntervalStatistics
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

        return new MelodicIntervalStatistics
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

    private static List<Motif> DetectMotifs(int[] intervals, Rational[] noteTimes, int minLength = 2, int maxLength = 6)
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
                    var significance = len * occurrences.Count / (double)intervals.Length;
                    significance = Math.Min(1.0, significance);

                    motifs.Add(new Motif
                    {
                        IntervalPattern = pattern,
                        // occurrences are interval indices; the pattern starting at interval index
                        // i begins on note i, whose onset is noteTimes[i]. Previously these indices
                        // were wrapped straight into Rational — index 3 surfaced as 3/1, three
                        // whole notes — so the real onsets extracted by Analyze(NoteBuffer) were
                        // discarded and replaced with the note's position in the sequence.
                        Occurrences = [.. occurrences.Select(i => noteTimes[i])],
                        Length = len,
                        Significance = significance
                    });
                }
            }
        }

        // Sort by significance
        return [.. motifs.OrderByDescending(m => m.Significance).Take(5)];
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
        double conjunctness,
        double complexity,
        int ambitus,
        MelodicIntervalStatistics stats)
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
        Statistics = new MelodicIntervalStatistics
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
