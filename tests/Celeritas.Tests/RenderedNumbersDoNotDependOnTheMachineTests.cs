// Copyright (c) 2025 Vladimir V. Shein

using System.Globalization;
using System.Text.RegularExpressions;
using Celeritas.CLI;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Midi;
using Celeritas.Core.Notation;
using Celeritas.Core.VoiceLeading;

namespace Celeritas.Tests;

/// <summary>
/// Every number this library renders for a human reads the same on every machine. Two renderers
/// did not: under a locale whose decimal separator is a comma,
/// <see cref="VoiceLeadingSolution.ToScore"/> printed "Total voice leading cost: 39,0" and
/// <see cref="KeyCorrelation.ToString"/> printed "C Major: 0,812" — while its own summary
/// promises "C Major: 0.812". The CLI then turned out to read the machine on every line where
/// it formatted a float itself: "Density: 4,00 voices avg", "Confidence: 88 %",
/// "(margin 0,10 over the runner-up)".
/// <para>
/// The examples gate could not have caught this: it deliberately builds with invariant
/// globalization so that it does not depend on the runner's locale, which means it never sees
/// what a caller on such a machine sees. These tests set the comma locale on purpose.
/// </para>
/// <para>
/// The standard covers what is written for a human: a ToString written by hand, a report, a
/// description, a line the CLI prints. A record's compiler-generated ToString — the
/// <c>MeterDetectionResult { Confidence = 0,55, ... }</c> form — is a debug representation and
/// sits outside it: it names members, prints a list as its type name, and is no rendering this
/// library has promised, so it follows the machine the way a debugger view does.
/// </para>
/// </summary>
[Collection(nameof(CliCommandTests))]
public class RenderedNumbersDoNotDependOnTheMachineTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-locale").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
    }

    /// <summary>Runs <paramref name="action"/> under a culture that writes decimals with a comma.</summary>
    private static void UnderACommaLocale(Action action)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal(",", culture.NumberFormat.NumberDecimalSeparator);

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// Runs the CLI through its real entry point under whatever culture is current, and returns
    /// everything it printed. A command that does not exit 0 is a failure here, not a sample:
    /// an error message compared with itself would pass for a rendering.
    /// </summary>
    private static string Cli(params string[] args)
    {
        var entryPoint = typeof(KeyConfidenceDescription).Assembly.EntryPoint
            ?? throw new InvalidOperationException("the CLI assembly has no entry point");

        var originalOut = Console.Out;
        var originalError = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            Console.SetError(captured);
            var exit = entryPoint.Invoke(null, [args]);
            Assert.True(exit is 0, $"celeritas {string.Join(' ', args)} exited {exit}:\n{captured}");
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    /// A passage in C major with a bVII in it, so that the harmonic-colour analyzer finds a
    /// Mixolydian turn and <c>midi analyze</c> prints its "(confidence 0.14)" line, written to
    /// <paramref name="name"/> in the work folder.
    /// </summary>
    private string MidiFixture(string name)
    {
        var path = Path.Combine(_work, name);
        if (!File.Exists(path))
        {
            var notes = MusicNotation.Parse(
                "4/4: [C3 G3 E4 C5]/4 [Bb2 F3 D4 Bb4]/4 [F3 A3 C4 F4]/4 [C3 G3 E4 C5]/4 | " +
                "[C3 G3 E4 C5]/4 [G3 B3 D4 G4]/4 [C3 G3 E4 C5]/4 [C3 G3 E4 C5]/4",
                validateMeasures: false);
            using var buffer = new NoteBuffer(notes.Length);
            foreach (var note in notes)
            {
                buffer.Add(note);
            }

            MidiIo.Export(buffer, path, new MidiExportOptions(Bpm: 97));
        }

        return path;
    }

    private string MusicXmlFixture(string name)
    {
        var path = Path.Combine(_work, name);
        if (!File.Exists(path))
        {
            using var buffer = MidiIo.Import(MidiFixture("for-xml.mid"));
            MusicXmlIo.Export(buffer, path);
        }

        return path;
    }

    /// <summary>
    /// Timings are a property of the machine, not of the library; a run under one locale and a
    /// run under the other will not take the same microseconds. Their format is checked in
    /// <see cref="TheCliWritesItsTimingsWithADot"/> instead.
    /// </summary>
    private static string WithoutTimings(string output) =>
        string.Join('\n', output.Split('\n').Where(l => !l.Contains(" time: ", StringComparison.Ordinal)));

    [Fact]
    public void TheVoiceLeadingScoreWritesItsCostWithADot()
    {
        UnderACommaLocale(() =>
        {
            var solution = new VoiceLeadingSolver().SolveFromSymbols(["C", "F", "G", "C"], 0);
            var score = solution.ToScore();

            var costLine = score.Split('\n').Single(l => l.Contains("Total voice leading cost"));
            Assert.Equal(
                $"Total voice leading cost: {solution.TotalCost.ToString("F1", CultureInfo.InvariantCulture)}",
                costLine.Trim());
            Assert.DoesNotContain(",", costLine);
        });
    }

    [Fact]
    public void AKeyCorrelationWritesItselfAsItsSummaryPromises()
    {
        UnderACommaLocale(() =>
        {
            var correlation = new KeyCorrelation(new KeySignature(0, true), 0.812f);

            Assert.Equal("C Major: 0.812", correlation.ToString());
        });
    }

    [Fact]
    public void AnEmptySolutionIsAnEmptyBufferNotAnError()
    {
        // NoteBuffer refuses a capacity of zero, so a solution with no voicings used to throw
        // ArgumentOutOfRangeException about "capacity" — a parameter the caller never passed.
        var none = new VoiceLeadingSolver().SolveFromSymbols([], 0);
        Assert.Empty(none.Voicings);

        using var buffer = none.ToNoteBuffer(Rational.Quarter);
        Assert.Equal(0, buffer.Count);

        // ...and a solution that could not be found is the same shape.
        var impossible = new VoiceLeadingSolver().SolveFromSymbols(["$$$"], 0);
        Assert.False(impossible.IsValid);
        using var alsoEmpty = impossible.ToNoteBuffer(Rational.Quarter);
        Assert.Equal(0, alsoEmpty.Count);
    }

    [Fact]
    public void TheCliQualifiesAKeyWithADot()
    {
        UnderACommaLocale(() =>
            Assert.Equal("  (margin 0.20 over the runner-up)", KeyConfidenceDescription.Describe(0.2f)));
    }

    [Fact]
    public void TheCliWritesAPercentageAsTheLibraryDoes()
    {
        // {x:P0} was the CLI's percent: "88%" on an English machine, "88 %" on a German one and
        // "88 %" under the invariant culture too, so no culture argument could make it agree
        // with the "confidence: 88%" the library prints for the same number. The CLI now rounds
        // the way the library does, so the two reports of one progression say the same percent.
        UnderACommaLocale(() =>
        {
            var report = ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]).ToFormattedReport();
            var confidence = Regex.Match(report, @"\(confidence: (\d+%)\)").Groups[1].Value;
            Assert.NotEmpty(confidence);

            var output = Cli("progression", "--chords", "Dm7", "G7", "Cmaj7");

            Assert.Contains($"Confidence: {confidence}", output, StringComparison.Ordinal);
            Assert.DoesNotContain(" %", output, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheCliWritesItsTimingsWithADot()
    {
        // These lines cannot go through the sweep below — no two runs take the same
        // microseconds — so their shape is pinned here instead.
        UnderACommaLocale(() =>
        {
            var keydetect = Cli("keydetect", "--notes", "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5");
            Assert.Matches(@"^\s*Analysis time: \d+\.\d µs$", TimingLine(keydetect));

            var voicelead = Cli("voicelead", "--chords", "Dm7", "G7", "Cmaj7");
            Assert.Matches(@"^\s*Solve time: \d+\.\d\d ms$", TimingLine(voicelead));

            var benchmark = Cli("benchmark");
            Assert.Matches(@"Transposed 1,000,000 notes: \d+\.\d\d ms", benchmark);
            Assert.Matches(@"Performance: \d+\.\d\d\d ns/note", benchmark);
            Assert.Matches(@"Throughput: \d+\.\d\d billion notes/sec", benchmark);
        });

        static string TimingLine(string output) =>
            output.Split('\n').Single(l => l.Contains(" time: ", StringComparison.Ordinal)).TrimEnd();
    }

    [Fact]
    public void EveryPublicToStringRendersTheSameUnderBothLocales()
    {
        // The two above were found by hand. This sweeps the rest: every public ToString and
        // description string this library produces, and every line the CLI prints, must be
        // identical under a dot locale and a comma locale, or a renderer somewhere is reading
        // the machine.
        var samples = new List<Func<string>>
        {
            () => KeyProfiler.DetectFromPitches([60, 62, 64, 65, 67, 69, 71]).ToString(),
            () => new KeySignature(7, false).ToString(),
            () => new ModalKey(2, Mode.Dorian).ToString(),
            () => ChordAnalyzer.Identify([60, 64, 67, 70]).ToString(),
            () => new Rational(3, 8).ToString(),
            () => ProgressionAdvisor.Analyze(["C", "Am", "F", "G"]).ToFormattedReport(),
            () => ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"]).Summary,
            () => string.Join("\n", ProgressionAdvisor.Analyze(["C", "F", "G7", "C"]).Chords.Select(c => c.Description)),
            () => new VoiceLeadingSolver().SolveFromSymbols(["Dm7", "G7", "C"], 0).ToScore(),
            () => KeyProfiler.DetectFromPitches([60, 64, 67]).TopKeys(3).Select(k => k.ToString()).Aggregate((a, b) => a + "|" + b),

            // The CLI: every command that writes a fraction or a percentage.
            () => KeyConfidenceDescription.Describe(0.2f) + KeyConfidenceDescription.Describe(0.35f, 7),
            () => Cli("analyze", "--notes", "C4", "D4", "E4", "F4", "G4", "A4", "B4"),
            () => Cli("progression", "--chords", "Dm7", "G7", "Cmaj7", "A7", "Dm7", "G7", "C"),
            () => WithoutTimings(Cli("keydetect", "--notes", "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5")),
            () => WithoutTimings(Cli("voicelead", "--chords", "Dm7", "G7", "Cmaj7")),
            () => Cli("mode", "--notes", "D", "E", "F", "G", "A", "B", "C"),
            () => Cli("polyphony", "--notes", "4/4: [C3 G3 E4 C5]/4 [D3 A3 F4 D5]/4 [G2 G3 D4 B4]/4 [C3 E3 G4 C5]/4 | [F3 A3 C4 F4]/2 [G3 B3 D4 G4]/2"),
            () => Cli("rhythm", "--durations", "1/4", "1/4", "1/8", "1/8", "1/4", "3/8", "1/8", "1/2", "--predict", "4"),
            () => Cli("melody", "--notes", "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5", "D5", "C5", "B4", "A4", "G4", "F#4", "G4", "C4", "D4", "E4", "F4"),
            () => Cli("midi", "info", "--in", MidiFixture("info.mid")),
            () => Cli("midi", "analyze", "--in", MidiFixture("analyze.mid")),
            () => Cli("midi", "analyze", "--in", MidiFixture("analyze.mid"), "--format", "summary"),
            () => Cli("midi", "analyze", "--in", MidiFixture("analyze.mid"), "--format", "timeline"),
            () => Cli("musicxml", "analyze", "--in", MusicXmlFixture("analyze.musicxml")),
        };

        foreach (var sample in samples)
        {
            string underDot;
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                underDot = sample();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            string? underComma = null;
            UnderACommaLocale(() => underComma = sample());

            Assert.Equal(underDot, underComma);
        }
    }

    [Fact]
    public void TheCliFixturesReachEveryLineTheSweepIsFor()
    {
        // The sweep can only catch a renderer it reaches. These pin that the samples above do
        // reach the lines that used to read the machine, so a fixture that stops producing a
        // modal turn, a swing ratio or an average pitch fails here rather than silently
        // narrowing the sweep.
        var midiAnalyze = Cli("midi", "analyze", "--in", MidiFixture("analyze.mid"));
        Assert.Matches(@"\(confidence \d\.\d\d\)", midiAnalyze);

        var polyphony = Cli("polyphony", "--notes", "4/4: [C3 G3 E4 C5]/4 [D3 A3 F4 D5]/4 [G2 G3 D4 B4]/4 [C3 E3 G4 C5]/4 | [F3 A3 C4 F4]/2 [G3 B3 D4 G4]/2");
        Assert.Matches(@"Avg Pitch: \d+\.\d ", polyphony);
        Assert.Matches(@"Density: \d\.\d\d voices avg", polyphony);
        Assert.Matches(@"OVERALL QUALITY: \d+% ", polyphony);

        var rhythm = Cli("rhythm", "--durations", "1/4", "1/4", "1/8", "1/8", "1/4", "3/8", "1/8", "1/2", "--predict", "4");
        Assert.Matches(@"Notes/measure: \d+\.\d", rhythm);
        Assert.Matches(@"Density: \d+\.\d\d notes/beat", rhythm);
        Assert.Matches(@"\(confidence: \d+%\)", rhythm);

        var melody = Cli("melody", "--notes", "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5", "D5", "C5", "B4", "A4", "G4", "F#4", "G4", "C4", "D4", "E4", "F4");
        Assert.Matches(@"Average interval: \d+\.\d semitones", melody);
        Assert.Matches(@"Conjunctness: \d+% ", melody);

        var midiInfo = Cli("midi", "info", "--in", MidiFixture("info.mid"));
        Assert.Matches(@"Duration \(whole notes\): \d+\.\d\d", midiInfo);
    }
}
