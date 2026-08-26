namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Utility class for applying ornaments to note sequences
/// </summary>
public static class OrnamentApplier
{
    /// <summary>
    /// Apply ornaments to a melody by note index (as used in examples).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="ornamentMap"/> is <see langword="null"/>.</exception>
    public static NoteEvent[] Apply(ReadOnlySpan<NoteEvent> melody, IReadOnlyDictionary<int, Ornament> ornamentMap)
    {
        ArgumentNullException.ThrowIfNull(ornamentMap);

        if (melody.Length == 0 || ornamentMap.Count == 0)
            return melody.ToArray();

        var result = new List<NoteEvent>(melody.Length);

        for (var i = 0; i < melody.Length; i++)
        {
            if (ornamentMap.TryGetValue(i, out var ornament))
            {
                // Ornaments have an init-only BaseNote. Rebase by cloning built-in ornaments.
                // For custom Ornament subclasses, we fall back to using the provided instance.
                result.AddRange(RebaseOrnament(ornament, melody[i]).Expand());
            }
            else
            {
                result.Add(melody[i]);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Apply ornaments to a melody by note index (as used in examples).
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="melody"/> or <paramref name="ornamentMap"/> is <see langword="null"/>.
    /// </exception>
    public static NoteEvent[] Apply(NoteEvent[] melody, IReadOnlyDictionary<int, Ornament> ornamentMap)
    {
        // Without this, AsSpan() maps null to an empty span and the caller gets back an
        // empty array as though the melody genuinely had no notes.
        ArgumentNullException.ThrowIfNull(melody);
        return Apply(melody.AsSpan(), ornamentMap);
    }

    private static Ornament RebaseOrnament(Ornament ornament, NoteEvent baseNote)
    {
        return ornament switch
        {
            Trill trill => new Trill
            {
                BaseNote = baseNote,
                Interval = trill.Interval,
                Speed = trill.Speed,
                StartWithUpper = trill.StartWithUpper,
                EndWithTurn = trill.EndWithTurn,
                HasTurnEnding = trill.HasTurnEnding
            },
            Mordent mordent => new Mordent
            {
                BaseNote = baseNote,
                Type = mordent.Type,
                Interval = mordent.Interval,
                Alternations = mordent.Alternations
            },
            Turn turn => new Turn
            {
                BaseNote = baseNote,
                Type = turn.Type,
                UpperInterval = turn.UpperInterval,
                LowerInterval = turn.LowerInterval,
                Anticipation = turn.Anticipation
            },
            Appoggiatura appoggiatura => new Appoggiatura
            {
                BaseNote = baseNote,
                Type = appoggiatura.Type,
                Interval = appoggiatura.Interval,
                Direction = appoggiatura.Direction
            },
            GraceNote grace => new GraceNote
            {
                BaseNote = baseNote,
                Type = grace.Type,
                Intervals = grace.Intervals,
                // The raw ratio, so an unset one stays unset: see GraceNote.ExplicitDurationRatio.
                ExplicitDurationRatio = grace.ExplicitDurationRatio
            },
            Glissando gliss => new Glissando
            {
                BaseNote = baseNote,
                TargetPitch = gliss.TargetPitch,
                IsAbsolute = gliss.IsAbsolute,
                Steps = gliss.Steps,
                Chromatic = gliss.Chromatic
            },
            Articulation artic => new Articulation
            {
                BaseNote = baseNote,
                Type = artic.Type,
                DurationMultiplier = artic.DurationMultiplier,
                VelocityMultiplier = artic.VelocityMultiplier
            },
            // Custom subclasses cannot be rebased generically; the caller's BaseNote is used as-is.
            _ => ornament
        };
    }

    /// <summary>
    /// Apply ornaments to a sequence of notes. An ornament is matched to a note by
    /// (Offset, Pitch) of its BaseNote, so multiple ornaments may share an offset
    /// (e.g. on different notes of a chord). Ornaments that match no note are ignored.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="notes"/> or <paramref name="ornaments"/> is <see langword="null"/>.
    /// </exception>
    public static NoteEvent[] ApplyOrnaments(NoteEvent[] notes, Ornament[] ornaments)
    {
        // With nothing to apply, the early return below hands `notes` straight back, so a null
        // melody came out as a null result rather than as a rejected argument.
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(ornaments);

        if (ornaments.Length == 0)
            return notes;

        var result = new List<NoteEvent>();
        var pending = new Dictionary<(Rational Offset, int Pitch), Queue<Ornament>>();
        foreach (var ornament in ornaments)
        {
            var key = (ornament.BaseNote.Offset, ornament.BaseNote.Pitch);
            if (!pending.TryGetValue(key, out var queue))
            {
                queue = new Queue<Ornament>();
                pending[key] = queue;
            }
            queue.Enqueue(ornament);
        }

        foreach (var note in notes)
        {
            if (pending.TryGetValue((note.Offset, note.Pitch), out var queue) && queue.Count > 0)
            {
                // Expand ornament and add resulting notes
                result.AddRange(queue.Dequeue().Expand());
            }
            else
            {
                // Add note as-is
                result.Add(note);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Create a trill ornament
    /// </summary>
    public static Trill CreateTrill(NoteEvent baseNote, int interval = 2, int speed = 8,
        bool startWithUpper = false, bool endWithTurn = false)
    {
        return new Trill
        {
            BaseNote = baseNote,
            Interval = interval,
            Speed = speed,
            StartWithUpper = startWithUpper,
            EndWithTurn = endWithTurn
        };
    }

    /// <summary>
    /// Create a mordent ornament
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="MordentType"/> value.</exception>
    public static Mordent CreateMordent(NoteEvent baseNote, MordentType type = MordentType.Upper,
        int interval = 2, int alternations = 1)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined MordentType value.");

        return new Mordent
        {
            BaseNote = baseNote,
            Type = type,
            Interval = interval,
            Alternations = alternations
        };
    }

    /// <summary>
    /// Create a turn ornament
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="TurnType"/> value.</exception>
    public static Turn CreateTurn(NoteEvent baseNote, TurnType type = TurnType.Normal,
        int upperInterval = 2, int lowerInterval = 2)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined TurnType value.");

        return new Turn
        {
            BaseNote = baseNote,
            Type = type,
            UpperInterval = upperInterval,
            LowerInterval = lowerInterval
        };
    }

    /// <summary>
    /// Create an appoggiatura ornament
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a defined <see cref="AppogiaturaType"/> value.</exception>
    public static Appoggiatura CreateAppoggiatura(NoteEvent baseNote,
        AppogiaturaType type = AppogiaturaType.Long, int interval = 2)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a defined AppogiaturaType value.");

        return new Appoggiatura
        {
            BaseNote = baseNote,
            Type = type,
            Interval = interval
        };
    }
}
