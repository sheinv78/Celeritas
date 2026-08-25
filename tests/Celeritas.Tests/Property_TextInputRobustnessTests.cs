// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Everything here takes text a user typed. Whatever the text, the answer must be either a
/// result or one of the exceptions the method documents — never an IndexOutOfRange or a
/// NullReference from somewhere inside, which is what a caller cannot handle and what turns a
/// bad chord symbol into a crashed application.
/// </summary>
public class PropertyTextInputRobustnessTests
{
    /// <summary>Text that leans on the shapes these parsers look for, plus noise.</summary>
    private static Gen<string> Symbolish =>
        Gen.OneOf(
            Gen.Const(""),
            Gen.Const(" "),
            Gen.Char[' ', '~'].Array[1, 12].Select(cs => new string(cs)),
            Gen.Char["ABCDEFGabcdefg#b0123456789majinsudo/(),+-"].Array[1, 12].Select(cs => new string(cs)),
            Gen.Char["CDEFGAB"].Array[1, 4].Select(cs => new string(cs)));

    private static bool OnlyDocumentedFailures(Action act, params Type[] allowed)
    {
        try
        {
            act();
            return true;
        }
        catch (Exception ex) when (allowed.Any(t => t.IsInstanceOfType(ex)))
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------- chord symbols ----------

    [Fact]
    public void ParsingAChordSymbol_EitherWorksOrComesBackEmpty()
    {
        Symbolish.Sample(text =>
        {
            int[]? pitches = null;

            var behaved = OnlyDocumentedFailures(
                () => pitches = ProgressionAdvisor.ParseChordSymbol(text));

            return behaved
                && pitches is not null
                && pitches.All(p => p is >= 0 and <= 127);
        }, iter: 1000);
    }

    [Fact]
    public void TryParsingAChordSymbol_NeverThrows()
    {
        Symbolish.Sample(text =>
        {
            var behaved = OnlyDocumentedFailures(() =>
            {
                ProgressionAdvisor.TryParseChordSymbol(text, out var pitches, out var errors);

                if (pitches.Any(p => p is < 0 or > 127))
                    throw new InvalidOperationException("a parsed pitch left the keyboard");

                if (errors is null)
                    throw new InvalidOperationException("errors was null");
            });

            return behaved;
        }, iter: 1000);
    }

    [Fact]
    public void ClassifyingAChordSymbol_AlwaysAnswersSomething()
    {
        Symbolish.Sample(text =>
        {
            ChordCharacterClassification? classification = null;

            var behaved = OnlyDocumentedFailures(
                () => classification = ChordCharacterClassifier.Classify(text));

            return behaved
                && classification is not null
                && classification.Stability is >= 0f and <= 1f
                && classification.Brightness is >= 0f and <= 1f;
        }, iter: 1000);
    }

    // ---------- progressions of them ----------

    [Fact]
    public void AnalyzingAProgressionOfAnyText_NeverFailsUndocumented()
    {
        Symbolish.Array[0, 6].Sample(symbols =>
            OnlyDocumentedFailures(
                () =>
                {
                    var report = ProgressionAdvisor.Analyze(symbols);

                    if (report.Key.Root > 11)
                        throw new InvalidOperationException("the key left the octave");
                },
                typeof(ArgumentException)),
            iter: 500);
    }

    [Fact]
    public void SuggestingTheNextChordForAnyText_NeverFailsUndocumented()
    {
        Symbolish.Array[0, 6].Sample(symbols =>
            OnlyDocumentedFailures(
                () =>
                {
                    var suggestions = ProgressionAdvisor.SuggestNext(symbols);

                    if (suggestions.Any(s => string.IsNullOrWhiteSpace(s.Chord)))
                        throw new InvalidOperationException("suggested a chord with no name");
                },
                typeof(ArgumentException)),
            iter: 500);
    }

    [Fact]
    public void DetectingACadenceInAnyText_NeverFailsUndocumented()
    {
        Symbolish.Array[0, 4].Sample(symbols =>
            OnlyDocumentedFailures(
                () => ProgressionAdvisor.DetectCadence(symbols),
                typeof(ArgumentException)),
            iter: 500);
    }

    // ---------- notation ----------

    [Fact]
    public void ParsingNotationOfAnyText_NeverFailsUndocumented()
    {
        Gen.OneOf(
            Symbolish,
            Gen.Char["CDEFGAB#b0123456789/:. ~[]Rwhqest"].Array[1, 20].Select(cs => new string(cs)))
            .Sample(text =>
                OnlyDocumentedFailures(
                    () =>
                    {
                        var notes = MusicNotation.Parse(text);

                        if (notes.Any(n => n.Pitch is (< 0 and not MusicNotation.RestPitch) or > 127))
                            throw new InvalidOperationException("a parsed note left the keyboard");

                        if (notes.Any(n => n.Duration <= Rational.Zero))
                            throw new InvalidOperationException("a parsed note had no duration");
                    },
                    typeof(ArgumentException), typeof(FormatException), typeof(OverflowException)),
                iter: 1000);
    }

    [Fact]
    public void ParsingANoteOfAnyText_NeverFailsUndocumented()
    {
        Symbolish.Sample(text =>
        {
            var tried = MusicNotation.TryParseNote(text, out var midi);

            return OnlyDocumentedFailures(() =>
            {
                if (tried && midi is < 0 or > 127)
                    throw new InvalidOperationException("TryParseNote accepted a pitch off the keyboard");
            });
        }, iter: 1000);
    }

    [Fact]
    public void ParsingAKeyOfAnyText_NeverFailsUndocumented()
    {
        Symbolish.Sample(text =>
            OnlyDocumentedFailures(
                () =>
                {
                    var key = MusicNotation.ParseKey(text);

                    if (key.Root > 11)
                        throw new InvalidOperationException("the key left the octave");
                },
                typeof(ArgumentException)),
            iter: 1000);
    }

    [Fact]
    public void ParsingADurationOfAnyText_NeverFailsUndocumented()
    {
        Symbolish.Sample(text =>
            OnlyDocumentedFailures(
                () =>
                {
                    var duration = MusicNotation.ParseDuration(text);

                    if (duration <= Rational.Zero)
                        throw new InvalidOperationException("a duration of nothing was accepted");
                },
                typeof(ArgumentException), typeof(FormatException), typeof(OverflowException)),
            iter: 1000);
    }

    // ---------- key detection from text ----------

    [Fact]
    public void DetectingAKeyFromAnyNotation_NeverFailsUndocumented()
    {
        Gen.OneOf(
            Symbolish,
            Gen.Char["CDEFGAB#b0123456789/: R"].Array[1, 20].Select(cs => new string(cs)))
            .Sample(text =>
                OnlyDocumentedFailures(
                    () =>
                    {
                        var result = KeyProfiler.DetectFromPitches(text);

                        if (result.Confidence is < 0f or > 1f)
                            throw new InvalidOperationException("confidence left its range");
                    },
                    typeof(ArgumentException), typeof(FormatException), typeof(OverflowException)),
                iter: 1000);
    }
}
