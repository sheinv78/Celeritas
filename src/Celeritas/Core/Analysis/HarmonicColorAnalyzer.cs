// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;
using Celeritas.Core.Harmonization;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Detects harmonic color features: chromatic notes, modal turns (mixture / modal borrowing),
/// and simple melodic harmony (chord tones vs common non-chord tones).
/// </summary>
public static class HarmonicColorAnalyzer
{
    /// <summary>
    /// Analyze melody + chord assignments in a known key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="chords"/> is <see langword="null"/>.</exception>
    public static HarmonicColorAnalysisResult Analyze(
        ReadOnlySpan<NoteEvent> melody,
        IReadOnlyList<ChordAssignment> chords,
        KeySignature key,
        HarmonicColorAnalysisOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chords);

        options ??= HarmonicColorAnalysisOptions.Default;

        var baseKey = ModalKey.FromKeySignature(key);
        var baseScaleMask = key.GetScaleMask();

        // Sort melody by time (defensive). Chords are expected to be ordered already.
        var orderedMelody = OrderByOffset(melody);

        var chromatic = AnalyzeChromaticNotes(orderedMelody, baseScaleMask, key);
        var melodicHarmony = AnalyzeMelodicHarmony(orderedMelody, chords);
        var modalTurns = AnalyzeModalTurns(chords, baseKey, options);

        return new HarmonicColorAnalysisResult(
            key: key,
            chromaticNotes: chromatic,
            modalTurns: modalTurns,
            melodicHarmony: melodicHarmony);
    }

    /// <summary>
    /// Analyze melody + a tuple-based chord progression (as used in examples).
    /// Each tuple is (ChordSymbol, StartTime). End times are inferred from the next chord,
    /// and the last chord defaults to a duration of 1 whole note.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="chordProgression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A chord symbol in <paramref name="chordProgression"/> cannot be parsed.</exception>
    public static HarmonicColorAnalysisResult Analyze(
        ReadOnlySpan<NoteEvent> melody,
        IReadOnlyList<(string Chord, Rational Start)> chordProgression,
        KeySignature key,
        HarmonicColorAnalysisOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chordProgression);

        if (chordProgression.Count == 0)
            return Analyze(melody, Array.Empty<ChordAssignment>(), key, options);

        var chords = BuildChordAssignments(chordProgression);
        return Analyze(melody, chords, key, options);
    }

    /// <summary>
    /// Analyze melody + a tuple-based chord progression (as used in examples).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="melody"/> or <paramref name="chordProgression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A chord symbol in <paramref name="chordProgression"/> cannot be parsed.</exception>
    public static HarmonicColorAnalysisResult Analyze(
        NoteEvent[] melody,
        (string Chord, Rational Start)[] chordProgression,
        KeySignature key,
        HarmonicColorAnalysisOptions? options = null)
    {
        // AsSpan() is null-safe and returns an empty span, so a null melody would be analyzed
        // as an empty one and answered "C Minor ... Mostly diatonic and stable."
        ArgumentNullException.ThrowIfNull(melody);
        ArgumentNullException.ThrowIfNull(chordProgression);
        return Analyze(melody.AsSpan(), chordProgression, key, options);
    }

    private static ChordAssignment[] BuildChordAssignments(IReadOnlyList<(string Chord, Rational Start)> chordProgression)
    {
        var n = chordProgression.Count;
        var chords = new ChordAssignment[n];

        for (var i = 0; i < n; i++)
        {
            var symbol = chordProgression[i].Chord;
            var start = chordProgression[i].Start;
            var end = i + 1 < n ? chordProgression[i + 1].Start : start + Rational.Whole;

            // Defensive: avoid zero/negative duration chords.
            if (end <= start)
                end = start + Rational.Whole;

            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                // A silent empty pitch set would produce a zero chord mask, making
                // every melody note over this chord an "OtherNonChordTone".
                throw new ArgumentException(
                    $"Unparseable chord symbol '{symbol}' at index {i} of the chord progression.",
                    nameof(chordProgression));
            }

            var mask = ChordAnalyzer.GetMask(pitches);
            var info = ChordLibrary.GetChord(mask);

            chords[i] = new ChordAssignment(start, end, info, pitches);
        }

        return chords;
    }

    private static IReadOnlyList<ChromaticPitchEvent> AnalyzeChromaticNotes(
        IReadOnlyList<NoteEvent> melody,
        ushort baseScaleMask,
        KeySignature key)
    {
        if (melody.Count == 0)
            return [];

        var result = new List<ChromaticPitchEvent>();

        for (var i = 0; i < melody.Count; i++)
        {
            var pitch = melody[i].Pitch;
            var pc = Mod12(pitch);

            if ((baseScaleMask & (1 << pc)) != 0)
                continue;

            result.Add(new ChromaticPitchEvent(
                Offset: melody[i].Offset,
                Pitch: pitch,
                PitchClass: (byte)pc,
                NoteName: ChordLibrary.NoteNames[pc],
                Alteration: DescribeAlteration(key, pc)));
        }

        return result;
    }

    private static IReadOnlyList<MelodicHarmonyEvent> AnalyzeMelodicHarmony(
        IReadOnlyList<NoteEvent> melody,
        IReadOnlyList<ChordAssignment> chords)
    {
        if (melody.Count == 0)
            return [];

        // If there are no chords, we can still emit a trivial analysis.
        if (chords.Count == 0)
        {
            var noChord = new MelodicHarmonyEvent[melody.Count];
            for (var i = 0; i < melody.Count; i++)
            {
                var pc = Mod12(melody[i].Pitch);
                noChord[i] = new MelodicHarmonyEvent(
                    Offset: melody[i].Offset,
                    Pitch: melody[i].Pitch,
                    PitchClass: (byte)pc,
                    ChordStart: melody[i].Offset,
                    ChordEnd: melody[i].Offset + melody[i].Duration,
                    ChordMask: 0,
                    IsChordTone: false,
                    Type: MelodicHarmonyEventType.Unclassified,
                    Description: "No chord context");
            }

            return noChord;
        }

        var events = new MelodicHarmonyEvent[melody.Count];
        var chordIndex = 0;

        // Precompute chord masks.
        var chordMasks = new ushort[chords.Count];
        for (var i = 0; i < chords.Count; i++)
        {
            chordMasks[i] = ChordAnalyzer.GetMask(chords[i].Pitches);
        }

        // First pass: chord tone or not.
        for (var i = 0; i < melody.Count; i++)
        {
            var note = melody[i];
            chordIndex = FindChordIndexAtTime(chords, note.Offset, chordIndex);

            var mask = chordMasks[chordIndex];
            var pc = Mod12(note.Pitch);
            var isChordTone = (mask & (1 << pc)) != 0;

            events[i] = new MelodicHarmonyEvent(
                Offset: note.Offset,
                Pitch: note.Pitch,
                PitchClass: (byte)pc,
                ChordStart: chords[chordIndex].Start,
                ChordEnd: chords[chordIndex].End,
                ChordMask: mask,
                IsChordTone: isChordTone,
                Type: isChordTone ? MelodicHarmonyEventType.ChordTone : MelodicHarmonyEventType.OtherNonChordTone,
                Description: isChordTone ? "Chord tone" : "Non-chord tone");
        }

        // Second pass: classify a few common non-chord tones using simple, local heuristics.
        for (var i = 0; i < events.Length; i++)
        {
            if (events[i].IsChordTone)
                continue;

            var prevIndex = i - 1;
            var nextIndex = i + 1;

            MelodicHarmonyEvent? prev = prevIndex >= 0 ? events[prevIndex] : null;
            MelodicHarmonyEvent? next = nextIndex < events.Length ? events[nextIndex] : null;

            // Prefer analyzing within the same chord slice.
            var inSameChordAsNext = next.HasValue && next.Value.ChordStart == events[i].ChordStart;
            var inSameChordAsPrev = prev.HasValue && prev.Value.ChordStart == events[i].ChordStart;

            // Appoggiatura heuristic: starts exactly on chord change and resolves by step.
            if (events[i].Offset == events[i].ChordStart && next.HasValue && inSameChordAsNext)
            {
                if (next.Value.IsChordTone && IsStep(events[i].Pitch, next.Value.Pitch))
                {
                    events[i] = events[i] with
                    {
                        Type = MelodicHarmonyEventType.Appoggiatura,
                        Description = "Appoggiatura-like: resolves by step"
                    };
                    continue;
                }
            }

            // Passing tone: chord tone -> step -> chord tone in same direction.
            if (prev.HasValue && next.HasValue && inSameChordAsPrev && inSameChordAsNext)
            {
                if (prev.Value.IsChordTone && next.Value.IsChordTone &&
                    IsStep(prev.Value.Pitch, events[i].Pitch) && IsStep(events[i].Pitch, next.Value.Pitch))
                {
                    var a = events[i].Pitch - prev.Value.Pitch;
                    var b = next.Value.Pitch - events[i].Pitch;
                    if (a != 0 && b != 0 && Math.Sign(a) == Math.Sign(b))
                    {
                        events[i] = events[i] with
                        {
                            Type = MelodicHarmonyEventType.PassingTone,
                            Description = "Passing tone"
                        };
                        continue;
                    }
                }

                // Neighbor tone: chord tone -> step away -> return.
                if (prev.Value.IsChordTone && next.Value.IsChordTone && prev.Value.Pitch == next.Value.Pitch &&
                    IsStep(prev.Value.Pitch, events[i].Pitch) && IsStep(events[i].Pitch, next.Value.Pitch))
                {
                    events[i] = events[i] with
                    {
                        Type = MelodicHarmonyEventType.NeighborTone,
                        Description = "Neighbor tone"
                    };
                    continue;
                }
            }

            // Suspension heuristic: note is held into a new chord, then resolves down by step.
            if (prev.HasValue && next.HasValue)
            {
                var chordChanged = prev.Value.ChordStart != events[i].ChordStart;
                if (chordChanged && prev.Value.Pitch == events[i].Pitch &&
                    prev.Value.IsChordTone && !events[i].IsChordTone &&
                    next.Value.IsChordTone && IsStep(events[i].Pitch, next.Value.Pitch) && next.Value.Pitch < events[i].Pitch)
                {
                    events[i] = events[i] with
                    {
                        Type = MelodicHarmonyEventType.Suspension,
                        Description = "Suspension-like: held tone resolves down"
                    };
                }
            }
        }

        return events;
    }

    private static IReadOnlyList<ModalTurnEvent> AnalyzeModalTurns(
        IReadOnlyList<ChordAssignment> chords,
        ModalKey baseKey,
        HarmonicColorAnalysisOptions options)
    {
        if (chords.Count == 0)
            return [];

        var window = Math.Clamp(options.ModalTurnWindowChords, 2, 16);
        if (chords.Count < window)
            window = chords.Count;

        // Precompute chord pitch-class masks.
        var chordMasks = new ushort[chords.Count];
        for (var i = 0; i < chords.Count; i++)
            chordMasks[i] = ChordAnalyzer.GetMask(chords[i].Pitches);

        var baseScale = ModeLibrary.GetScaleMask(baseKey);
        var result = new List<ModalTurnEvent>();

        for (var start = 0; start <= chords.Count - window; start++)
        {
            ushort usedMask = 0;
            for (var j = 0; j < window; j++)
                usedMask |= chordMasks[start + j];

            var usedCount = BitOperations.PopCount(usedMask);
            if (usedCount == 0)
                continue;

            var baseCoverage = Coverage(usedMask, baseScale);
            var (bestMode, bestCoverage) = FindBestMode(baseKey.Root, baseKey.Mode, usedMask);

            // Only report if we get a meaningful improvement.
            var improvement = bestCoverage - baseCoverage;
            if (bestMode == baseKey.Mode)
                continue;

            if (bestCoverage < options.MinModalTurnCoverage)
                continue;

            if (improvement < options.MinModalTurnImprovement)
                continue;

            // Avoid spamming: coalesce overlapping windows of same mode.
            var end = start + window - 1;
            if (result.Count > 0)
            {
                var last = result[^1];
                if (last.Mode == bestMode && start <= last.EndChordIndex)
                {
                    var updated = last with
                    {
                        EndChordIndex = end,
                        Confidence = Math.Max(last.Confidence, improvement)
                    };
                    result[^1] = updated;
                    continue;
                }
            }

            var outOfBase = (ushort)(usedMask & (ushort)~baseScale);
            result.Add(new ModalTurnEvent(
                StartChordIndex: start,
                EndChordIndex: end,
                Mode: bestMode,
                Confidence: improvement,
                OutOfKeyPitchClassMask: outOfBase));
        }

        return result;
    }

    private static (Mode mode, double coverage) FindBestMode(byte root, Mode baseMode, ushort usedMask)
    {
        // Keep the search small and musical.
        ReadOnlySpan<Mode> candidates = baseMode == Mode.Ionian
            ? [Mode.Ionian, Mode.Lydian, Mode.Mixolydian, Mode.LydianDominant, Mode.Aeolian]
            : [Mode.Aeolian, Mode.Dorian, Mode.Phrygian, Mode.HarmonicMinor, Mode.MelodicMinor, Mode.PhrygianDominant, Mode.Locrian];

        var bestMode = baseMode;
        var bestCoverage = Coverage(usedMask, ModeLibrary.GetScaleMask(new ModalKey(root, baseMode)));

        for (var i = 0; i < candidates.Length; i++)
        {
            var mode = candidates[i];
            var mask = ModeLibrary.GetScaleMask(new ModalKey(root, mode));
            var cov = Coverage(usedMask, mask);
            if (cov > bestCoverage)
            {
                bestCoverage = cov;
                bestMode = mode;
            }
        }

        return (bestMode, bestCoverage);
    }

    private static double Coverage(ushort usedMask, ushort scaleMask)
    {
        var used = BitOperations.PopCount(usedMask);
        if (used == 0)
            return 0;
        var covered = BitOperations.PopCount((uint)(usedMask & scaleMask));
        return (double)covered / used;
    }

    private static int FindChordIndexAtTime(IReadOnlyList<ChordAssignment> chords, Rational time, int startIndex)
    {
        var i = Math.Clamp(startIndex, 0, chords.Count - 1);

        // Move forward while time is beyond current chord.
        while (i + 1 < chords.Count && time >= chords[i].End)
            i++;

        // Move backward if time is before current chord start.
        while (i > 0 && time < chords[i].Start)
            i--;

        return i;
    }

    private static bool IsStep(int a, int b)
    {
        var d = Math.Abs(b - a);
        return d is 1 or 2;
    }

    private static int Mod12(int pitch)
    {
        var pc = pitch % 12;
        return pc < 0 ? pc + 12 : pc;
    }

    private static string DescribeAlteration(KeySignature key, int pitchClass)
    {
        // Describe relative to tonic in semitones.
        var interval = (pitchClass - key.Root + 12) % 12;

        // Major reference: 0,2,4,5,7,9,11.
        // Natural minor reference: 0,2,3,5,7,8,10.
        // For non-diatonic intervals, label common alterations.
        return interval switch
        {
            1 => "b2",
            3 when key.IsMajor => "b3",
            4 when !key.IsMajor => "#3",
            6 => key.IsMajor ? "#4" : "#4/b5",
            8 when key.IsMajor => "b6",
            9 when !key.IsMajor => "#6",
            10 when key.IsMajor => "b7",
            11 when !key.IsMajor => "#7",
            _ => "chromatic"
        };
    }

    private static IReadOnlyList<NoteEvent> OrderByOffset(ReadOnlySpan<NoteEvent> melody)
    {
        if (melody.IsEmpty)
            return [];

        // Fast path if already ordered.
        var ordered = true;
        for (var i = 1; i < melody.Length; i++)
        {
            if (melody[i - 1].Offset > melody[i].Offset)
            {
                ordered = false;
                break;
            }
        }

        if (ordered)
            return melody.ToArray();

        var copy = melody.ToArray();
        Array.Sort(copy, static (a, b) => a.Offset.CompareTo(b.Offset));
        return copy;
    }
}

/// <summary>
/// Options controlling modal turn detection thresholds.
/// </summary>
/// <param name="ModalTurnWindowChords">Sliding window size, in chords, for modal-turn detection.</param>
/// <param name="MinModalTurnCoverage">Minimum scale coverage (0..1) required to report a modal turn.</param>
/// <param name="MinModalTurnImprovement">Minimum coverage gain over the base mode required to report a modal turn.</param>
public sealed record HarmonicColorAnalysisOptions(
    int ModalTurnWindowChords,
    double MinModalTurnCoverage,
    double MinModalTurnImprovement)
{
    /// <summary>Default thresholds (window 4 chords, coverage 0.85, improvement 0.12).</summary>
    public static HarmonicColorAnalysisOptions Default { get; } = new(
        ModalTurnWindowChords: 4,
        MinModalTurnCoverage: 0.85,
        MinModalTurnImprovement: 0.12);
}

/// <summary>
/// Combined analysis result.
/// </summary>
public sealed record HarmonicColorAnalysisResult
{
    // Produced by HarmonicColorAnalyzer; not constructible by consumers (#18 API freeze).
    internal HarmonicColorAnalysisResult(
        KeySignature key,
        IReadOnlyList<ChromaticPitchEvent> chromaticNotes,
        IReadOnlyList<ModalTurnEvent> modalTurns,
        IReadOnlyList<MelodicHarmonyEvent> melodicHarmony)
    {
        Key = key;
        ChromaticNotes = chromaticNotes;
        ModalTurns = modalTurns;
        MelodicHarmony = melodicHarmony;
    }

    /// <summary>The key the melody and chords were analyzed in.</summary>
    public KeySignature Key { get; init; }

    /// <summary>Melody pitches falling outside the key's diatonic scale.</summary>
    public IReadOnlyList<ChromaticPitchEvent> ChromaticNotes { get; init; }

    /// <summary>Detected segments better explained by a borrowed mode.</summary>
    public IReadOnlyList<ModalTurnEvent> ModalTurns { get; init; }

    /// <summary>Per-note classification against the current chord slice.</summary>
    public IReadOnlyList<MelodicHarmonyEvent> MelodicHarmony { get; init; }

    /// <summary>
    /// A heuristic 0..10 score describing how "colorful" the harmony feels,
    /// based on chromatic notes, non-chord tones, and modal turns.
    /// </summary>
    public double ColorfulnessRating
    {
        get
        {
            var total = MelodicHarmony?.Count ?? 0;
            if (total <= 0)
                return 0;

            var chromatic = ChromaticNotes?.Count ?? 0;

            var nonChord = 0;
            if (MelodicHarmony != null)
            {
                for (var i = 0; i < MelodicHarmony.Count; i++)
                {
                    if (!MelodicHarmony[i].IsChordTone)
                        nonChord++;
                }
            }

            var maxModalConfidence = 0.0;
            if (ModalTurns != null)
            {
                for (var i = 0; i < ModalTurns.Count; i++)
                {
                    var c = ModalTurns[i].Confidence;
                    if (c > maxModalConfidence)
                        maxModalConfidence = c;
                }
            }

            var chromaticRatio = (double)chromatic / total;
            var nonChordRatio = (double)nonChord / total;
            var modalFactor = Math.Clamp(maxModalConfidence, 0.0, 1.0);

            var score01 = (0.55 * chromaticRatio) + (0.30 * nonChordRatio) + (0.15 * modalFactor);
            var score10 = 10.0 * Math.Clamp(score01, 0.0, 1.0);
            return Math.Round(score10, 1);
        }
    }

    /// <summary>
    /// A short human-readable summary derived from <see cref="ColorfulnessRating"/>.
    /// </summary>
    public string Description
    {
        get
        {
            var r = ColorfulnessRating;
            return r switch
            {
                < 2.0 => "Mostly diatonic and stable.",
                < 5.0 => "Moderately colorful (some non-chord tones / chromaticism).",
                < 8.0 => "Colorful (noticeable chromaticism or modal mixture).",
                _ => "Highly colorful / chromatic (strong modal mixture or altered tones)."
            };
        }
    }
}

/// <summary>
/// A pitch outside the diatonic scale of the analyzed key.
/// </summary>
/// <param name="Offset">Onset time in whole-note units.</param>
/// <param name="Pitch">MIDI pitch number (middle C = 60).</param>
/// <param name="PitchClass">Pitch class (0-11).</param>
/// <param name="NoteName">Note name of the pitch class.</param>
/// <param name="Alteration">Alteration label relative to the tonic (e.g. <c>b6</c>, <c>#4</c>).</param>
public readonly record struct ChromaticPitchEvent(
    Rational Offset,
    int Pitch,
    byte PitchClass,
    string NoteName,
    string Alteration);

/// <summary>
/// A detected segment where a different mode explains the pitch material better than the base key mode.
/// </summary>
/// <param name="StartChordIndex">Index of the first chord in the segment.</param>
/// <param name="EndChordIndex">Index of the last chord in the segment.</param>
/// <param name="Mode">The mode that best explains the segment.</param>
/// <param name="Confidence">Coverage improvement over the base mode.</param>
/// <param name="OutOfKeyPitchClassMask">12-bit pitch-class mask (bit n = pitch class n) of pitch classes used that lie outside the base key. A mask (not an array) keeps this record struct's value equality meaningful.</param>
public readonly record struct ModalTurnEvent(
    int StartChordIndex,
    int EndChordIndex,
    Mode Mode,
    double Confidence,
    int OutOfKeyPitchClassMask);

/// <summary>
/// Classification of a melody note in harmonic context.
/// </summary>
public enum MelodicHarmonyEventType
{
    /// <summary>Note belongs to the current chord.</summary>
    ChordTone = 0,

    /// <summary>Stepwise connective tone between two chord tones moving in one direction.</summary>
    PassingTone = 1,

    /// <summary>Steps away from a chord tone and returns to it.</summary>
    NeighborTone = 2,

    /// <summary>Accented non-chord tone on a chord change that resolves by step.</summary>
    Appoggiatura = 3,

    /// <summary>Tone held into a new chord that then resolves down by step.</summary>
    Suspension = 4,

    /// <summary>Non-chord tone not matching a more specific category.</summary>
    OtherNonChordTone = 5,

    /// <summary>Not classified (e.g. no chord context).</summary>
    Unclassified = 6
}

/// <summary>
/// One melody note analyzed against the current chord slice.
/// </summary>
/// <param name="Offset">Onset time in whole-note units.</param>
/// <param name="Pitch">MIDI pitch number (middle C = 60).</param>
/// <param name="PitchClass">Pitch class (0-11).</param>
/// <param name="ChordStart">Start time of the chord slice, in whole-note units.</param>
/// <param name="ChordEnd">End time of the chord slice, in whole-note units.</param>
/// <param name="ChordMask">12-bit pitch-class mask of the chord slice.</param>
/// <param name="IsChordTone">Whether the note is a member of the chord.</param>
/// <param name="Type">Classification of the note.</param>
/// <param name="Description">Human-readable description of the classification.</param>
public readonly record struct MelodicHarmonyEvent(
    Rational Offset,
    int Pitch,
    byte PitchClass,
    Rational ChordStart,
    Rational ChordEnd,
    ushort ChordMask,
    bool IsChordTone,
    MelodicHarmonyEventType Type,
    string Description);
