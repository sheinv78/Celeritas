// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Text;

namespace Celeritas.Core.Analysis;

/// <summary>
/// The figures a chord carries above the seventh chord or triad the library named as its core:
/// the ninth, eleventh and thirteenth, natural or altered, a second fifth, a seventh over a
/// suspension. <see cref="ChordAnalyzer.Identify(ReadOnlySpan{int}, int)"/> names "G7b9" a
/// dominant seventh on G, which gives the roman numeral its case and its function; this writes
/// the rest of what the symbol said after it — "V7(b9)", "V9", "V13(b9)", "V7(b9,#9)",
/// "V7sus4", "ii9", "Imaj9" — the way a musician annotates an extended chord, and leaves a
/// chord the templates name whole exactly as it was.
/// </summary>
/// <remarks>
/// <para>
/// The natural extensions take the place of the seventh's figure, highest wins — a thirteenth
/// chord is written "13", not "7(9,11,13)" — and the alterations follow in parentheses in
/// ascending order: b9, #9, #11 (b5 when the core's fifth is not perfect), b13 (#5 likewise),
/// then a major seventh over a minor one. Over a suspension the seventh's figure precedes the
/// suspension, "V7sus4", "V9sus4"; over a triad it follows the triad's mark, "III+maj7". A chord
/// without a seventh takes its extensions as adds: "I6(add9)" for C6/9 — and the sixth chord is
/// the core there, so the six is not an add.
/// </para>
/// <para>
/// Before this the roman numeral of an extended chord was "?": the report had no quality for
/// it, and so no case, no suffix and no function.
/// </para>
/// </remarks>
internal static class ChordFigures
{
    /// <summary>
    /// <paramref name="label"/> — a roman numeral or Nashville number for <paramref name="info"/>'s
    /// core, beginning with the cased numeral that <paramref name="bareNumeral"/> gives the length
    /// of — with the figures of <paramref name="pitches"/> above that core spliced in. A chord the
    /// templates name whole has no figures and comes back unchanged.
    /// </summary>
    public static string Figure(string label, string bareNumeral, int[] pitches, ChordInfo info)
    {
        if (label.Length < bareNumeral.Length)
            return label;

        var mask = ChordAnalyzer.GetMask(pitches);
        if (!ChordLibrary.TryGetCore(mask, info.RootPitchClass, out var quality, out var coreMask) || coreMask == mask)
            return label;

        var root = info.RootPitchClass;
        var core = Rotate(coreMask, root);
        var extras = Rotate((ushort)(mask & ~coreMask), root);

        var numeral = label[..bareNumeral.Length];
        var suffix = label[bareNumeral.Length..];

        var coreHasSeventh = Has(core, 10) || Has(core, 11) || quality == ChordQuality.Diminished7;
        var hasSeventh = coreHasSeventh || Has(extras, 10) || Has(extras, 11);

        string? extension = null;
        string? seventh = null;
        var inParentheses = new List<string>();

        for (var semitone = 1; semitone < 12; semitone++)
        {
            if (!Has(extras, semitone))
                continue;

            switch (semitone)
            {
                case 1:
                    inParentheses.Add("b9");
                    break;
                case 2:
                    Natural("9", "add9");
                    break;
                case 3:
                    inParentheses.Add("#9");
                    break;
                case 5:
                    Natural("11", "add11");
                    break;
                case 6:
                    inParentheses.Add(Has(core, 7) ? "#11" : "b5");
                    break;
                case 8:
                    inParentheses.Add(Has(core, 7) ? "b13" : "#5");
                    break;
                case 9:
                    Natural("13", "add6");
                    break;
                case 10:
                    seventh = "7";
                    break;
                case 11:
                    if (Has(core, 10) || Has(extras, 10))
                        inParentheses.Add("maj7");
                    else
                        seventh = "maj7";
                    break;
            }
        }

        var result = new StringBuilder(numeral);
        var seventhFigure = suffix.IndexOf('7');

        if (seventhFigure >= 0)
        {
            // A seventh core: the highest natural extension takes the seventh's place — "7" to
            // "9", "maj7" to "maj9", "ø7" to "ø9", "m7b5" to "m9b5".
            result.Append(extension is null ? suffix : suffix[..seventhFigure] + extension + suffix[(seventhFigure + 1)..]);
        }
        else if (seventh is not null)
        {
            var figure = extension is null ? seventh : seventh.Replace("7", extension, StringComparison.Ordinal);
            if (suffix.StartsWith("sus", StringComparison.Ordinal))
                result.Append(figure).Append(suffix);
            else
                result.Append(suffix).Append(figure);
        }
        else
        {
            // No seventh figure to stand in for: a stack of fourths, whose seventh is in the core
            // but not in its label. The extension is named in the parentheses; a core with no
            // seventh at all took its extensions as adds above.
            result.Append(suffix);
            if (extension is not null)
                inParentheses.Insert(0, extension);
        }

        if (inParentheses.Count > 0)
            result.Append('(').Append(string.Join(',', inParentheses)).Append(')');

        return result.ToString();

        void Natural(string figure, string add)
        {
            if (hasSeventh)
                extension = figure;
            else
                inParentheses.Add(add);
        }
    }

    private static bool Has(ushort set, int semitone) => (set & (1 << semitone)) != 0;

    /// <summary>A pitch-class mask as semitones above <paramref name="root"/>.</summary>
    private static ushort Rotate(ushort mask, int root) =>
        (ushort)(((mask >> root) | (mask << (12 - root))) & 0xFFF);
}
