namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Appoggiatura ornament - accented non-harmonic note that resolves by step
/// </summary>
public sealed class Appoggiatura : Ornament
{
    /// <summary>
    /// Type of appoggiatura
    /// </summary>
    public AppogiaturaType Type { get; init; } = AppogiaturaType.Long;

    /// <summary>
    /// Interval from appoggiatura to main note (positive = above, negative = below)
    /// </summary>
    public int Interval { get; init; } = 2;

    /// <summary>
    /// Direction of approach: 1 = from above, -1 = from below, 0 = use sign of Interval.
    /// Used by some examples.
    /// </summary>
    public int Direction { get; init; } = 0;

    public override NoteEvent[] Expand()
    {
        var signedInterval = Direction == 0
            ? Interval
            : Math.Sign(Direction) * Math.Abs(Interval);

        var appogiaturaPitch = BaseNote.Pitch + signedInterval;

        // Appoggiatura always produces 2 notes - use stack allocation
        Span<NoteEvent> notes = stackalloc NoteEvent[2];

        if (Type == AppogiaturaType.Long)
        {
            // Long appoggiatura takes half the duration
            var appogiaturaDuration = BaseNote.Duration / 2;
            var mainDuration = BaseNote.Duration - appogiaturaDuration;

            notes[0] = new NoteEvent(appogiaturaPitch, BaseNote.Offset, appogiaturaDuration, BaseNote.Velocity);
            notes[1] = new NoteEvent(BaseNote.Pitch, BaseNote.Offset + appogiaturaDuration, mainDuration, BaseNote.Velocity);
        }
        else
        {
            // Short appoggiatura (acciaccatura) - very brief, but never longer than half
            // the base note (a base note <= 1/32 would otherwise get a zero/negative main note)
            var appogiaturaDuration = new Rational(1, 32); // 32nd note
            var halfBase = BaseNote.Duration / 2;
            if (appogiaturaDuration > halfBase)
                appogiaturaDuration = halfBase;

            var mainDuration = BaseNote.Duration - appogiaturaDuration;

            notes[0] = new NoteEvent(appogiaturaPitch, BaseNote.Offset, appogiaturaDuration, BaseNote.Velocity);
            notes[1] = new NoteEvent(BaseNote.Pitch, BaseNote.Offset + appogiaturaDuration, mainDuration, BaseNote.Velocity);
        }

        return notes.ToArray();
    }
}

/// <summary>
/// Type of appoggiatura
/// </summary>
public enum AppogiaturaType
{
    /// <summary>
    /// Long appoggiatura (takes significant duration)
    /// </summary>
    Long,

    /// <summary>
    /// Short appoggiatura / acciaccatura (very brief)
    /// </summary>
    Short
}
