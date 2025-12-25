namespace Celeritas.Core.FiguredBass;

/// <summary>
/// Realizes figured bass notation into actual chord voicings
/// </summary>
public class FiguredBassRealizer
{
    private readonly FiguredBassOptions _options;

    public FiguredBassRealizer(FiguredBassOptions? options = null)
    {
        _options = options ?? new FiguredBassOptions();
    }

    /// <summary>
    /// Realize a sequence of figured bass symbols into chord voicings
    /// </summary>
    public NoteEvent[] Realize(FiguredBassSymbol[] symbols)
    {
        var result = new List<NoteEvent>();

        foreach (var symbol in symbols)
        {
            var voicing = RealizeSymbol(symbol);
            result.AddRange(voicing);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Realize a single figured bass symbol
    /// </summary>
    public NoteEvent[] RealizeSymbol(FiguredBassSymbol symbol)
    {
        var intervals = NormalizeFigures(symbol.Figures);
        var notes = new List<NoteEvent>();

        // Bass note
        notes.Add(new NoteEvent(symbol.BassPitch, symbol.Time, symbol.Duration, 0.8f));

        // Realize upper voices based on intervals
        foreach (var interval in intervals)
        {
            var pitch = CalculatePitch(symbol.BassPitch, interval, symbol.Accidentals);

            // Adjust pitch to appropriate octave range
            pitch = AdjustToRange(pitch, _options.MinPitch, _options.MaxPitch);

            notes.Add(new NoteEvent(pitch, symbol.Time, symbol.Duration, 0.7f));
        }

        return notes.ToArray();
    }

    /// <summary>
    /// Normalize figured bass figures to standard intervals
    /// </summary>
    private int[] NormalizeFigures(int[] figures)
    {
        if (figures.Length == 0)
        {
            // Empty = root position triad (5/3)
            return [3, 5];
        }

        // Common figured bass abbreviations
        return figures switch
        {
            [6] => [3, 6],           // 6 = first inversion (6/3)
            [6, 4] => [4, 6],        // 6/4 = second inversion
            [7] => [3, 5, 7],        // 7 = dominant seventh
            [6, 5] => [3, 5, 6],     // 6/5 = first inversion seventh
            [4, 3] => [3, 4, 6],     // 4/3 = second inversion seventh
            [4, 2] or [2] => [2, 4, 6], // 4/2 or 2 = third inversion seventh
            [9] => [3, 5, 9],        // 9 = ninth chord
            [5, 3] => [3, 5],        // 5/3 = explicit root position
            _ => figures             // Use as-is
        };
    }

    /// <summary>
    /// Calculate pitch from bass note and interval
    /// </summary>
    private int CalculatePitch(int bassPitch, int interval, Dictionary<int, char>? accidentals)
    {
        // Convert figured bass interval to semitones
        var semitones = interval switch
        {
            2 => 2,   // Major second
            3 => 4,   // Major third
            4 => 5,   // Perfect fourth
            5 => 7,   // Perfect fifth
            6 => 9,   // Major sixth
            7 => 10,  // Minor seventh (dominant)
            8 => 12,  // Octave
            9 => 14,  // Major ninth
            _ => 0
        };

        var pitch = bassPitch + semitones;

        // Apply accidentals if specified
        if (accidentals != null && accidentals.TryGetValue(interval, out var accidental))
        {
            pitch += accidental switch
            {
                '#' => 1,
                'b' => -1,
                'n' => 0,
                _ => 0
            };
        }

        return pitch;
    }

    /// <summary>
    /// Adjust pitch to be within specified range
    /// </summary>
    private int AdjustToRange(int pitch, int minPitch, int maxPitch)
    {
        while (pitch < minPitch)
            pitch += 12;
        while (pitch > maxPitch)
            pitch -= 12;
        return pitch;
    }

    /// <summary>
    /// Parse figured bass notation string (e.g., "6", "7", "6/5", "4/3")
    /// </summary>
    public static int[] ParseFigures(string figuresStr)
    {
        if (string.IsNullOrWhiteSpace(figuresStr))
            return [];

        var parts = figuresStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => int.Parse(p.Trim())).ToArray();
    }

    /// <summary>
    /// Parse accidentals from figured bass string (e.g., "#3", "b7")
    /// </summary>
    public static Dictionary<int, char> ParseAccidentals(string figuresStr)
    {
        var accidentals = new Dictionary<int, char>();

        foreach (var c in figuresStr)
        {
            if (c == '#' || c == 'b' || c == 'n')
            {
                // Look for following digit
                var idx = figuresStr.IndexOf(c);
                if (idx + 1 < figuresStr.Length && char.IsDigit(figuresStr[idx + 1]))
                {
                    var interval = int.Parse(figuresStr[idx + 1].ToString());
                    accidentals[interval] = c;
                }
            }
        }

        return accidentals;
    }
}

/// <summary>
/// Options for figured bass realization
/// </summary>
public class FiguredBassOptions
{
    /// <summary>
    /// Minimum pitch for upper voices (default: C3)
    /// </summary>
    public int MinPitch { get; init; } = 48;

    /// <summary>
    /// Maximum pitch for upper voices (default: C6)
    /// </summary>
    public int MaxPitch { get; init; } = 84;

    /// <summary>
    /// Voice leading style
    /// </summary>
    public VoiceLeadingStyle Style { get; init; } = VoiceLeadingStyle.Smooth;
}

/// <summary>
/// Voice leading style for figured bass realization
/// </summary>
public enum VoiceLeadingStyle
{
    /// <summary>
    /// Smooth voice leading (minimal movement)
    /// </summary>
    Smooth,

    /// <summary>
    /// Strict style (common practice rules)
    /// </summary>
    Strict,

    /// <summary>
    /// Free style (more melodic upper voices)
    /// </summary>
    Free
}
