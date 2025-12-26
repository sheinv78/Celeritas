// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Buffers;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Types of melodic motion between two voices.
/// </summary>
public enum MotionType
{
    /// <summary>Voices move in the same direction by the same interval.</summary>
    Parallel,

    /// <summary>Voices move in the same direction by different intervals.</summary>
    Similar,

    /// <summary>Voices move in opposite directions.</summary>
    Contrary,

    /// <summary>One voice stays, the other moves.</summary>
    Oblique,

    /// <summary>Both voices stay on the same pitch.</summary>
    Static
}

/// <summary>
/// Interval quality in terms of consonance/dissonance.
/// </summary>
public enum IntervalQuality
{
    /// <summary>Unison, octave, perfect fifth.</summary>
    PerfectConsonance,

    /// <summary>Major/minor thirds and sixths.</summary>
    ImperfectConsonance,

    /// <summary>Major second, minor seventh.</summary>
    MildDissonance,

    /// <summary>Minor second, major seventh, tritone.</summary>
    SharpDissonance
}

/// <summary>
/// Information about the interval between two voices at a specific time.
/// </summary>
public readonly struct VoiceInterval
{
    public int Voice1 { get; init; }
    public int Voice2 { get; init; }
    public Rational Time { get; init; }
    public int Pitch1 { get; init; }
    public int Pitch2 { get; init; }
    public int Interval => Math.Abs(Pitch1 - Pitch2) % 12;
    public int RawInterval => Math.Abs(Pitch1 - Pitch2);
    public IntervalQuality Quality => ClassifyInterval(Interval);

    private static IntervalQuality ClassifyInterval(int semitones) => semitones switch
    {
        0 => IntervalQuality.PerfectConsonance,      // Unison
        1 => IntervalQuality.SharpDissonance,        // Minor 2nd
        2 => IntervalQuality.MildDissonance,         // Major 2nd
        3 => IntervalQuality.ImperfectConsonance,    // Minor 3rd
        4 => IntervalQuality.ImperfectConsonance,    // Major 3rd
        5 => IntervalQuality.PerfectConsonance,      // Perfect 4th (context-dependent)
        6 => IntervalQuality.SharpDissonance,        // Tritone
        7 => IntervalQuality.PerfectConsonance,      // Perfect 5th
        8 => IntervalQuality.ImperfectConsonance,    // Minor 6th
        9 => IntervalQuality.ImperfectConsonance,    // Major 6th
        10 => IntervalQuality.MildDissonance,        // Minor 7th
        11 => IntervalQuality.SharpDissonance,       // Major 7th
        _ => IntervalQuality.PerfectConsonance       // Octave+
    };

    public override string ToString()
    {
        var intervalName = Interval switch
        {
            0 => "P1",
            1 => "m2",
            2 => "M2",
            3 => "m3",
            4 => "M3",
            5 => "P4",
            6 => "TT",
            7 => "P5",
            8 => "m6",
            9 => "M6",
            10 => "m7",
            11 => "M7",
            _ => $"{Interval}"
        };
        return $"{intervalName} ({Quality})";
    }
}

/// <summary>
/// Motion analysis between two voice transitions.
/// </summary>
public readonly struct VoiceMotion
{
    public int Voice1 { get; init; }
    public int Voice2 { get; init; }
    public Rational FromTime { get; init; }
    public Rational ToTime { get; init; }
    public int Voice1Motion { get; init; }  // Semitones moved (+/-)
    public int Voice2Motion { get; init; }
    public MotionType Type { get; init; }
    public VoiceInterval FromInterval { get; init; }
    public VoiceInterval ToInterval { get; init; }

    /// <summary>Check if this is parallel 5ths or octaves (voice leading error).</summary>
    public bool IsParallelPerfect =>
        Type == MotionType.Parallel &&
        (FromInterval.Interval is 0 or 7 or 12) &&
        (ToInterval.Interval is 0 or 7 or 12);

    /// <summary>Check if this is hidden 5ths/octaves (similar motion to perfect interval).</summary>
    public bool IsHiddenPerfect =>
        Type == MotionType.Similar &&
        (ToInterval.Interval is 0 or 7 or 12);
}

/// <summary>
/// Counterpoint violation detected in the analysis.
/// </summary>
public sealed class CounterpointViolation
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required Rational Time { get; init; }
    public required int Voice1 { get; init; }
    public required int Voice2 { get; init; }
    public required string Severity { get; init; } // "Error", "Warning", "Style"
}

/// <summary>
/// Complete polyphony analysis result.
/// </summary>
public sealed class PolyphonyAnalysisResult
{
    public required VoiceSeparationResult Voices { get; init; }

    /// <summary>Intervals at each time point.</summary>
    public required IReadOnlyList<VoiceInterval> Intervals { get; init; }

    /// <summary>Voice motions between consecutive time points.</summary>
    public required IReadOnlyList<VoiceMotion> Motions { get; init; }

    /// <summary>Detected counterpoint violations.</summary>
    public required IReadOnlyList<CounterpointViolation> Violations { get; init; }

    /// <summary>Statistics about motion types.</summary>
    public required MotionStatistics MotionStats { get; init; }

    /// <summary>Statistics about interval usage.</summary>
    public required IntervalStatistics IntervalStats { get; init; }

    /// <summary>Overall polyphony quality score (0-1).</summary>
    public float QualityScore { get; init; }

    /// <summary>Texture density (average notes sounding simultaneously).</summary>
    public float TextureDensity { get; init; }

    /// <summary>Voice independence score (0-1, higher = more independent voices).</summary>
    public float VoiceIndependence { get; init; }
}

/// <summary>
/// Statistics about motion types in the piece.
/// </summary>
public sealed class MotionStatistics
{
    public int Parallel { get; init; }
    public int Similar { get; init; }
    public int Contrary { get; init; }
    public int Oblique { get; init; }
    public int Static { get; init; }

    public int Total => Parallel + Similar + Contrary + Oblique + Static;

    public float ParallelPercent => Total > 0 ? Parallel * 100f / Total : 0;
    public float SimilarPercent => Total > 0 ? Similar * 100f / Total : 0;
    public float ContraryPercent => Total > 0 ? Contrary * 100f / Total : 0;
    public float ObliquePercent => Total > 0 ? Oblique * 100f / Total : 0;
}

/// <summary>
/// Statistics about interval usage in the piece.
/// </summary>
public sealed class IntervalStatistics
{
    public int[] IntervalCounts { get; } = new int[12];

    public int PerfectConsonances { get; init; }
    public int ImperfectConsonances { get; init; }
    public int MildDissonances { get; init; }
    public int SharpDissonances { get; init; }

    public int Total => PerfectConsonances + ImperfectConsonances + MildDissonances + SharpDissonances;

    public float ConsonanceRatio => Total > 0
        ? (PerfectConsonances + ImperfectConsonances) * 100f / Total
        : 100f;

    public float DissonanceRatio => Total > 0
        ? (MildDissonances + SharpDissonances) * 100f / Total
        : 0f;
}

/// <summary>
/// Analyzer for polyphonic music - voice leading, counterpoint, texture.
/// </summary>
public static class PolyphonyAnalyzer
{
    /// <summary>
    /// Convenience wrapper used by examples: checks basic counterpoint issues and returns counts.
    /// </summary>
    public static CounterpointRulesCheckResult CheckCounterpointRules(IEnumerable<NoteEvent> notes, int maxVoices = 4)
    {
        var arr = notes as NoteEvent[] ?? notes.ToArray();
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return CheckCounterpointRules(buffer, maxVoices);
    }

    /// <summary>
    /// Convenience wrapper used by examples: checks basic counterpoint issues and returns counts.
    /// </summary>
    public static CounterpointRulesCheckResult CheckCounterpointRules(NoteBuffer buffer, int maxVoices = 4)
    {
        var analysis = Analyze(buffer, maxVoices);
        var violations = analysis.Violations;

        var parallel5ths = violations.Count(v => v.Type == "Parallel Fifths");
        var parallel8ves = violations.Count(v => v.Type == "Parallel Octaves");
        var hidden = violations.Count(v => v.Type == "Hidden Perfect Interval");

        var (voiceCrossing, spacing) = PolyphonyAnalyzerHelpers.AnalyzeCrossingsAndSpacing(analysis.Voices);

        return new CounterpointRulesCheckResult
        {
            ParallelFifths = parallel5ths,
            ParallelOctaves = parallel8ves,
            HiddenParallels = hidden,
            VoiceCrossing = voiceCrossing,
            SpacingViolations = spacing,
            QualityScore = PolyphonyAnalyzerHelpers.Clamp01(analysis.QualityScore - (spacing * 0.02f) - (voiceCrossing * 0.02f)),
            Violations = violations
        };
    }

    /// <summary>
    /// Detect simple imitation (canon-like) between voices.
    /// </summary>
    public static ImitationDetectionResult DetectImitation(IEnumerable<NoteEvent> notes, int maxVoices = 4)
    {
        var arr = notes as NoteEvent[] ?? notes.ToArray();
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return DetectImitation(buffer, maxVoices);
    }

    /// <summary>
    /// Detect simple imitation (canon-like) between voices.
    /// </summary>
    public static ImitationDetectionResult DetectImitation(NoteBuffer buffer, int maxVoices = 4)
    {
        var voices = VoiceSeparator.Separate(buffer, maxVoices);
        if (voices.Voices.Count < 2)
            return ImitationDetectionResult.None;

        // Build interval sequences for each voice.
        var sequences = voices.Voices
            .Select(v => v.Notes
                .OrderBy(n => n.Offset)
                .Select(n => n.Pitch)
                .ToArray())
            .ToArray();

        // Use a small motif length for detection.
        const int motifLen = 4;
        for (var v1 = 0; v1 < sequences.Length; v1++)
        {
            var s1 = sequences[v1];
            if (s1.Length < motifLen)
                continue;

            var i1 = PolyphonyAnalyzerHelpers.ToIntervals(s1);
            for (var v2 = v1 + 1; v2 < sequences.Length; v2++)
            {
                var s2 = sequences[v2];
                if (s2.Length < motifLen)
                    continue;

                var i2 = PolyphonyAnalyzerHelpers.ToIntervals(s2);
                var match = PolyphonyAnalyzerHelpers.FindIntervalMatch(i1, i2, motifLen - 1);
                if (match.HasValue)
                {
                    var (start1, start2) = match.Value;
                    var p1 = s1[start1];
                    var p2 = s2[start2];
                    var interval = p2 - p1;

                    // Estimate time delay using note offsets.
                    var t1 = voices.Voices[v1].Notes.OrderBy(n => n.Offset).ElementAt(start1).Offset;
                    var t2 = voices.Voices[v2].Notes.OrderBy(n => n.Offset).ElementAt(start2).Offset;
                    var delay = t2 - t1;

                    return new ImitationDetectionResult
                    {
                        HasImitation = true,
                        Type = "Canon",
                        Interval = interval,
                        TimeDelay = delay,
                        VoicesInvolved = [v1 + 1, v2 + 1]
                    };
                }
            }
        }

        return ImitationDetectionResult.None;
    }

    /// <summary>
    /// Perform complete polyphony analysis on a NoteBuffer.
    /// </summary>
    public static PolyphonyAnalysisResult Analyze(NoteBuffer buffer, int maxVoices = 4)
    {
        // First, separate voices
        var voices = VoiceSeparator.Separate(buffer, maxVoices);

        if (voices.Voices.Count < 2)
        {
            return new PolyphonyAnalysisResult
            {
                Voices = voices,
                Intervals = [],
                Motions = [],
                Violations = [],
                MotionStats = new MotionStatistics(),
                IntervalStats = new IntervalStatistics(),
                QualityScore = 1.0f,
                TextureDensity = voices.Voices.Count,
                VoiceIndependence = 1.0f
            };
        }

        // Collect all time points where notes start or end
        var timePoints = CollectTimePoints(voices);

        // Analyze intervals at each time point
        var intervals = AnalyzeIntervals(voices, timePoints);

        // Analyze voice motions
        var motions = AnalyzeMotions(voices, timePoints);

        // Detect counterpoint violations
        var violations = DetectViolations(motions, intervals);

        // Calculate statistics
        var motionStats = CalculateMotionStats(motions);
        var intervalStats = CalculateIntervalStats(intervals);

        // Calculate texture density
        var density = CalculateTextureDensity(voices, timePoints);

        // Calculate voice independence
        var independence = CalculateVoiceIndependence(motions);

        // Calculate overall quality
        var quality = CalculateQuality(violations, motionStats, intervalStats, independence);

        return new PolyphonyAnalysisResult
        {
            Voices = voices,
            Intervals = intervals,
            Motions = motions,
            Violations = violations,
            MotionStats = motionStats,
            IntervalStats = intervalStats,
            QualityScore = quality,
            TextureDensity = density,
            VoiceIndependence = independence
        };
    }

    private static List<Rational> CollectTimePoints(VoiceSeparationResult voices)
    {
        var times = new HashSet<Rational>();

        foreach (var voice in voices.Voices)
        {
            foreach (var note in voice.Notes)
            {
                times.Add(note.Offset);
                times.Add(note.End);
            }
        }

        return times.OrderBy(t => t).ToList();
    }

    private static List<VoiceInterval> AnalyzeIntervals(
        VoiceSeparationResult voices,
        List<Rational> timePoints)
    {
        // Estimate capacity: avg 2-3 intervals per time point for typical polyphony
        var intervals = new List<VoiceInterval>(timePoints.Count * 3);

        foreach (var time in timePoints)
        {
            var soundingNotes = GetSoundingNotes(voices, time);

            // Calculate intervals between all voice pairs
            for (int i = 0; i < soundingNotes.Count; i++)
            {
                for (int j = i + 1; j < soundingNotes.Count; j++)
                {
                    intervals.Add(new VoiceInterval
                    {
                        Voice1 = soundingNotes[i].voiceIdx,
                        Voice2 = soundingNotes[j].voiceIdx,
                        Time = time,
                        Pitch1 = soundingNotes[i].pitch,
                        Pitch2 = soundingNotes[j].pitch
                    });
                }
            }
        }

        return intervals;
    }

    private static List<(int voiceIdx, int pitch)> GetSoundingNotes(
        VoiceSeparationResult voices,
        Rational time)
    {
        // Pre-allocate with reasonable capacity to avoid List resizing
        var result = new List<(int, int)>(voices.Voices.Count);

        for (int v = 0; v < voices.Voices.Count; v++)
        {
            var voice = voices.Voices[v];
            var note = voice.Notes.FirstOrDefault(n =>
                n.Offset <= time && n.End > time);

            if (note.Pitch > 0) // Valid note
            {
                result.Add((v, note.Pitch));
            }
        }

        return result;
    }

    private static List<VoiceMotion> AnalyzeMotions(
        VoiceSeparationResult voices,
        List<Rational> timePoints)
    {
        var motions = new List<VoiceMotion>();

        for (int t = 0; t < timePoints.Count - 1; t++)
        {
            var time1 = timePoints[t];
            var time2 = timePoints[t + 1];

            var notes1 = GetSoundingNotes(voices, time1);
            var notes2 = GetSoundingNotes(voices, time2);

            // Analyze motion between each voice pair
            for (int i = 0; i < voices.Voices.Count; i++)
            {
                for (int j = i + 1; j < voices.Voices.Count; j++)
                {
                    var pitch1_t1 = notes1.FirstOrDefault(n => n.voiceIdx == i).pitch;
                    var pitch2_t1 = notes1.FirstOrDefault(n => n.voiceIdx == j).pitch;
                    var pitch1_t2 = notes2.FirstOrDefault(n => n.voiceIdx == i).pitch;
                    var pitch2_t2 = notes2.FirstOrDefault(n => n.voiceIdx == j).pitch;

                    if (pitch1_t1 == 0 || pitch2_t1 == 0 || pitch1_t2 == 0 || pitch2_t2 == 0)
                        continue;

                    var motion1 = pitch1_t2 - pitch1_t1;
                    var motion2 = pitch2_t2 - pitch2_t1;
                    var motionType = ClassifyMotion(motion1, motion2);

                    motions.Add(new VoiceMotion
                    {
                        Voice1 = i,
                        Voice2 = j,
                        FromTime = time1,
                        ToTime = time2,
                        Voice1Motion = motion1,
                        Voice2Motion = motion2,
                        Type = motionType,
                        FromInterval = new VoiceInterval
                        {
                            Voice1 = i,
                            Voice2 = j,
                            Time = time1,
                            Pitch1 = pitch1_t1,
                            Pitch2 = pitch2_t1
                        },
                        ToInterval = new VoiceInterval
                        {
                            Voice1 = i,
                            Voice2 = j,
                            Time = time2,
                            Pitch1 = pitch1_t2,
                            Pitch2 = pitch2_t2
                        }
                    });
                }
            }
        }

        return motions;
    }

    private static MotionType ClassifyMotion(int motion1, int motion2)
    {
        if (motion1 == 0 && motion2 == 0)
            return MotionType.Static;

        if (motion1 == 0 || motion2 == 0)
            return MotionType.Oblique;

        if (Math.Sign(motion1) != Math.Sign(motion2))
            return MotionType.Contrary;

        if (motion1 == motion2)
            return MotionType.Parallel;

        return MotionType.Similar;
    }

    private static List<CounterpointViolation> DetectViolations(
        List<VoiceMotion> motions,
        List<VoiceInterval> intervals)
    {
        // Pre-allocate assuming few violations (optimistic case)
        var violations = new List<CounterpointViolation>(motions.Count / 10);

        foreach (var motion in motions)
        {
            // Parallel fifths
            if (motion.FromInterval.Interval == 7 && motion.ToInterval.Interval == 7 &&
                motion.Type == MotionType.Parallel)
            {
                violations.Add(new CounterpointViolation
                {
                    Type = "Parallel Fifths",
                    Description = $"Voices {motion.Voice1 + 1} and {motion.Voice2 + 1} move in parallel perfect fifths",
                    Time = motion.FromTime,
                    Voice1 = motion.Voice1,
                    Voice2 = motion.Voice2,
                    Severity = "Error"
                });
            }

            // Parallel octaves/unisons
            if ((motion.FromInterval.Interval == 0 || motion.FromInterval.RawInterval == 12) &&
                (motion.ToInterval.Interval == 0 || motion.ToInterval.RawInterval == 12) &&
                motion.Type == MotionType.Parallel)
            {
                violations.Add(new CounterpointViolation
                {
                    Type = "Parallel Octaves",
                    Description = $"Voices {motion.Voice1 + 1} and {motion.Voice2 + 1} move in parallel octaves/unisons",
                    Time = motion.FromTime,
                    Voice1 = motion.Voice1,
                    Voice2 = motion.Voice2,
                    Severity = "Error"
                });
            }

            // Hidden fifths/octaves (similar motion to perfect interval in outer voices)
            if (motion.IsHiddenPerfect && (motion.Voice1 == 0 || motion.Voice2 == 0))
            {
                violations.Add(new CounterpointViolation
                {
                    Type = "Hidden Perfect Interval",
                    Description = $"Similar motion to perfect {(motion.ToInterval.Interval == 7 ? "fifth" : "octave")} in outer voices",
                    Time = motion.FromTime,
                    Voice1 = motion.Voice1,
                    Voice2 = motion.Voice2,
                    Severity = "Warning"
                });
            }

            // Large leap without contrary motion
            if (Math.Abs(motion.Voice1Motion) > 12 || Math.Abs(motion.Voice2Motion) > 12)
            {
                violations.Add(new CounterpointViolation
                {
                    Type = "Large Leap",
                    Description = $"Voice {(Math.Abs(motion.Voice1Motion) > 12 ? motion.Voice1 + 1 : motion.Voice2 + 1)} leaps more than an octave",
                    Time = motion.FromTime,
                    Voice1 = motion.Voice1,
                    Voice2 = motion.Voice2,
                    Severity = "Style"
                });
            }
        }

        // Check for voice crossing in intervals
        foreach (var interval in intervals)
        {
            // Voice crossing: lower-numbered voice has lower pitch
            if (interval.Voice1 < interval.Voice2 && interval.Pitch1 < interval.Pitch2)
            {
                // This might be intentional, but flag as style issue
                // (Flagging would generate too many warnings, so we skip mild crossings)
            }
        }

        // Check for unresolved dissonances
        for (int i = 0; i < intervals.Count - 1; i++)
        {
            if (intervals[i].Quality == IntervalQuality.SharpDissonance)
            {
                // Check if resolved in next time slice
                var nextInSameVoices = intervals.Skip(i + 1)
                    .FirstOrDefault(iv => iv.Voice1 == intervals[i].Voice1 &&
                                          iv.Voice2 == intervals[i].Voice2);

                if (nextInSameVoices.Quality == IntervalQuality.SharpDissonance)
                {
                    violations.Add(new CounterpointViolation
                    {
                        Type = "Unresolved Dissonance",
                        Description = $"Sharp dissonance ({intervals[i]}) not resolved by step",
                        Time = intervals[i].Time,
                        Voice1 = intervals[i].Voice1,
                        Voice2 = intervals[i].Voice2,
                        Severity = "Warning"
                    });
                }
            }
        }

        return violations;
    }

    private static MotionStatistics CalculateMotionStats(List<VoiceMotion> motions)
    {
        return new MotionStatistics
        {
            Parallel = motions.Count(m => m.Type == MotionType.Parallel),
            Similar = motions.Count(m => m.Type == MotionType.Similar),
            Contrary = motions.Count(m => m.Type == MotionType.Contrary),
            Oblique = motions.Count(m => m.Type == MotionType.Oblique),
            Static = motions.Count(m => m.Type == MotionType.Static)
        };
    }

    private static IntervalStatistics CalculateIntervalStats(List<VoiceInterval> intervals)
    {
        var counts = new int[12];
        foreach (var iv in intervals)
        {
            counts[iv.Interval]++;
        }

        return new IntervalStatistics
        {
            PerfectConsonances = intervals.Count(i => i.Quality == IntervalQuality.PerfectConsonance),
            ImperfectConsonances = intervals.Count(i => i.Quality == IntervalQuality.ImperfectConsonance),
            MildDissonances = intervals.Count(i => i.Quality == IntervalQuality.MildDissonance),
            SharpDissonances = intervals.Count(i => i.Quality == IntervalQuality.SharpDissonance)
        };
    }

    private static float CalculateTextureDensity(VoiceSeparationResult voices, List<Rational> timePoints)
    {
        if (timePoints.Count < 2) return voices.Voices.Count;

        float totalDensity = 0;

        for (int i = 0; i < timePoints.Count - 1; i++)
        {
            var midpoint = timePoints[i];
            var sounding = 0;

            foreach (var voice in voices.Voices)
            {
                if (voice.Notes.Any(n => n.Offset <= midpoint && n.End > midpoint))
                    sounding++;
            }

            totalDensity += sounding;
        }

        return totalDensity / (timePoints.Count - 1);
    }

    private static float CalculateVoiceIndependence(List<VoiceMotion> motions)
    {
        if (motions.Count == 0) return 1.0f;

        // Independence = proportion of contrary + oblique motion
        var independentMotions = motions.Count(m =>
            m.Type == MotionType.Contrary || m.Type == MotionType.Oblique);

        return (float)independentMotions / motions.Count;
    }

    private static float CalculateQuality(
        List<CounterpointViolation> violations,
        MotionStatistics motionStats,
        IntervalStatistics intervalStats,
        float independence)
    {
        float score = 1.0f;

        // Penalize violations
        foreach (var v in violations)
        {
            score -= v.Severity switch
            {
                "Error" => 0.15f,
                "Warning" => 0.05f,
                "Style" => 0.02f,
                _ => 0.01f
            };
        }

        // Bonus for variety of motion
        if (motionStats.Total > 0)
        {
            var varietyBonus = Math.Min(
                motionStats.ContraryPercent / 100f * 0.1f,
                0.1f);
            score += varietyBonus;
        }

        // Bonus for consonance/dissonance balance (aim for 70-80% consonance)
        var consonanceRatio = intervalStats.ConsonanceRatio / 100f;
        if (consonanceRatio is >= 0.6f and <= 0.9f)
            score += 0.05f;

        // Factor in independence
        score = score * 0.7f + independence * 0.3f;

        return Math.Clamp(score, 0f, 1f);
    }
}

public sealed class CounterpointRulesCheckResult
{
    public required int ParallelFifths { get; init; }
    public required int ParallelOctaves { get; init; }
    public required int HiddenParallels { get; init; }
    public required int VoiceCrossing { get; init; }
    public required int SpacingViolations { get; init; }
    public required float QualityScore { get; init; }
    public required IReadOnlyList<CounterpointViolation> Violations { get; init; }
}

public sealed class ImitationDetectionResult
{
    public required bool HasImitation { get; init; }
    public string Type { get; init; } = "";
    public int Interval { get; init; }
    public Rational TimeDelay { get; init; }
    public IReadOnlyList<int> VoicesInvolved { get; init; } = [];

    public static ImitationDetectionResult None => new()
    {
        HasImitation = false,
        Type = "",
        Interval = 0,
        TimeDelay = Rational.Zero,
        VoicesInvolved = []
    };
}

file static class PolyphonyAnalyzerHelpers
{
    public static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;

    public static (int crossings, int spacing) AnalyzeCrossingsAndSpacing(VoiceSeparationResult voices)
    {
        if (voices.Voices.Count < 2)
            return (0, 0);

        // Collect all distinct time points where any note starts.
        var times = new SortedSet<Rational>();
        foreach (var v in voices.Voices)
        {
            foreach (var n in v.Notes)
                times.Add(n.Offset);
        }

        var crossings = 0;
        var spacing = 0;

        foreach (var t in times)
        {
            var sounding = voices.Voices
                .Select(v => GetSoundingPitch(v, t))
                .ToArray();

            // Voice crossing: higher voice pitch < lower voice pitch at same time.
            for (var i = 0; i < sounding.Length - 1; i++)
            {
                if (sounding[i].HasValue && sounding[i + 1].HasValue && sounding[i]!.Value < sounding[i + 1]!.Value)
                    crossings++;
            }

            // Spacing: SA and AT within octave, TB within 2 octaves (heuristic).
            for (var i = 0; i < sounding.Length - 1; i++)
            {
                if (!sounding[i].HasValue || !sounding[i + 1].HasValue)
                    continue;

                var dist = Math.Abs(sounding[i]!.Value - sounding[i + 1]!.Value);
                var limit = i < 2 ? 12 : 24;
                if (dist > limit)
                    spacing++;
            }
        }

        return (crossings, spacing);
    }

    private static int? GetSoundingPitch(Voice voice, Rational t)
    {
        // Linear scan is fine for small examples.
        for (var i = voice.Notes.Count - 1; i >= 0; i--)
        {
            var n = voice.Notes[i];
            if (n.Offset <= t && n.End > t)
                return n.Pitch;
        }

        return null;
    }

    public static int[] ToIntervals(int[] pitches)
    {
        if (pitches.Length < 2)
            return [];

        var ints = new int[pitches.Length - 1];
        for (var i = 1; i < pitches.Length; i++)
            ints[i - 1] = pitches[i] - pitches[i - 1];

        return ints;
    }

    public static (int start1, int start2)? FindIntervalMatch(int[] a, int[] b, int len)
    {
        if (a.Length < len || b.Length < len)
            return null;

        for (var i = 0; i <= a.Length - len; i++)
        {
            for (var j = 0; j <= b.Length - len; j++)
            {
                var ok = true;
                for (var k = 0; k < len; k++)
                {
                    if (a[i + k] != b[j + k])
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return (i, j);
            }
        }

        return null;
    }
}
