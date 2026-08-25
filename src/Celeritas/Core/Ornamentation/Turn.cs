namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Turn ornament - upper neighbor, main note, lower neighbor, main note
/// </summary>
public sealed class Turn : Ornament
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
    /// Whether the turn happens before the beat (anticipation).
    /// When <see langword="true"/>, the three ornamental notes are compressed into the
    /// first quarter of the base duration (each taking 1/12 of it) so the closing
    /// principal note enters early and holds the remaining 3/4; when
    /// <see langword="false"/>, all four notes split the duration equally.
    /// Either way the expansion sums exactly to the base note's duration.
    /// </summary>
    public bool Anticipation { get; init; } = false;

    /// <summary>Expands into four notes tracing the turn around the main note.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Type"/> is not a defined <see cref="TurnType"/> value.</exception>
    public override NoteEvent[] Expand()
    {
        if (!Enum.IsDefined(Type))
            throw new ArgumentOutOfRangeException(nameof(Type), Type, "Not a defined TurnType value.");

        var upperPitch = Playable(BaseNote.Pitch + UpperInterval);
        var lowerPitch = Playable(BaseNote.Pitch - LowerInterval);

        // Anticipation compresses the ornamental notes at the start; on-beat turns use
        // four equal notes. The final principal note always absorbs the exact remainder.
        var ornamentDuration = Anticipation ? BaseNote.Duration / 12 : BaseNote.Duration / 4;
        var principalDuration = BaseNote.Duration - (ornamentDuration * 3);

        // Normal: Upper - Main - Lower - Main; Inverted: Lower - Main - Upper - Main
        var (firstPitch, thirdPitch) = Type == TurnType.Normal
            ? (upperPitch, lowerPitch)
            : (lowerPitch, upperPitch);

        // Turn always produces 4 notes - use stack allocation
        Span<NoteEvent> notes = stackalloc NoteEvent[4];
        var currentTime = BaseNote.Offset;

        notes[0] = new NoteEvent(firstPitch, currentTime, ornamentDuration, BaseNote.Velocity);
        currentTime += ornamentDuration;

        notes[1] = new NoteEvent(BaseNote.Pitch, currentTime, ornamentDuration, BaseNote.Velocity);
        currentTime += ornamentDuration;

        notes[2] = new NoteEvent(thirdPitch, currentTime, ornamentDuration, BaseNote.Velocity);
        currentTime += ornamentDuration;

        notes[3] = new NoteEvent(BaseNote.Pitch, currentTime, principalDuration, BaseNote.Velocity);

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
