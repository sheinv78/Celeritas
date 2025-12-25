namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Utility class for applying ornaments to note sequences
/// </summary>
public static class OrnamentApplier
{
    /// <summary>
    /// Apply ornaments to a sequence of notes
    /// </summary>
    public static NoteEvent[] ApplyOrnaments(NoteEvent[] notes, Ornament[] ornaments)
    {
        if (ornaments.Length == 0)
            return notes;

        var result = new List<NoteEvent>();
        var ornamentDict = ornaments.ToDictionary(o => o.BaseNote.Offset);

        foreach (var note in notes)
        {
            if (ornamentDict.TryGetValue(note.Offset, out var ornament))
            {
                // Expand ornament and add resulting notes
                var expanded = ornament.Expand();
                result.AddRange(expanded);
            }
            else
            {
                // Add note as-is
                result.Add(note);
            }
        }

        return result.ToArray();
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
    public static Mordent CreateMordent(NoteEvent baseNote, MordentType type = MordentType.Upper,
        int interval = 2, int alternations = 1)
    {
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
    public static Turn CreateTurn(NoteEvent baseNote, TurnType type = TurnType.Normal,
        int upperInterval = 2, int lowerInterval = 2)
    {
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
    public static Appoggiatura CreateAppoggiatura(NoteEvent baseNote,
        AppogiaturaType type = AppogiaturaType.Long, int interval = 2)
    {
        return new Appoggiatura
        {
            BaseNote = baseNote,
            Type = type,
            Interval = interval
        };
    }
}
