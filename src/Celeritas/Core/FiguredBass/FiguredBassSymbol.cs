namespace Celeritas.Core.FiguredBass;

/// <summary>
/// Represents a figured bass symbol and its realization
/// </summary>
public class FiguredBassSymbol
{
    /// <summary>
    /// Bass note pitch
    /// </summary>
    public required int BassPitch { get; init; }

    /// <summary>
    /// Figured bass intervals (empty = root position 5/3)
    /// </summary>
    public required int[] Figures { get; init; }

    /// <summary>
    /// Accidentals for specific intervals (# = sharp, b = flat, n = natural)
    /// Key: interval number, Value: accidental
    /// </summary>
    public Dictionary<int, char>? Accidentals { get; init; }

    /// <summary>
    /// Duration of the symbol
    /// </summary>
    public required Rational Duration { get; init; }

    /// <summary>
    /// Time position
    /// </summary>
    public required Rational Time { get; init; }
}
