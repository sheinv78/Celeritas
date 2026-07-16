// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text;

namespace Celeritas.Core;

/// <summary>
/// Parser for musical notation (scientific pitch notation)
/// Supports: C4, D#5, Bb3, etc.
/// </summary>
public static class MusicNotation
{
    /// <summary>
    /// Special pitch value indicating a rest (silence).
    /// </summary>
    public const int RestPitch = -1;

    /// <summary>
    /// Parse scientific pitch notation to MIDI pitch number
    /// Examples: "C4" -> 60, "A4" -> 69, "C#5" -> 73, "Db3" -> 49
    /// </summary>
    public static int ParseNote(string notation)
    {
        ArgumentNullException.ThrowIfNull(notation);

        if (!TryParseNote(notation.AsSpan(), out var midi))
        {
            throw new ArgumentException($"Invalid note notation: {notation}. Expected formats: 60, C4, D#5, Bb3", nameof(notation));
        }

        return midi;
    }

    /// <summary>
    /// Try-parse scientific pitch notation to MIDI pitch number.
    /// Accepts: MIDI numbers (0-127), C4, D#5, Db3, Bb3, and Unicode accidentals (♯, ♭).
    /// </summary>
    public static bool TryParseNote(ReadOnlySpan<char> notation, out int midi)
    {
        midi = 0;

        notation = notation.Trim();
        if (notation.IsEmpty)
        {
            return false;
        }

        // MIDI number (0-127)
        if (int.TryParse(notation, out var midiNumber))
        {
            if ((uint)midiNumber <= 127u)
            {
                midi = midiNumber;
                return true;
            }
            return false;
        }

        // Root pitch class
        if (!TryParsePitchClass(notation, out var pitchClass, out var octaveCarry, out var consumed))
        {
            return false;
        }

        var octaveSpan = notation[consumed..];
        if (octaveSpan.IsEmpty)
        {
            return false;
        }

        if (!int.TryParse(octaveSpan, out var octave))
        {
            return false;
        }

        // MIDI number: (octave + 1) * 12 + pitchClass, where C-1 = 0.
        // octaveCarry keeps enharmonic spellings in the right octave (Cb4 = B3, B#4 = C5).
        var value = ((octave + octaveCarry + 1) * 12) + pitchClass;
        if ((uint)value > 127u)
        {
            return false;
        }

        midi = value;
        return true;
    }

    /// <summary>
    /// Parse music notation into note events.
    /// Supports: notes, chords, rests, ties, time signatures, measures, polyphony.
    /// Examples: "C4/4 E4/4 G4/2", "[C4 E4 G4]/4", "C4/4~ C4/4", "4/4: C4/4 E4/4 | G4/2"
    /// </summary>
    /// <param name="input">Music notation string</param>
    /// <param name="validateMeasures">Validate measure durations against time signature</param>
    /// <returns>Array of note events with timing information</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static NoteEvent[] Parse(string input, bool validateMeasures = false)
    {
        // string.IsNullOrWhiteSpace downstream treats null as blank, so null used to come back
        // as an empty NoteEvent[] — the same answer a blank string legitimately gets.
        ArgumentNullException.ThrowIfNull(input);
        return MusicNotationAntlrParser.ParseNotes(input, validateMeasures);
    }

    /// <summary>
    /// Parse music notation into a full result: notes plus directives (tempo, dynamics,
    /// sections, parts), the leading time signature, and any parse errors. Use this when you
    /// need more than the note events <see cref="Parse(string, bool)"/> returns.
    /// </summary>
    /// <param name="input">Music notation string</param>
    /// <param name="validateMeasures">Validate measure durations against time signature</param>
    /// <returns>The parsed notes together with directives, time signature, and errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static ParseResult ParseFull(string input, bool validateMeasures = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MusicNotationAntlrParser.Parse(input, validateMeasures);
    }

    /// <summary>
    /// Parse duration string
    /// Supports: 1 (whole), 2 (half), 4 (quarter), 8 (eighth), 16 (16th)
    ///           w/whole, h/half, q/quarter, e/eighth, s/16th
    ///           Dotted: 4. (dotted quarter = 3/8), 2. (dotted half = 3/4)
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="duration"/> is <see langword="null"/>.</exception>
    public static Rational ParseDuration(string duration)
    {
        ArgumentNullException.ThrowIfNull(duration);

        var isDotted = duration.EndsWith('.');
        var baseDuration = isDotted ? duration[..^1] : duration;

        var baseValue = baseDuration.ToLowerInvariant() switch
        {
            "1" or "w" or "whole" => new Rational(1, 1),
            "2" or "h" or "half" => new Rational(1, 2),
            "4" or "q" or "quarter" => new Rational(1, 4),
            "8" or "e" or "eighth" => new Rational(1, 8),
            "16" or "s" or "16th" or "sixteenth" => new Rational(1, 16),
            "32" or "t" or "32nd" or "thirtysecond" => new Rational(1, 32),
            _ => throw new ArgumentException($"Invalid duration: {duration}")
        };

        return isDotted switch
        {
            // Dotted note: add half of the base duration
            true => baseValue + (baseValue / 2),
            _ => baseValue
        };
    }

    /// <summary>
    /// Format duration to string
    /// </summary>
    /// <param name="duration">Duration as Rational</param>
    /// <param name="useDot">Enable dotted note notation (e.g., 3/8 -> "4.")</param>
    /// <param name="useLetters">Use letter notation (q, h, e, w) instead of numbers</param>
    /// <returns>Formatted duration string</returns>
    public static string FormatDuration(Rational duration, bool useDot = true, bool useLetters = false)
    {
        // Check for dotted notes if enabled
        if (useDot)
        {
            // Dotted note: numerator = 3, denominator = 2^(n+1)
            // Examples: 3/8 -> 4., 3/16 -> 8., 3/4 -> 2.
            // Formula: dotted note value = denominator / 2
            if (duration.Numerator == 3 && IsPowerOfTwo(duration.Denominator))
            {
                var baseNote = duration.Denominator / 2;
                return useLetters switch
                {
                    true => baseNote switch
                    {
                        1 => "w.",
                        2 => "h.",
                        4 => "q.",
                        8 => "e.",
                        16 => "s.",
                        32 => "t.",
                        _ => $"{duration.Numerator}/{duration.Denominator}"
                    },
                    _ => baseNote switch
                    {
                        1 => "1.",
                        2 => "2.",
                        4 => "4.",
                        8 => "8.",
                        16 => "16.",
                        32 => "32.",
                        _ => $"{duration.Numerator}/{duration.Denominator}"
                    }
                };
            }
        }

        return duration.Numerator switch
        {
            // Standard durations
            1 when IsPowerOfTwo(duration.Denominator) => useLetters switch
            {
                true => duration.Denominator switch
                {
                    1 => "w",
                    2 => "h",
                    4 => "q",
                    8 => "e",
                    16 => "s",
                    32 => "t",
                    _ => $"{duration.Numerator}/{duration.Denominator}"
                },
                _ => duration.Denominator switch
                {
                    1 => "1",
                    2 => "2",
                    4 => "4",
                    8 => "8",
                    16 => "16",
                    32 => "32",
                    _ => $"{duration.Numerator}/{duration.Denominator}"
                }
            },
            _ => $"{duration.Numerator}/{duration.Denominator}"
        };

        // Fallback: rational format
    }

    private static bool IsPowerOfTwo(long n) => n > 0 && (n & (n - 1)) == 0;

    /// <summary>
    /// Format note sequence to string with chord grouping
    /// </summary>
    /// <param name="sequence">Sequence of note events</param>
    /// <param name="useDot">Enable dotted note notation</param>
    /// <param name="useLetters">Use letter notation (q, h, e, w) instead of numbers</param>
    /// <param name="groupChords">Group simultaneous notes as chords [C4 E4 G4]/4</param>
    /// <returns>Formatted sequence (e.g., "C4/4 [E4 G4]/4 R/2" or "C4:q [E4 G4]:q R:h")</returns>
    public static string FormatNoteSequence(ReadOnlySpan<NoteEvent> sequence, bool useDot = true, bool useLetters = false, bool groupChords = true)
    {
        if (sequence.IsEmpty)
        {
            return string.Empty;
        }

        var separator = useLetters ? ':' : '/';
        var sb = new StringBuilder();
        var i = 0;

        while (i < sequence.Length)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            // Check if next notes form a chord (same offset and duration)
            if (groupChords && i < sequence.Length - 1)
            {
                var chordNotes = new List<NoteEvent> { sequence[i] };
                var chordOffset = sequence[i].Offset;
                var chordDuration = sequence[i].Duration;

                // Collect all notes with same offset and duration
                var j = i + 1;
                while (j < sequence.Length &&
                       sequence[j].Offset == chordOffset &&
                       sequence[j].Duration == chordDuration &&
                       sequence[j].Pitch != RestPitch)
                {
                    chordNotes.Add(sequence[j]);
                    j++;
                }

                // If we found a chord (2+ notes), format as chord
                if (chordNotes.Count > 1 && sequence[i].Pitch != RestPitch)
                {
                    sb.Append('[');
                    for (var k = 0; k < chordNotes.Count; k++)
                    {
                        if (k > 0)
                        {
                            sb.Append(' ');
                        }

                        sb.Append(ToNotation(chordNotes[k].Pitch));
                    }
                    sb.Append(']');
                    sb.Append(separator);
                    sb.Append(FormatDuration(chordDuration, useDot, useLetters));
                    i = j;
                    continue;
                }
            }

            // Single note or rest
            var note = sequence[i];
            if (note.Pitch == RestPitch)
            {
                sb.Append('R');
            }
            else
            {
                sb.Append(ToNotation(note.Pitch));
            }

            sb.Append(separator);
            sb.Append(FormatDuration(note.Duration, useDot, useLetters));
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Format a single directive to string notation
    /// </summary>
    private static string FormatDirective(NotationDirective directive, bool useLetters = false)
    {
        return directive switch
        {
            TempoBpmDirective bpm => bpm is { TargetBpm: not null, RampDuration: not null }
                ? $"@bpm {bpm.Bpm} -> {bpm.TargetBpm} {(useLetters ? ':' : '/')}{FormatDuration(bpm.RampDuration.Value, useDot: true, useLetters)}"
                : $"@bpm {bpm.Bpm}",

            TempoCharacterDirective tempo => NeedsQuotes(tempo.Character)
                ? $"@tempo \"{tempo.Character}\""
                : $"@tempo {tempo.Character}",

            SectionDirective section => NeedsQuotes(section.Label)
                ? $"@section \"{section.Label}\""
                : $"@section {section.Label}",

            PartDirective part => NeedsQuotes(part.Name)
                ? $"@part \"{part.Name}\""
                : $"@part {part.Name}",

            DynamicsDirective dyn => dyn.Type switch
            {
                DynamicsType.Static => $"@dynamics {dyn.StartLevel}",
                DynamicsType.Crescendo => dyn.TargetLevel != null
                    ? $"@cresc to {dyn.TargetLevel}"
                    : "@cresc",
                DynamicsType.Diminuendo => dyn.TargetLevel != null
                    ? $"@dim to {dyn.TargetLevel}"
                    : "@dim",
                _ => throw new ArgumentException($"Unknown dynamics type: {dyn.Type}")
            },

            _ => throw new ArgumentException($"Unknown directive type: {directive.GetType().Name}")
        };

        static bool NeedsQuotes(string value) =>
            value.Length == 0 || value.Contains(' ') || value.Contains('\t') || !char.IsLower(value[0]);
    }

    /// <summary>
    /// Format notes and directives together in timeline order
    /// </summary>
    public static string FormatWithDirectives(
        ReadOnlySpan<NoteEvent> notes,
        ReadOnlySpan<NotationDirective> directives,
        bool useDot = true,
        bool useLetters = false,
        bool groupChords = true)
    {
        if (notes.IsEmpty && directives.IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var noteIndex = 0;
        var directiveIndex = 0;
        var currentTime = Rational.Zero;

        while (noteIndex < notes.Length || directiveIndex < directives.Length)
        {
            // Insert directives that occur at or before current time
            while (directiveIndex < directives.Length && directives[directiveIndex].Time <= currentTime)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(FormatDirective(directives[directiveIndex], useLetters));
                directiveIndex++;
            }

            // Add next note/chord
            if (noteIndex < notes.Length)
            {
                var nextNoteTime = notes[noteIndex].Offset;

                // Check if there are directives before next note
                if (directiveIndex < directives.Length && directives[directiveIndex].Time < nextNoteTime)
                {
                    currentTime = directives[directiveIndex].Time;
                    continue;
                }

                // Format note(s) starting at this time
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                // Check for chord (groupChords logic from FormatNoteSequence)
                if (groupChords && noteIndex < notes.Length - 1)
                {
                    var chordNotes = new List<NoteEvent> { notes[noteIndex] };
                    var chordOffset = notes[noteIndex].Offset;
                    var chordDuration = notes[noteIndex].Duration;

                    var j = noteIndex + 1;
                    while (j < notes.Length &&
                           notes[j].Offset == chordOffset &&
                           notes[j].Duration == chordDuration &&
                           notes[j].Pitch != RestPitch)
                    {
                        chordNotes.Add(notes[j]);
                        j++;
                    }

                    if (chordNotes.Count > 1 && notes[noteIndex].Pitch != RestPitch)
                    {
                        var separator = useLetters ? ':' : '/';
                        sb.Append('[');
                        for (var k = 0; k < chordNotes.Count; k++)
                        {
                            if (k > 0)
                            {
                                sb.Append(' ');
                            }

                            sb.Append(ToNotation(chordNotes[k].Pitch));
                        }
                        sb.Append(']');
                        sb.Append(separator);
                        sb.Append(FormatDuration(chordDuration, useDot, useLetters));
                        currentTime = chordOffset + chordDuration;
                        noteIndex = j;
                        continue;
                    }
                }

                // Single note
                var note = notes[noteIndex];
                var sep = useLetters ? ':' : '/';
                if (note.Pitch == RestPitch)
                {
                    sb.Append('R');
                }
                else
                {
                    sb.Append(ToNotation(note.Pitch));
                }
                sb.Append(sep);
                sb.Append(FormatDuration(note.Duration, useDot, useLetters));
                currentTime = note.Offset + note.Duration;
                noteIndex++;
            }
            else if (directiveIndex < directives.Length)
            {
                // Notes are exhausted but directives remain past currentTime:
                // jump to the next directive's time so the drain loop above emits it
                // (otherwise nothing advances and the loop never terminates).
                currentTime = directives[directiveIndex].Time;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert MIDI pitch number to scientific notation
    /// Examples: 60 -> "C4", 69 -> "A4", 73 -> "C#5"
    /// </summary>
    public static string ToNotation(int midiPitch, bool preferSharps = true)
    {
        if (midiPitch is < 0 or > 127)
        {
            throw new ArgumentException($"MIDI pitch must be 0-127, got {midiPitch}", nameof(midiPitch));
        }

        var octave = (midiPitch / 12) - 1;
        var pitchClass = midiPitch % 12;

        var noteName = pitchClass switch
        {
            0 => "C",
            1 => preferSharps ? "C#" : "Db",
            2 => "D",
            3 => preferSharps ? "D#" : "Eb",
            4 => "E",
            5 => "F",
            6 => preferSharps ? "F#" : "Gb",
            7 => "G",
            8 => preferSharps ? "G#" : "Ab",
            9 => "A",
            10 => preferSharps ? "A#" : "Bb",
            11 => "B",
            _ => "?"
        };

        return $"{noteName}{octave}";
    }

    /// <summary>
    /// Parse key signature from various formats
    /// Supports: "C", "Cm", "C minor", "c", "C#", "C# major", "Db minor"
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="keyString"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="keyString"/> is blank or is not a key signature.</exception>
    public static KeySignature ParseKey(string keyString)
    {
        // IsNullOrWhiteSpace would otherwise catch null and report it as "cannot be empty",
        // which is both the wrong exception type and a wrong description: null is a missing
        // argument, not a blank one.
        ArgumentNullException.ThrowIfNull(keyString);

        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new ArgumentException("Key signature cannot be empty", nameof(keyString));
        }

        if (!TryParseKey(keyString.AsSpan(), out var key))
        {
            throw new ArgumentException($"Invalid key signature: {keyString}. Expected formats: C, Cm, C minor, C# major", nameof(keyString));
        }

        return key;
    }

    /// <summary>
    /// Try-parse a key signature.
    /// Accepts: C, Cm, C minor, C major, C#, Db minor, etc.
    /// </summary>
    private static bool TryParseKey(ReadOnlySpan<char> keyString, out KeySignature key)
    {
        key = default;

        keyString = keyString.Trim();
        if (keyString.IsEmpty)
        {
            return false;
        }

        // Detect minor: contains "min"/"minor" or ends with 'm' (but not "maj"/"major")
        var lower = keyString.ToString().ToLowerInvariant();
        var isMinor = lower.Contains("minor", StringComparison.Ordinal) ||
                      lower.Contains("min", StringComparison.Ordinal) ||
                      (lower.EndsWith('m') && !lower.EndsWith("maj", StringComparison.Ordinal) && !lower.EndsWith("major", StringComparison.Ordinal));

        lower = lower.Length switch
        {
            // Strip trailing 'm' (e.g., "cm")
            > 1 when lower[^1] == 'm' => lower[..^1],
            // Strip mode keywords
            _ => lower.Replace("major", "", StringComparison.Ordinal)
                .Replace("minor", "", StringComparison.Ordinal)
                .Replace("maj", "", StringComparison.Ordinal)
                .Replace("min", "", StringComparison.Ordinal)
                .Trim()
        };

        if (!TryParsePitchClass(lower.AsSpan(), out var pitchClass, out _))
        {
            return false;
        }

        key = new KeySignature((byte)pitchClass, !isMinor);
        return true;
    }

    internal static bool TryParsePitchClass(ReadOnlySpan<char> text, out int pitchClass, out int consumed)
        => TryParsePitchClass(text, out pitchClass, out _, out consumed);

    /// <summary>
    /// Parses a note name with optional accidental. <paramref name="pitchClass"/> is the
    /// normalized pitch class (0-11); <paramref name="octaveCarry"/> is -1/0/+1 when the
    /// accidental crosses an octave boundary (Cb → B with carry -1, B# → C with carry +1),
    /// so octave-aware callers can compute the correct MIDI pitch.
    /// </summary>
    internal static bool TryParsePitchClass(ReadOnlySpan<char> text, out int pitchClass, out int octaveCarry, out int consumed)
    {
        pitchClass = 0;
        octaveCarry = 0;
        consumed = 0;

        text = text.Trim();
        if (text.IsEmpty)
        {
            return false;
        }

        var c = text[0];
        pitchClass = c switch
        {
            'C' or 'c' => 0,
            'D' or 'd' => 2,
            'E' or 'e' => 4,
            'F' or 'f' => 5,
            'G' or 'g' => 7,
            'A' or 'a' => 9,
            'B' or 'b' => 11,
            _ => -1
        };

        if (pitchClass < 0)
        {
            return false;
        }

        consumed = 1;
        if (text.Length >= 2)
        {
            var accidental = text[1];
            if (accidental is '#' or '♯')
            {
                pitchClass += 1;
                consumed = 2;
            }
            else if (accidental is 'b' or '♭')
            {
                pitchClass -= 1;
                consumed = 2;
            }
        }

        if (pitchClass < 0)
        {
            pitchClass += 12;
            octaveCarry = -1;
        }
        else if (pitchClass > 11)
        {
            pitchClass -= 12;
            octaveCarry = 1;
        }

        return true;
    }
}
