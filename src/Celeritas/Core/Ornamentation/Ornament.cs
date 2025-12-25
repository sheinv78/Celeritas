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
}
