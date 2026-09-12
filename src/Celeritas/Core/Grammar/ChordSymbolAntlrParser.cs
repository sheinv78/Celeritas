// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Antlr4.Runtime;
using Celeritas.Core.Grammar;

namespace Celeritas.Core;

/// <summary>
/// ANTLR-based chord symbol parser.
/// Supports: root note + accidentals, qualities, extensions, alterations, add/omit, slash bass, and simple polychords.
/// Implementation detail behind <see cref="Analysis.ProgressionAdvisor.ParseChordSymbol"/> and
/// <c>ProgressionAdvisor.TryParseChordSymbol</c>, which are the public entry points.
/// </summary>
internal static class ChordSymbolAntlrParser
{
    /// <summary>
    /// Parse a chord symbol into MIDI pitches (octave 4 root = C4/60), each pitch named once.
    /// For slash chords, bass is placed at octave 3 (C3/48).
    /// For polychords ("C|G"), subsequent layers are placed one octave higher.
    /// </summary>
    /// <remarks>
    /// The layers of a polychord used to be concatenated as built, so a pitch two layers share —
    /// the D of "C9|D", which is the ninth of the lower chord and the root of the upper one an
    /// octave up; the Db of "C7(b9,#9)|Db" — came back twice in one list.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static int[] ParsePitches(string input)
    {
        // Guard here rather than leaning on TryParsePitches: it reports null as an ordinary
        // parse failure, which would surface as ArgumentException — the wrong exception for
        // a missing argument, and one a caller cannot tell apart from a malformed symbol.
        ArgumentNullException.ThrowIfNull(input);

        if (!TryParsePitches(input, out var pitches, out var errors))
            throw new ArgumentException($"Parse errors: {string.Join("; ", errors)}");

        return pitches;
    }

    public static bool TryParsePitches(string input, out int[] pitches)
    {
        return TryParsePitches(input, out pitches, out _);
    }

    public static bool TryParsePitches(string input, out int[] pitches, out IReadOnlyList<string> errors) =>
        TryParsePitches(input, out pitches, out _, out errors);

    /// <summary>
    /// Parses a chord symbol into its pitches and the pitch class of the root it names — the
    /// root of the first chord of a polychord. A symbol states its root; the readers that
    /// rediscovered it from the pitches named chords their caller did not write.
    /// </summary>
    public static bool TryParsePitches(string input, out int[] pitches, out int rootPitchClass, out IReadOnlyList<string> errors)
    {
        pitches = [];
        rootPitchClass = 0;

        // Null is unparsable input, not an empty chord: report failure the way
        // int.TryParse(null, out _) does, rather than claiming a successful parse.
        if (input is null)
        {
            errors = ["Input is null."];
            return false;
        }

        // Blank is unparsable input, not an empty chord — the same call as null, one line up.
        // Reporting it as a *successful* parse of zero pitches defeated the one thing this Try*
        // overload exists to do: let a caller tell "not a chord" apart from "parsed to nothing".
        // On `true` with an empty array, a caller still had to test pitches.Length — exactly the
        // check the bool was meant to replace.
        if (string.IsNullOrWhiteSpace(input))
        {
            errors = ["Input is blank."];
            return false;
        }

        input = NormalizeAccidentals(input);
        input = NormalizePlusAlterations(input);

        // A code-point stream, not AntlrInputStream: that one feeds the lexer UTF-16 units, so a
        // character outside the Basic Multilingual Plane — an emoji, a musical symbol such as 𝄞 —
        // arrived as a lone high surrogate, and the lexer's own error display threw
        // ArgumentException on it before any listener was told. A symbol this parser could not
        // read was promised a `false`, and "🎵" got an exception instead.
        var inputStream = CharStreams.fromString(input);
        var lexer = new ChordSymbolLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new ChordSymbolParser(tokenStream);

        var mutableErrors = new List<string>();
        var lexerErrorListener = new LexerErrorListener(mutableErrors);
        var parserErrorListener = new ParserErrorListener(mutableErrors);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrorListener);

        var tree = parser.symbol();

        if (mutableErrors.Count > 0)
        {
            errors = mutableErrors;
            return false;
        }

        try
        {
            var visitor = new ChordSymbolVisitorImpl();
            pitches = visitor.Visit(tree);
            rootPitchClass = visitor.RootPitchClass ?? 0;
        }
        catch (ChordSymbolParseException ex)
        {
            // Semantic errors the grammar cannot express (out-of-range numbers,
            // unsupported alteration/add degrees) are ordinary parse failures.
            pitches = [];
            mutableErrors.Add(ex.Message);
            errors = mutableErrors;
            return false;
        }

        errors = [];
        return true;
    }

    private static string NormalizePlusAlterations(string input)
    {
        // Many chord charts use "+5" / "+9" to mean "#5" / "#9".
        // But "+" is also used for augmented quality (e.g., "C+", "C+7", "C+9").
        // Heuristic: treat '+' as an alteration only when it is preceded by a digit, '(' or ','
        // and is followed by one of {5,9,11,13}.

        ReadOnlySpan<char> s = input;
        var changed = false;
        var chars = input.ToCharArray();

        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '+')
                continue;

            if (i == 0)
                continue;

            var prev = s[i - 1];
            if (!(char.IsDigit(prev) || prev == '(' || prev == ','))
                continue;

            var j = i + 1;
            if (j >= s.Length || !char.IsDigit(s[j]))
                continue;

            var start = j;
            while (j < s.Length && char.IsDigit(s[j]))
                j++;

            if (!int.TryParse(s[start..j], out var degree))
                continue;

            if (degree is 5 or 9 or 11 or 13)
            {
                chars[i] = '#';
                changed = true;
            }
        }

        return changed ? new string(chars) : input;
    }

    private static string NormalizeAccidentals(string input)
    {
        // Keep the lexer/parser simple by normalizing unicode accidentals early.
        // This also ensures all downstream logic deals with a single representation.
        return input
            .Replace('♯', '#')
            .Replace('♭', 'b');
    }
}

/// <summary>
/// Signals a chord-symbol input the grammar accepts but the builder cannot give a
/// meaning to (out-of-range numbers, unsupported alteration/add degrees). Caught in
/// <see cref="ChordSymbolAntlrParser.TryParsePitches(string, out int[], out IReadOnlyList{string})"/>
/// and reported as an ordinary parse failure.
/// </summary>
internal sealed class ChordSymbolParseException(string message) : Exception(message);

internal sealed class ChordSymbolVisitorImpl : ChordSymbolBaseVisitor<int[]>
{
    /// <summary>The pitch class of the root the symbol names; of the first chord for a polychord.</summary>
    public int? RootPitchClass { get; private set; }

    public override int[] VisitSymbol(ChordSymbolParser.SymbolContext context)
    {
        return Visit(context.polychord());
    }

    public override int[] VisitPolychord(ChordSymbolParser.PolychordContext context)
    {
        var chords = context.chord();
        if (chords.Length == 0)
            return [];

        if (chords.Length == 1)
            return Visit(chords[0]);

        // A pitch two layers share is one pitch: the ninth of "C9" and the root of the D triad
        // stacked above it both land on D5.
        var pitches = new List<int>();
        var seen = new HashSet<int>();
        for (var i = 0; i < chords.Length; i++)
        {
            // Stack each additional chord one octave above the previous to reduce collisions.
            var rootBase = 60 + (12 * i);
            foreach (var pitch in BuildChordPitches(chords[i], rootBase))
            {
                if (seen.Add(pitch))
                    pitches.Add(pitch);
            }
        }

        return [.. pitches];
    }

    public override int[] VisitChord(ChordSymbolParser.ChordContext context)
    {
        return [.. BuildChordPitches(context, 60)];
    }

    private List<int> BuildChordPitches(ChordSymbolParser.ChordContext chord, int rootBase)
    {
        var rootPc = ParsePitchClass(chord.note());
        RootPitchClass ??= rootPc;
        var builder = new ChordBuildState();

        // Preserve suffix ordering as written.
        foreach (var suffix in chord.chordSuffix())
        {
            if (suffix.group() is { } group)
            {
                foreach (var item in group.groupItem())
                    ApplyGroupItem(builder, item);
                continue;
            }

            ApplySuffix(builder, suffix);
        }

        int? bassPc = null;
        if (chord.slashBass() is { } slash)
            bassPc = ParsePitchClass(slash.note());

        var intervals = builder.BuildIntervals();

        var rootPitch = rootBase + rootPc;
        var pitches = new List<int>(intervals.Count + 1);

        // Bass first if slash chord
        int? bassOverridePitch = null;
        if (bassPc.HasValue)
        {
            bassOverridePitch = 48 + bassPc.Value;
            pitches.Add(bassOverridePitch.Value);
        }

        foreach (var interval in intervals)
        {
            var pitch = rootPitch + interval;
            if (!bassOverridePitch.HasValue || (pitch % 12) != (bassOverridePitch.Value % 12))
                pitches.Add(pitch);
        }

        return pitches;
    }

    private static void ApplySuffix(ChordBuildState builder, ChordSymbolParser.ChordSuffixContext suffix)
    {
        if (suffix.quality() is { } q)
        {
            var qText = q.GetText();

            // "Cmmaj7" / "C-maj7" / "CmM7": a bare maj/M/Δ AFTER an explicit minor marks the major
            // seventh instead of overwriting the minor third (same rule as the parenthesized m(maj7) path).
            if (builder.IsMinorTriad &&
                (string.Equals(qText, "maj", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(qText, "major", StringComparison.OrdinalIgnoreCase) ||
                 qText is "Δ" or "M"))
            {
                builder.MarkMajorSeventh();
                return;
            }

            builder.ApplyQuality(qText);
            return;
        }

        if (suffix.extension() is { } ext)
        {
            var extText = ext.GetText();
            if (extText is "6/9" or "69")
            {
                builder.ApplySixNine();
                return;
            }

            // A number after a bare sus ("sus2"/"sus4") is resolved inside ApplyExtension, so
            // the parenthesized path below reads it the same way.
            builder.ApplyExtension(ParseDegree(extText, extText));
            return;
        }

        if (suffix.alteration() is { } alt)
        {
            var altText = alt.GetText();
            var accidental = altText.StartsWith("#", StringComparison.Ordinal) ? "#" : "b";
            var num = new string([.. altText.Where(char.IsDigit)]);
            if (num.Length > 0)
                builder.ApplyAlteration(accidental, ParseDegree(num, altText));
            return;
        }

        if (suffix.addTone() is { } add)
        {
            // add9, add2, add11...
            var addText = add.GetText();
            var num = new string([.. addText.Where(char.IsDigit)]);
            if (num.Length > 0)
                builder.ApplyAdd(ParseDegree(num, addText));
            return;
        }

        if (suffix.omitTone() is { } omit)
        {
            // no3, omit5...
            var omitText = omit.GetText();
            var num = new string([.. omitText.Where(char.IsDigit)]);
            if (num.Length > 0)
                builder.ApplyOmit(ParseDegree(num, omitText));
            return;
        }

        if (suffix.modifier() is { } m)
        {
            builder.ApplyModifier(m.GetText());
        }
    }

    private static void ApplyGroupItem(ChordBuildState builder, ChordSymbolParser.GroupItemContext item)
    {
        // Avoid relying on token/rule accessor names; inspect child rule contexts.
        var text = item.GetText();
        if (string.Equals(text, "alt", StringComparison.OrdinalIgnoreCase))
        {
            builder.ApplyModifier("alt");
            return;
        }

        if (item.children is null)
            return;

        foreach (var child in item.children)
        {
            switch (child)
            {
                case ChordSymbolParser.AddToneContext add:
                    // add9/add11...
                    var addText = add.GetText();
                    var addNum = new string([.. addText.Where(char.IsDigit)]);
                    if (addNum.Length > 0)
                        builder.ApplyAdd(ParseDegree(addNum, addText));
                    return;
                case ChordSymbolParser.OmitToneContext omit:
                    // Supports: omit3 / no3
                    var omitText = omit.GetText().ToLowerInvariant();
                    var omitNum = new string([.. omitText.Where(char.IsDigit)]);
                    if (omitNum.Length > 0)
                        builder.ApplyOmit(ParseDegree(omitNum, omitText));
                    return;
                case ChordSymbolParser.AlterationContext alt:
                    var altText = alt.GetText();
                    var accidental = altText.StartsWith("#", StringComparison.Ordinal) ? "#" : "b";
                    var num = new string([.. altText.Where(char.IsDigit)]);
                    if (num.Length > 0)
                        builder.ApplyAlteration(accidental, ParseDegree(num, altText));
                    return;
                case ChordSymbolParser.ExtensionContext ext:
                    var extText = ext.GetText();
                    if (extText is "6/9" or "69")
                    {
                        builder.ApplySixNine();
                        continue;
                    }
                    builder.ApplyExtension(ParseDegree(extText, extText));
                    continue;
                case ChordSymbolParser.ModifierContext m:
                    // "(5)": the same POWER token the unparenthesized path hands to ApplyModifier.
                    builder.ApplyModifier(m.GetText());
                    continue;
                case ChordSymbolParser.QualityContext q:
                    // Allows things like m(maj7) or (Δ9)
                    var qText = q.GetText();
                    if (builder.IsMinorTriad && (string.Equals(qText, "maj", StringComparison.OrdinalIgnoreCase) || string.Equals(qText, "major", StringComparison.OrdinalIgnoreCase) || qText is "Δ" or "M"))
                    {
                        builder.MarkMajorSeventh();
                        continue;
                    }
                    builder.ApplyQuality(qText);
                    continue;
            }
        }
    }

    /// <summary>
    /// Parses a degree/extension number, rejecting values that do not fit an int
    /// (e.g. "C99999999999999999999") as parse errors instead of overflowing.
    /// </summary>
    private static int ParseDegree(string digits, string source)
    {
        if (!int.TryParse(digits, out var value))
            throw new ChordSymbolParseException($"Number out of range in '{source}'.");
        return value;
    }

    /// <summary>
    /// The pitch class of a root or slash bass. The letter is a capital: a lead sheet writes its
    /// roots in capitals, and the grammar's PITCH_NAME token admits nothing else, so "c7" and
    /// "cm7" are refused before this runs — a lowercase letter is not a chord by any convention
    /// a musician relies on, and "b" is the flat sign.
    /// </summary>
    /// <remarks>
    /// This switch used to carry a lowercase arm beside each capital, code no input could reach,
    /// which read as if the parser accepted "cm7". It never did; the arms are gone.
    /// </remarks>
    private static int ParsePitchClass(ChordSymbolParser.NoteContext note)
    {
        var text = note.GetText();
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var n = text[0];
        var pc = n switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            // The lexer admits only A-G here; anything else is a grammar change, not a chord.
            _ => throw new ChordSymbolParseException($"Not a root letter: '{n}'.")
        };

        // Remaining characters are accidentals ('-' is a quality token, never an accidental).
        for (var i = 1; i < text.Length; i++)
        {
            pc += text[i] switch
            {
                '#' => 1,
                'b' => -1,
                _ => 0
            };
        }

        pc %= 12;
        if (pc < 0)
            pc += 12;
        return pc;
    }
}

internal sealed class ChordBuildState
{
    private TriadQuality _triad = TriadQuality.Major;

    /// <summary>
    /// Whether a marker has already named the triad. A "maj"/"M" that follows one is about the
    /// seventh, not the triad — see the maj arm of <see cref="ApplyQuality"/>.
    /// </summary>
    private bool _triadNamed;

    public bool IsMinorTriad => _triad == TriadQuality.Minor;

    public bool SusPending { get; private set; }

    private bool _wantsMajorSeventh;
    private bool _explicitMinor;
    private bool _explicitMajor;

    private bool _omit3;
    private bool _omit5;
    private bool _omit7;

    private bool _power;

    private int? _extension;

    /// <summary>
    /// Every alteration written for a degree, keyed by the degree (5, 9, 11 or 13) and holding
    /// the semitones above the root each alteration names. Building removes the natural pitch of
    /// an altered degree and adds every alteration named for it, so "C7(b9,#9)" carries both
    /// altered ninths — the stock altered-dominant sound — and "C7(b5,#5)" both altered fifths.
    /// "alt" and "ø" write into the same sets the explicit alterations do.
    /// </summary>
    /// <remarks>
    /// One nullable slot per degree used to hold the last alteration written, so "C7(b9,#9)" came
    /// back without its b9 and "C7(#9,b9)" without its #9: the pitch set of a symbol depended on
    /// the order its alterations were written in, and both ninths could not be written at all.
    /// </remarks>
    private readonly Dictionary<int, HashSet<int>> _alterations = [];

    /// <summary>
    /// Whether "ø" or "halfdim" named the chord. The half-diminished seventh is the minor one, and
    /// this mark is the only thing that says so for a diminished triad: "m7b5" gets its minor
    /// seventh from the minor triad it is written on.
    /// </summary>
    /// <remarks>
    /// <see cref="ResolveSeventh"/> used to read any altered fifth as this mark, so an explicit
    /// b5 on a diminished seventh chord — "Cdim7(b5)", a redundant flat on a fifth that is already
    /// flat — flipped its seventh from diminished to minor and answered Cø7 for a chord that was
    /// written Cdim7.
    /// </remarks>
    private bool _halfDiminished;

    private readonly HashSet<int> _adds = [];

    public void ApplySixNine()
    {
        _extension = Math.Max(_extension ?? 0, 6);
        _adds.Add(14);
    }

    public void MarkMajorSeventh()
    {
        _explicitMajor = true;
        _wantsMajorSeventh = true;
    }

    public void ApplyQuality(string text)
    {
        var t = text.Trim();

        // Normalize common variants.
        // A bare Δ implies the major seventh even without an extension ("CΔ" = Cmaj7).
        if (t is "Δ" or "△")
        {
            _explicitMajor = true;
            _wantsMajorSeventh = true;
            return;
        }

        // Single uppercase 'M' is major ("CM" = C triad, "CM7" = Cmaj7); it must be
        // matched before lowercasing, which would turn it into the minor marker.
        if (t == "M")
        {
            if (!_triadNamed)
            {
                _triad = TriadQuality.Major;
                _triadNamed = true;
            }

            _explicitMajor = true;
            return;
        }

        t = t.ToLowerInvariant();

        switch (t)
        {
            case "maj":
            case "major":
                // "Cmaj" is a plain major triad; _explicitMajor still makes a following
                // extension use the major seventh ("Cmaj7"/"Cmaj9").
                //
                // A maj marker that FOLLOWS another triad quality is about the seventh, not the
                // triad: "Caugmaj7" is an augmented triad with a major seventh. Overwriting the
                // triad here turned it — and "Cdimmaj7" and "Csus4maj7" — into a plain Cmaj7,
                // silently returning a different chord from the one that was written.
                if (!_triadNamed)
                {
                    _triad = TriadQuality.Major;
                    _triadNamed = true;
                }

                _explicitMajor = true;
                break;
            case "min":
            case "minor":
            case "m":
            case "-":
                _triad = TriadQuality.Minor;
                _triadNamed = true;
                _explicitMinor = true;
                break;
            case "dim":
            case "o":
            case "°":
                _triad = TriadQuality.Diminished;
                _triadNamed = true;
                break;
            case "aug":
            case "+":
                _triad = TriadQuality.Augmented;
                _triadNamed = true;
                break;
            case "sus":
                _triad = TriadQuality.Sus4;
                _triadNamed = true;
                SusPending = true;
                break;
            case "ø":
            case "halfdim":
                _triad = TriadQuality.Diminished;
                _extension = Math.Max(_extension ?? 0, 7);
                Alter(5, 6);
                // half-diminished has a minor seventh
                _halfDiminished = true;
                _wantsMajorSeventh = false;
                break;
        }
    }

    public void ApplySus(int n)
    {
        _triad = n == 2 ? TriadQuality.Sus2 : TriadQuality.Sus4;
        SusPending = false;
    }

    public void ApplyModifier(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        switch (t)
        {
            case "5":
                _power = true;
                _omit3 = true;
                break;
            case "alt":
                _extension = Math.Max(_extension ?? 0, 7);
                // Default altered dominant interpretation (minimal): #5 and b9.
                Alter(5, 8);
                Alter(9, 13);
                break;
        }
    }

    /// <summary>
    /// A bare number after the root and its quality. 6, 7, 9, 11 and 13 are the extensions;
    /// 2, 4 and 5 are lead-sheet shorthand — "C2" is Cadd9, "C4" is Csus4, "C5" the power
    /// chord — and after a bare "sus" a 2 or 4 names the suspension. Any other number names no
    /// chord and fails the parse, the way an unsupported add or altered degree does.
    /// </summary>
    /// <remarks>
    /// Every positive number used to be accepted, and only 6 and 7 upward acted on, so "C2",
    /// "C3" and "C4" all parsed silently to a plain C major triad and "C8" to a C7. The sus
    /// check lived in the unparenthesized caller alone, so "C(sus2)" read as C sus4.
    /// </remarks>
    public void ApplyExtension(int n)
    {
        // "sus2" / "sus4" is often written as SUS + 2/4: the number is the suspension, not an
        // extension, whichever path delivered it.
        if (SusPending && n is 2 or 4)
        {
            ApplySus(n);
            return;
        }

        switch (n)
        {
            case 2:
                _adds.Add(MapAddDegreeToSemitones(9));
                break;
            case 4:
                ApplySus(4);
                _triadNamed = true;
                break;
            case 5:
                // The lexer hands a lone "5" to ApplyModifier as the POWER token; this arm keeps
                // the meaning with the number rather than with token precedence.
                ApplyModifier("5");
                break;
            case 6 or 7 or 9 or 11 or 13:
                _extension = Math.Max(_extension ?? 0, n);
                break;
            default:
                throw new ChordSymbolParseException($"Unsupported extension: {n} (expected 2, 4, 5, 6, 7, 9, 11 or 13).");
        }
    }

    public void ApplyAdd(int n)
    {
        if (n is not (2 or 4 or 6 or 9 or 11 or 13))
            throw new ChordSymbolParseException($"Unsupported add degree: add{n} (expected 2, 4, 6, 9, 11 or 13).");

        _adds.Add(MapAddDegreeToSemitones(n));
    }

    public void ApplyOmit(int n)
    {
        switch (n)
        {
            case 3:
                _omit3 = true;
                break;
            case 5:
                _omit5 = true;
                break;
            case 7:
                _omit7 = true;
                break;
        }
    }

    /// <summary>
    /// An altered fifth, ninth, eleventh or thirteenth. A degree may be altered more than once —
    /// "C7(b9,#9)", "C7(b5,#5)" — and keeps every alteration written for it; writing the same
    /// one twice is one note. Any other degree names no chord and fails the parse.
    /// </summary>
    public void ApplyAlteration(string accidental, int degree)
    {
        if (degree is not (5 or 9 or 11 or 13))
            throw new ChordSymbolParseException($"Unsupported altered degree: {accidental}{degree} (expected 5, 9, 11 or 13).");

        var delta = accidental.Contains('#') ? 1 : -1;
        Alter(degree, MapExtensionDegreeToSemitones(degree) + delta);
    }

    private void Alter(int degree, int semitones)
    {
        if (!_alterations.TryGetValue(degree, out var altered))
        {
            altered = [];
            _alterations[degree] = altered;
        }

        altered.Add(semitones);
    }

    /// <summary>
    /// The semitones above the root the symbol names, in order. The triad — or the bare fifth of
    /// a power chord — and the extension chain go in first; then every altered degree loses its
    /// natural pitch and gains each alteration written for it; then the explicit adds, which are
    /// heard whatever else was written. The power chord takes the same road as every other
    /// chord, so "C5(b9)" is C, G and Db.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The power chord used to return early with its fifth and its adds, so an altered ninth,
    /// eleventh or thirteenth written on it — "C5(b9)", "C5(#11)" — was dropped without a word,
    /// against the rule that this parser refuses what it cannot spell rather than spelling
    /// something else.
    /// </para>
    /// <para>
    /// The adds used to go in before the alterations, so an alteration of the same degree took
    /// the added natural out with the one from the extension chain: "C7(b9)add9" lost the D that
    /// was written beside its Db. A ninth chord's own ninth still gives way — "C9(b9)" has no D,
    /// that is the convention — but an explicit add is a note asked for by name.
    /// </para>
    /// </remarks>
    public List<int> BuildIntervals()
    {
        var intervals = new HashSet<int> { 0 };

        var (third, fifth) = _triad switch
        {
            TriadQuality.Major => (4, 7),
            TriadQuality.Minor => (3, 7),
            TriadQuality.Diminished => (3, 6),
            TriadQuality.Augmented => (4, 8),
            TriadQuality.Sus2 => (2, 7),
            TriadQuality.Sus4 => (5, 7),
            _ => (4, 7)
        };

        // A power chord is the root and the perfect fifth, whatever triad marker sits beside it.
        if (_power)
            fifth = 7;

        if (!_power && !_omit3)
            intervals.Add(third);

        if (!_omit5)
            intervals.Add(fifth);

        AddExtensions(intervals);

        // Every altered degree loses its natural pitch — put in by the triad or by the extension
        // above, so C9(b9,#9) has no natural ninth — and gains every alteration written for it.
        foreach (var (degree, altered) in _alterations)
        {
            if (degree == 5 && _omit5)
                continue;

            intervals.Remove(NaturalPitchOf(degree));
            intervals.UnionWith(altered);
        }

        foreach (var add in _adds)
            intervals.Add(add);

        return [.. intervals.OrderBy(x => x)];
    }

    /// <summary>
    /// The pitch an alteration of <paramref name="degree"/> displaces: the perfect fifth, or the
    /// one natural of an upper degree. A diminished or augmented fifth named by the triad's own
    /// quality is not displaced: "aug" says #5 and "(b5)" says b5, and a chord written
    /// "Caug7(b5)" carries both fifths, exactly as "C7(b5,#5)" does.
    /// </summary>
    /// <remarks>
    /// Every fifth used to be displaced, so "Caug7(b5)" came back as C7b5 with the augmented fifth
    /// that was written gone, and "Cdim7(#5)" lost its diminished one.
    /// </remarks>
    private static int NaturalPitchOf(int degree) => MapExtensionDegreeToSemitones(degree);

    private void AddExtensions(HashSet<int> intervals)
    {
        // A bare Δ or maj-after-minor ("CΔ", "CmΔ", "Cmmaj") marks the major seventh
        // without an explicit extension: default the extension to 7 so the seventh is
        // actually emitted instead of collapsing to a plain triad.
        var extension = _extension ?? (_wantsMajorSeventh ? 7 : (int?)null);

        if (!extension.HasValue)
            return;

        var ext = extension.Value;

        if (ext == 6)
        {
            intervals.Add(9);
        }
        else if (ext >= 7)
        {
            if (!_omit7)
                intervals.Add(ResolveSeventh(ext));

            if (ext >= 9)
                intervals.Add(14);
            if (ext >= 11)
                intervals.Add(17);
            if (ext >= 13)
                intervals.Add(21);
        }
    }

    private int ResolveSeventh(int ext)
    {
        // If "maj" appears anywhere, interpret 7/9/11/13 as major 7th.
        if (_explicitMajor || _wantsMajorSeventh)
            return 11;

        // Minor triads default to minor 7th for 7/9/11/13.
        if (_explicitMinor || _triad == TriadQuality.Minor)
            return 10;

        return _triad switch
        {
            // Diminished: if explicitly dim7, use diminished 7th (9 semitones); otherwise minor 7th.
            // Only the half-diminished mark ("ø", "halfdim") asks for the minor one — an altered
            // fifth written on a diminished chord says nothing about its seventh.
            TriadQuality.Diminished when ext == 7 && !_halfDiminished => 9,
            _ => 10
        };
    }

    private static int MapExtensionDegreeToSemitones(int degree) => degree switch
    {
        5 => 7,
        9 => 14,
        11 => 17,
        13 => 21,
        _ => degree
    };

    private static int MapAddDegreeToSemitones(int degree) => degree switch
    {
        2 => 2,
        4 => 5,
        6 => 9,
        9 => 14,
        11 => 17,
        13 => 21,
        _ => degree
    };

    private enum TriadQuality
    {
        Major,
        Minor,
        Diminished,
        Augmented,
        Sus2,
        Sus4
    }
}
