using System.Buffers;

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Trill ornament - rapid alternation between the main note and the note above
/// </summary>
public sealed class Trill : Ornament
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

    /// <summary>Expands into a rapid alternation of main and upper notes, optionally closing with a turn.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Speed"/> is not positive.</exception>
    public override NoteEvent[] Expand()
    {
        if (Speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(Speed), Speed, "Trill speed must be positive");

        var endWithTurn = EndWithTurn || HasTurnEnding;
        var noteDuration = new Rational(1, Speed * 4); // Duration per trill note
        var upperNote = BaseNote.Pitch + Interval;
        var lowerNote = BaseNote.Pitch - (Interval == 2 ? 2 : 1); // For turn ending

        var currentTime = BaseNote.Offset;
        var endTime = BaseNote.Offset + BaseNote.Duration;

        // Calculate how many notes fit
        var totalNotes = (int)((BaseNote.Duration.Numerator * Speed * 4) / BaseNote.Duration.Denominator);

        // The base note is shorter than a single trill unit — expanding would silently
        // delete it; keep the plain note instead.
        if (totalNotes == 0)
            return [BaseNote];

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

            // Stretch the final note to the exact end of the base note so the expansion
            // always sums to BaseNote.Duration (no gap before the next melody note).
            if (count > 0)
            {
                var last = buffer[count - 1];
                var lastEnd = last.Offset + last.Duration;
                if (lastEnd != endTime)
                {
                    buffer[count - 1] = new NoteEvent(last.Pitch, last.Offset, endTime - last.Offset, last.Velocity);
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
