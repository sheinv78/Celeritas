// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Core;

/// <summary>
/// Parser for musical notation (scientific pitch notation)
/// Supports: C4, D#5, Bb3, etc.
/// </summary>
public static partial class MusicNotation
{
    /// <summary>
    /// Special pitch value indicating a rest (silence).
    /// </summary>
    public const int REST_PITCH = -1;

    /// <summary>
    /// Parse scientific pitch notation to MIDI pitch number
    /// Examples: "C4" -> 60, "A4" -> 69, "C#5" -> 73, "Db3" -> 49
    /// </summary>
    public static int ParseNote(string notation)
    {
        if (notation is null)
            throw new ArgumentNullException(nameof(notation));

        if (!TryParseNote(notation.AsSpan(), out var midi))
            throw new ArgumentException($"Invalid note notation: {notation}. Expected formats: 60, C4, D#5, Bb3", nameof(notation));

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
            return false;

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
        if (!TryParsePitchClass(notation, out var pitchClass, out var consumed))
            return false;

        var octaveSpan = notation[consumed..];
        if (octaveSpan.IsEmpty)
            return false;

        if (!int.TryParse(octaveSpan, out var octave))
            return false;

        // MIDI number: (octave + 1) * 12 + pitchClass, where C-1 = 0
        var value = (octave + 1) * 12 + pitchClass;
        if ((uint)value > 127u)
            return false;

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
    public static NoteEvent[] Parse(string input, bool validateMeasures = false)
    {
        return MusicNotationAntlrParser.ParseNotes(input, validateMeasures);
    }

    /// <summary>
    /// Parse duration string
    /// Supports: 1 (whole), 2 (half), 4 (quarter), 8 (eighth), 16 (16th)
    ///           w/whole, h/half, q/quarter, e/eighth, s/16th
    ///           Dotted: 4. (dotted quarter = 3/8), 2. (dotted half = 3/4)
    /// </summary>
    public static Rational ParseDuration(string duration)
    {
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

        // Dotted note: add half of the base duration
        if (isDotted)
            return baseValue + baseValue / 2;

        return baseValue;
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
                if (useLetters)
                {
                    return baseNote switch
                    {
                        1 => "w.",
                        2 => "h.",
                        4 => "q.",
                        8 => "e.",
                        16 => "s.",
                        32 => "t.",
                        _ => $"{duration.Numerator}/{duration.Denominator}"
                    };
                }
                else
                {
                    return baseNote switch
                    {
                        1 => "1.",
                        2 => "2.",
                        4 => "4.",
                        8 => "8.",
                        16 => "16.",
                        32 => "32.",
                        _ => $"{duration.Numerator}/{duration.Denominator}"
                    };
                }
            }
        }

        // Standard durations
        if (duration.Numerator == 1 && IsPowerOfTwo(duration.Denominator))
        {
            if (useLetters)
            {
                return duration.Denominator switch
                {
                    1 => "w",
                    2 => "h",
                    4 => "q",
                    8 => "e",
                    16 => "s",
                    32 => "t",
                    _ => $"{duration.Numerator}/{duration.Denominator}"
                };
            }
            else
            {
                return duration.Denominator switch
                {
                    1 => "1",
                    2 => "2",
                    4 => "4",
                    8 => "8",
                    16 => "16",
                    32 => "32",
                    _ => $"{duration.Numerator}/{duration.Denominator}"
                };
            }
        }

        // Fallback: rational format
        return $"{duration.Numerator}/{duration.Denominator}";
    }

    private static bool IsPowerOfTwo(long n) => n > 0 && (n & (n - 1)) == 0;

    /// <summary>
    /// Format note sequence to string
    /// </summary>
    /// <param name="sequence">Sequence of note events</param>
    /// <param name="useDot">Enable dotted note notation</param>
    /// <param name="useLetters">Use letter notation (q, h, e, w) instead of numbers</param>
    /// <returns>Formatted sequence (e.g., "C4/4 E4/8 G4/2." or "C4:q E4:e G4:h.")</returns>
    public static string FormatNoteSequence(ReadOnlySpan<NoteEvent> sequence, bool useDot = true, bool useLetters = false)
    {
        if (sequence.IsEmpty)
            return string.Empty;

        var separator = useLetters ? ':' : '/';
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < sequence.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            var note = sequence[i];

            // Handle rests
            if (note.Pitch == REST_PITCH)
            {
                sb.Append('R');
            }
            else
            {
                sb.Append(ToNotation(note.Pitch));
            }

            sb.Append(separator);
            sb.Append(FormatDuration(note.Duration, useDot, useLetters));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Convert MIDI pitch number to scientific notation
    /// Examples: 60 -> "C4", 69 -> "A4", 73 -> "C#5"
    /// </summary>
    public static string ToNotation(int midiPitch, bool preferSharps = true)
    {
        if (midiPitch < 0 || midiPitch > 127)
            throw new ArgumentException($"MIDI pitch must be 0-127, got {midiPitch}", nameof(midiPitch));

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
    public static KeySignature ParseKey(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
            throw new ArgumentException("Key signature cannot be empty", nameof(keyString));

        if (!TryParseKey(keyString.AsSpan(), out var key))
            throw new ArgumentException($"Invalid key signature: {keyString}. Expected formats: C, Cm, C minor, C# major", nameof(keyString));

        return key;
    }

    /// <summary>
    /// Try-parse a key signature.
    /// Accepts: C, Cm, C minor, C major, C#, Db minor, etc.
    /// </summary>
    public static bool TryParseKey(ReadOnlySpan<char> keyString, out KeySignature key)
    {
        key = default;

        keyString = keyString.Trim();
        if (keyString.IsEmpty)
            return false;

        // Detect minor: contains "min"/"minor" or ends with 'm' (but not "maj"/"major")
        var lower = keyString.ToString().ToLowerInvariant();
        var isMinor = lower.Contains("minor", StringComparison.Ordinal) ||
                      lower.Contains("min", StringComparison.Ordinal) ||
                      (lower.EndsWith('m') && !lower.EndsWith("maj", StringComparison.Ordinal) && !lower.EndsWith("major", StringComparison.Ordinal));

        // Strip mode keywords
        lower = lower
            .Replace("major", "", StringComparison.Ordinal)
            .Replace("minor", "", StringComparison.Ordinal)
            .Replace("maj", "", StringComparison.Ordinal)
            .Replace("min", "", StringComparison.Ordinal)
            .Trim();

        // Strip trailing 'm' (e.g., "cm")
        if (lower.Length > 1 && lower[^1] == 'm')
            lower = lower[..^1];

        if (!TryParsePitchClass(lower.AsSpan(), out var pitchClass, out _))
            return false;

        key = new KeySignature((byte)pitchClass, !isMinor);
        return true;
    }

    internal static bool TryParsePitchClass(ReadOnlySpan<char> text, out int pitchClass, out int consumed)
    {
        pitchClass = 0;
        consumed = 0;

        text = text.Trim();
        if (text.IsEmpty)
            return false;

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
            return false;

        consumed = 1;
        if (text.Length >= 2)
        {
            var accidental = text[1];
            if (accidental == '#' || accidental == '♯')
            {
                pitchClass = (pitchClass + 1) % 12;
                consumed = 2;
            }
            else if (accidental == 'b' || accidental == '♭')
            {
                pitchClass = (pitchClass + 11) % 12;
                consumed = 2;
            }
        }

        return true;
    }

    /// <summary>
    /// Try to parse time signature prefix from sequence string
    /// Formats: "3/4: notes...", "4/4| notes...", "6/8 | notes..."
    /// </summary>
    private static bool TryParseTimeSignaturePrefix(string sequence, out Analysis.TimeSignature timeSignature, out string remainingSequence)
    {
        timeSignature = default;
        remainingSequence = sequence;

        // Look for pattern: "N/N:" or "N/N|" at the start
        var colonIdx = sequence.IndexOf(':');
        var barIdx = sequence.IndexOf('|');

        var separatorIdx = -1;
        if (colonIdx >= 0 && barIdx >= 0)
            separatorIdx = Math.Min(colonIdx, barIdx);
        else if (colonIdx >= 0)
            separatorIdx = colonIdx;
        else if (barIdx >= 0)
            separatorIdx = barIdx;

        if (separatorIdx <= 0 || separatorIdx > 10) // Reasonable limit
            return false;

        var prefix = sequence[..separatorIdx].Trim();
        var slashIdx = prefix.IndexOf('/');

        if (slashIdx <= 0)
            return false;

        var beatsStr = prefix[..slashIdx].Trim();
        var unitStr = prefix[(slashIdx + 1)..].Trim();

        if (int.TryParse(beatsStr, out var beats) && int.TryParse(unitStr, out var unit))
        {
            timeSignature = new Analysis.TimeSignature(beats, unit);
            remainingSequence = sequence[(separatorIdx + 1)..].TrimStart();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Split text by whitespace, but respect brackets for chords
    /// Example: "C4/4 [C4 E4 G4]/4 D4/4" -> ["C4/4", "[C4 E4 G4]/4", "D4/4"]
    /// </summary>
    private static List<string> SplitRespectingBrackets(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var bracketDepth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '[' || c == '(')
            {
                bracketDepth++;
                current.Append(c);
            }
            else if (c == ']' || c == ')')
            {
                bracketDepth--;
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && bracketDepth == 0)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
