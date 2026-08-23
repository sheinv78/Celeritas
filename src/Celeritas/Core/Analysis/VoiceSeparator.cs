// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core.Analysis;

/// <summary>
/// Represents a separated voice (melodic line) in polyphonic music.
/// </summary>
public sealed class Voice
{
    // Produced by voice separation; not constructible by consumers (#18 API freeze).
    internal Voice() { }

    /// <summary>Voice index (0 = highest/soprano, increasing = lower voices).</summary>
    public int Index { get; init; }

    /// <summary>Name of the voice (Soprano, Alto, Tenor, Bass, or Voice N).</summary>
    public string Name { get; init; } = "";

    /// <summary>Notes in this voice, ordered by time.</summary>
    public List<VoiceNote> Notes { get; } = [];

    /// <summary>Pitch range of this voice.</summary>
    public (int Min, int Max) Range => Notes.Count > 0
        ? (Notes.Min(n => n.Pitch), Notes.Max(n => n.Pitch))
        : (0, 0);

    /// <summary>Lowest pitch (MIDI) in this voice (0 if empty).</summary>
    public int AmbitusStart => Range.Min;

    /// <summary>Highest pitch (MIDI) in this voice (0 if empty).</summary>
    public int AmbitusEnd => Range.Max;

    /// <summary>Average pitch of this voice.</summary>
    public float AveragePitch => Notes.Count > 0
        ? (float)Notes.Average(n => n.Pitch)
        : 0;
}

/// <summary>
/// A note assigned to a specific voice.
/// </summary>
public readonly record struct VoiceNote
{
    /// <summary>MIDI pitch number (middle C = 60).</summary>
    public int Pitch { get; init; }

    /// <summary>Onset time in whole-note units.</summary>
    public Rational Offset { get; init; }

    /// <summary>Duration in whole-note units.</summary>
    public Rational Duration { get; init; }

    /// <summary>Note velocity.</summary>
    public float Velocity { get; init; }

    /// <summary>Original index in the NoteBuffer.</summary>
    public int OriginalIndex { get; init; }

    /// <summary>End time (<c>Offset</c> + <c>Duration</c>) in whole-note units.</summary>
    public Rational End => Offset + Duration;

    /// <summary>Formats as note name, octave, and onset (e.g. <c>C4 @ 0</c>).</summary>
    public override string ToString() =>
        $"{ChordLibrary.NoteNames[Pitch % 12]}{(Pitch / 12) - 1} @ {Offset}";
}

/// <summary>
/// Result of voice separation analysis.
/// </summary>
public sealed record VoiceSeparationResult
{
    /// <summary>Separated voices, ordered highest to lowest; empty voices are omitted.</summary>
    public required IReadOnlyList<Voice> Voices { get; init; }

    /// <summary>Total number of notes in the source buffer.</summary>
    public required int TotalNotes { get; init; }

    /// <summary>Count of detected voice crossings.</summary>
    public required int VoiceCrossings { get; init; }

    /// <summary>Heuristic separation quality, 0..1 (higher = cleaner).</summary>
    public required float SeparationQuality { get; init; }

    /// <summary>Get the voice assignment for each original note index.</summary>
    public Dictionary<int, int> NoteToVoice { get; init; } = [];
}

/// <summary>
/// Algorithm for separating polyphonic music into individual voices.
/// Uses pitch proximity and voice leading principles.
/// </summary>
public static class VoiceSeparator
{
    private static readonly VoiceSeparatorOptions DefaultOptions = new();

    /// <summary>
    /// Extra assignment cost (semitones) for voices that have no real notes yet,
    /// so continuing an active voice wins over a synthetic register seed on ties.
    /// </summary>
    private const int SeedContinuityPenalty = 4;

    /// <summary>
    /// Assignment cost that makes a voice whose previous note still sounds at the new
    /// onset effectively unavailable: temporally overlapping notes are different voices
    /// by definition. Large enough to lose to ANY pitch-distance alternative (so a free
    /// voice is opened instead), yet finite so the min-cost fallback still assigns the
    /// note (never drops it) when every voice is sounding at maxVoices.
    /// </summary>
    private const double OverlapPenalty = 10_000;

    /// <summary>
    /// Soft penalty per voice-order violation when <see cref="VoiceSeparatorOptions.AllowCrossings"/>
    /// is false. Large enough to dominate ordinary pitch distances, small enough that a
    /// forced crossing still beats an overlap (notes are never dropped).
    /// </summary>
    private const double CrossingPenalty = 50;

    /// <summary>
    /// Superlinear cost factor applied to melodic motion beyond a whole step when
    /// <see cref="VoiceSeparatorOptions.PreferStepwise"/> is set: cost += (distance-2)^2 * factor.
    /// </summary>
    private const double StepwiseCostFactor = 0.25;

    /// <summary>
    /// Separate notes into voices using pitch-proximity algorithm.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static VoiceSeparationResult Separate(NoteBuffer buffer, int maxVoices = 4)
        => Separate(buffer, maxVoices, DefaultOptions);

    /// <summary>
    /// Convenience SATB separation: returns exactly 4 voices named Soprano/Alto/Tenor/Bass.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="notes"/> is <see langword="null"/>.</exception>
    public static SatbSeparationResult SeparateIntoSatb(IEnumerable<NoteEvent> notes, VoiceSeparatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var arr = notes as NoteEvent[] ?? [.. notes];
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return SeparateIntoSatb(buffer, options);
    }

    /// <summary>
    /// Convenience SATB separation: returns exactly 4 voices named Soprano/Alto/Tenor/Bass.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static SatbSeparationResult SeparateIntoSatb(NoteBuffer buffer, VoiceSeparatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var res = Separate(buffer, maxVoices: 4, options ?? DefaultOptions);

        // Map detected voices to SATB labels by pitch register (not by list position:
        // filtering empty voices shifts indices, and e.g. a tenor/bass duet must not
        // become "Soprano/Alto"). Unused labels get empty stub voices.
        var nonEmpty = res.Voices
            .Where(v => v.Notes.Count > 0)
            .OrderByDescending(v => v.AveragePitch)
            .Take(4)
            .ToList();

        // Typical SATB register centers (same values as InitializeVoiceRanges).
        int[] centers = [72, 64, 57, 48];
        string[] names = ["Soprano", "Alto", "Tenor", "Bass"];

        var slots = new Voice?[4];
        if (nonEmpty.Count > 0)
        {
            var assignment = MinCostIncreasingAssignment(
                nonEmpty.Count, 4,
                (i, s) => Math.Abs(nonEmpty[i].AveragePitch - centers[s]));

            for (var i = 0; i < nonEmpty.Count; i++)
                slots[assignment[i]] = nonEmpty[i];
        }

        var labeled = new Voice[4];
        for (var s = 0; s < 4; s++)
        {
            labeled[s] = slots[s] is { } voice
                ? RenameVoice(voice, names[s])
                : new Voice { Index = s, Name = names[s] };
        }

        return new SatbSeparationResult
        {
            Full = res,
            Soprano = labeled[0],
            Alto = labeled[1],
            Tenor = labeled[2],
            Bass = labeled[3]
        };
    }

    private static Voice RenameVoice(Voice source, string name)
    {
        var v = new Voice { Index = source.Index, Name = name };
        v.Notes.AddRange(source.Notes);
        return v;
    }

    /// <summary>
    /// Separate notes into voices with custom options.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static VoiceSeparationResult Separate(NoteBuffer buffer, int maxVoices, VoiceSeparatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        // Every voice table below is sized by maxVoices, so a bad count does not fail here — it
        // fails deep inside the assignment loop, as an IndexOutOfRangeException at zero or an
        // OverflowException from `new int[-1]`, neither of which names the argument at fault.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxVoices);

        if (buffer.Count == 0)
        {
            return new VoiceSeparationResult
            {
                Voices = [],
                TotalNotes = 0,
                VoiceCrossings = 0,
                SeparationQuality = 1.0f,
                NoteToVoice = []
            };
        }

        // Collect notes with indices - pre-allocate exact size
        var notes = new List<(VoiceNote note, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            notes.Add((new VoiceNote
            {
                Pitch = buffer.PitchAt(i),
                Offset = buffer.GetOffset(i),
                Duration = buffer.GetDuration(i),
                Velocity = buffer.GetVelocity(i),
                OriginalIndex = i
            }, i));
        }

        // Sort by onset time, then by pitch (high to low for voice assignment)
        notes.Sort((a, b) =>
        {
            var offsetCmp = a.note.Offset.CompareTo(b.note.Offset);
            return offsetCmp != 0 ? offsetCmp : b.note.Pitch.CompareTo(a.note.Pitch);
        });

        // Initialize voices
        var voices = new List<Voice>();
        for (int i = 0; i < maxVoices; i++)
        {
            voices.Add(new Voice
            {
                Index = i,
                Name = GetVoiceName(i, maxVoices)
            });
        }

        var noteToVoice = new Dictionary<int, int>();
        var voiceLastPitch = new int[maxVoices];
        // Tracks whether a voice contains real notes yet; voiceLastPitch starts with
        // synthetic register seeds which must not count as crossing partners.
        var voiceHasNotes = new bool[maxVoices];
        // End time of each voice's latest note: a voice still sounding at a new onset
        // must not swallow that onset (overlapping notes are different voices).
        var voiceLastEnd = new Rational[maxVoices];
        Array.Fill(voiceLastEnd, Rational.Zero);
        var voiceCrossings = 0;

        // Initialize voice pitches based on typical ranges
        InitializeVoiceRanges(voiceLastPitch, maxVoices);

        // Process each time slice
        var timeSlices = GroupByOnset(notes);

        foreach (var sliceNotes in timeSlices)
        {
            // Slice notes are already ordered by pitch (high to low): the global sort
            // orders by onset, then pitch descending, and grouping preserves it.
            var sliceOnset = sliceNotes[0].note.Offset;

            if (sliceNotes.Count <= maxVoices)
            {
                // Assign notes to voices preserving pitch order (higher note -> higher
                // voice) while minimizing total distance to each voice's previous pitch,
                // so a monophonic line stays in the voice nearest its register.
                var assignment = MinCostIncreasingAssignment(
                    sliceNotes.Count, maxVoices,
                    (i, v) => AssignmentCost(
                        sliceNotes[i].note.Pitch, sliceOnset, v,
                        voiceLastPitch, voiceLastEnd, voiceHasNotes, maxVoices, options));

                for (int i = 0; i < sliceNotes.Count; i++)
                {
                    var (note, origIndex) = sliceNotes[i];
                    var voiceIdx = assignment[i];

                    voices[voiceIdx].Notes.Add(note);
                    noteToVoice[origIndex] = voiceIdx;

                    // Check for voice crossing (only against voices that already
                    // contain real notes, never against synthetic seed pitches)
                    if (voiceIdx > 0 && voiceHasNotes[voiceIdx - 1] && note.Pitch > voiceLastPitch[voiceIdx - 1])
                        voiceCrossings++;
                    if (voiceIdx < maxVoices - 1 && voiceHasNotes[voiceIdx + 1] && note.Pitch < voiceLastPitch[voiceIdx + 1])
                        voiceCrossings++;

                    voiceLastPitch[voiceIdx] = note.Pitch;
                    voiceHasNotes[voiceIdx] = true;
                    if (note.End > voiceLastEnd[voiceIdx])
                        voiceLastEnd[voiceIdx] = note.End;
                }
            }
            else
            {
                // More notes than voices: use pitch-proximity assignment
                var usedVoices = new bool[maxVoices];

                // First pass: assign to nearest available voice; once every voice is
                // taken, overflow notes go to the voice with the nearest last pitch.
                foreach (var (note, origIndex) in sliceNotes)
                {
                    var voiceIdx = FindBestVoice(
                        note.Pitch, sliceOnset,
                        voiceLastPitch, voiceLastEnd, voiceHasNotes,
                        usedVoices, maxVoices, options);

                    voices[voiceIdx].Notes.Add(note);
                    noteToVoice[origIndex] = voiceIdx;
                    usedVoices[voiceIdx] = true;

                    // Count crossings here too (previously only the <= maxVoices branch did)
                    if (voiceIdx > 0 && voiceHasNotes[voiceIdx - 1] && note.Pitch > voiceLastPitch[voiceIdx - 1])
                        voiceCrossings++;
                    if (voiceIdx < maxVoices - 1 && voiceHasNotes[voiceIdx + 1] && note.Pitch < voiceLastPitch[voiceIdx + 1])
                        voiceCrossings++;

                    voiceLastPitch[voiceIdx] = note.Pitch;
                    voiceHasNotes[voiceIdx] = true;
                    if (note.End > voiceLastEnd[voiceIdx])
                        voiceLastEnd[voiceIdx] = note.End;
                }
            }
        }

        // Calculate separation quality
        var quality = CalculateSeparationQuality(voices, voiceCrossings);

        return new VoiceSeparationResult
        {
            Voices = [.. voices.Where(v => v.Notes.Count > 0)],
            TotalNotes = buffer.Count,
            VoiceCrossings = voiceCrossings,
            SeparationQuality = quality,
            NoteToVoice = noteToVoice
        };
    }

    private static List<List<(VoiceNote note, int index)>> GroupByOnset(
        List<(VoiceNote note, int index)> notes)
    {
        // Estimate group count (assume avg 2-3 notes per onset for polyphony)
        var estimatedGroups = notes.Count / 2;
        var groups = new List<List<(VoiceNote, int)>>(estimatedGroups);
        if (notes.Count == 0) return groups;

        var currentGroup = new List<(VoiceNote, int)>(4) { notes[0] }; // Typical chord size
        var currentOnset = notes[0].note.Offset;

        for (int i = 1; i < notes.Count; i++)
        {
            if (notes[i].note.Offset == currentOnset)
            {
                currentGroup.Add(notes[i]);
            }
            else
            {
                groups.Add(currentGroup);
                currentGroup = new List<(VoiceNote, int)>(4) { notes[i] };
                currentOnset = notes[i].note.Offset;
            }
        }
        groups.Add(currentGroup);

        return groups;
    }

    private static void InitializeVoiceRanges(int[] voiceLastPitch, int maxVoices)
    {
        // Typical SATB ranges (MIDI): S=60-81, A=53-74, T=48-69, B=40-62
        if (maxVoices >= 4)
        {
            voiceLastPitch[0] = 72; // Soprano center
            voiceLastPitch[1] = 64; // Alto center
            voiceLastPitch[2] = 57; // Tenor center
            voiceLastPitch[3] = 48; // Bass center
        }
        else if (maxVoices == 3)
        {
            voiceLastPitch[0] = 72;
            voiceLastPitch[1] = 60;
            voiceLastPitch[2] = 48;
        }
        else if (maxVoices == 2)
        {
            voiceLastPitch[0] = 67;
            voiceLastPitch[1] = 52;
        }
        else
        {
            voiceLastPitch[0] = 60;
        }
    }

    /// <summary>
    /// Find the minimal-cost strictly increasing assignment of <paramref name="itemCount"/>
    /// items (ordered) to <paramref name="slotCount"/> slots (ordered), i.e. item i goes to
    /// slot a[i] with a[0] &lt; a[1] &lt; ... Preserves ordering (high-to-low pitches map to
    /// top-to-bottom voices) while minimizing the total assignment cost.
    /// </summary>
    private static int[] MinCostIncreasingAssignment(int itemCount, int slotCount, Func<int, int, double> cost)
    {
        const double Infinity = double.MaxValue / 4;

        // dp[i, s] = min cost of assigning items 0..i with item i in slot s
        var dp = new double[itemCount, slotCount];
        var prev = new int[itemCount, slotCount];

        for (var s = 0; s < slotCount; s++)
        {
            dp[0, s] = s <= slotCount - itemCount ? cost(0, s) : Infinity;
            prev[0, s] = -1;
        }

        for (var i = 1; i < itemCount; i++)
        {
            var bestPrev = -1;
            var bestPrevCost = Infinity;

            for (var s = 0; s < slotCount; s++)
            {
                // Best predecessor uses any slot < s for item i-1
                if (s > 0 && dp[i - 1, s - 1] < bestPrevCost)
                {
                    bestPrevCost = dp[i - 1, s - 1];
                    bestPrev = s - 1;
                }

                // Item i in slot s must leave room for items after it
                var feasible = s >= i && s <= slotCount - (itemCount - i);
                dp[i, s] = feasible && bestPrev >= 0 ? bestPrevCost + cost(i, s) : Infinity;
                prev[i, s] = bestPrev;
            }
        }

        // Find best final slot and backtrack
        var bestSlot = itemCount - 1;
        for (var s = itemCount - 1; s < slotCount; s++)
        {
            if (dp[itemCount - 1, s] < dp[itemCount - 1, bestSlot])
                bestSlot = s;
        }

        var assignment = new int[itemCount];
        for (var i = itemCount - 1; i >= 0; i--)
        {
            assignment[i] = bestSlot;
            bestSlot = prev[i, bestSlot];
        }

        return assignment;
    }

    /// <summary>
    /// Cost of assigning a note to a candidate voice: pitch distance from the voice's
    /// last pitch, plus penalties for large jumps, synthetic seeds, non-stepwise motion
    /// (<see cref="VoiceSeparatorOptions.PreferStepwise"/>), temporal overlap with the
    /// voice's still-sounding note, and order violations against currently sounding
    /// voices (<see cref="VoiceSeparatorOptions.AllowCrossings"/> = false).
    /// </summary>
    private static double AssignmentCost(
        int pitch,
        Rational onset,
        int voiceIdx,
        int[] voiceLastPitch,
        Rational[] voiceLastEnd,
        bool[] voiceHasNotes,
        int maxVoices,
        VoiceSeparatorOptions options)
    {
        var distance = Math.Abs(pitch - voiceLastPitch[voiceIdx]);
        double cost = distance;

        if (distance > options.MaxMelodicInterval)
            cost += options.LargeJumpPenalty;

        if (!voiceHasNotes[voiceIdx])
        {
            // Prefer continuing a voice with real notes over starting a fresh voice
            // whose "last pitch" is just a synthetic seed — otherwise a monophonic
            // line drifts across voices on ties.
            cost += SeedContinuityPenalty;
        }
        else
        {
            // PreferStepwise: superlinear cost for melodic motion beyond a whole step,
            // so a leaping continuation loses to a nearer (or free) voice. Synthetic
            // seed distances are not melodic motion and are exempt.
            if (options.PreferStepwise && distance > 2)
                cost += (distance - 2) * (distance - 2) * StepwiseCostFactor;

            // A voice whose latest note still sounds at this onset cannot take the
            // note without collapsing simultaneous notes into one line; make it
            // effectively unavailable, but keep the cost finite so the min-cost
            // fallback still assigns (never drops) the note when ALL voices overlap.
            if (voiceLastEnd[voiceIdx] > onset)
                cost += OverlapPenalty;
        }

        if (!options.AllowCrossings)
        {
            // Soft penalty for ordering the note above a higher voice's currently
            // sounding pitch, or below a lower one's. Soft: a crossing remains
            // possible when no crossing-free assignment exists.
            for (int other = 0; other < maxVoices; other++)
            {
                if (other == voiceIdx || !voiceHasNotes[other] || voiceLastEnd[other] <= onset)
                    continue;
                if ((other < voiceIdx && pitch > voiceLastPitch[other]) ||
                    (other > voiceIdx && pitch < voiceLastPitch[other]))
                {
                    cost += CrossingPenalty;
                }
            }
        }

        return cost;
    }

    private static int FindBestVoice(
        int pitch,
        Rational onset,
        int[] voiceLastPitch,
        Rational[] voiceLastEnd,
        bool[] voiceHasNotes,
        bool[] usedVoices,
        int maxVoices,
        VoiceSeparatorOptions options)
    {
        var bestVoice = -1;
        var minCost = double.MaxValue;

        for (int v = 0; v < maxVoices; v++)
        {
            if (usedVoices[v]) continue;

            var cost = AssignmentCost(pitch, onset, v, voiceLastPitch, voiceLastEnd, voiceHasNotes, maxVoices, options);
            if (cost < minCost)
            {
                minCost = cost;
                bestVoice = v;
            }
        }

        if (bestVoice >= 0)
            return bestVoice;

        // All voices already used in this slice (overflow): distribute the extra note
        // to the voice with the nearest last pitch instead of dumping it into voice 0
        // (min-cost fallback: a note is never dropped).
        bestVoice = 0;
        var minDistance = int.MaxValue;
        for (int v = 0; v < maxVoices; v++)
        {
            var distance = Math.Abs(pitch - voiceLastPitch[v]);
            if (distance < minDistance)
            {
                minDistance = distance;
                bestVoice = v;
            }
        }

        return bestVoice;
    }

    private static float CalculateSeparationQuality(List<Voice> voices, int crossings)
    {
        if (voices.All(v => v.Notes.Count == 0)) return 1.0f;

        var crossingPenalty = crossings * 0.05f;

        // Check for melodic smoothness
        float totalJumps = 0;
        int jumpCount = 0;

        foreach (var voice in voices)
        {
            for (int i = 1; i < voice.Notes.Count; i++)
            {
                var jump = Math.Abs(voice.Notes[i].Pitch - voice.Notes[i - 1].Pitch);
                totalJumps += jump;
                jumpCount++;
            }
        }

        var avgJump = jumpCount > 0 ? totalJumps / jumpCount : 0;
        var jumpPenalty = Math.Max(0, (avgJump - 4) * 0.02f); // Penalize avg jump > 4 semitones

        return Math.Clamp(1.0f - crossingPenalty - jumpPenalty, 0f, 1f);
    }

    private static string GetVoiceName(int index, int total)
    {
        return total switch
        {
            4 => index switch
            {
                0 => "Soprano",
                1 => "Alto",
                2 => "Tenor",
                3 => "Bass",
                _ => $"Voice {index + 1}"
            },
            3 => index switch
            {
                0 => "Upper",
                1 => "Middle",
                2 => "Lower",
                _ => $"Voice {index + 1}"
            },
            2 => index == 0 ? "Upper" : "Lower",
            _ => $"Voice {index + 1}"
        };
    }
}

/// <summary>
/// SATB (Soprano/Alto/Tenor/Bass) separation convenience result.
/// </summary>
public sealed record SatbSeparationResult
{
    /// <summary>The underlying general separation result.</summary>
    public required VoiceSeparationResult Full { get; init; }

    /// <summary>Soprano voice (highest register); an empty stub if unfilled.</summary>
    public required Voice Soprano { get; init; }

    /// <summary>Alto voice; an empty stub if unfilled.</summary>
    public required Voice Alto { get; init; }

    /// <summary>Tenor voice; an empty stub if unfilled.</summary>
    public required Voice Tenor { get; init; }

    /// <summary>Bass voice (lowest register); an empty stub if unfilled.</summary>
    public required Voice Bass { get; init; }
}

/// <summary>
/// Options for voice separation algorithm.
/// </summary>
public sealed class VoiceSeparatorOptions
{
    /// <summary>Maximum melodic interval before penalty (semitones).</summary>
    public int MaxMelodicInterval { get; init; } = 7;

    /// <summary>Penalty for jumps larger than MaxMelodicInterval.</summary>
    public int LargeJumpPenalty { get; init; } = 12;

    /// <summary>
    /// Prefer stepwise motion: melodic motion beyond a whole step (2 semitones) within a
    /// voice incurs a superlinear extra cost, so a large leap favors a nearer voice (or
    /// opening a free one) over continuing the same line.
    /// </summary>
    public bool PreferStepwise { get; init; } = true;

    /// <summary>
    /// Allow voice crossings. When <see langword="false"/>, an assignment that would put
    /// a note above the currently sounding pitch of a higher voice (or below a lower
    /// one's) pays a large soft penalty: crossings are avoided whenever an alternative
    /// assignment exists, but notes are never dropped.
    /// </summary>
    public bool AllowCrossings { get; init; } = true;
}
