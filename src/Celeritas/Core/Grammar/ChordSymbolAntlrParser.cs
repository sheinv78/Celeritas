// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Antlr4.Runtime;
using Celeritas.Core.Grammar;

namespace Celeritas.Core;

/// <summary>
/// ANTLR-based chord symbol parser.
/// Supports: root note + accidentals, qualities, extensions, alterations, add/omit, slash bass, and simple polychords.
/// </summary>
public static class ChordSymbolAntlrParser
{
    /// <summary>
    /// Parse a chord symbol into MIDI pitches (octave 4 root = C4/60).
    /// For slash chords, bass is placed at octave 3 (C3/48).
    /// For polychords ("C|G"), subsequent layers are placed one octave higher.
    /// </summary>
    public static int[] ParsePitches(string input)
    {
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

        if (string.IsNullOrWhiteSpace(input))
        {
            errors = Array.Empty<string>();
            return true;
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

        var visitor = new ChordSymbolVisitorImpl();
        pitches = visitor.Visit(tree);
        errors = Array.Empty<string>();
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

            if (!int.TryParse(s.Slice(start, j - start), out var degree))
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
            builder.ApplyQuality(q.GetText());
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

            var n = int.Parse(extText);

            // Special-case: "sus2" / "sus4" is often written as SUS + 2/4.
            if (builder.SusPending && (n == 2 || n == 4))
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
            var num = new string(altText.Where(char.IsDigit).ToArray());
            if (num.Length > 0)
                builder.ApplyAlteration(accidental, int.Parse(num));
            return;
        }

        if (suffix.addTone() is { } add)
        {
            // add9, add2, add11...
            var num = new string(add.GetText().Where(char.IsDigit).ToArray());
            if (num.Length > 0)
                builder.ApplyAdd(int.Parse(num));
            return;
        }

        if (suffix.omitTone() is { } omit)
        {
            // no3, omit5...
            var num = new string(omit.GetText().Where(char.IsDigit).ToArray());
            if (num.Length > 0)
                builder.ApplyOmit(int.Parse(num));
            return;
        }

        if (suffix.modifier() is { } m)
        {
            builder.ApplyModifier(m.GetText());
            return;
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
                    var addNum = new string(add.GetText().Where(char.IsDigit).ToArray());
                    if (addNum.Length > 0)
                        builder.ApplyAdd(int.Parse(addNum));
                    return;
                case ChordSymbolParser.OmitToneContext omit:
                    // Supports: omit3 / no3
                    var omitText = omit.GetText().ToLowerInvariant();
                    var omitNum = new string(omitText.Where(char.IsDigit).ToArray());
                    if (omitNum.Length > 0)
                        builder.ApplyOmit(int.Parse(omitNum));
                    return;
                case ChordSymbolParser.AlterationContext alt:
                    var altText = alt.GetText();
                    var accidental = altText.StartsWith("#", StringComparison.Ordinal) ? "#" : "b";
                    var num = new string(altText.Where(char.IsDigit).ToArray());
                    if (num.Length > 0)
                        builder.ApplyAlteration(accidental, int.Parse(num));
                    return;
                case ChordSymbolParser.ExtensionContext ext:
                    var extText = ext.GetText();
                    if (extText is "6/9" or "69")
                    {
                        builder.ApplySixNine();
                        continue;
                    }
                    builder.ApplyExtension(int.Parse(extText));
                    continue;
                case ChordSymbolParser.QualityContext q:
                    // Allows things like m(maj7) or (Δ9)
                    var qText = q.GetText();
                    if (builder.IsMinorTriad && (string.Equals(qText, "maj", StringComparison.OrdinalIgnoreCase) || string.Equals(qText, "major", StringComparison.OrdinalIgnoreCase) || qText == "Δ"))
                    {
                        builder.MarkMajorSeventh();
                        continue;
                    }
                    builder.ApplyQuality(qText);
                    continue;
            }
        }
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

        // Remaining characters are accidentals.
        for (var i = 1; i < text.Length; i++)
        {
            pc += text[i] switch
            {
                '#' => 1,
                'b' => -1,
                '-' => -1,
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

    private readonly HashSet<int> _adds = new();

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
        if (t is "Δ" or "△")
        {
            _explicitMajor = true;
            _wantsMajorSeventh = true;
            return;
        }

        t = t.ToLowerInvariant();

        switch (t)
        {
            case "maj":
            case "major":
                _triad = TriadQuality.Major;
                _explicitMajor = true;
                _wantsMajorSeventh = true;
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
        _extension = Math.Max(_extension ?? 0, n);
    }

    public void ApplyAdd(int n)
    {
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
        if (_extension.HasValue)
        {
            var ext = _extension.Value;

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

        // Diminished: if explicitly dim7, use diminished 7th (9 semitones); otherwise minor 7th.
        if (_triad == TriadQuality.Diminished && ext == 7 && !_alteredFifth.HasValue)
            return 9;

        return 10; // dominant/minor seventh
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
