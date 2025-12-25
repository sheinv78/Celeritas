// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core.Harmonization;

namespace Celeritas.Core.Accompaniment;

/// <summary>
/// Engine-native accompaniment generator.
/// Produces <see cref="NoteEvent"/> events from harmonic rhythm.
/// </summary>
public static class AccompanimentGenerator
{
    /// <summary>
    /// Generate accompaniment from a harmonization output (chord assignments).
    /// Uses the provided chord pitches (voicing) and adds a bass line.
    /// </summary>
    public static NoteEvent[] Generate(IReadOnlyList<ChordAssignment> chords, AccompanimentOptions? options = null)
    {
        var opt = options ?? AccompanimentOptions.Default;
        if (chords.Count == 0)
            return [];

        // Heuristic: typical ~2 notes/segment (block) or ~8-16 notes/segment (arpeggio).
        var initialCapacity = opt.Pattern == AccompanimentPattern.Block
            ? chords.Count * (1 + Math.Min(opt.MaxChordTones, 4))
            : chords.Count * 12;

        var events = new List<NoteEvent>(Math.Max(initialCapacity, 16));

        foreach (var chord in chords)
        {
            var start = chord.Start;
            var end = chord.End;
            var duration = end - start;
            if (duration.Numerator <= 0)
                continue;

            var chordPitchClasses = GetUniquePitchClasses(chord.Pitches, opt.MaxChordTones);
            if (chordPitchClasses.Length == 0)
                continue;

            var bassPitch = PitchClassToMidiAtOrAbove(chord.Chord.RootPitchClass, OctaveToMidiBase(opt.BassOctave));

            if (opt.Pattern == AccompanimentPattern.Block)
            {
                events.Add(new NoteEvent(bassPitch, start, duration, opt.BassVelocity));

                var chordVoicing = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
                for (var i = 0; i < chordVoicing.Length; i++)
                    events.Add(new NoteEvent(chordVoicing[i], start, duration, opt.ChordVelocity));

                continue;
            }

            // Arpeggio
            var step = opt.Subdivision;
            if (step.Numerator <= 0)
                step = Rational.Eighth;

            var chordVoicingArp = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
            if (chordVoicingArp.Length == 0)
                continue;

            var t = start;
            var stepIndex = 0;
            while (t < end)
            {
                var remaining = end - t;
                var noteDuration = remaining < step ? remaining : step;

                // Pattern: bass on first step, then cycle chord tones.
                if (stepIndex == 0)
                {
                    events.Add(new NoteEvent(bassPitch, t, noteDuration, opt.BassVelocity));
                }
                else
                {
                    var chordTone = chordVoicingArp[(stepIndex - 1) % chordVoicingArp.Length];
                    events.Add(new NoteEvent(chordTone, t, noteDuration, opt.ChordVelocity));
                }

                t += step;
                stepIndex++;
            }
        }

        return events.ToArray();
    }

    /// <summary>
    /// Generate accompaniment from a roman-numeral progression.
    /// Chords are spelled in the provided key.
    /// </summary>
    public static NoteEvent[] Generate(
        IReadOnlyList<HarmonicRhythmItem> progression,
        KeySignature key,
        AccompanimentOptions? options = null)
    {
        var opt = options ?? AccompanimentOptions.Default;
        if (progression.Count == 0)
            return [];

        var initialCapacity = opt.Pattern == AccompanimentPattern.Block
            ? progression.Count * (1 + Math.Min(opt.MaxChordTones, 4))
            : progression.Count * 12;

        var events = new List<NoteEvent>(Math.Max(initialCapacity, 16));

        var offset = Rational.Zero;

        Span<byte> pcs = stackalloc byte[8];
        foreach (var item in progression)
        {
            var roman = item.Chord;
            var duration = item.Duration;
            if (!roman.IsValid || duration.Numerator <= 0)
            {
                offset += duration;
                continue;
            }

            var pcCount = roman.WritePitchClasses(key, pcs);
            if (pcCount <= 0)
            {
                offset += duration;
                continue;
            }

            var chordPitchClasses = DeduplicatePitchClasses(pcs.Slice(0, pcCount), opt.MaxChordTones);
            if (chordPitchClasses.Length == 0)
            {
                offset += duration;
                continue;
            }

            var rootPc = roman.GetRootPitchClass(key);
            var bassPitch = PitchClassToMidiAtOrAbove(rootPc, OctaveToMidiBase(opt.BassOctave));

            if (opt.Pattern == AccompanimentPattern.Block)
            {
                events.Add(new NoteEvent(bassPitch, offset, duration, opt.BassVelocity));

                var chordVoicing = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
                for (var i = 0; i < chordVoicing.Length; i++)
                    events.Add(new NoteEvent(chordVoicing[i], offset, duration, opt.ChordVelocity));

                offset += duration;
                continue;
            }

            // Arpeggio
            var step = opt.Subdivision;
            if (step.Numerator <= 0)
                step = Rational.Eighth;

            var chordVoicingArp = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
            if (chordVoicingArp.Length == 0)
            {
                offset += duration;
                continue;
            }

            var t = offset;
            var end = offset + duration;
            var stepIndex = 0;
            while (t < end)
            {
                var remaining = end - t;
                var noteDuration = remaining < step ? remaining : step;

                if (stepIndex == 0)
                {
                    events.Add(new NoteEvent(bassPitch, t, noteDuration, opt.BassVelocity));
                }
                else
                {
                    var chordTone = chordVoicingArp[(stepIndex - 1) % chordVoicingArp.Length];
                    events.Add(new NoteEvent(chordTone, t, noteDuration, opt.ChordVelocity));
                }

                t += step;
                stepIndex++;
            }

            offset += duration;
        }

        return events.ToArray();
    }

    private static byte[] GetUniquePitchClasses(int[] pitches, int max)
    {
        if (pitches.Length == 0 || max <= 0)
            return [];

        Span<bool> seen = stackalloc bool[12];
        var tmp = new byte[Math.Min(12, max)];
        var count = 0;

        for (var i = 0; i < pitches.Length && count < tmp.Length; i++)
        {
            var pc = (byte)(pitches[i] % 12);
            if (seen[pc])
                continue;
            seen[pc] = true;
            tmp[count++] = pc;
        }

        if (count == 0)
            return [];

        Array.Sort(tmp, 0, count);
        var result = new byte[count];
        Array.Copy(tmp, result, count);
        return result;
    }

    private static byte[] DeduplicatePitchClasses(ReadOnlySpan<byte> pitchClasses, int max)
    {
        if (pitchClasses.IsEmpty || max <= 0)
            return [];

        Span<bool> seen = stackalloc bool[12];
        var tmp = new byte[Math.Min(12, Math.Min(max, pitchClasses.Length))];
        var count = 0;

        for (var i = 0; i < pitchClasses.Length && count < tmp.Length; i++)
        {
            var pc = (byte)(pitchClasses[i] % 12);
            if (seen[pc])
                continue;
            seen[pc] = true;
            tmp[count++] = pc;
        }

        if (count == 0)
            return [];

        Array.Sort(tmp, 0, count);
        var result = new byte[count];
        Array.Copy(tmp, result, count);
        return result;
    }

    private static int[] VoicePitchClasses(byte[] pitchClasses, int octave)
    {
        if (pitchClasses.Length == 0)
            return [];

        var baseMidi = OctaveToMidiBase(octave);
        var voiced = new int[pitchClasses.Length];

        // Simple closed-position voicing above baseMidi.
        for (var i = 0; i < pitchClasses.Length; i++)
        {
            voiced[i] = PitchClassToMidiAtOrAbove(pitchClasses[i], baseMidi);
        }

        Array.Sort(voiced);

        // Ensure strictly ascending (avoid duplicates across octave boundaries).
        for (var i = 1; i < voiced.Length; i++)
        {
            while (voiced[i] <= voiced[i - 1])
                voiced[i] += 12;
        }

        return voiced;
    }

    private static int OctaveToMidiBase(int octave) => 12 * (octave + 1);

    private static int PitchClassToMidiAtOrAbove(byte pitchClass, int minMidi)
    {
        var basePc = ((minMidi % 12) + 12) % 12;
        var delta = (pitchClass - basePc + 12) % 12;
        return minMidi + delta;
    }
}
