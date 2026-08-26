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
    /// <exception cref="ArgumentNullException"><paramref name="chords"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is a <c>default(AccompanimentOptions)</c>-like value (see <see cref="AccompanimentOptions.Default"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">A computed MIDI pitch falls outside 0..127 (bad <see cref="AccompanimentOptions.BassOctave"/> or <see cref="AccompanimentOptions.ChordOctave"/>).</exception>
    public static NoteEvent[] Generate(IReadOnlyList<ChordAssignment> chords, AccompanimentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chords);

        var opt = options ?? AccompanimentOptions.Default;
        ValidateOptions(opt);
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

            var chordPitchClasses = GetUniquePitchClasses(chord.Pitches, opt.MaxChordTones, chord.Chord.RootPitchClass);
            if (chordPitchClasses.Length == 0)
                continue;

            var bassPitch = ValidateMidiPitch(
                PitchClassToMidiAtOrAbove(chord.Chord.RootPitchClass, OctaveToMidiBase(opt.BassOctave)),
                nameof(AccompanimentOptions.BassOctave));

            if (opt.Pattern == AccompanimentPattern.Block)
            {
                events.Add(new NoteEvent(bassPitch, start, duration, opt.BassVelocity));

                var chordVoicing = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
                events.AddRange(chordVoicing.Select(t1 => new NoteEvent(t1, start, duration, opt.ChordVelocity)));

                continue;
            }

            // Arpeggio
            var step = opt.Subdivision;
            step = step.Numerator switch
            {
                <= 0 => Rational.Eighth,
                _ => step
            };

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

        return [.. events];
    }

    /// <summary>
    /// Generate accompaniment from a roman-numeral progression.
    /// Chords are spelled in the provided key.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="progression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is a <c>default(AccompanimentOptions)</c>-like value (see <see cref="AccompanimentOptions.Default"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">A computed MIDI pitch falls outside 0..127 (bad <see cref="AccompanimentOptions.BassOctave"/> or <see cref="AccompanimentOptions.ChordOctave"/>).</exception>
    public static NoteEvent[] Generate(
        IReadOnlyList<HarmonicRhythmItem> progression,
        KeySignature key,
        AccompanimentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(progression);

        var opt = options ?? AccompanimentOptions.Default;
        ValidateOptions(opt);
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

            var chordPitchClasses = DeduplicatePitchClasses(pcs[..pcCount], opt.MaxChordTones);
            if (chordPitchClasses.Length == 0)
            {
                offset += duration;
                continue;
            }

            var rootPc = roman.GetRootPitchClass(key);
            var bassPitch = ValidateMidiPitch(
                PitchClassToMidiAtOrAbove(rootPc, OctaveToMidiBase(opt.BassOctave)),
                nameof(AccompanimentOptions.BassOctave));

            if (opt.Pattern == AccompanimentPattern.Block)
            {
                events.Add(new NoteEvent(bassPitch, offset, duration, opt.BassVelocity));

                var chordVoicing = VoicePitchClasses(chordPitchClasses, opt.ChordOctave);
                events.AddRange(chordVoicing.Select(t1 => new NoteEvent(t1, offset, duration, opt.ChordVelocity)));

                offset += duration;
                continue;
            }

            // Arpeggio
            var step = opt.Subdivision;
            step = step.Numerator switch
            {
                <= 0 => Rational.Eighth,
                _ => step
            };

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

        return [.. events];
    }

    /// <summary>
    /// The pitch classes of <paramref name="pitches"/>, at most <paramref name="max"/> of them,
    /// keeping the ones that carry the chord.
    /// </summary>
    /// <remarks>
    /// This used to keep whichever came first in the array, so the same chord written
    /// [60, 64, 67, 70] and [70, 67, 64, 60] was voiced as root-and-third one way and
    /// seventh-and-fifth the other: re-ordering a caller's pitches changed the harmony. What is
    /// kept now follows the music — root, then third, then seventh, then fifth, then whatever
    /// colour is left, which is the order a player drops notes in when there are not enough
    /// fingers or voices for all of them.
    /// </remarks>
    private static byte[] GetUniquePitchClasses(int[] pitches, int max, byte rootPitchClass)
    {
        if (pitches.Length == 0 || max <= 0)
            return [];

        Span<bool> seen = stackalloc bool[12];
        var distinct = new List<byte>(Math.Min(12, pitches.Length));

        foreach (var pitch in pitches)
        {
            // Pitch classes are cyclic: fold so a negative pitch maps into 0..11 rather
            // than wrapping past the (byte) cast into an out-of-bounds seen[] index.
            var pc = (byte)PitchMath.Fold(pitch);
            if (seen[pc])
                continue;
            seen[pc] = true;
            distinct.Add(pc);
        }

        if (distinct.Count == 0)
            return [];

        distinct.Sort((a, b) =>
        {
            var byRole = ChordToneRank(a, rootPitchClass).CompareTo(ChordToneRank(b, rootPitchClass));
            return byRole != 0
                ? byRole
                : PitchMath.Fold(a - rootPitchClass).CompareTo(PitchMath.Fold(b - rootPitchClass));
        });

        var kept = distinct.Take(Math.Min(12, max)).ToList();
        kept.Sort();
        return [.. kept];
    }

    /// <summary>How readily a chord tone is given up when there is not room for all of them.</summary>
    private static int ChordToneRank(byte pitchClass, byte rootPitchClass) =>
        PitchMath.Fold(pitchClass - rootPitchClass) switch
        {
            0 => 0,             // the root
            3 or 4 => 1,        // the third, which carries the quality
            10 or 11 => 2,      // the seventh
            7 => 3,             // the perfect fifth, the first note a player drops
            _ => 4              // colour: ninths, elevenths, altered fifths
        };

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

        foreach (var pitch in voiced)
            ValidateMidiPitch(pitch, nameof(AccompanimentOptions.ChordOctave));

        return voiced;
    }

    private static void ValidateOptions(in AccompanimentOptions options)
    {
        // A default(AccompanimentOptions) struct has MaxChordTones == 0, which produces
        // no chord tones at all and used to realize silently to an empty accompaniment.
        if (options.MaxChordTones <= 0)
        {
            throw new ArgumentException(
                "MaxChordTones must be positive; this looks like default(AccompanimentOptions). " +
                "Pass null or AccompanimentOptions.Default to use the standard defaults, " +
                "or start from AccompanimentOptions.Default with a `with` expression to customize.",
                "options");
        }
    }

    private static int ValidateMidiPitch(int pitch, string optionName)
    {
        if (pitch is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(
                optionName,
                pitch,
                $"Computed MIDI pitch is outside 0..127; adjust AccompanimentOptions.{optionName}.");
        }

        return pitch;
    }

    private static int OctaveToMidiBase(int octave) => 12 * (octave + 1);

    private static int PitchClassToMidiAtOrAbove(byte pitchClass, int minMidi)
    {
        var basePc = PitchMath.Fold(minMidi);
        var delta = (pitchClass - basePc + 12) % 12;
        return minMidi + delta;
    }
}
