// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

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
    /// <summary>Index of the first (upper) voice.</summary>
    public int Voice1 { get; init; }

    /// <summary>Index of the second (lower) voice.</summary>
    public int Voice2 { get; init; }

    /// <summary>Time offset of the interval, in whole-note units.</summary>
    public Rational Time { get; init; }

    /// <summary>MIDI pitch of the first voice (middle C = 60).</summary>
    public int Pitch1 { get; init; }

    /// <summary>MIDI pitch of the second voice (middle C = 60).</summary>
    public int Pitch2 { get; init; }

    /// <summary>Interval class in semitones reduced to 0-11 (octave-equivalent).</summary>
    public int Interval => Math.Abs(Pitch1 - Pitch2) % 12;

    /// <summary>Absolute distance between the two pitches in semitones, not octave-reduced.</summary>
    public int RawInterval => Math.Abs(Pitch1 - Pitch2);

    /// <summary>Consonance/dissonance classification of the interval class.</summary>
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

    /// <summary>Returns the interval name (e.g. <c>P5</c>) followed by its quality.</summary>
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
    /// <summary>Index of the first voice.</summary>
    public int Voice1 { get; init; }

    /// <summary>Index of the second voice.</summary>
    public int Voice2 { get; init; }

    /// <summary>Time offset of the starting interval, in whole-note units.</summary>
    public Rational FromTime { get; init; }

    /// <summary>Signed semitones moved by the first voice (positive = up).</summary>
    public int Voice1Motion { get; init; }  // Semitones moved (+/-)

    /// <summary>Signed semitones moved by the second voice (positive = up).</summary>
    public int Voice2Motion { get; init; }

    /// <summary>Classification of the relative motion between the two voices.</summary>
    public MotionType Type { get; init; }

    /// <summary>Interval between the voices before the motion.</summary>
    public VoiceInterval FromInterval { get; init; }

    /// <summary>Interval between the voices after the motion.</summary>
    public VoiceInterval ToInterval { get; init; }

    /// <summary>Check if this is hidden 5ths/octaves (similar motion to perfect interval).</summary>
    public bool IsHiddenPerfect =>
        Type == MotionType.Similar &&
        (ToInterval.Interval is 0 or 7 or 12);
}

/// <summary>
/// Counterpoint violation detected in the analysis.
/// </summary>
public sealed record CounterpointViolation
{
    /// <summary>Kind of violation (e.g. <c>Parallel Fifths</c>, <c>Large Leap</c>).</summary>
    public required string Type { get; init; }

    /// <summary>Human-readable description of the violation.</summary>
    public required string Description { get; init; }

    /// <summary>Time offset where the violation occurs, in whole-note units.</summary>
    public required Rational Time { get; init; }

    /// <summary>Index of the first voice involved.</summary>
    public required int Voice1 { get; init; }

    /// <summary>Index of the second voice involved.</summary>
    public required int Voice2 { get; init; }

    /// <summary>Severity label: <c>Error</c>, <c>Warning</c>, or <c>Style</c>.</summary>
    public required string Severity { get; init; } // "Error", "Warning", "Style"
}

/// <summary>
/// Complete polyphony analysis result.
/// </summary>
public sealed record PolyphonyAnalysisResult
{
    /// <summary>Result of separating the input into individual voices.</summary>
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

    /// <summary>Texture density: the time-weighted average number of voices sounding simultaneously
    /// (each segment between successive note starts/ends counts in proportion to its length).</summary>
    public float TextureDensity { get; init; }

    /// <summary>Voice independence score (0-1, higher = more independent voices).</summary>
    public float VoiceIndependence { get; init; }
}

/// <summary>
/// Statistics about motion types in the piece.
/// </summary>
public sealed class MotionStatistics
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal MotionStatistics() { }

    /// <summary>Number of parallel-motion transitions.</summary>
    public int Parallel { get; init; }

    /// <summary>Number of similar-motion transitions.</summary>
    public int Similar { get; init; }

    /// <summary>Number of contrary-motion transitions.</summary>
    public int Contrary { get; init; }

    /// <summary>Number of oblique-motion transitions.</summary>
    public int Oblique { get; init; }

    /// <summary>Number of static (no-movement) transitions.</summary>
    public int Static { get; init; }

    /// <summary>Total number of motion transitions counted.</summary>
    public int Total => Parallel + Similar + Contrary + Oblique + Static;

    /// <summary>Percentage of transitions that are parallel motion.</summary>
    public float ParallelPercent => Total > 0 ? Parallel * 100f / Total : 0;

    /// <summary>Percentage of transitions that are similar motion.</summary>
    public float SimilarPercent => Total > 0 ? Similar * 100f / Total : 0;

    /// <summary>Percentage of transitions that are contrary motion.</summary>
    public float ContraryPercent => Total > 0 ? Contrary * 100f / Total : 0;

    /// <summary>Percentage of transitions that are oblique motion.</summary>
    public float ObliquePercent => Total > 0 ? Oblique * 100f / Total : 0;
}

/// <summary>
/// Statistics about interval usage in the piece.
/// </summary>
public sealed class IntervalStatistics
{
    // Produced by analysis; not constructible by consumers (#18 API freeze).
    internal IntervalStatistics() { }

    /// <summary>Occurrence count for each interval class (index 0-11 = semitones mod 12).</summary>
    public int[] IntervalCounts { get; } = new int[12];

    /// <summary>Number of perfect-consonance intervals (unison, fifth, octave, fourth).</summary>
    public int PerfectConsonances { get; init; }

    /// <summary>Number of imperfect-consonance intervals (thirds and sixths).</summary>
    public int ImperfectConsonances { get; init; }

    /// <summary>Number of mild-dissonance intervals (major second, minor seventh).</summary>
    public int MildDissonances { get; init; }

    /// <summary>Number of sharp-dissonance intervals (minor second, major seventh, tritone).</summary>
    public int SharpDissonances { get; init; }

    /// <summary>Total number of classified intervals.</summary>
    public int Total => PerfectConsonances + ImperfectConsonances + MildDissonances + SharpDissonances;

    /// <summary>Percentage of intervals that are consonant (perfect + imperfect); 100 when none.</summary>
    public float ConsonanceRatio => Total > 0
        ? (PerfectConsonances + ImperfectConsonances) * 100f / Total
        : 100f;

    /// <summary>Percentage of intervals that are dissonant (mild + sharp); 0 when none.</summary>
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
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static CounterpointRulesCheckResult CheckCounterpointRules(IEnumerable<NoteEvent> notes, int maxVoices = 4)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var arr = notes as NoteEvent[] ?? [.. notes];
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return CheckCounterpointRules(buffer, maxVoices);
    }

    /// <summary>
    /// Convenience wrapper used by examples: checks basic counterpoint issues and returns counts.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static CounterpointRulesCheckResult CheckCounterpointRules(NoteBuffer buffer, int maxVoices = 4)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var analysis = Analyze(buffer, maxVoices);
        var violations = analysis.Violations;

        var parallel5Ths = violations.Count(v => v.Type == "Parallel Fifths");
        var parallel8Ves = violations.Count(v => v.Type == "Parallel Octaves");
        var hidden = violations.Count(v => v.Type == "Hidden Perfect Interval");

        var (voiceCrossing, spacing) = PolyphonyAnalyzerHelpers.AnalyzeCrossingsAndSpacing(analysis.Voices);

        return new CounterpointRulesCheckResult
        {
            ParallelFifths = parallel5Ths,
            ParallelOctaves = parallel8Ves,
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
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static ImitationDetectionResult DetectImitation(IEnumerable<NoteEvent> notes, int maxVoices = 4)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var arr = notes as NoteEvent[] ?? [.. notes];
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return DetectImitation(buffer, maxVoices);
    }

    /// <summary>
    /// Detect simple imitation (canon-like) between voices.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static ImitationDetectionResult DetectImitation(NoteBuffer buffer, int maxVoices = 4)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var voices = VoiceSeparator.Separate(buffer, maxVoices);
        if (voices.Voices.Count < 2)
        {
            return ImitationDetectionResult.None;
        }

        // Build interval sequences for each voice (Voice.Notes are already time-ordered).
        var sequences = voices.Voices
            .Select(v => v.Notes
                .Select(n => n.Pitch)
                .ToArray())
            .ToArray();

        // Use a small motif length for detection.
        const int motifLen = 4;
        for (var v1 = 0; v1 < sequences.Length; v1++)
        {
            var s1 = sequences[v1];
            if (s1.Length < motifLen)
            {
                continue;
            }

            var i1 = PolyphonyAnalyzerHelpers.ToIntervals(s1);
            for (var v2 = v1 + 1; v2 < sequences.Length; v2++)
            {
                var s2 = sequences[v2];
                if (s2.Length < motifLen)
                {
                    continue;
                }

                // Two voices doubling one line are not a canon, however distinctive the line.
                // The zero-delay guard below only rejects the aligned match, and a melody with
                // a repeating figure also matches itself at a shifted position — so strict
                // octave doubling of a tune that repeats was reported as a canon at that shift.
                if (PolyphonyAnalyzerHelpers.MoveTogether(voices.Voices[v1], voices.Voices[v2]))
                {
                    continue;
                }

                var i2 = PolyphonyAnalyzerHelpers.ToIntervals(s2);
                foreach (var (start1, start2) in PolyphonyAnalyzerHelpers.FindIntervalMatches(i1, i2, motifLen - 1))
                {
                    // A real imitation needs a distinctive motif: a run of identical
                    // intervals (any shared scale fragment) matches trivially and is
                    // not a canon.
                    if (!PolyphonyAnalyzerHelpers.HasDistinctIntervals(i1, start1, motifLen - 1, minDistinct: 2))
                    {
                        continue;
                    }

                    var p1 = s1[start1];
                    var p2 = s2[start2];

                    // Time delay between the two entries; either voice may lead, but a
                    // zero delay is simultaneous (parallel) motion, not imitation.
                    var t1 = voices.Voices[v1].Notes[start1].Offset;
                    var t2 = voices.Voices[v2].Notes[start2].Offset;
                    var delay = t2 - t1;
                    if (delay == Rational.Zero)
                    {
                        continue;
                    }

                    // The subject is whichever voice states the motif first and the answer is
                    // the other, so the interval — how far the answer transposes the subject —
                    // has to be read in that order. Taken from the voice list it followed
                    // register instead, and a canon answered an octave ABOVE was reported at -12.
                    var leads = delay > Rational.Zero;
                    var interval = leads ? p2 - p1 : p1 - p2;

                    if (delay < Rational.Zero)
                    {
                        delay = -delay;
                    }

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
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static PolyphonyAnalysisResult Analyze(NoteBuffer buffer, int maxVoices = 4)
    {
        ArgumentNullException.ThrowIfNull(buffer);

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
                // Measured the same way as for several voices: TextureDensity is documented as
                // the time-weighted average number of voices sounding, and reporting the voice
                // COUNT here said 1.0 for a single line that is silent for half its span.
                TextureDensity = voices.Voices.Count == 0
                    ? 0f
                    : CalculateTextureDensity(voices, CollectTimePoints(voices)),
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
        var violations = DetectViolations(motions, intervals, voices);

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

        return [.. times.OrderBy(t => t)];
    }

    private static List<VoiceInterval> AnalyzeIntervals(
        VoiceSeparationResult voices,
        List<Rational> timePoints)
    {
        // PERF: O(timePoints × notes) sweep via GetSoundingNotes; a sweep-line over
        // note start/end events would bring this down to O(N log N).
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
            // Explicit search instead of FirstOrDefault + pitch sentinel, so MIDI
            // pitch 0 is treated as a real note rather than "no note".
            var voice = voices.Voices[v];
            foreach (var note in voice.Notes)
            {
                if (note.Offset <= time && note.End > time)
                {
                    result.Add((v, note.Pitch));
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Pitch of the given voice in a sounding-notes list, or null when the voice
    /// is silent (avoids the MIDI-pitch-0 sentinel problem of FirstOrDefault).
    /// </summary>
    private static int? FindVoicePitch(List<(int voiceIdx, int pitch)> notes, int voiceIdx)
    {
        foreach (var (v, pitch) in notes)
        {
            if (v == voiceIdx)
            {
                return pitch;
            }
        }

        return null;
    }

    private static List<VoiceMotion> AnalyzeMotions(
        VoiceSeparationResult voices,
        List<Rational> timePoints)
    {
        // PERF: O(timePoints × notes) sweep via GetSoundingNotes; a sweep-line over
        // note start/end events would bring this down to O(N log N).
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
                    var pitch1T1 = FindVoicePitch(notes1, i);
                    var pitch2T1 = FindVoicePitch(notes1, j);
                    var pitch1T2 = FindVoicePitch(notes2, i);
                    var pitch2T2 = FindVoicePitch(notes2, j);

                    if (pitch1T1 is not { } p1T1 || pitch2T1 is not { } p2T1 ||
                        pitch1T2 is not { } p1T2 || pitch2T2 is not { } p2T2)
                    {
                        continue;
                    }

                    var motion1 = p1T2 - p1T1;
                    var motion2 = p2T2 - p2T1;
                    var motionType = ClassifyMotion(motion1, motion2);

                    motions.Add(new VoiceMotion
                    {
                        Voice1 = i,
                        Voice2 = j,
                        FromTime = time1,
                        Voice1Motion = motion1,
                        Voice2Motion = motion2,
                        Type = motionType,
                        FromInterval = new VoiceInterval
                        {
                            Voice1 = i,
                            Voice2 = j,
                            Time = time1,
                            Pitch1 = p1T1,
                            Pitch2 = p2T1
                        },
                        ToInterval = new VoiceInterval
                        {
                            Voice1 = i,
                            Voice2 = j,
                            Time = time2,
                            Pitch1 = p1T2,
                            Pitch2 = p2T2
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
        {
            return MotionType.Static;
        }

        if (motion1 == 0 || motion2 == 0)
        {
            return MotionType.Oblique;
        }

        if (Math.Sign(motion1) != Math.Sign(motion2))
        {
            return MotionType.Contrary;
        }

        if (motion1 == motion2)
        {
            return MotionType.Parallel;
        }

        return MotionType.Similar;
    }

    private static List<CounterpointViolation> DetectViolations(
        List<VoiceMotion> motions,
        List<VoiceInterval> intervals,
        VoiceSeparationResult voices)
    {
        // Pre-allocate assuming few violations (optimistic case)
        var violations = new List<CounterpointViolation>(motions.Count / 10);

        // (voice, moment) pairs whose leap has already been reported: motions are per voice
        // pair, so the same leap arrives once for every other voice sounding beside it.
        var leapsReported = new HashSet<(int Voice, Rational Time)>();

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

            // Hidden fifths/octaves (similar motion to perfect interval in outer voices).
            // The outer voices are the highest and the lowest — the list is ordered by register,
            // so that is the first and the last. The guard used to be "either voice is the
            // highest", which let soprano-alto and soprano-tenor pairs through and then
            // described them, in the text below, as being in the outer voices.
            if (motion.IsHiddenPerfect
                && motion.Voice1 == 0
                && motion.Voice2 == voices.Voices.Count - 1)
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

            // A leap is something one voice does, but motions are recorded per voice PAIR, so
            // a single leap in the top voice of four was reported three times — once for each
            // voice it happened to be paired with. Report each leaping voice once per moment.
            ReportLeap(motion.Voice1, motion.Voice1Motion);
            ReportLeap(motion.Voice2, motion.Voice2Motion);

            void ReportLeap(int voice, int semitones)
            {
                if (Math.Abs(semitones) <= 12 || !leapsReported.Add((voice, motion.FromTime)))
                {
                    return;
                }

                violations.Add(new CounterpointViolation
                {
                    Type = "Large Leap",
                    Description = $"Voice {voice + 1} leaps more than an octave",
                    Time = motion.FromTime,
                    Voice1 = voice,
                    Voice2 = voice,
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

        // Check for unresolved dissonances. Evaluate a voice-pair interval only at
        // time points where at least one of the pair's notes has its ONSET: the
        // interval list re-samples every sounding pair at EVERY global time point,
        // so a single sustained dissonance beside a moving third voice would
        // otherwise be re-counted once per time point.
        var onsetTimes = new HashSet<Rational>[voices.Voices.Count];
        for (int v = 0; v < voices.Voices.Count; v++)
        {
            onsetTimes[v] = [.. voices.Voices[v].Notes.Select(n => n.Offset)];
        }

        for (int i = 0; i < intervals.Count; i++)
        {
            var current = intervals[i];
            if (current.Quality != IntervalQuality.SharpDissonance)
            {
                continue;
            }

            if (!onsetTimes[current.Voice1].Contains(current.Time) &&
                !onsetTimes[current.Voice2].Contains(current.Time))
            {
                continue; // Sustained state, already evaluated at its onset.
            }

            // Resolution check via the pair's next interval (index scan; the interval
            // list is ordered by time). No later interval means the dissonance was
            // never resolved.
            var resolved = false;
            for (int j = i + 1; j < intervals.Count; j++)
            {
                if (intervals[j].Voice1 != current.Voice1 || intervals[j].Voice2 != current.Voice2)
                {
                    continue;
                }

                // An interval is recorded at every time point where ANY voice has an onset, so
                // a third voice subdividing the beat produces an entry for this pair with both
                // of its pitches unchanged — the same dissonance still being held. Reading that
                // as the resolution reported "not resolved by step" for a dissonance that does
                // resolve, on the next beat, as soon as any other voice moved in between.
                if (intervals[j].Pitch1 == current.Pitch1 && intervals[j].Pitch2 == current.Pitch2)
                {
                    continue;
                }

                resolved = intervals[j].Quality != IntervalQuality.SharpDissonance;
                break;
            }

            if (!resolved)
            {
                violations.Add(new CounterpointViolation
                {
                    Type = "Unresolved Dissonance",
                    Description = $"Sharp dissonance ({current}) not resolved by step",
                    Time = current.Time,
                    Voice1 = current.Voice1,
                    Voice2 = current.Voice2,
                    Severity = "Warning"
                });
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
        int perfect = 0, imperfect = 0, mild = 0, sharp = 0;
        foreach (var iv in intervals)
        {
            counts[iv.Interval]++;
            switch (iv.Quality)
            {
                case IntervalQuality.PerfectConsonance:
                    perfect++;
                    break;
                case IntervalQuality.ImperfectConsonance:
                    imperfect++;
                    break;
                case IntervalQuality.MildDissonance:
                    mild++;
                    break;
                default:
                    sharp++;
                    break;
            }
        }

        var stats = new IntervalStatistics
        {
            PerfectConsonances = perfect,
            ImperfectConsonances = imperfect,
            MildDissonances = mild,
            SharpDissonances = sharp
        };

        // IntervalCounts is a fixed get-only array on the stats object; fill it in
        // place (the local histogram used to be built and then dropped, leaving the
        // property permanently all zeros).
        counts.CopyTo(stats.IntervalCounts, 0);
        return stats;
    }

    private static float CalculateTextureDensity(VoiceSeparationResult voices, List<Rational> timePoints)
    {
        if (timePoints.Count < 2)
        {
            return voices.Voices.Count;
        }

        // PERF: O(timePoints × notes) sweep; a sweep-line over note start/end events
        // would bring this down to O(N log N).
        double weightedSum = 0;
        double totalLength = 0;

        for (int i = 0; i < timePoints.Count - 1; i++)
        {
            // Time points include every note start and end, so the set of sounding
            // voices is constant across the segment; sample it at the segment start
            // and weight by the segment's length (an unweighted per-segment average
            // would let eight sixteenth-note segments outvote one whole-note chord).
            var segmentStart = timePoints[i];
            var segmentLength = (timePoints[i + 1] - segmentStart).ToDouble();
            var sounding = 0;

            foreach (var voice in voices.Voices)
            {
                if (voice.Notes.Any(n => n.Offset <= segmentStart && n.End > segmentStart))
                {
                    sounding++;
                }
            }

            weightedSum += sounding * segmentLength;
            totalLength += segmentLength;
        }

        return totalLength > 0 ? (float)(weightedSum / totalLength) : voices.Voices.Count;
    }

    private static float CalculateVoiceIndependence(List<VoiceMotion> motions)
    {
        if (motions.Count == 0)
        {
            return 1.0f;
        }

        // Independence = proportion of contrary + oblique motion
        var independentMotions = motions.Count(m =>
            m.Type is MotionType.Contrary or MotionType.Oblique);

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
        {
            score += 0.05f;
        }

        // Factor in independence
        score = (score * 0.7f) + (independence * 0.3f);

        return Math.Clamp(score, 0f, 1f);
    }
}

/// <summary>
/// Summary counts from a basic counterpoint rules check.
/// </summary>
public sealed record CounterpointRulesCheckResult
{
    /// <summary>Number of parallel-fifth motions detected.</summary>
    public required int ParallelFifths { get; init; }

    /// <summary>Number of parallel-octave/unison motions detected.</summary>
    public required int ParallelOctaves { get; init; }

    /// <summary>Number of hidden (direct) perfect-interval motions detected.</summary>
    public required int HiddenParallels { get; init; }

    /// <summary>Number of voice-crossing occurrences detected.</summary>
    public required int VoiceCrossing { get; init; }

    /// <summary>Number of voice-spacing violations detected.</summary>
    public required int SpacingViolations { get; init; }

    /// <summary>Overall quality score in the range 0-1.</summary>
    public required float QualityScore { get; init; }

    /// <summary>Full list of counterpoint violations underlying the counts.</summary>
    public required IReadOnlyList<CounterpointViolation> Violations { get; init; }
}

/// <summary>
/// Result of detecting imitation (canon-like repetition) between voices.
/// </summary>
public sealed record ImitationDetectionResult
{
    /// <summary>Whether imitation was detected.</summary>
    public required bool HasImitation { get; init; }

    /// <summary>Kind of imitation (e.g. <c>Canon</c>); empty when none.</summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// How far the answer transposes the subject, in semitones: positive when the voice that
    /// enters second is higher, negative when it is lower. A canon answered an octave below
    /// reports -12.
    /// </summary>
    public int Interval { get; init; }

    /// <summary>Time delay between the leading and following voice, in whole-note units.</summary>
    public Rational TimeDelay { get; init; }

    /// <summary>One-based indices of the voices involved in the imitation.</summary>
    public IReadOnlyList<int> VoicesInvolved { get; init; } = [];

    /// <summary>Shared instance representing no detected imitation.</summary>
    public static ImitationDetectionResult None => new()
    {
        HasImitation = false,
        Type = "",
        Interval = 0,
        TimeDelay = Rational.Zero,
        VoicesInvolved = []
    };
}

static file class PolyphonyAnalyzerHelpers
{
    public static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> are one line doubled: every note
    /// starts when its counterpart does and stays the same interval away. A canon is one voice
    /// answering another later; voices moving together in octaves or thirds are a single idea.
    /// </summary>
    public static bool MoveTogether(Voice a, Voice b)
    {
        if (a.Notes.Count == 0 || a.Notes.Count != b.Notes.Count)
        {
            return false;
        }

        var interval = b.Notes[0].Pitch - a.Notes[0].Pitch;
        for (var i = 0; i < a.Notes.Count; i++)
        {
            if (a.Notes[i].Offset != b.Notes[i].Offset ||
                b.Notes[i].Pitch - a.Notes[i].Pitch != interval)
            {
                return false;
            }
        }

        return true;
    }

    public static (int crossings, int spacing) AnalyzeCrossingsAndSpacing(VoiceSeparationResult voices)
    {
        if (voices.Voices.Count < 2)
        {
            return (0, 0);
        }

        // Collect all distinct time points where any note starts.
        var times = new SortedSet<Rational>();
        foreach (var v in voices.Voices)
        {
            foreach (var n in v.Notes)
            {
                times.Add(n.Offset);
            }
        }

        var crossings = 0;
        var spacing = 0;
        var lowestVoiceIndex = voices.Voices.Max(v => v.Index);

        foreach (var t in times)
        {
            var sounding = voices.Voices
                .Select(v => GetSoundingPitch(v, t))
                .ToArray();

            // Voice crossing: higher voice pitch < lower voice pitch at same time.
            for (var i = 0; i < sounding.Length - 1; i++)
            {
                if (sounding[i].HasValue && sounding[i + 1].HasValue && sounding[i]!.Value < sounding[i + 1]!.Value)
                {
                    crossings++;
                }
            }

            // Spacing: upper voices within an octave of each other, the bass within two
            // octaves of the voice above it (heuristic).
            for (var i = 0; i < sounding.Length - 1; i++)
            {
                if (!sounding[i].HasValue || !sounding[i + 1].HasValue)
                {
                    continue;
                }

                var dist = Math.Abs(sounding[i]!.Value - sounding[i + 1]!.Value);

                // Which pair this is follows the voices' own SATB slots, not their position in
                // this list, which holds only the voices that are present. With an empty slot
                // above them, a tenor and a bass landed at positions 0 and 1 and were judged by
                // the soprano-alto octave rule, so an ordinary tenor/bass duet was reported as
                // badly spaced.
                var limit = voices.Voices[i + 1].Index == lowestVoiceIndex ? 24 : 12;
                if (dist > limit)
                {
                    spacing++;
                }
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
            {
                return n.Pitch;
            }
        }

        return null;
    }

    public static int[] ToIntervals(int[] pitches)
    {
        if (pitches.Length < 2)
        {
            return [];
        }

        var ints = new int[pitches.Length - 1];
        for (var i = 1; i < pitches.Length; i++)
        {
            ints[i - 1] = pitches[i] - pitches[i - 1];
        }

        return ints;
    }

    public static IEnumerable<(int start1, int start2)> FindIntervalMatches(int[] a, int[] b, int len)
    {
        if (a.Length < len || b.Length < len)
        {
            yield break;
        }

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
                {
                    yield return (i, j);
                }
            }
        }
    }

    /// <summary>
    /// Whether the interval window [start, start+len) contains at least
    /// <paramref name="minDistinct"/> distinct interval values.
    /// </summary>
    public static bool HasDistinctIntervals(int[] intervals, int start, int len, int minDistinct)
    {
        var distinct = 0;
        for (var i = start; i < start + len; i++)
        {
            var seen = false;
            for (var j = start; j < i; j++)
            {
                if (intervals[j] == intervals[i])
                {
                    seen = true;
                    break;
                }
            }

            if (!seen && ++distinct >= minDistinct)
            {
                return true;
            }
        }

        return distinct >= minDistinct;
    }
}
