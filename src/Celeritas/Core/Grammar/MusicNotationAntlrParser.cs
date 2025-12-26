// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Antlr4.Runtime;
using Celeritas.Core.Analysis;
using Celeritas.Core.Grammar;
using Celeritas.Core.Ornamentation;

namespace Celeritas.Core;

/// <summary>
/// ANTLR-based music notation parser.
/// Supports: notes, chords, rests, ties, time signatures, measures, polyphony.
/// </summary>
public static class MusicNotationAntlrParser
{
    /// <summary>
    /// Parse music notation string into note events.
    /// </summary>
    /// <param name="input">Music notation string</param>
    /// <param name="validateMeasures">Validate measure durations against time signature</param>
    /// <returns>Parsed result with notes and metadata</returns>
    public static ParseResult Parse(string input, bool validateMeasures = false)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new ParseResult([], null, [], []);

        var inputStream = new AntlrInputStream(input);
        var lexer = new MusicNotationLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new MusicNotationParser(tokenStream);

        // Collect errors
        var errors = new List<string>();
        var lexerErrorListener = new LexerErrorListener(errors);
        var parserErrorListener = new ParserErrorListener(errors);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrorListener);

        var tree = parser.sequence();

        if (errors.Count > 0)
        {
            throw new ArgumentException($"Parse errors: {string.Join("; ", errors)}");
        }

        var visitor = new MusicNotationVisitorImpl(validateMeasures);
        var notes = visitor.Visit(tree);

        return new ParseResult(notes, visitor.TimeSignature, visitor.Directives.ToArray(), errors);
    }

    /// <summary>
    /// Parse music notation string into note events (simple overload).
    /// </summary>
    public static NoteEvent[] ParseNotes(string input, bool validateMeasures = false)
    {
        return Parse(input, validateMeasures).Notes;
    }
}

/// <summary>
/// Result of parsing music notation.
/// </summary>
public record ParseResult(
    NoteEvent[] Notes,
    TimeSignature? TimeSignature,
    NotationDirective[] Directives,
    IReadOnlyList<string> Errors
);

/// <summary>
/// ANTLR lexer error listener.
/// </summary>
internal class LexerErrorListener : IAntlrErrorListener<int>
{
    private readonly List<string> _errors;

    public LexerErrorListener(List<string> errors) => _errors = errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        _errors.Add($"Line {line}:{charPositionInLine} - {msg}");
    }
}

/// <summary>
/// ANTLR parser error listener.
/// </summary>
internal class ParserErrorListener : BaseErrorListener
{
    private readonly List<string> _errors;

    public ParserErrorListener(List<string> errors) => _errors = errors;

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        _errors.Add($"Line {line}:{charPositionInLine} - {msg}");
    }
}

/// <summary>
/// Visitor implementation that converts parse tree to NoteEvent array.
/// </summary>
internal class MusicNotationVisitorImpl : MusicNotationBaseVisitor<NoteEvent[]>
{
    private readonly bool _validateMeasures;
    private readonly List<NoteEvent> _notes = new();
    private readonly List<NotationDirective> _directives = new();
    private readonly HashSet<int> _pendingTies = new(); // Pitches that have pending ties
    private Rational _currentTime = Rational.Zero;
    private int _currentMeasure = 1;
    private Rational _measureStart = Rational.Zero;

    public TimeSignature? TimeSignature { get; private set; }
    public IReadOnlyList<NotationDirective> Directives => _directives;

    public MusicNotationVisitorImpl(bool validateMeasures)
    {
        _validateMeasures = validateMeasures;
    }

    public override NoteEvent[] VisitSequence(MusicNotationParser.SequenceContext context)
    {
        // Get all time signatures and voices
        var timeSignatures = context.timeSignature();
        var voices = context.voice();

        int timeSigIndex = 0;

        // Parse initial time signature if present
        if (timeSignatures.Length > 0 && timeSigIndex < timeSignatures.Length)
        {
            // Check if first time signature comes before first voice
            var firstTimeSig = timeSignatures[0];
            var firstVoice = voices[0];

            if (firstTimeSig.Start.StartIndex < firstVoice.Start.StartIndex)
            {
                VisitTimeSignature(firstTimeSig);
                timeSigIndex++;
            }
        }

        // Parse voices (measures) with potential time signature changes
        for (int voiceIdx = 0; voiceIdx < voices.Length; voiceIdx++)
        {
            var voice = voices[voiceIdx];

            // Check if there's a time signature change before this voice (after the bar)
            if (timeSigIndex < timeSignatures.Length)
            {
                var timeSig = timeSignatures[timeSigIndex];
                // Time signature should be between previous voice end and this voice start
                if (voiceIdx > 0 && timeSig.Start.StartIndex < voice.Start.StartIndex)
                {
                    VisitTimeSignature(timeSig);
                    timeSigIndex++;
                }
            }

            VisitVoice(voice);

            // After each voice (measure), validate if needed
            if (_validateMeasures && TimeSignature.HasValue)
            {
                var measureDuration = _currentTime - _measureStart;
                var expectedDuration = TimeSignature.Value.MeasureDuration;

                if (measureDuration != expectedDuration)
                {
                    throw new ArgumentException(
                        $"Measure {_currentMeasure} duration mismatch: expected {expectedDuration} ({TimeSignature.Value}), " +
                        $"but got {measureDuration}");
                }
            }

            _currentMeasure++;
            _measureStart = _currentTime;
        }

        return _notes.ToArray();
    }

    public override NoteEvent[] VisitTimeSignature(MusicNotationParser.TimeSignatureContext context)
    {
        var ints = context.INT();
        var beats = int.Parse(ints[0].GetText());
        var unit = int.Parse(ints[1].GetText());
        TimeSignature = new TimeSignature(beats, unit);
        return [];
    }

    public override NoteEvent[] VisitVoice(MusicNotationParser.VoiceContext context)
    {
        foreach (var element in context.element())
        {
            VisitElement(element);
        }
        return [];
    }

    public override NoteEvent[] VisitElement(MusicNotationParser.ElementContext context)
    {
        if (context.directive() != null)
            VisitDirective(context.directive());
        else if (context.polyphonicBlock() != null)
            VisitPolyphonicBlock(context.polyphonicBlock());
        else if (context.note() != null)
            VisitNote(context.note());
        else if (context.chord() != null)
            VisitChord(context.chord());
        else if (context.rest() != null)
            VisitRest(context.rest());

        return [];
    }

    public override NoteEvent[] VisitNote(MusicNotationParser.NoteContext context)
    {
        var pitch = ParsePitch(context.pitch());
        var duration = context.duration() != null ? ParseDuration(context.duration()) : Rational.Quarter; // Default to quarter note
        var hasTie = context.tie() != null;

        // Create base note
        var baseNote = new NoteEvent(pitch, _currentTime, duration);

        // Check if this note is tied from a previous note
        if (_pendingTies.Contains(pitch))
        {
            // Find the previous note with this pitch and extend it
            for (int i = _notes.Count - 1; i >= 0; i--)
            {
                if (_notes[i].Pitch == pitch)
                {
                    var prevNote = _notes[i];
                    _notes[i] = new NoteEvent(pitch, prevNote.Offset, prevNote.Duration + duration, prevNote.Velocity);
                    _pendingTies.Remove(pitch);

                    // If this note also has a tie, keep it pending
                    if (hasTie)
                        _pendingTies.Add(pitch);

                    _currentTime += duration;
                    return [];
                }
            }
            // Note not found (shouldn't happen), remove from pending
            _pendingTies.Remove(pitch);
        }

        // Apply ornament if present
        if (context.ornament() != null)
        {
            var ornamentedNotes = ParseOrnament(context.ornament(), baseNote);
            _notes.AddRange(ornamentedNotes);
        }
        else
        {
            _notes.Add(baseNote);
        }

        _currentTime += duration;

        // If this note has a tie, mark it as pending for the next note
        if (hasTie)
            _pendingTies.Add(pitch);

        return [];
    }

    public override NoteEvent[] VisitChord(MusicNotationParser.ChordContext context)
    {
        // Chord duration is optional - use it as default if no individual durations
        Rational? chordDuration = context.duration() != null
            ? ParseDuration(context.duration())
            : null;

        var notesInChord = context.noteInChord();
        var maxDuration = Rational.Zero;

        foreach (var noteCtx in notesInChord)
        {
            var pitch = ParsePitch(noteCtx.pitch());

            // Individual duration takes precedence, then chord duration, then error
            Rational duration;
            if (noteCtx.duration() != null)
            {
                duration = ParseDuration(noteCtx.duration());
            }
            else if (chordDuration.HasValue)
            {
                duration = chordDuration.Value;
            }
            else
            {
                throw new ArgumentException(
                    $"Note {noteCtx.pitch().GetText()} in chord has no duration and chord has no default duration");
            }

            _notes.Add(new NoteEvent(pitch, _currentTime, duration));

            if (duration > maxDuration)
                maxDuration = duration;
        }

        // Advance time by the maximum note duration in the chord
        _currentTime += maxDuration;
        return [];
    }

    public override NoteEvent[] VisitRest(MusicNotationParser.RestContext context)
    {
        var duration = context.duration() != null ? ParseDuration(context.duration()) : Rational.Quarter; // Default to quarter rest
        _notes.Add(new NoteEvent(MusicNotation.REST_PITCH, _currentTime, duration));
        _currentTime += duration;
        return [];
    }

    private int ParsePitch(MusicNotationParser.PitchContext context)
    {
        var pitchName = context.PITCH_NAME().GetText().ToUpperInvariant();
        var pitchClass = pitchName[0] switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentException($"Invalid pitch name: {pitchName}")
        };

        // Handle accidentals
        var accidentalCtx = context.accidental();
        if (accidentalCtx != null)
        {
            if (accidentalCtx.SHARP() != null)
                pitchClass = (pitchClass + 1) % 12;
            else if (accidentalCtx.FLAT() != null)
                pitchClass = (pitchClass + 11) % 12;
        }

        // Get octave
        var octave = int.Parse(context.octave().INT().GetText());

        // Calculate MIDI pitch: C4 = 60
        return (octave + 1) * 12 + pitchClass;
    }

    private Rational ParseDuration(MusicNotationParser.DurationContext context)
    {
        var valueCtx = context.durationValue();
        var isDotted = context.DOT() != null;

        Rational baseDuration;

        if (valueCtx.INT() != null)
        {
            var value = int.Parse(valueCtx.INT().GetText());
            baseDuration = new Rational(1, value);
        }
        else if (valueCtx.DURATION_LETTER() != null)
        {
            var letter = valueCtx.DURATION_LETTER().GetText().ToUpperInvariant();
            baseDuration = letter switch
            {
                "W" => new Rational(1, 1),
                "H" => new Rational(1, 2),
                "Q" => new Rational(1, 4),
                "E" => new Rational(1, 8),
                "S" => new Rational(1, 16),
                "T" => new Rational(1, 32),
                _ => throw new ArgumentException($"Invalid duration letter: {letter}")
            };
        }
        else
        {
            throw new ArgumentException("Invalid duration");
        }

        // Dotted note: add half of the base duration
        if (isDotted)
            baseDuration = baseDuration + baseDuration / 2;

        return baseDuration;
    }
    private NoteEvent[] ParseOrnament(MusicNotationParser.OrnamentContext context, NoteEvent baseNote)
    {
        var ornamentType = context.ornamentType();
        var paramsCtx = context.ornamentParams();

        // Parse optional parameters
        var paramsList = new List<int>();
        if (paramsCtx != null)
        {
            foreach (var intToken in paramsCtx.INT())
            {
                paramsList.Add(int.Parse(intToken.GetText()));
            }
        }

        Ornament ornament;

        if (ornamentType.TRILL() != null)
        {
            // Syntax: {tr:interval:speed} or {tr:interval} or {tr}
            var interval = paramsList.Count > 0 ? paramsList[0] : 2;
            var speed = paramsList.Count > 1 ? paramsList[1] : 8;
            ornament = new Trill
            {
                BaseNote = baseNote,
                Interval = interval,
                Speed = speed
            };
        }
        else if (ornamentType.MORDENT() != null)
        {
            // Syntax: {m:type:interval:alternations} or {m:type} or {m}
            // type: 0=upper, 1=lower
            var type = paramsList.Count > 0 && paramsList[0] == 1 ? MordentType.Lower : MordentType.Upper;
            var interval = paramsList.Count > 1 ? paramsList[1] : 2;
            var alternations = paramsList.Count > 2 ? paramsList[2] : 1;
            ornament = new Mordent
            {
                BaseNote = baseNote,
                Type = type,
                Interval = interval,
                Alternations = alternations
            };
        }
        else if (ornamentType.TURN() != null)
        {
            // Syntax: {turn:type:upperInterval:lowerInterval} or {turn}
            // type: 0=normal, 1=inverted
            var type = paramsList.Count > 0 && paramsList[0] == 1 ? TurnType.Inverted : TurnType.Normal;
            var upperInterval = paramsList.Count > 1 ? paramsList[1] : 2;
            var lowerInterval = paramsList.Count > 2 ? paramsList[2] : 2;
            ornament = new Turn
            {
                BaseNote = baseNote,
                Type = type,
                UpperInterval = upperInterval,
                LowerInterval = lowerInterval
            };
        }
        else if (ornamentType.APPOGGIATURA() != null)
        {
            // Syntax: {app:type:interval} or {app}
            // type: 0=long, 1=short
            var type = paramsList.Count > 0 && paramsList[0] == 1 ? AppogiaturaType.Short : AppogiaturaType.Long;
            var interval = paramsList.Count > 1 ? paramsList[1] : 2;
            ornament = new Appoggiatura
            {
                BaseNote = baseNote,
                Type = type,
                Interval = interval
            };
        }
        else
        {
            throw new ArgumentException($"Unknown ornament type: {ornamentType.GetText()}");
        }

        return ornament.Expand();
    }

    // Polyphonic block parsing

    public override NoteEvent[] VisitPolyphonicBlock(MusicNotationParser.PolyphonicBlockContext context)
    {
        var blockStartTime = _currentTime;
        var maxVoiceDuration = Rational.Zero;

        var voices = context.voice();

        foreach (var voiceCtx in voices)
        {
            // Each voice starts at the same block start time
            var voiceStartTime = blockStartTime;
            _currentTime = voiceStartTime;

            // Parse this voice
            VisitVoice(voiceCtx);

            // Track the duration of this voice
            var voiceDuration = _currentTime - voiceStartTime;
            if (voiceDuration > maxVoiceDuration)
                maxVoiceDuration = voiceDuration;
        }

        // Advance timeline by the longest voice
        _currentTime = blockStartTime + maxVoiceDuration;

        return [];
    }

    // Directive parsing methods

    public override NoteEvent[] VisitDirective(MusicNotationParser.DirectiveContext context)
    {
        if (context.bpmDirective() != null)
            VisitBpmDirective(context.bpmDirective());
        else if (context.tempoDirective() != null)
            VisitTempoDirective(context.tempoDirective());
        else if (context.characterDirective() != null)
            VisitCharacterDirective(context.characterDirective());
        else if (context.sectionDirective() != null)
            VisitSectionDirective(context.sectionDirective());
        else if (context.partDirective() != null)
            VisitPartDirective(context.partDirective());
        else if (context.dynamicsDirective() != null)
            VisitDynamicsDirective(context.dynamicsDirective());

        return [];
    }

    public override NoteEvent[] VisitBpmDirective(MusicNotationParser.BpmDirectiveContext context)
    {
        var bpm = int.Parse(context.INT(0).GetText());
        int? targetBpm = null;
        Rational? rampDuration = null;

        // Check for ramp: @bpm 120 -> 140 /2
        if (context.ARROW() != null)
        {
            targetBpm = int.Parse(context.INT(1).GetText());
            if (context.duration() != null)
            {
                rampDuration = ParseDuration(context.duration());
            }
        }

        _directives.Add(new TempoBpmDirective
        {
            Time = _currentTime,
            Bpm = bpm,
            TargetBpm = targetBpm,
            RampDuration = rampDuration
        });

        return [];
    }

    public override NoteEvent[] VisitTempoDirective(MusicNotationParser.TempoDirectiveContext context)
    {
        var value = ParseDirectiveValue(context.directiveValue());
        _directives.Add(new TempoCharacterDirective
        {
            Time = _currentTime,
            Character = value
        });
        return [];
    }

    public override NoteEvent[] VisitCharacterDirective(MusicNotationParser.CharacterDirectiveContext context)
    {
        // For now, treat @character same as @tempo (musical character/expression)
        var value = ParseDirectiveValue(context.directiveValue());
        _directives.Add(new TempoCharacterDirective
        {
            Time = _currentTime,
            Character = value
        });
        return [];
    }

    public override NoteEvent[] VisitSectionDirective(MusicNotationParser.SectionDirectiveContext context)
    {
        var label = ParseDirectiveValue(context.directiveValue());
        _directives.Add(new SectionDirective
        {
            Time = _currentTime,
            Label = label
        });
        return [];
    }

    public override NoteEvent[] VisitPartDirective(MusicNotationParser.PartDirectiveContext context)
    {
        var name = ParseDirectiveValue(context.directiveValue());
        _directives.Add(new PartDirective
        {
            Time = _currentTime,
            Name = name
        });
        return [];
    }

    public override NoteEvent[] VisitDynamicsDirective(MusicNotationParser.DynamicsDirectiveContext context)
    {
        if (context.DYNAMICS() != null)
        {
            // Static dynamics: @dynamics pp
            var level = ParseDynamicsValue(context.dynamicsValue());
            _directives.Add(new DynamicsDirective
            {
                Time = _currentTime,
                Type = DynamicsType.Static,
                StartLevel = level
            });
        }
        else if (context.CRESC() != null)
        {
            // Crescendo: @cresc or @cresc to ff
            string? targetLevel = null;
            if (context.TO() != null && context.dynamicsValue() != null)
            {
                targetLevel = ParseDynamicsValue(context.dynamicsValue());
            }
            _directives.Add(new DynamicsDirective
            {
                Time = _currentTime,
                Type = DynamicsType.Crescendo,
                TargetLevel = targetLevel
            });
        }
        else if (context.DIM() != null)
        {
            // Diminuendo: @dim or @dim to p
            string? targetLevel = null;
            if (context.TO() != null && context.dynamicsValue() != null)
            {
                targetLevel = ParseDynamicsValue(context.dynamicsValue());
            }
            _directives.Add(new DynamicsDirective
            {
                Time = _currentTime,
                Type = DynamicsType.Diminuendo,
                TargetLevel = targetLevel
            });
        }
        return [];
    }

    private string ParseDirectiveValue(MusicNotationParser.DirectiveValueContext context)
    {
        if (context.STRING() != null)
        {
            // Remove surrounding quotes
            var text = context.STRING().GetText();
            return text.Substring(1, text.Length - 2);
        }
        else if (context.IDENT() != null)
        {
            return context.IDENT().GetText();
        }
        throw new ArgumentException("Invalid directive value");
    }

    private string ParseDynamicsValue(MusicNotationParser.DynamicsValueContext context)
    {
        if (context.DYNAMICS_LEVEL() != null)
        {
            return context.DYNAMICS_LEVEL().GetText().ToLower();
        }
        else if (context.IDENT() != null)
        {
            return context.IDENT().GetText();
        }
        throw new ArgumentException("Invalid dynamics value");
    }
}
