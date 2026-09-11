namespace Celeritas.Core.FiguredBass;

/// <summary>
/// Realizes figured bass notation into actual chord voicings
/// </summary>
public sealed class FiguredBassRealizer
{
    private readonly FiguredBassOptions _options;

    private bool AllowVoiceCrossing => _options is FiguredBassRealizerOptions o && o.AllowVoiceCrossing;

    /// <summary>Creates a realizer with the given options, or defaults when <paramref name="options"/> is <see langword="null"/>.</summary>
    public FiguredBassRealizer(FiguredBassOptions? options = null)
    {
        _options = options ?? new FiguredBassOptions();
    }

    /// <summary>Creates a realizer with extended realizer options (voice crossing, movement limits).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
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

            // MaxPitch is a preference and documented as one; the keyboard is not. Upper voices
            // are stacked above the bass, so a bass near the top of the range pushed them past
            // MIDI 127 — a bass of 125 realized as 125, 129, 132, 136, pitches that cannot be
            // played, written to a file, or named. There is no octave left to move them to
            // without putting them under the bass, so the figure is refused rather than
            // realized as something unplayable.
            foreach (var note in voicing)
            {
                if (note.Pitch is < 0 or > 127)
                {
                    throw new ArgumentException(
                        $"The figure over bass {symbol.BassPitch} at {symbol.Time} needs a voice at MIDI {note.Pitch}, " +
                        "which is off the keyboard; the bass leaves no room above it for the figures.",
                        nameof(symbols));
                }
            }

            result.AddRange(voicing);

            previousUpperVoices = voicing.Length switch
            {
                // Cache upper voices for the next symbol.
                > 1 => [.. voicing.Skip(1).Select(n => n.Pitch)],
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
            new(symbol.BassPitch, symbol.Time, symbol.Duration)
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
            // Stack each upper voice at the lowest octave strictly above the bass and
            // above the previous upper voice (so the voicing never crosses), raised
            // further when the bass sits below MinPitch.
            var floor = Math.Max(symbol.BassPitch, _options.MinPitch - 1);
            for (var i = 0; i < targetPitchClasses.Length; i++)
            {
                var realized = LowestPitchOfClassAbove(targetPitchClasses[i], floor);
                floor = realized;
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
                symbol.BassPitch,
                _options.MinPitch,
                _options.MaxPitch);
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

        return [.. notes];
    }

    private static int ChooseClosestPitchInRange(
        int pitchClass,
        int previousPitch,
        int bassPitch,
        int minPitch,
        int maxPitch)
    {
        // Upper voices must stay strictly above the bass, even when the configured
        // range would otherwise allow dipping below it.
        var floor = Math.Max(minPitch, bassPitch + 1);

        // Enumerate octave candidates within range for the given pitch-class and pick
        // the one closest to the previous pitch of this voice. This is inherently the
        // minimum movement, so FiguredBassRealizerOptions.MaxVoiceMovement is satisfied
        // whenever it is satisfiable; when it is not, the closest candidate is the
        // documented best-effort fallback (the realizer never fails mid-progression).
        var best = int.MinValue;
        for (var p = LowestPitchOfClassAbove(pitchClass, floor - 1); p <= maxPitch; p += 12)
        {
            if (best == int.MinValue || Math.Abs(p - previousPitch) < Math.Abs(best - previousPitch))
            {
                best = p;
            }
        }

        if (best == int.MinValue)
        {
            // Range too narrow to hold this pitch class above the bass: keep the pitch
            // class and the above-bass guarantee (soft violation of MaxPitch).
            return LowestPitchOfClassAbove(pitchClass, bassPitch);
        }

        return best;
    }

    /// <summary>
    /// Lowest pitch of the given pitch class strictly above <paramref name="floor"/>.
    /// </summary>
    private static int LowestPitchOfClassAbove(int pitchClass, int floor)
    {
        var delta = PitchMath.Fold(pitchClass - PitchMath.Fold(floor));
        if (delta == 0)
        {
            delta = 12;
        }

        return floor + delta;
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
            new(symbol.BassPitch, symbol.Time, symbol.Duration)
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
    /// <remarks>
    /// A lone "3" or "5" — which is how an accidental on the third is written, "#" standing for
    /// "#3" — names one interval of a root-position triad and implies the other, exactly as an
    /// unfigured bass implies both. Without that entry the figure fell through to "use as-is"
    /// and the dominant of every minor-key cadence written the historical way, a bare sharp
    /// under the bass, realized as a two-note chord with no fifth.
    /// </remarks>
    private static int[] NormalizeFigures(int[] figures)
    {
        return figures.Length switch
        {
            0 => [3, 5],
            _ => figures switch
            {
                [3] or [5] or [3, 5] => [3, 5], // one figure of the triad names the whole 5/3
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
            pitch = accidental switch
            {
                '#' => pitch + 1,
                'b' => pitch - 1,
                // 'n' cancels the key's alteration: force the natural (unaltered-letter)
                // pitch of the diatonic degree, e.g. "n3" above D in D major is F natural,
                // not the key's F#.
                'n' => NaturalizeDegree(bassPitch, interval, pitch),
                _ => pitch
            };
        }

        return pitch;
    }

    private static readonly int[] NaturalPitchClasses = [0, 2, 4, 5, 7, 9, 11]; // C D E F G A B

    /// <summary>
    /// Forces the natural (unaltered-letter) pitch of the diatonic degree reached by
    /// <paramref name="interval"/> above the bass: the target letter is counted from the
    /// bass letter, and the diatonic pitch is moved to that letter's natural pitch class.
    /// A chromatic bass is treated as a sharpened natural (letter of the natural below).
    /// </summary>
    private static int NaturalizeDegree(int bassPitch, int interval, int diatonicPitch)
    {
        var bassPc = PitchMath.Fold(bassPitch);
        var letterIndex = Array.IndexOf(NaturalPitchClasses, bassPc);
        if (letterIndex < 0)
        {
            // Chromatic bass: spell as a sharp (letter of the natural a semitone below).
            letterIndex = Array.IndexOf(NaturalPitchClasses, PitchMath.Fold(bassPc - 1));
        }

        var steps = Math.Max(interval, 1) - 1;
        var naturalPc = NaturalPitchClasses[(letterIndex + steps) % 7];

        // Move the diatonic pitch to the natural letter pitch by the shortest distance.
        var delta = PitchMath.Fold(naturalPc - PitchMath.Fold(diatonicPitch));
        if (delta > 6)
        {
            delta -= 12;
        }

        return diatonicPitch + delta;
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

        // A range narrower than an octave can leave the pitch below MinPitch after the
        // downward pass; the final clamp guarantees the [MinPitch, MaxPitch] contract
        // even when the pitch class has to be given up.
        return Math.Clamp(pitch, minPitch, maxPitch);
    }

    /// <summary>
    /// Parse figured bass notation string (e.g., "6", "7", "6/5", "#3/#5").
    /// </summary>
    /// <remarks>
    /// Figures are separated by a slash, a comma, a dash or whitespace — the ways a stack of
    /// figures gets written on one line — so "6/4", "6 4", "6-4" and "6,4" are all a six-four. An
    /// accidental may precede its figure ("#6"), follow it ("6#"), or be written as a trailing
    /// plus for a sharp ("6+"), and a figure may run to two digits ("b10"). An accidental with no
    /// figure at all ("#") is the figured-bass convention for the third, and reads as "#3". The
    /// accidentals are read by <see cref="ParseAccidentals"/> from the same tokens, so the two
    /// methods cannot disagree about which figure an accidental belongs to.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="figuresStr"/> is <see langword="null"/>.</exception>
    public static int[] ParseFigures(string figuresStr)
    {
        // IsNullOrWhiteSpace() accepts null, so null returned an empty int[] — indistinguishable
        // from an unfigured bass, which realizes as a plain root-position triad.
        ArgumentNullException.ThrowIfNull(figuresStr);

        return [.. Tokenize(figuresStr).Select(t => t.Figure)];
    }

    /// <summary>
    /// Parse accidentals from figured bass string (e.g., "#3", "b7", "#3/#5"), keyed by the
    /// figure each one alters.
    /// </summary>
    /// <remarks>
    /// Reads the same tokens <see cref="ParseFigures"/> does. Before that, an accidental was
    /// recognised only immediately in front of a single digit: "6#" and "6+" — the postfix forms
    /// older editions use — were silently dropped, "b10" put its flat on a figure 1 that does not
    /// exist and left the tenth natural, and a bare "#", which every figured-bass reader knows
    /// as a raised third, altered nothing. Each of those realized as a chord the figures did not
    /// ask for, with no error to say so.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="figuresStr"/> is <see langword="null"/>.</exception>
    public static Dictionary<int, char> ParseAccidentals(string figuresStr)
    {
        ArgumentNullException.ThrowIfNull(figuresStr);

        var accidentals = new Dictionary<int, char>();
        foreach (var token in Tokenize(figuresStr))
        {
            if (token.Accidental is { } accidental)
            {
                accidentals[token.Figure] = accidental;
            }
        }

        return accidentals;
    }

    /// <summary>The figure a token names and the accidental on it, if any.</summary>
    private readonly record struct FigureToken(int Figure, char? Accidental);

    /// <summary>
    /// One pass over the text that both public readers share: figures separated by slash, comma
    /// or whitespace; an accidental before or after its digits, or a trailing plus for a sharp;
    /// digits taken whole; a lone accidental standing for the third.
    /// </summary>
    private static List<FigureToken> Tokenize(string figuresStr)
    {
        var tokens = new List<FigureToken>();
        if (string.IsNullOrWhiteSpace(figuresStr))
        {
            return tokens;
        }

        foreach (var part in figuresStr.Split(FigureSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            char? accidental = null;
            var digits = 0;
            var value = 0;

            foreach (var c in part)
            {
                if (char.IsDigit(c))
                {
                    value = (value * 10) + (c - '0');
                    digits++;
                }
                else if (c is '#' or 'b' or 'n')
                {
                    accidental = c;
                }
                else if (c == '+')
                {
                    accidental = '#';
                }

                // Anything else — a stray letter, a dash inside a part — is ignored, as it was.
            }

            if (digits > 0)
            {
                tokens.Add(new FigureToken(value, accidental));
            }
            else if (accidental is not null)
            {
                // A bare accidental alters the third.
                tokens.Add(new FigureToken(3, accidental));
            }
        }

        return tokens;
    }

    // A dash too: no figure is 64, so "6-4" can only mean two figures.
    private static readonly char[] FigureSeparators = ['/', ',', '-', ' ', '\t', '\r', '\n'];
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
    /// Preferred ceiling for upper voices (default: C6)
    /// </summary>
    /// <remarks>
    /// Best-effort, not a hard bound. <see cref="VoiceLeadingStyle.Free"/> (and the per-symbol
    /// <see cref="FiguredBassRealizer.RealizeSymbol"/>) clamps every upper voice into
    /// [<see cref="MinPitch"/>, MaxPitch]. The default <see cref="VoiceLeadingStyle.Smooth"/> and
    /// <see cref="VoiceLeadingStyle.Strict"/> paths may exceed it: the first chord — and any chord
    /// where the voice count changes — is stacked upward from the bass without consulting it, and
    /// later chords fall back to the lowest placement above the bass when the range cannot hold the
    /// required pitch class. With the defaults (MinPitch 48, MaxPitch 84) a root-position bass of 83
    /// realizes as 83, 86, 89. <see cref="MinPitch"/> is honored on every path.
    /// </remarks>
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
    /// Strict style (common practice rules).
    /// </summary>
    /// <remarks>
    /// The realizer takes the same path for this as for <see cref="Smooth"/> and produces the
    /// same notes: the common-practice rules this value names — no parallel fifths or octaves,
    /// the seventh resolved down — are not enforced here. Choose it to say what the music is
    /// meant to be; to have the rules actually applied, realize the bass and then check or solve
    /// the result with <see cref="Celeritas.Core.VoiceLeading.VoiceLeadingRules"/> or
    /// <see cref="Celeritas.Core.VoiceLeading.VoiceLeadingSolver"/>, which do enforce them.
    /// </remarks>
    Strict,

    /// <summary>
    /// Free style (more melodic upper voices)
    /// </summary>
    Free
}
