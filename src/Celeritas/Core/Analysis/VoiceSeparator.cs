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

    internal Voice(int index, string name, List<VoiceNote> notes)
    {
        Index = index;
        Name = name;
        Notes = notes;
    }

    /// <summary>
    /// Position of this voice among the voices returned with it, 0 = highest: in
    /// <see cref="VoiceSeparationResult.Voices"/> its place in the list, so
    /// <c>Voices[voice.Index]</c> is this voice; in a <see cref="SatbSeparationResult"/> its
    /// label, Soprano 0, Alto 1, Tenor 2, Bass 3. In <c>Voices</c>, <see cref="Name"/> records
    /// the register the voice's average pitch falls in; in an SATB result it is the label.
    /// </summary>
    /// <remarks>
    /// This used to be the register slot the separator had used (soprano 0 … bass 3), which is
    /// what <see cref="VoiceSeparationResult.NoteToVoice"/> and the crossing and spacing findings
    /// reported too, while every other voice number in an analysis was a position in
    /// <c>Voices</c>. Empty slots are dropped from that list, so for a tenor/bass duet the two
    /// numberings disagreed and <c>Voices[NoteToVoice[i]]</c> threw. There is one numbering now.
    /// </remarks>
    public int Index { get; init; }

    /// <summary>
    /// Name of the voice: the register its average pitch is nearest, each name used once and in
    /// order down the list — Soprano, Alto, Tenor, Bass with four voices to fill, Upper, Middle,
    /// Lower with three, Upper and Lower with two; <c>Voice N</c> by position otherwise.
    /// </summary>
    /// <remarks>
    /// This used to be the register slot the voice's first note had been placed in, which is not
    /// where the voice lies: a line entering above an active soprano opened in the alto slot and
    /// was named Alto while being the highest voice in the list.
    /// </remarks>
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
    /// <remarks>
    /// Folded rather than `%`: C# keeps the sign, so a pitch below zero — which
    /// <see cref="MusicMath.Transpose(NoteBuffer, int)"/> documents it can produce — indexed the
    /// name table backwards and printing a separated voice threw IndexOutOfRangeException.
    /// The octave is computed by flooring for the same reason, so B-1 does not print as B0.
    /// </remarks>
    public override string ToString() =>
        $"{ChordLibrary.NoteNames[PitchMath.Fold(Pitch)]}{(int)Math.Floor(Pitch / 12.0) - 1} @ {Offset}";
}

/// <summary>
/// Result of voice separation analysis.
/// </summary>
public sealed record VoiceSeparationResult
{
    /// <summary>
    /// Separated voices, ordered highest to lowest by average pitch; empty voices are omitted.
    /// Each voice's <see cref="Voice.Index"/> is its position here, and so is every voice number
    /// in <see cref="NoteToVoice"/> and in the polyphony analysis and counterpoint check built on
    /// this result.
    /// </summary>
    /// <remarks>
    /// The order used to be that of the register slots the separator had assigned in, which is
    /// the order of the voices' first notes, not of the voices: once a line may enter above an
    /// active voice, the highest voice could come second.
    /// </remarks>
    public required IReadOnlyList<Voice> Voices { get; init; }

    /// <summary>Total number of notes in the source buffer.</summary>
    public required int TotalNotes { get; init; }

    /// <summary>
    /// Count of detected voice crossings: notes placed above the latest pitch of the voice listed
    /// before theirs, or below that of the voice listed after it.
    /// </summary>
    public required int VoiceCrossings { get; init; }

    /// <summary>Heuristic separation quality, 0..1 (higher = cleaner).</summary>
    public required float SeparationQuality { get; init; }

    /// <summary>
    /// The voice each note went to, keyed by the note's index in the source buffer: the value is
    /// a position in <see cref="Voices"/>, so <c>Voices[NoteToVoice[i]]</c> is the voice holding
    /// note <c>i</c>. Rests have no entry.
    /// </summary>
    /// <remarks>
    /// The value used to be the register slot the note was assigned to (soprano 0 … bass 3),
    /// which is not a position in <see cref="Voices"/> once an empty slot has been dropped: a
    /// tenor/bass duet mapped every note to 2 or 3 in a list of two voices.
    /// </remarks>
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
    /// Cost of opening a voice that has no real notes yet: one more than twice what continuing an
    /// active voice by <see cref="VoiceSeparatorOptions.MaxMelodicInterval"/> costs, so a note
    /// within that interval of a voice's last note continues the voice — alone, or as one of two
    /// voices moving together — rather than opening a fresh one at a register seed.
    /// </summary>
    /// <remarks>
    /// This was a flat 4 semitones on top of the distance from the seed, against a continuing
    /// cost of 13.25 at a fifth (7 plus a stepwise surcharge of 6.25), so a third or a fourth cost
    /// more than starting a new voice at a nearby seed: a four-note statement spanning a fifth was
    /// cut across two voices in twelve of the thirty registers from G3 to C6, and a canon whose
    /// answer never landed whole in one voice was not a canon to
    /// <see cref="PolyphonyAnalyzer.DetectImitation(NoteBuffer, int)"/>. The factor of two is
    /// measured, not guessed: at one, two voices leaping a fifth together (26.5) still lost to one
    /// of them taking the other's note and a new voice opening (7.25 + 14.25), which broke a canon
    /// at the fifth on every restart of its subject.
    /// </remarks>
    private static double SeedContinuityPenalty(VoiceSeparatorOptions options)
    {
        var widest = Math.Max(0, options.MaxMelodicInterval);
        double continuing = widest;
        if (options.PreferStepwise && widest > 2)
            continuing += (widest - 2) * (widest - 2) * StepwiseCostFactor;

        return (2 * continuing) + 1;
    }

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
    /// Weight of the distance from a register seed in the cost of opening the voice seeded there:
    /// enough to open a new line in the free voice nearest its register — which is what slot
    /// order means when crossings are forbidden — and never as much as a semitone of melodic
    /// distance, so the choice of which note continues an active voice is made on melodic grounds.
    /// </summary>
    /// <remarks>
    /// The seed distance used to count in full, plus the large-jump penalty beyond a fifth, as if
    /// a seed were a note the voice had sung. A voice entering far from every free seed was then
    /// cheaper to open for the active voice's own next note than for the entrant, so the entrant
    /// took over the active voice and its continuation was pushed into the new one.
    /// </remarks>
    private const double SeedDistanceWeight = 0.01;

    /// <summary>
    /// Cost per whole note of silence between a voice's last note and the note it would take,
    /// so that of two voices an equal melodic distance away the one that has just stopped sounding
    /// continues, not the one that finished earlier.
    /// </summary>
    private const double RecencyWeight = 0.25;

    /// <summary>
    /// Separate notes into voices using pitch-proximity algorithm. Notes that overlap in time are
    /// forced into different voices; the <see cref="VoiceSeparatorOptions"/> overload adds
    /// voice-crossing and stepwise-motion penalties on top of that.
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

        // The general result is ordered highest to lowest by average pitch; label its voices by
        // the SATB register each average is nearest, each label once and in order — the labelling
        // the general result itself carries when four voices were in play, applied here also when
        // fewer notes capped the count and it named them Upper/Lower. Unused labels get empty
        // stub voices.
        var labels = NamesByRegister([.. res.Voices.Select(v => v.AveragePitch)], maxVoices: 4);

        // Filled or empty, each voice is indexed by its label. A filled voice used to keep the
        // index it had in the general result, so a line the separator had placed in the alto slot
        // but whose average pitch is a tenor's was returned as Tenor with index 1 — the same index
        // as the empty Alto beside it.
        var labeled = new Voice[4];
        for (var s = 0; s < 4; s++)
        {
            var name = GetVoiceName(s, 4);
            var position = Array.IndexOf(labels, name);
            labeled[s] = position >= 0
                ? new Voice(s, name, [.. res.Voices[position].Notes])
                : new Voice { Index = s, Name = name };
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

    /// <summary>
    /// Separate notes into voices with custom options. At each onset the notes go to distinct
    /// voices at the least total cost, a note's cost being its distance from the voice's last
    /// pitch: a line continues in the voice nearest its last note, a note further than
    /// <see cref="VoiceSeparatorOptions.MaxMelodicInterval"/> from every voice opens a new one
    /// while a voice is free, and notes that overlap in time are forced into different voices.
    /// <see cref="VoiceSeparatorOptions"/> adds a penalty for voice-order violations when
    /// <see cref="VoiceSeparatorOptions.AllowCrossings"/> is false and a superlinear penalty for
    /// motion beyond a whole step when <see cref="VoiceSeparatorOptions.PreferStepwise"/> is set.
    /// The voices come back highest first by average pitch, named for their register.
    /// </summary>
    /// <remarks>
    /// The assignment within an onset used to keep the voices in register order whatever
    /// <see cref="VoiceSeparatorOptions.AllowCrossings"/> said — the higher note always went to
    /// the lower-numbered voice — so a line entering above an active voice took that voice over
    /// and pushed its continuation into a new one: two lines cut and re-joined, and a canon
    /// answered an octave above its subject came back from
    /// <see cref="PolyphonyAnalyzer.DetectImitation(NoteBuffer, int)"/> as answered below, or not
    /// at all, depending on the register the subject started in. Opening a new voice was also
    /// cheap enough — the seed's distance plus 4 — that a line moving by a third or a fourth was
    /// cut in two whenever a free voice was seeded nearby. The order constraint now applies only
    /// when crossings are forbidden, and a new voice costs more than any melodic continuation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static VoiceSeparationResult Separate(NoteBuffer buffer, int maxVoices, VoiceSeparatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(options);

        // Every voice table below is sized by maxVoices, so a bad count does not fail here — it
        // fails deep inside the assignment loop, as an IndexOutOfRangeException at zero or an
        // OverflowException from `new int[-1]`, neither of which names the argument at fault.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxVoices);

        // A voice needs at least one note, so asking for more voices than there are notes can
        // only produce empty ones — which the result drops anyway. Without this, maxVoices of
        // int.MaxValue seeded two billion voice tables and the call never came back.
        //
        // Counted over the notes that SOUND, which is what the separator places: counting rests
        // too made the cap depend on how the silence was written down, and the same music with
        // its rests spelled out separated into a different number of voices.
        var sounding = 0;
        for (var i = 0; i < buffer.Count; i++)
        {
            if (buffer.PitchAt(i) != MusicNotation.RestPitch) sounding++;
        }

        maxVoices = Math.Min(maxVoices, Math.Max(1, sounding));

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

        // Collect notes with indices - pre-allocate exact size.
        //
        // Rests are skipped: MusicNotation.Parse marks them with RestPitch (-1), and a buffer
        // filled straight from it would give every rest a voice of its own, an octave below
        // the lowest real note, skewing the register assignment and the crossing count.
        var notes = new List<(VoiceNote note, int index)>(buffer.Count);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer.PitchAt(i) == MusicNotation.RestPitch)
                continue;

            notes.Add((new VoiceNote
            {
                Pitch = buffer.PitchAt(i),
                Offset = buffer.GetOffset(i),
                Duration = buffer.GetDuration(i),
                Velocity = buffer.GetVelocity(i),
                OriginalIndex = i
            }, i));
        }

        if (notes.Count == 0)
        {
            return new VoiceSeparationResult
            {
                Voices = [],
                TotalNotes = buffer.Count,
                VoiceCrossings = 0,
                SeparationQuality = 1.0f,
                NoteToVoice = []
            };
        }

        // Sort by onset time, then by pitch (high to low for voice assignment)
        notes.Sort((a, b) =>
        {
            var offsetCmp = a.note.Offset.CompareTo(b.note.Offset);
            return offsetCmp != 0 ? offsetCmp : b.note.Pitch.CompareTo(a.note.Pitch);
        });

        // The assignment works in slots: maxVoices of them, each seeded at a register centre so
        // that a line opens the voice nearest its register. Which slot a line lands in is not
        // its place in the result — the voices are ordered and named by their average pitch once
        // every note is placed.
        var slotNotes = new List<VoiceNote>[maxVoices];
        for (int i = 0; i < maxVoices; i++)
            slotNotes[i] = [];

        var noteToSlot = new Dictionary<int, int>();
        var voiceLastPitch = new int[maxVoices];
        // Tracks whether a voice contains real notes yet; voiceLastPitch starts with
        // synthetic register seeds which must not count as crossing partners.
        var voiceHasNotes = new bool[maxVoices];
        // End time of each voice's latest note: a voice still sounding at a new onset
        // must not swallow that onset (overlapping notes are different voices).
        var voiceLastEnd = new Rational[maxVoices];
        Array.Fill(voiceLastEnd, Rational.Zero);
        var seedPenalty = SeedContinuityPenalty(options);

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
                // Assign the notes of the slice to distinct voices at the least total cost, each
                // note's cost being its distance from the voice's previous pitch (plus the
                // penalties below), so every line continues in the voice nearest its last note.
                //
                // With crossings allowed, which is the default, the assignment is free. It used
                // to be ordered as well — the higher note of a slice always went to the
                // lower-numbered slot — so a line entering above an active voice was forced into
                // that voice's slot and the active voice's own continuation pushed down a slot:
                // two lines cut and re-joined, and a canon answered above its subject read as a
                // canon answered below. The order is kept only when crossings are forbidden,
                // which is what that option means.
                double Cost(int i, int v) => AssignmentCost(
                    sliceNotes[i].note.Pitch, sliceOnset, v,
                    voiceLastPitch, voiceLastEnd, voiceHasNotes, maxVoices, seedPenalty, options);

                var assignment = options.AllowCrossings
                    ? MinCostAssignment(sliceNotes.Count, maxVoices, Cost)
                    : MinCostIncreasingAssignment(sliceNotes.Count, maxVoices, Cost);

                for (int i = 0; i < sliceNotes.Count; i++)
                {
                    var (note, origIndex) = sliceNotes[i];
                    var voiceIdx = assignment[i];

                    slotNotes[voiceIdx].Add(note);
                    noteToSlot[origIndex] = voiceIdx;

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
                        usedVoices, maxVoices, seedPenalty, options);

                    slotNotes[voiceIdx].Add(note);
                    noteToSlot[origIndex] = voiceIdx;
                    usedVoices[voiceIdx] = true;

                    voiceLastPitch[voiceIdx] = note.Pitch;
                    voiceHasNotes[voiceIdx] = true;
                    if (note.End > voiceLastEnd[voiceIdx])
                        voiceLastEnd[voiceIdx] = note.End;
                }
            }
        }

        // The voices present, highest line first — by average pitch, the way the SATB labelling
        // has always ordered them — and named by the register that average falls in. Their
        // position in this list is the number every voice index in the result, and in the
        // analyses built on it, refers to. The list used to follow slot order and the name the
        // slot, which, once the assignment is free, could put the highest line second and call it
        // Alto.
        var orderedSlots = Enumerable.Range(0, maxVoices)
            .Where(slot => slotNotes[slot].Count > 0)
            .OrderByDescending(slot => slotNotes[slot].Average(n => n.Pitch))
            .ThenBy(slot => slot)
            .ToArray();

        var names = NamesByRegister(
            [.. orderedSlots.Select(slot => (float)slotNotes[slot].Average(n => n.Pitch))],
            maxVoices);

        var present = new List<Voice>(orderedSlots.Length);
        var positionOfSlot = new int[maxVoices];
        Array.Fill(positionOfSlot, -1);
        for (var position = 0; position < orderedSlots.Length; position++)
        {
            positionOfSlot[orderedSlots[position]] = position;
            present.Add(new Voice(position, names[position], slotNotes[orderedSlots[position]]));
        }

        var noteToPosition = new Dictionary<int, int>(noteToSlot.Count);
        foreach (var (noteIndex, slot) in noteToSlot)
            noteToPosition[noteIndex] = positionOfSlot[slot];

        // A crossing is a note placed above the latest pitch of the voice listed above it, or
        // below that of the voice listed below it — counted between neighbours in the final
        // order, never against a seed. Counting it during the assignment, between neighbouring
        // slots, would count a line that opened in a lower slot and stays above its neighbour as
        // crossing on every note.
        var voiceCrossings = CountCrossings(timeSlices, noteToSlot, positionOfSlot, present.Count);

        // Calculate separation quality
        var quality = CalculateSeparationQuality(present, voiceCrossings);

        return new VoiceSeparationResult
        {
            Voices = present,
            TotalNotes = buffer.Count,
            VoiceCrossings = voiceCrossings,
            SeparationQuality = quality,
            NoteToVoice = noteToPosition
        };
    }

    /// <summary>
    /// Counts the notes placed above the latest pitch of the voice before them in the final order
    /// or below that of the voice after them, walking the notes in the order they were placed.
    /// </summary>
    private static int CountCrossings(
        List<List<(VoiceNote note, int index)>> timeSlices,
        Dictionary<int, int> noteToSlot,
        int[] positionOfSlot,
        int voiceCount)
    {
        var lastPitch = new int[voiceCount];
        var hasNotes = new bool[voiceCount];
        var crossings = 0;

        foreach (var slice in timeSlices)
        {
            foreach (var (note, origIndex) in slice)
            {
                var position = positionOfSlot[noteToSlot[origIndex]];

                if (position > 0 && hasNotes[position - 1] && note.Pitch > lastPitch[position - 1])
                    crossings++;
                if (position < voiceCount - 1 && hasNotes[position + 1] && note.Pitch < lastPitch[position + 1])
                    crossings++;

                lastPitch[position] = note.Pitch;
                hasNotes[position] = true;
            }
        }

        return crossings;
    }

    /// <summary>
    /// Names for voices already ordered highest to lowest by average pitch: for up to four voices
    /// the register each average is nearest, Soprano/Alto/Tenor/Bass or Upper/Middle/Lower or
    /// Upper/Lower depending on how many were asked for, each label used once and in order;
    /// beyond four, <c>Voice N</c> by position.
    /// </summary>
    private static string[] NamesByRegister(float[] averagePitches, int maxVoices)
    {
        var names = new string[averagePitches.Length];
        if (averagePitches.Length == 0)
            return names;

        if (maxVoices > 4)
        {
            for (var i = 0; i < names.Length; i++)
                names[i] = $"Voice {i + 1}";

            return names;
        }

        var centers = new int[maxVoices];
        InitializeVoiceRanges(centers, maxVoices);

        var assignment = MinCostIncreasingAssignment(
            averagePitches.Length, maxVoices,
            (i, s) => Math.Abs(averagePitches[i] - centers[s]));

        for (var i = 0; i < names.Length; i++)
            names[i] = GetVoiceName(assignment[i], maxVoices);

        return names;
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
    /// Find the minimal-cost assignment of <paramref name="itemCount"/> items to distinct slots
    /// among <paramref name="slotCount"/> (at least as many), with no constraint on their order:
    /// item i goes to slot a[i], all a[i] different. The Hungarian method, in O(items² × slots).
    /// </summary>
    private static int[] MinCostAssignment(int itemCount, int slotCount, Func<int, int, double> cost)
    {
        // Rows are items and columns slots, both 1-based here with row 0 and column 0 as the
        // scratch entries the method needs; u and v are the potentials, matched[j] the item in
        // slot j (0 for none), way[j] the column the augmenting path came from.
        var matrix = new double[itemCount + 1, slotCount + 1];
        for (var i = 1; i <= itemCount; i++)
        {
            for (var j = 1; j <= slotCount; j++)
                matrix[i, j] = cost(i - 1, j - 1);
        }

        var u = new double[itemCount + 1];
        var v = new double[slotCount + 1];
        var matched = new int[slotCount + 1];
        var way = new int[slotCount + 1];
        var minToSlot = new double[slotCount + 1];
        var used = new bool[slotCount + 1];

        for (var i = 1; i <= itemCount; i++)
        {
            matched[0] = i;
            var j0 = 0;
            Array.Fill(minToSlot, double.PositiveInfinity);
            Array.Clear(used);

            do
            {
                used[j0] = true;
                var i0 = matched[j0];
                var delta = double.PositiveInfinity;
                var j1 = 0;

                for (var j = 1; j <= slotCount; j++)
                {
                    if (used[j])
                        continue;

                    var current = matrix[i0, j] - u[i0] - v[j];
                    if (current < minToSlot[j])
                    {
                        minToSlot[j] = current;
                        way[j] = j0;
                    }

                    if (minToSlot[j] < delta)
                    {
                        delta = minToSlot[j];
                        j1 = j;
                    }
                }

                for (var j = 0; j <= slotCount; j++)
                {
                    if (used[j])
                    {
                        u[matched[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minToSlot[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (matched[j0] != 0);

            do
            {
                var j1 = way[j0];
                matched[j0] = matched[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        var assignment = new int[itemCount];
        for (var j = 1; j <= slotCount; j++)
        {
            if (matched[j] != 0)
                assignment[matched[j] - 1] = j - 1;
        }

        return assignment;
    }

    /// <summary>
    /// Cost of assigning a note to a candidate voice. For a voice with notes: the pitch distance
    /// from its last note, plus penalties for large jumps, non-stepwise motion
    /// (<see cref="VoiceSeparatorOptions.PreferStepwise"/>), silence since its last note ended and
    /// temporal overlap with a note of it still sounding. For a voice with none: the seed penalty,
    /// plus a hair of the distance from its register seed. Either way, order violations against
    /// currently sounding voices when <see cref="VoiceSeparatorOptions.AllowCrossings"/> is false.
    /// </summary>
    private static double AssignmentCost(
        int pitch,
        Rational onset,
        int voiceIdx,
        int[] voiceLastPitch,
        Rational[] voiceLastEnd,
        bool[] voiceHasNotes,
        int maxVoices,
        double seedPenalty,
        VoiceSeparatorOptions options)
    {
        var distance = Math.Abs(pitch - voiceLastPitch[voiceIdx]);
        double cost;

        if (!voiceHasNotes[voiceIdx])
        {
            // A voice with no notes yet: its "last pitch" is a register seed, and the distance to
            // a seed is not melodic motion. Opening the voice costs the same wherever the note
            // is, which keeps a line whose next note lies within MaxMelodicInterval in its voice,
            // and a sliver of the distance so a new line opens the free voice nearest its
            // register.
            cost = seedPenalty + (SeedDistanceWeight * distance);
        }
        else
        {
            cost = distance;

            if (distance > options.MaxMelodicInterval)
                cost += options.LargeJumpPenalty;

            // PreferStepwise: superlinear cost for melodic motion beyond a whole step,
            // so a leaping continuation loses to a nearer (or free) voice.
            if (options.PreferStepwise && distance > 2)
                cost += (distance - 2) * (distance - 2) * StepwiseCostFactor;

            // A voice whose latest note still sounds at this onset cannot take the
            // note without collapsing simultaneous notes into one line; make it
            // effectively unavailable, but keep the cost finite so the min-cost
            // fallback still assigns (never drops) the note when ALL voices overlap.
            // A voice that has stopped sounding pays for the silence since, so that of
            // two voices an equal distance away the one still going on continues.
            if (voiceLastEnd[voiceIdx] > onset)
                cost += OverlapPenalty;
            else
                cost += (onset - voiceLastEnd[voiceIdx]).ToDouble() * RecencyWeight;
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
        double seedPenalty,
        VoiceSeparatorOptions options)
    {
        var bestVoice = -1;
        var minCost = double.MaxValue;

        for (int v = 0; v < maxVoices; v++)
        {
            if (usedVoices[v]) continue;

            var cost = AssignmentCost(pitch, onset, v, voiceLastPitch, voiceLastEnd, voiceHasNotes, maxVoices, seedPenalty, options);
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

        // A voice is one line: at any moment it sounds one note. A note that begins while
        // another in the same voice is still sounding is a note the separation could not place,
        // and nothing here counted them — so eleven notes struck together came back as four
        // voices, one of them holding eight at once, and the score called that separation
        // perfect. This is the one thing a separation is for, so it is the one thing the score
        // must report; the melodic-jump term above cannot see it, because notes piled on the
        // same onset are a step apart in pitch and look like the smoothest line there is.
        var piled = 0;
        var placed = 0;
        foreach (var voice in voices)
        {
            var soundingUntil = Rational.Zero;
            var first = true;
            foreach (var note in voice.Notes)
            {
                placed++;
                if (!first && note.Offset < soundingUntil)
                {
                    piled++;
                }

                var end = note.Offset + note.Duration;
                soundingUntil = first || end > soundingUntil ? end : soundingUntil;
                first = false;
            }
        }

        var pilePenalty = placed > 0 ? (float)piled / placed : 0f;

        return Math.Clamp(1.0f - crossingPenalty - jumpPenalty - pilePenalty, 0f, 1f);
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
/// SATB (Soprano/Alto/Tenor/Bass) separation convenience result. The four voices are indexed by
/// their label — Soprano 0, Alto 1, Tenor 2, Bass 3 — whether filled or an empty stub.
/// </summary>
/// <remarks>
/// A filled voice used to keep the index it had in <see cref="Full"/>, which is the register
/// slot the separator had used, not the label chosen here by average pitch; a line placed in the
/// alto slot but labelled Tenor came back with index 1, the same as the empty Alto beside it.
/// </remarks>
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
    /// <summary>
    /// Maximum melodic interval before penalty (semitones). A voice continues through any
    /// interval up to this one rather than a new voice opening; beyond it the leap is penalised
    /// and, while a voice is free, the note opens a new one.
    /// </summary>
    public int MaxMelodicInterval { get; init; } = 7;

    /// <summary>Penalty for jumps larger than MaxMelodicInterval.</summary>
    public int LargeJumpPenalty { get; init; } = 12;

    /// <summary>
    /// Prefer stepwise motion: melodic motion beyond a whole step (2 semitones) within a
    /// voice incurs a superlinear extra cost, so of two readings of the same notes the one
    /// with the smaller leaps wins — two voices leaping a sixth in parallel rather than one
    /// stepping while the other leaps over it.
    /// </summary>
    public bool PreferStepwise { get; init; } = true;

    /// <summary>
    /// Allow voice crossings. When <see langword="true"/>, the default, the notes of an onset go
    /// to whichever voices continue them most smoothly, so a line may enter or move above a
    /// voice listed before it. When <see langword="false"/>, the notes of an onset are kept in
    /// the voices' order, and an assignment that would put a note above the currently sounding
    /// pitch of a higher voice (or below a lower one's) pays a large soft penalty: crossings are
    /// avoided whenever an alternative assignment exists, but notes are never dropped.
    /// </summary>
    /// <remarks>
    /// The order of the notes within an onset used to be kept whatever this said, so with
    /// crossings allowed a line entering above an active voice still displaced it.
    /// </remarks>
    public bool AllowCrossings { get; init; } = true;
}
