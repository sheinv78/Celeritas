namespace Celeritas.Core.FiguredBass;

/// <summary>
/// Realizes figured bass notation into actual chord voicings
/// </summary>
public sealed class FiguredBassRealizer
{
    private readonly FiguredBassOptions _options;

    private bool AllowVoiceCrossing => _options is FiguredBassRealizerOptions o ? o.AllowVoiceCrossing : false;
    private int? MaxVoiceMovement => _options is FiguredBassRealizerOptions o ? o.MaxVoiceMovement : null;

    public FiguredBassRealizer(FiguredBassOptions? options = null)
    {
        _options = options ?? new FiguredBassOptions();
    }

    public FiguredBassRealizer(FiguredBassRealizerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Realize a sequence of figured bass symbols into chord voicings
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="symbols"/> is <see langword="null"/>.</exception>
    public NoteEvent[] Realize(FiguredBassSymbol[] symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        var result = new List<NoteEvent>();

        int[]? previousUpperVoices = null;

        foreach (var symbol in symbols)
        {
            var voicing = RealizeSymbolWithVoiceLeading(symbol, previousUpperVoices);
            result.AddRange(voicing);

            previousUpperVoices = voicing.Length switch
            {
                // Cache upper voices for the next symbol.
                > 1 => voicing.Skip(1).Select(n => n.Pitch).ToArray(),
                _ => null
            };
        }

        return [.. result];
    }

    private NoteEvent[] RealizeSymbolWithVoiceLeading(FiguredBassSymbol symbol, int[]? previousUpperVoices)
    {
        // Free style: keep existing per-chord behavior.
        if (_options.Style == VoiceLeadingStyle.Free)
        {
            var realized = RealizeSymbol(symbol);
            return AllowVoiceCrossing switch
            {
                false when realized.Length > 2 => EnforceUpperVoiceOrdering(symbol, realized),
                _ => realized
            };
        }

        var intervals = NormalizeFigures(symbol.Figures);

        var notes = new List<NoteEvent>(1 + intervals.Length)
        {
            // Bass note
            new NoteEvent(symbol.BassPitch, symbol.Time, symbol.Duration)
        };

        // Generate target pitch-classes for upper voices.
        var targetPitchClasses = new int[intervals.Length];
        for (var i = 0; i < intervals.Length; i++)
        {
            var pitch = CalculatePitch(symbol.BassPitch, intervals[i], symbol.Accidentals);
            targetPitchClasses[i] = PitchMath.Fold(pitch);
        }

        // If voice count changes, reset voice leading.
        if (previousUpperVoices == null || previousUpperVoices.Length != targetPitchClasses.Length)
        {
            for (var i = 0; i < targetPitchClasses.Length; i++)
            {
                var basePitch = targetPitchClasses[i] + (12 * 4); // start around octave 4
                var realized = AdjustToRange(basePitch, _options.MinPitch, _options.MaxPitch);
                notes.Add(new NoteEvent(realized, symbol.Time, symbol.Duration, 0.7f));
            }

            var realizedNotes = notes.ToArray();
            return AllowVoiceCrossing switch
            {
                false when realizedNotes.Length > 2 => EnforceUpperVoiceOrdering(symbol, realizedNotes),
                _ => realizedNotes
            };
        }

        // Smooth/Strict: pick octave placements closest to previous voices.
        var newUpper = new int[targetPitchClasses.Length];
        for (var i = 0; i < targetPitchClasses.Length; i++)
        {
            newUpper[i] = ChooseClosestPitchInRange(
                targetPitchClasses[i],
                previousUpperVoices[i],
                _options.MinPitch,
                _options.MaxPitch,
                MaxVoiceMovement);
        }

        if (!AllowVoiceCrossing)
        {
            // Prevent crossing by nudging voices up by octaves as needed.
            for (var i = 1; i < newUpper.Length; i++)
            {
                while (newUpper[i] <= newUpper[i - 1] && newUpper[i] + 12 <= _options.MaxPitch)
                {
                    newUpper[i] += 12;
                }
            }

            // If still crossed (because we're at range limit), sort as a last resort.
            // This breaks voice identity but keeps a valid voicing.
            for (var i = 1; i < newUpper.Length; i++)
            {
                if (newUpper[i] <= newUpper[i - 1])
                {
                    Array.Sort(newUpper);
                    break;
                }
            }
        }

        for (var i = 0; i < newUpper.Length; i++)
        {
            notes.Add(new NoteEvent(newUpper[i], symbol.Time, symbol.Duration, 0.7f));
        }

        return notes.ToArray();
    }

    private static int ChooseClosestPitchInRange(
        int pitchClass,
        int previousPitch,
        int minPitch,
        int maxPitch,
        int? maxMovement)
    {
        // Enumerate octave candidates within range for the given pitch-class.
        var candidates = new List<int>();
        for (var p = pitchClass; p <= maxPitch; p += 12)
        {
            if (p >= minPitch)
            {
                candidates.Add(p);
            }
        }

        if (candidates.Count == 0)
        {
            // Fallback (should not happen for sane ranges)
            return Math.Clamp(pitchClass, minPitch, maxPitch);
        }

        // If constrained, prefer any candidate within movement first.
        if (maxMovement is { } limit)
        {
            var within = candidates
                .Select(p => (p, diff: Math.Abs(p - previousPitch)))
                .Where(x => x.diff <= limit)
                .OrderBy(x => x.diff)
                .ToList();

            return within.Count switch
            {
                0 => throw new InvalidOperationException(
                    $"Cannot realize voice within MaxVoiceMovement={limit} semitones."),
                _ => within[0].p
            };
        }

        // Unconstrained: choose closest.
        return candidates
            .OrderBy(p => Math.Abs(p - previousPitch))
            .First();
    }

    private static NoteEvent[] EnforceUpperVoiceOrdering(FiguredBassSymbol symbol, NoteEvent[] notes)
    {
        if (notes.Length <= 2)
        {
            return notes;
        }

        var bass = notes[0];
        var upper = notes.Skip(1).OrderBy(n => n.Pitch).ToArray();

        var result = new NoteEvent[1 + upper.Length];
        result[0] = bass;
        for (var i = 0; i < upper.Length; i++)
        {
            result[i + 1] = new NoteEvent(upper[i].Pitch, symbol.Time, symbol.Duration, upper[i].Velocity);
        }

        return result;
    }

    /// <summary>
    /// Realize a single figured bass symbol
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    public NoteEvent[] RealizeSymbol(FiguredBassSymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var intervals = NormalizeFigures(symbol.Figures);
        var notes = new List<NoteEvent>
        {
            // Bass note
            new NoteEvent(symbol.BassPitch, symbol.Time, symbol.Duration)
        };

        // Realize upper voices based on intervals
        foreach (var interval in intervals)
        {
            var pitch = CalculatePitch(symbol.BassPitch, interval, symbol.Accidentals);

            // Adjust pitch to appropriate octave range
            pitch = AdjustToRange(pitch, _options.MinPitch, _options.MaxPitch);

            notes.Add(new NoteEvent(pitch, symbol.Time, symbol.Duration, 0.7f));
        }

        return [.. notes];
    }

    /// <summary>
    /// Normalize figured bass figures to standard intervals
    /// </summary>
    private static int[] NormalizeFigures(int[] figures)
    {
        return figures.Length switch
        {
            0 => [3, 5],
            _ => figures switch
            {
                [6] => [3, 6], // 6 = first inversion (6/3)
                [6, 4] => [4, 6], // 6/4 = second inversion
                [7] => [3, 5, 7], // 7 = dominant seventh
                [6, 5] => [3, 5, 6], // 6/5 = first inversion seventh
                [4, 3] => [3, 4, 6], // 4/3 = second inversion seventh
                [4, 2] or [2] => [2, 4, 6], // 4/2 or 2 = third inversion seventh
                [9] => [3, 5, 9], // 9 = ninth chord
                [5, 3] => [3, 5], // 5/3 = explicit root position
                _ => figures // Use as-is
            }
        };

        // Common figured bass abbreviations
    }

    /// <summary>
    /// Calculate pitch from bass note and figured-bass interval.
    /// Figures are DIATONIC by definition: "3" above A in C major is C (minor third),
    /// not C# — the interval is counted along the key's scale. Accidentals in the
    /// figures chromatically alter the diatonic pitch.
    /// </summary>
    private int CalculatePitch(int bassPitch, int interval, Dictionary<int, char>? accidentals)
    {
        var pitch = bassPitch + DiatonicIntervalSemitones(bassPitch, interval);

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

    private int DiatonicIntervalSemitones(int bassPitch, int interval)
    {
        if (interval <= 1)
            return 0;

        var scale = _options.Key.GetScale(); // 7 ascending pitch classes of the key
        var bassPc = PitchMath.Fold(bassPitch);
        var idx = Array.IndexOf(scale, bassPc);

        if (idx < 0)
        {
            // Chromatic bass (not in the key): fall back to the closest generic mapping.
            return interval switch
            {
                2 => 2,
                3 => 4,
                4 => 5,
                5 => 7,
                6 => 9,
                7 => 10,
                8 => 12,
                9 => 14,
                _ => 0
            };
        }

        // Sum the ascending semitone steps degree-by-degree. Summing directly is
        // robust to the scale array wrapping mod-12 mid-array (true for every key
        // except C major, e.g. G major is [7,9,11,0,2,4,6]); a closed-form
        // scale[target]-scale[bass]+12*octaves double-counts an octave there.
        var steps = interval - 1;
        var semitones = 0;
        for (var k = 0; k < steps; k++)
        {
            var cur = scale[(idx + k) % 7];
            var next = scale[(idx + k + 1) % 7];
            var step = next - cur;
            if (step <= 0)
                step += 12; // ascending step across the octave wrap
            semitones += step;
        }

        return semitones;
    }

    /// <summary>
    /// Adjust pitch to be within specified range
    /// </summary>
    private static int AdjustToRange(int pitch, int minPitch, int maxPitch)
    {
        while (pitch < minPitch)
        {
            pitch += 12;
        }

        while (pitch > maxPitch)
        {
            pitch -= 12;
        }

        return pitch;
    }

    /// <summary>
    /// Parse figured bass notation string (e.g., "6", "7", "6/5", "#3/#5").
    /// Accidental prefixes are tolerated here (use <see cref="ParseAccidentals"/> to read them).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="figuresStr"/> is <see langword="null"/>.</exception>
    public static int[] ParseFigures(string figuresStr)
    {
        // IsNullOrWhiteSpace() accepts null, so null returned an empty int[] — indistinguishable
        // from an unfigured bass, which realizes as a plain root-position triad.
        ArgumentNullException.ThrowIfNull(figuresStr);

        if (string.IsNullOrWhiteSpace(figuresStr))
        {
            return [];
        }

        var parts = figuresStr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var figures = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            var digits = new string(part.Where(char.IsDigit).ToArray());
            if (digits.Length > 0)
            {
                figures.Add(int.Parse(digits));
            }
        }

        return [.. figures];
    }

    /// <summary>
    /// Parse accidentals from figured bass string (e.g., "#3", "b7", "#3/#5")
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="figuresStr"/> is <see langword="null"/>.</exception>
    public static Dictionary<int, char> ParseAccidentals(string figuresStr)
    {
        ArgumentNullException.ThrowIfNull(figuresStr);

        var accidentals = new Dictionary<int, char>();

        for (var i = 0; i < figuresStr.Length; i++)
        {
            var c = figuresStr[i];
            if (c is '#' or 'b' or 'n' && i + 1 < figuresStr.Length && char.IsDigit(figuresStr[i + 1]))
            {
                var interval = figuresStr[i + 1] - '0';
                accidentals[interval] = c;
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

    /// <summary>
    /// Key used to interpret figures diatonically (default: C major).
    /// Figured-bass intervals are scale steps in this key; accidentals in the
    /// figures alter them chromatically.
    /// </summary>
    public KeySignature Key { get; init; } = new(0, true);
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
