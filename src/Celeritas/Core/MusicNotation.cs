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
    /// <remarks>
    /// This value is reserved across the library: a note event carrying it is silence, not a
    /// note. Analysis ignores it — it contributes no pitch class, no onset and no duration — and
    /// the MIDI and MusicXML writers leave a gap where it falls rather than writing a note.
    /// <see cref="MusicMath.Transpose(NoteBuffer, int)"/> leaves it alone for the same reason.
    /// </remarks>
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
    /// <exception cref="ArgumentException"><paramref name="input"/> is not valid notation: a syntax
    /// error, an unknown pitch or duration, or a pitch outside the MIDI range 0-127.</exception>
    public static NoteEvent[] Parse(string input, bool validateMeasures = false)
    {
        // string.IsNullOrWhiteSpace downstream treats null as blank, so null used to come back
        // as an empty NoteEvent[] — the same answer a blank string legitimately gets.
        ArgumentNullException.ThrowIfNull(input);
        return MusicNotationAntlrParser.ParseNotes(input, validateMeasures);
    }

    /// <summary>
    /// Parse music notation into a full result: notes plus directives (tempo, dynamics,
    /// sections, parts) and the leading time signature. Use this when you need more than the
    /// note events <see cref="Parse(string, bool)"/> returns.
    /// </summary>
    /// <param name="input">Music notation string</param>
    /// <param name="validateMeasures">Validate measure durations against time signature</param>
    /// <returns>
    /// The parsed notes together with directives and the leading time signature.
    /// <see cref="ParseResult.Errors"/> is always empty — a parse error throws instead.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> is not valid notation: a syntax
    /// error, an unknown pitch or duration, or a pitch outside the MIDI range 0-127.</exception>
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
                    _ => FallbackForm(duration)
                }
            },
            _ => FallbackForm(duration)
        };
    }

    /// <summary>
    /// The written form of a duration outside the plain note values, chosen so the parser can
    /// read it back: <c>1/n</c> is the denominator on its own, which is how a tuplet is written
    /// (<c>C4/12</c> is a triplet eighth).
    /// </summary>
    /// <remarks>
    /// A duration whose numerator is not 1 has no single written form — a note lasting 5/4 is
    /// written as tied notes, which only a sequence can express. This used to emit the rational
    /// as "5/4", and the note came out as "C4/5/4", which is not notation at all: it did not
    /// parse. The sequence writer splits such a duration up before it gets here; what reaches
    /// this point is a lone duration being displayed, so the rational is the honest answer.
    /// </remarks>
    private static string FallbackForm(Rational duration) =>
        duration.Numerator == 1
            ? duration.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"{duration.Numerator}/{duration.Denominator}";

    /// <summary>
    /// Splits <paramref name="duration"/> into pieces the notation can each write down. A
    /// duration of the form 1/n, or a dotted note value, is one piece; anything else — a note
    /// lasting two whole notes, or five quarters — becomes that many pieces of 1/n, which a
    /// melodic line joins with ties and a silence simply lists one rest after another.
    /// </summary>
    private static List<Rational> SplitIntoWritablePieces(Rational duration)
    {
        var pieces = new List<Rational>();

        // A duration the notation writes in one go stays one piece — including the dotted note
        // values, whose 3/2 and 3/4 would otherwise be broken up by the whole-note loop below
        // and come back as a tie the grammar does not accept on a chord.
        if (IsWritableAlone(duration))
        {
            return [duration];
        }

        var remaining = duration;

        // Whole notes first, so two whole notes is a tie of two rather than a list of pieces
        // as long as the numerator.
        while (remaining >= Rational.Whole)
        {
            pieces.Add(Rational.Whole);
            remaining -= Rational.Whole;
        }

        if (remaining > Rational.Zero)
        {
            if (IsWritableAlone(remaining))
            {
                pieces.Add(remaining);
            }
            else
            {
                var piece = new Rational(1, remaining.Denominator);
                for (long k = 0; k < remaining.Numerator; k++)
                {
                    pieces.Add(piece);
                }
            }
        }

        return pieces;
    }

    /// <summary>True when the notation has a single written form for this duration.</summary>
    private static bool IsWritableAlone(Rational duration) =>
        duration.Numerator == 1
        || (duration.Numerator == 3 && IsPowerOfTwo(duration.Denominator) && duration.Denominator >= 2);

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

        // Notation is read as a timeline, so what is written has to be able to hold the timeline
        // it was given. Written as one melodic line, three things silently became different
        // music: a gap between two notes vanished (they were emitted back to back), notes that
        // begin together but last different lengths turned into a succession, and overlapping
        // notes did the same — "C4/4 E4/2" for a C and an E struck together reads back as a C
        // followed by an E. Lay the notes out in as many voices as the timeline needs, and use
        // the polyphonic form when that is more than one.
        var voices = SeparateForNotation(sequence, groupChords);

        if (voices.Count == 1)
        {
            return FormatVoice(voices[0], useDot, useLetters, groupChords);
        }

        var sb = new StringBuilder();
        sb.Append("<< ");
        for (var v = 0; v < voices.Count; v++)
        {
            if (v > 0)
            {
                sb.Append(" | ");
            }

            sb.Append(FormatVoice(voices[v], useDot, useLetters, groupChords));
        }

        sb.Append(" >>");
        return sb.ToString();
    }

    /// <summary>
    /// Lays the notes out in the fewest voices that a melodic line can hold: within a voice each
    /// event starts at or after the end of the one before it, and notes sharing an event share
    /// an offset and a duration, so they can be written as a chord.
    /// </summary>
    private static List<List<NoteEvent>> SeparateForNotation(ReadOnlySpan<NoteEvent> sequence, bool groupChords)
    {
        var ordered = new List<NoteEvent>(sequence.Length);
        foreach (ref readonly var note in sequence)
        {
            ordered.Add(note);
        }

        ordered.Sort(static (a, b) =>
        {
            var byTime = a.Offset.CompareTo(b.Offset);
            if (byTime != 0) return byTime;
            var byLength = a.Duration.CompareTo(b.Duration);
            return byLength != 0 ? byLength : a.Pitch.CompareTo(b.Pitch);
        });

        var voices = new List<List<NoteEvent>>();
        foreach (var note in ordered)
        {
            var placed = false;
            foreach (var voice in voices)
            {
                var last = voice[^1];

                // Joins the chord this voice is holding, or starts after the voice is free.
                // A duration that has to be written as tied pieces cannot join a chord: the
                // notation ties notes, not chords, so "[F4 G4]/1~ [F4 G4]/1" does not parse.
                // Such notes each become their own voice and carry their own ties.
                var joinsChord = groupChords
                    && note.Pitch != RestPitch
                    && last.Pitch != RestPitch
                    && last.Offset == note.Offset
                    && last.Duration == note.Duration
                    && IsWritableAlone(note.Duration);

                if (joinsChord || last.Offset + last.Duration <= note.Offset)
                {
                    voice.Add(note);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                voices.Add([note]);
            }
        }

        return voices;
    }

    /// <summary>
    /// Writes one voice as a melodic line, filling the silence before and between its notes with
    /// rests so that reading it back puts every note where it started.
    /// </summary>
    private static string FormatVoice(List<NoteEvent> voice, bool useDot, bool useLetters, bool groupChords)
    {
        var separator = useLetters ? ':' : '/';
        var sb = new StringBuilder();
        var cursor = Rational.Zero;
        var i = 0;

        while (i < voice.Count)
        {
            var note = voice[i];

            if (note.Offset > cursor)
            {
                // One rest per writable piece: rests are not tied, they simply follow one
                // another, and consecutive rests add up to the same silence.
                foreach (var piece in SplitIntoWritablePieces(note.Offset - cursor))
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append('R');
                    sb.Append(separator);
                    sb.Append(FormatDuration(piece, useDot, useLetters));
                }

                cursor = note.Offset;
            }

            // Everything at this offset with this duration is one chord.
            var j = i + 1;
            if (groupChords && note.Pitch != RestPitch && IsWritableAlone(note.Duration))
            {
                while (j < voice.Count &&
                       voice[j].Offset == note.Offset &&
                       voice[j].Duration == note.Duration &&
                       voice[j].Pitch != RestPitch)
                {
                    j++;
                }
            }

            // A duration the notation cannot write in one go becomes tied pieces — a note
            // lasting two whole notes is two whole notes tied, which is how it would be
            // engraved. Writing the rational instead produced "C4/5/4", which does not parse.
            var pieces = SplitIntoWritablePieces(note.Duration);
            for (var piece = 0; piece < pieces.Count; piece++)
            {
                if (sb.Length > 0) sb.Append(' ');

                if (j - i > 1)
                {
                    sb.Append('[');
                    for (var k = i; k < j; k++)
                    {
                        if (k > i) sb.Append(' ');
                        sb.Append(ToNotation(voice[k].Pitch));
                    }

                    sb.Append(']');
                }
                else if (note.Pitch == RestPitch)
                {
                    sb.Append('R');
                }
                else
                {
                    sb.Append(ToNotation(note.Pitch));
                }

                sb.Append(separator);
                sb.Append(FormatDuration(pieces[piece], useDot, useLetters));

                // Tie every piece but the last to the one after it, so they sound as one note.
                // Rests are not tied: they are simply written one after another.
                if (piece < pieces.Count - 1 && note.Pitch != RestPitch)
                {
                    sb.Append('~');
                }
            }

            cursor = note.Offset + note.Duration;
            i = j;
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
            // The ramp duration is optional in the notation, so "@bpm=120->180" is a ramp with
            // no stated length. Requiring both parts dropped the target and wrote back
            // "@bpm 120" — a passage that ramped to 180 came out holding its opening tempo.
            TempoBpmDirective { TargetBpm: not null, RampDuration: not null } ramp =>
                $"@bpm {ramp.Bpm} -> {ramp.TargetBpm} {(useLetters ? ':' : '/')}{FormatDuration(ramp.RampDuration.Value, useDot: true, useLetters)}",
            TempoBpmDirective { TargetBpm: not null } target => $"@bpm {target.Bpm} -> {target.TargetBpm}",
            TempoBpmDirective bpm => $"@bpm {bpm.Bpm}",

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

        // Scratch list reused across iterations (cleared per note) to avoid a
        // single-element List allocation for every note in the sequence.
        var chordNotes = groupChords ? new List<NoteEvent>() : null;

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
                    chordNotes!.Clear();
                    chordNotes.Add(notes[noteIndex]);
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
    /// The whole string must be consumed: a pitch class, then at most one mode token. No token
    /// means major; the word forms "min"/"minor" and "maj"/"major" are case-insensitive and may
    /// follow a single space. A lone 'm'/'M' must attach directly to the pitch and is
    /// case-significant, so "Em" is E minor and "EM" is E major (changed in 0.10.0). Trailing
    /// text is rejected — "Gm7", "dorian" and "Cat" are all invalid.
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
    /// The whole input must be consumed: a pitch-class prefix followed by at most one
    /// mode token ("m" / "min" / "minor" for minor forms, "M" / "maj" / "major" for
    /// major forms; no token at all also means major). Word forms may be separated from
    /// the pitch by a single space and are case-insensitive; a lone 'm'/'M' attaches
    /// directly to the pitch and is case-significant (Em = E minor, EM = E major).
    /// </summary>
    private static bool TryParseKey(ReadOnlySpan<char> keyString, out KeySignature key)
    {
        key = default;

        keyString = keyString.Trim();
        if (keyString.IsEmpty)
        {
            return false;
        }

        if (!TryParsePitchClass(keyString, out var pitchClass, out var consumed))
        {
            return false;
        }

        // The remainder after the pitch class must be exactly one mode token; a single
        // space is allowed only before the word forms. Anything else is not a key.
        var remainder = keyString[consumed..];
        var hadSpace = false;
        if (!remainder.IsEmpty && remainder[0] == ' ')
        {
            hadSpace = true;
            remainder = remainder[1..];
            if (remainder.IsEmpty)
            {
                return false;
            }
        }

        bool isMajor;
        if (remainder.IsEmpty)
        {
            isMajor = true;
        }
        else if (remainder.Length == 1)
        {
            // Single-letter mode: attaches directly to the pitch (never after a space),
            // and case is significant ("Em" = E minor, "EM" = E major).
            if (hadSpace || (remainder[0] != 'm' && remainder[0] != 'M'))
            {
                return false;
            }

            isMajor = remainder[0] == 'M';
        }
        else if (remainder.Equals("min", StringComparison.OrdinalIgnoreCase) ||
                 remainder.Equals("minor", StringComparison.OrdinalIgnoreCase))
        {
            isMajor = false;
        }
        else if (remainder.Equals("maj", StringComparison.OrdinalIgnoreCase) ||
                 remainder.Equals("major", StringComparison.OrdinalIgnoreCase))
        {
            isMajor = true;
        }
        else
        {
            return false;
        }

        key = new KeySignature((byte)pitchClass, isMajor);
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
