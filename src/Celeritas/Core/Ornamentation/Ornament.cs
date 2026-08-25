namespace Celeritas.Core.Ornamentation;

/// <summary>
/// Base class for all ornaments (trills, mordents, turns, etc.)
/// </summary>
public abstract class Ornament
{
    /// <summary>
    /// The base note to which the ornament is applied
    /// </summary>
    public required NoteEvent BaseNote { get; init; }

    /// <summary>
    /// Expand the ornament into a sequence of note events
    /// </summary>
    public abstract NoteEvent[] Expand();

    /// <summary>
    /// Holds an ornamental pitch on the keyboard.
    /// </summary>
    /// <remarks>
    /// A neighbour or passing pitch can fall off either end: a lower mordent on MIDI 0 reached
    /// -1, an upper one on 127 reached 128. Those are not pitches — <c>MusicNotation.ToNotation</c>
    /// refuses them, MusicXML export writes an impossible octave, and the MIDI writer silently
    /// clamps them anyway. Clamping here keeps the ornament audible (it narrows at the very edge
    /// of the keyboard, where a performer has nowhere to go either) instead of handing the rest
    /// of the library a pitch it cannot represent.
    /// </remarks>
    private protected static int Playable(int pitch) => Math.Clamp(pitch, 0, 127);
}
