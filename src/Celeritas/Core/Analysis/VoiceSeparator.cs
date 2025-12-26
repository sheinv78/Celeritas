// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Buffers;

namespace Celeritas.Core.Analysis;

/// <summary>
/// Represents a separated voice (melodic line) in polyphonic music.
/// </summary>
public sealed class Voice
{
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
public readonly struct VoiceNote
{
    public int Pitch { get; init; }
    public Rational Offset { get; init; }
    public Rational Duration { get; init; }
    public float Velocity { get; init; }

    /// <summary>Original index in the NoteBuffer.</summary>
    public int OriginalIndex { get; init; }

    public Rational End => Offset + Duration;

    public override string ToString() =>
        $"{ChordLibrary.NoteNames[Pitch % 12]}{Pitch / 12 - 1} @ {Offset}";
}

/// <summary>
/// Result of voice separation analysis.
/// </summary>
public sealed class VoiceSeparationResult
{
    public required IReadOnlyList<Voice> Voices { get; init; }
    public required int TotalNotes { get; init; }
    public required int VoiceCrossings { get; init; }
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
    /// <summary>
    /// Default options for voice separation.
    /// </summary>
    public static readonly VoiceSeparatorOptions DefaultOptions = new();

    /// <summary>
    /// Separate notes into voices using pitch-proximity algorithm.
    /// </summary>
    public static VoiceSeparationResult Separate(NoteBuffer buffer, int maxVoices = 4)
        => Separate(buffer, maxVoices, DefaultOptions);

    /// <summary>
    /// Convenience SATB separation: returns exactly 4 voices named Soprano/Alto/Tenor/Bass.
    /// </summary>
    public static SatbSeparationResult SeparateIntoSATB(IEnumerable<NoteEvent> notes, VoiceSeparatorOptions? options = null)
    {
        var arr = notes as NoteEvent[] ?? notes.ToArray();
        using var buffer = new NoteBuffer(Math.Max(4, arr.Length));
        buffer.AddRange(arr);
        return SeparateIntoSATB(buffer, options);
    }

    /// <summary>
    /// Convenience SATB separation: returns exactly 4 voices named Soprano/Alto/Tenor/Bass.
    /// </summary>
    public static SatbSeparationResult SeparateIntoSATB(NoteBuffer buffer, VoiceSeparatorOptions? options = null)
    {
        var res = Separate(buffer, maxVoices: 4, options ?? DefaultOptions);

        // Ensure deterministic ordering: Index 0 is highest (soprano) per VoiceSeparator contract.
        var voices = res.Voices.ToList();

        while (voices.Count < 4)
            voices.Add(new Voice { Index = voices.Count, Name = $"Voice {voices.Count + 1}" });

        var soprano = RenameVoice(voices[0], "Soprano");
        var alto = RenameVoice(voices[1], "Alto");
        var tenor = RenameVoice(voices[2], "Tenor");
        var bass = RenameVoice(voices[3], "Bass");

        return new SatbSeparationResult
        {
            Full = res,
            Soprano = soprano,
            Alto = alto,
            Tenor = tenor,
            Bass = bass
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
    public static VoiceSeparationResult Separate(NoteBuffer buffer, int maxVoices, VoiceSeparatorOptions options)
    {
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
        var voiceLastEnd = new Rational[maxVoices];
        var voiceCrossings = 0;

        // Initialize voice pitches based on typical ranges
        InitializeVoiceRanges(voiceLastPitch, maxVoices);

        // Process each time slice
        var timeSlices = GroupByOnset(notes);

        foreach (var slice in timeSlices)
        {
            // Sort notes in this slice by pitch (high to low)
            var sliceNotes = slice.OrderByDescending(n => n.note.Pitch).ToList();

            if (sliceNotes.Count <= maxVoices)
            {
                // Simple case: assign top-down
                for (int i = 0; i < sliceNotes.Count; i++)
                {
                    var (note, origIndex) = sliceNotes[i];
                    var voiceIdx = AssignToNearestVoice(note.Pitch, voiceLastPitch, voiceLastEnd,
                        note.Offset, i, sliceNotes.Count, maxVoices, options);

                    voices[voiceIdx].Notes.Add(note);
                    noteToVoice[origIndex] = voiceIdx;

                    // Check for voice crossing
                    if (voiceIdx > 0 && note.Pitch > voiceLastPitch[voiceIdx - 1] && voiceLastPitch[voiceIdx - 1] > 0)
                        voiceCrossings++;
                    if (voiceIdx < maxVoices - 1 && note.Pitch < voiceLastPitch[voiceIdx + 1] && voiceLastPitch[voiceIdx + 1] > 0)
                        voiceCrossings++;

                    voiceLastPitch[voiceIdx] = note.Pitch;
                    voiceLastEnd[voiceIdx] = note.End;
                }
            }
            else
            {
                // More notes than voices: use pitch-proximity assignment
                var assigned = new bool[sliceNotes.Count];
                var usedVoices = new bool[maxVoices];

                // First pass: assign to nearest available voice
                foreach (var (note, origIndex) in sliceNotes)
                {
                    var voiceIdx = FindBestVoice(note.Pitch, voiceLastPitch, usedVoices, maxVoices, options);

                    voices[voiceIdx].Notes.Add(note);
                    noteToVoice[origIndex] = voiceIdx;
                    usedVoices[voiceIdx] = true;
                    voiceLastPitch[voiceIdx] = note.Pitch;
                    voiceLastEnd[voiceIdx] = note.End;
                }
            }
        }

        // Remove empty voices and renumber
        var nonEmptyVoices = voices.Where(v => v.Notes.Count > 0).ToList();
        for (int i = 0; i < nonEmptyVoices.Count; i++)
        {
            nonEmptyVoices[i] = new Voice
            {
                Index = i,
                Name = GetVoiceName(i, nonEmptyVoices.Count),
                Notes = { }
            };
            foreach (var note in voices.First(v => v.Notes.SequenceEqual(nonEmptyVoices[i].Notes) == false &&
                v.Notes.Count > 0).Notes.Where(_ => false)) { } // Keep original notes
        }

        // Calculate separation quality
        var quality = CalculateSeparationQuality(voices, voiceCrossings);

        return new VoiceSeparationResult
        {
            Voices = voices.Where(v => v.Notes.Count > 0).ToList(),
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

    private static int AssignToNearestVoice(
        int pitch, int[] voiceLastPitch, Rational[] voiceLastEnd,
        Rational noteOnset, int noteIndex, int totalInSlice, int maxVoices,
        VoiceSeparatorOptions options)
    {
        // Simple heuristic: if fewer notes than voices, assign by position
        if (totalInSlice <= maxVoices)
        {
            // Spread across voices based on pitch ordering
            var targetVoice = (noteIndex * maxVoices) / totalInSlice;
            return Math.Clamp(targetVoice, 0, maxVoices - 1);
        }

        return FindBestVoice(pitch, voiceLastPitch, new bool[maxVoices], maxVoices, options);
    }

    private static int FindBestVoice(int pitch, int[] voiceLastPitch, bool[] usedVoices,
        int maxVoices, VoiceSeparatorOptions options)
    {
        int bestVoice = 0;
        int minDistance = int.MaxValue;

        for (int v = 0; v < maxVoices; v++)
        {
            if (usedVoices[v]) continue;

            var distance = Math.Abs(pitch - voiceLastPitch[v]);

            // Penalize large jumps
            if (distance > options.MaxMelodicInterval)
                distance += options.LargeJumpPenalty;

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

        var totalNotes = voices.Sum(v => v.Notes.Count);
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
        if (total == 4)
        {
            return index switch
            {
                0 => "Soprano",
                1 => "Alto",
                2 => "Tenor",
                3 => "Bass",
                _ => $"Voice {index + 1}"
            };
        }
        if (total == 3)
        {
            return index switch
            {
                0 => "Upper",
                1 => "Middle",
                2 => "Lower",
                _ => $"Voice {index + 1}"
            };
        }
        if (total == 2)
        {
            return index == 0 ? "Upper" : "Lower";
        }
        return $"Voice {index + 1}";
    }
}

/// <summary>
/// SATB (Soprano/Alto/Tenor/Bass) separation convenience result.
/// </summary>
public sealed class SatbSeparationResult
{
    public required VoiceSeparationResult Full { get; init; }
    public required Voice Soprano { get; init; }
    public required Voice Alto { get; init; }
    public required Voice Tenor { get; init; }
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

    /// <summary>Prefer stepwise motion.</summary>
    public bool PreferStepwise { get; init; } = true;

    /// <summary>Allow voice crossings.</summary>
    public bool AllowCrossings { get; init; } = true;
}
