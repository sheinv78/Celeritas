using System.Buffers;

namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Mordent ornament - brief alternation with upper or lower neighbor
/// </summary>
public class Mordent : Ornament
{
    /// <summary>
    /// Type of mordent
    /// </summary>
    public MordentType Type { get; init; } = MordentType.Upper;

    /// <summary>
    /// Interval in semitones (default: 2 for whole tone, 1 for half tone)
    /// </summary>
    public int Interval { get; init; } = 2;

    /// <summary>
    /// Number of alternations (1 = single mordent, 2 = double mordent)
    /// </summary>
    public int Alternations { get; init; } = 1;

    public override NoteEvent[] Expand()
    {
        var noteCount = 2 * Alternations + 1; // Main + alternations
        var noteDuration = BaseNote.Duration / noteCount;

        var neighborPitch = Type == MordentType.Upper
            ? BaseNote.Pitch + Interval
            : BaseNote.Pitch - Interval;

        var currentTime = BaseNote.Offset;

        // Use stack allocation for small counts
        Span<NoteEvent> notes = noteCount <= 16 
            ? stackalloc NoteEvent[noteCount]
            : new NoteEvent[noteCount];

        // Pattern: Main - Neighbor - Main (- Neighbor - Main for double)
        for (int i = 0; i < noteCount; i++)
        {
            var pitch = (i % 2 == 0) ? BaseNote.Pitch : neighborPitch;
            notes[i] = new NoteEvent(pitch, currentTime, noteDuration, BaseNote.Velocity);
            currentTime += noteDuration;
        }

        return notes.ToArray();
    }
}

/// <summary>
/// Type of mordent
/// </summary>
public enum MordentType
{
    /// <summary>
    /// Upper mordent (with note above)
    /// </summary>
    Upper,

    /// <summary>
    /// Lower mordent / inverted mordent (with note below)
    /// </summary>
    Lower
}
