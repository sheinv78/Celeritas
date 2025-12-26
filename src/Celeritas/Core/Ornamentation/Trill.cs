using System.Buffers;

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Trill ornament - rapid alternation between the main note and the note above
/// </summary>
public class Trill : Ornament
{
    /// <summary>
    /// Interval in semitones (default: 2 for whole tone, 1 for half tone)
    /// </summary>
    public int Interval { get; init; } = 2;

    /// <summary>
    /// Speed of the trill (notes per quarter note)
    /// </summary>
    public int Speed { get; init; } = 8;

    /// <summary>
    /// Whether to start with the upper note
    /// </summary>
    public bool StartWithUpper { get; init; } = false;

    /// <summary>
    /// Whether to end with a turn (lower neighbor + main note)
    /// </summary>
    public bool EndWithTurn { get; init; } = false;

    /// <summary>
    /// Backward/compat alias used by examples.
    /// </summary>
    public bool HasTurnEnding { get; init; } = false;

    public override NoteEvent[] Expand()
    {
        var endWithTurn = EndWithTurn || HasTurnEnding;
        var noteDuration = new Rational(1, Speed * 4); // Duration per trill note
        var upperNote = BaseNote.Pitch + Interval;
        var lowerNote = BaseNote.Pitch - (Interval == 2 ? 2 : 1); // For turn ending

        var currentTime = BaseNote.Offset;
        var endTime = BaseNote.Offset + BaseNote.Duration;

        // Calculate how many notes fit
        var totalNotes = (int)((BaseNote.Duration.Numerator * Speed * 4) / BaseNote.Duration.Denominator);
        var maxNotes = totalNotes + (endWithTurn ? 2 : 0);

        // Rent buffer from pool
        var buffer = ArrayPool<NoteEvent>.Shared.Rent(maxNotes);
        var count = 0;

        try
        {
            // Reserve space for turn if needed
            if (endWithTurn && totalNotes >= 3)
            {
                totalNotes -= 2; // Last two notes for the turn
            }

            bool useUpper = StartWithUpper;

            // Main trill
            for (int i = 0; i < totalNotes && currentTime < endTime; i++)
            {
                var pitch = useUpper ? upperNote : BaseNote.Pitch;
                buffer[count++] = new NoteEvent(pitch, currentTime, noteDuration, BaseNote.Velocity);

                currentTime += noteDuration;
                useUpper = !useUpper;
            }

            // Add turn ending if requested
            if (endWithTurn && currentTime < endTime)
            {
                // Lower neighbor
                buffer[count++] = new NoteEvent(lowerNote, currentTime, noteDuration, BaseNote.Velocity);
                currentTime += noteDuration;

                // Main note
                if (currentTime < endTime)
                {
                    buffer[count++] = new NoteEvent(BaseNote.Pitch, currentTime, endTime - currentTime, BaseNote.Velocity);
                }
            }

            // Copy to result array
            var result = new NoteEvent[count];
            Array.Copy(buffer, result, count);
            return result;
        }
        finally
        {
            ArrayPool<NoteEvent>.Shared.Return(buffer);
        }
    }
}
