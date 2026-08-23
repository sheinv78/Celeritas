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
    /// Parse a chord symbol into MIDI pitches (octave 4 root = C4/60).
    /// For slash chords, bass is placed at octave 3 (C3/48).
    /// For polychords ("C|G"), subsequent layers are placed one octave higher.
    /// </summary>
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

    public static bool TryParsePitches(string input, out int[] pitches, out IReadOnlyList<string> errors)
    {
        pitches = [];

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

        var inputStream = new AntlrInputStream(input);
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

        var pitches = new List<int>();
        for (var i = 0; i < chords.Length; i++)
        {
            // Stack each additional chord one octave above the previous to reduce collisions.
            var rootBase = 60 + (12 * i);
            pitches.AddRange(BuildChordPitches(chords[i], rootBase));
        }

        return [.. pitches];
    }

    public override int[] VisitChord(ChordSymbolParser.ChordContext context)
    {
        return [.. BuildChordPitches(context, 60)];
    }

    private static List<int> BuildChordPitches(ChordSymbolParser.ChordContext chord, int rootBase)
    {
        var rootPc = ParsePitchClass(chord.note());
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

            var n = ParseDegree(extText, extText);

            // Special-case: "sus2" / "sus4" is often written as SUS + 2/4.
            if (builder.SusPending && n is 2 or 4)
            {
                builder.ApplySus(n);
                return;
            }

            builder.ApplyExtension(n);
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

    private static int ParsePitchClass(ChordSymbolParser.NoteContext note)
    {
        var text = note.GetText();
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var n = text[0];
        var pc = n switch
        {
            'C' or 'c' => 0,
            'D' or 'd' => 2,
            'E' or 'e' => 4,
            'F' or 'f' => 5,
            'G' or 'g' => 7,
            'A' or 'a' => 9,
            'B' or 'b' => 11,
            _ => 0
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

    private int? _alteredFifth;
    private int? _alteredNinth;
    private int? _alteredEleventh;
    private int? _alteredThirteenth;

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
            _triad = TriadQuality.Major;
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
                _triad = TriadQuality.Major;
                _explicitMajor = true;
                break;
            case "min":
            case "minor":
            case "m":
            case "-":
                _triad = TriadQuality.Minor;
                _explicitMinor = true;
                break;
            case "dim":
            case "o":
            case "°":
                _triad = TriadQuality.Diminished;
                break;
            case "aug":
            case "+":
                _triad = TriadQuality.Augmented;
                break;
            case "sus":
                _triad = TriadQuality.Sus4;
                SusPending = true;
                break;
            case "ø":
            case "halfdim":
                _triad = TriadQuality.Diminished;
                _extension = Math.Max(_extension ?? 0, 7);
                _alteredFifth = 6;
                // half-diminished has a minor seventh
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
                _alteredFifth = 8;
                _alteredNinth = 13;
                break;
        }
    }

    public void ApplyExtension(int n)
    {
        if (n == 0)
            throw new ChordSymbolParseException("Extension 0 is not a valid chord extension.");

        _extension = Math.Max(_extension ?? 0, n);
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

    public void ApplyAlteration(string accidental, int degree)
    {
        var delta = accidental.Contains('#') ? 1 : -1;
        var semitones = MapExtensionDegreeToSemitones(degree) + delta;

        switch (degree)
        {
            case 5:
                _alteredFifth = semitones;
                break;
            case 9:
                _alteredNinth = semitones;
                break;
            case 11:
                _alteredEleventh = semitones;
                break;
            case 13:
                _alteredThirteenth = semitones;
                break;
            default:
                throw new ChordSymbolParseException($"Unsupported altered degree: {accidental}{degree} (expected 5, 9, 11 or 13).");
        }
    }

    public List<int> BuildIntervals()
    {
        var intervals = new HashSet<int> { 0 };

        if (_power)
        {
            if (!_omit5)
                intervals.Add(_alteredFifth ?? 7);
            AddExtensionsAndAdds(intervals);
            return [.. intervals.OrderBy(x => x)];
        }

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

        if (!_omit3)
            intervals.Add(third);

        if (!_omit5)
            intervals.Add(_alteredFifth ?? fifth);

        AddExtensionsAndAdds(intervals);

        // Apply 5th alteration after base build too (e.g., C7(b5)).
        if (_alteredFifth.HasValue && !_omit5)
        {
            intervals.Remove(7);
            intervals.Remove(6);
            intervals.Remove(8);
            intervals.Add(_alteredFifth.Value);
        }

        if (_alteredNinth.HasValue)
        {
            intervals.Remove(14);
            intervals.Add(_alteredNinth.Value);
        }

        if (_alteredEleventh.HasValue)
        {
            intervals.Remove(17);
            intervals.Add(_alteredEleventh.Value);
        }

        if (_alteredThirteenth.HasValue)
        {
            intervals.Remove(21);
            intervals.Add(_alteredThirteenth.Value);
        }

        return [.. intervals.OrderBy(x => x)];
    }

    private void AddExtensionsAndAdds(HashSet<int> intervals)
    {
        // A bare Δ or maj-after-minor ("CΔ", "CmΔ", "Cmmaj") marks the major seventh
        // without an explicit extension: default the extension to 7 so the seventh is
        // actually emitted instead of collapsing to a plain triad.
        var extension = _extension ?? (_wantsMajorSeventh ? 7 : (int?)null);

        if (extension.HasValue)
        {
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

        foreach (var add in _adds)
            intervals.Add(add);
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
            TriadQuality.Diminished when ext == 7 && !_alteredFifth.HasValue => 9,
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
