using System.Buffers;

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Turn ornament - upper neighbor, main note, lower neighbor, main note
/// </summary>
public class Turn : Ornament
{
    /// <summary>
    /// Type of turn
    /// </summary>
    public TurnType Type { get; init; } = TurnType.Normal;

    /// <summary>
    /// Upper interval in semitones (default: 2 for whole tone)
    /// </summary>
    public int UpperInterval { get; init; } = 2;

    /// <summary>
    /// Lower interval in semitones (default: 2 for whole tone)
    /// </summary>
    public int LowerInterval { get; init; } = 2;

    /// <summary>
    /// Whether the turn happens before the beat (anticipation)
    /// </summary>
    public bool Anticipation { get; init; } = false;

    public override NoteEvent[] Expand()
    {
        var upperPitch = BaseNote.Pitch + UpperInterval;
        var lowerPitch = BaseNote.Pitch - LowerInterval;

        // Turn always produces 4 notes - use stack allocation
        Span<NoteEvent> notes = stackalloc NoteEvent[4];
        var noteDuration = BaseNote.Duration / 4;
        var currentTime = BaseNote.Offset;

        if (Type == TurnType.Normal)
        {
            // Upper - Main - Lower - Main
            notes[0] = new NoteEvent(upperPitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[1] = new NoteEvent(BaseNote.Pitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[2] = new NoteEvent(lowerPitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[3] = new NoteEvent(BaseNote.Pitch, currentTime, noteDuration, BaseNote.Velocity);
        }
        else if (Type == TurnType.Inverted)
        {
            // Lower - Main - Upper - Main
            notes[0] = new NoteEvent(lowerPitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[1] = new NoteEvent(BaseNote.Pitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[2] = new NoteEvent(upperPitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;

            notes[3] = new NoteEvent(BaseNote.Pitch, currentTime, noteDuration, BaseNote.Velocity);
        }

        return notes.ToArray();
    }
}

/// <summary>
/// Type of turn
/// </summary>
public enum TurnType
{
    /// <summary>
    /// Normal turn: upper - main - lower - main
    /// </summary>
    Normal,

    /// <summary>
    /// Inverted turn: lower - main - upper - main
    /// </summary>
    Inverted
}
