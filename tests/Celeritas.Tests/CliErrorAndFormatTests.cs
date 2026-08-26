// Copyright (c) 2025 Vladimir V. Shein

using System.Reflection;
using Celeritas.CLI;
using Celeritas.Core;

namespace Celeritas.Tests;

/// <summary>
/// What the CLI does when it is given nothing, given nonsense, or asked for a report format the
/// other tests never request. These are the paths a user actually hits by mistake, and each one
/// has to end in a readable message and a non-zero exit rather than a stack trace.
/// </summary>
[Collection(nameof(CliCommandTests))]
public class CliErrorAndFormatTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-clierr").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static (int ExitCode, string Output) Run(params string[] args)
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
            var result = entryPoint.Invoke(null, [args]);
            return (result is int code ? code : 0, captured.ToString());
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return (-1, captured + Environment.NewLine + ex.InnerException);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private string MidiFileOf(string notation)
    {
        var path = Path.Combine(_work, $"{Guid.NewGuid():N}.mid");
        Assert.Equal(0, Run("midi", "export", "--out", path, "--notes", notation).ExitCode);
        return path;
    }

    // ---------- nothing to work on ----------

    public static TheoryData<string[]> EmptyInputs =>
    [
        ["analyze", "--notes", ""],
        ["keydetect", "--notes", ""],
        ["voicelead", "--chords", ""],
        ["mode", "--notes", ""],
        ["polyphony", "--notes", ""],
        ["melody", "--notes", ""],
        ["progression", "--chords", ""],
    ];

    [Theory]
    [MemberData(nameof(EmptyInputs))]
    public void ACommandGivenNothing_SaysHowToUseIt(string[] args)
    {
        var (exit, output) = Run(args);

        Assert.NotEqual(0, exit);
        Assert.Contains("Error:", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    // ---------- nonsense to work on ----------

    [Fact]
    public void KeyDetect_GivenUnparsableNotes_SaysSo()
    {
        var (exit, output) = Run("keydetect", "--notes", "Zzz Qqq");

        Assert.NotEqual(0, exit);
        Assert.Contains("Invalid note notation: Zzz", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Melody_GivenUnparsableNotes_SaysSo()
    {
        var (exit, output) = Run("melody", "--notes", "Zzz Qqq");

        Assert.NotEqual(0, exit);
        Assert.Contains("Invalid note notation: Zzz", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Polyphony_GivenUnparsableNotation_SaysWhatItCouldNotRead()
    {
        var (exit, output) = Run("polyphony", "--notes", "!!! not notation !!!");

        Assert.NotEqual(0, exit);
        Assert.Contains("Could not parse music notation", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_GivenOneUnparsableNote_WarnsAndCarriesOn()
    {
        var (exit, output) = Run("mode", "--notes", "D4 E4 Zzz F4 G4 A4 B4 C5");

        Assert.Equal(0, exit);
        Assert.Contains("Could not parse 'Zzz'", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_AcceptsBarePitchClasses()
    {
        // No octave numbers: the CLI supplies one, because mode detection ignores octave.
        var (exit, output) = Run("mode", "--notes", "D E F G A B C");

        Assert.Equal(0, exit);
        Assert.Contains("MODE", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mode_AcceptsMidiNumbers()
    {
        var (exit, output) = Run("mode", "--notes", "62 64 65 67 69 71 72");

        Assert.Equal(0, exit);
        Assert.Contains("MODE", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VoiceLead_GivenAnUnparsableChord_SaysSoAndFails()
    {
        // "I could not read these chords" and "these chords have no valid voice leading" are
        // different answers, and they used to share exit code 0 — a script could not tell a
        // typo from a musical verdict.
        var (exit, output) = Run("voicelead", "--chords", "Czz Qqq");

        Assert.NotEqual(0, exit);
        Assert.Contains("could not be read", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VoiceLead_GivenChordsItCanRead_StillReportsItsVerdict()
    {
        var (exit, output) = Run("voicelead", "--chords", "C", "F", "G", "C");

        Assert.Equal(0, exit);
        Assert.Contains("VOICE LEADING", output, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- rhythm ----------

    [Fact]
    public void Rhythm_GivenABadMeter_SaysWhatItExpected()
    {
        var (exit, output) = Run("rhythm", "--durations", "1/4 1/4", "--meter", "seven");

        Assert.NotEqual(0, exit);
        Assert.Contains("Invalid meter", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Rhythm_GivenABadDuration_SaysWhatItExpected()
    {
        var (exit, output) = Run("rhythm", "--durations", "1/4 quaver");

        Assert.NotEqual(0, exit);
        Assert.Contains("Invalid duration", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("classical")]
    [InlineData("jazz")]
    [InlineData("rock")]
    [InlineData("latin")]
    [InlineData("waltz")]
    public void Rhythm_WithNoDurations_DemonstratesTheStyleModel(string style)
    {
        var (exit, output) = Run("rhythm", "--style", style);

        Assert.Equal(0, exit);
        Assert.Contains("STYLE", output, StringComparison.Ordinal);
        Assert.Contains("Model Statistics", output, StringComparison.Ordinal);
        Assert.Contains("Generated rhythm", output, StringComparison.Ordinal);
    }

    // ---------- transpose ----------

    [Fact]
    public void Transpose_WithADelay_PrintsEachNoteInTurn()
    {
        var (exit, output) = Run("transpose", "--notes", "C4 E4 G4", "--semitones", "2", "--delay", "1");

        Assert.Equal(0, exit);
        Assert.Contains("Delay: 1 ms between notes", output, StringComparison.Ordinal);
    }

    // ---------- progression reports ----------

    [Fact]
    public void Progression_InHarmonicMinor_SaysSo()
    {
        var (exit, output) = Run("progression", "--chords", "Am,B7,E,Am");

        Assert.Equal(0, exit);
        Assert.Contains("HARMONIC MINOR", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_InMelodicMinor_SaysSo()
    {
        var (exit, output) = Run("progression", "--chords", "Am,Dm,G#m7b5,Am");

        Assert.Equal(0, exit);
        Assert.Contains("MELODIC MINOR", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_WithBorrowedChords_SaysSo()
    {
        var (exit, output) = Run("progression", "--chords", "C,Fm,C,G");

        Assert.Equal(0, exit);
        Assert.Contains("MODAL MIXTURE", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_ThatModulates_PrintsEveryModulation()
    {
        var (exit, output) = Run(
            "progression", "--chords", "C,F,G,C,Am,D,G,D,G,C,G");

        Assert.Equal(0, exit);
        Assert.Contains("MODULATION", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Relationship:", output, StringComparison.Ordinal);
        Assert.Contains("Pivot:", output, StringComparison.Ordinal);
    }

    // ---------- midi analyze formats ----------

    [Fact]
    public void MidiAnalyze_SummaryFormat_PrintsTheHeadlineNumbers()
    {
        var path = MidiFileOf("4/4: [C4 E4 G4]/2 [C4 E4 G4]/2 [F4 A4 C5]/2 [G4 B4 D5]/2");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "summary");

        Assert.Equal(0, exit);
        Assert.Contains("Analysis Summary", output, StringComparison.Ordinal);
        Assert.Contains("Chromatic notes:", output, StringComparison.Ordinal);
        Assert.Contains("Modal turns", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Melodic harmony:", output, StringComparison.Ordinal);
        Assert.Contains("Chord Timeline (first 5)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiAnalyze_SummaryOfAChromaticPiece_ListsTheChromaticNotes()
    {
        var path = MidiFileOf("4/4: C4/4 C#4/4 D4/4 D#4/4 E4/4 F4/4 F#4/4 G4/4");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "summary");

        Assert.Equal(0, exit);
        Assert.Contains("Chromatic Notes (top 10)", output, StringComparison.Ordinal);
        Assert.Contains("(b2)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiAnalyze_TimelineFormat_PrintsEventsInTime()
    {
        // A chromatic line: every note outside C major becomes a timeline event.
        var path = MidiFileOf("4/4: C4/4 C#4/4 D4/4 D#4/4 E4/4 F4/4 F#4/4 G4/4");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "timeline");

        Assert.Equal(0, exit);
        Assert.Contains("Timeline (top 50 events)", output, StringComparison.Ordinal);
        Assert.Contains("Beat", output, StringComparison.Ordinal);
        Assert.Contains("Chromatic:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiAnalyze_TimelineOfAPlainDiatonicPiece_HasNothingToShow()
    {
        var path = MidiFileOf("4/4: [C4 E4 G4]/2 [C4 E4 G4]/2 [F4 A4 C5]/2 [G4 B4 D5]/2");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "timeline");

        Assert.Equal(0, exit);
        Assert.Contains("Timeline (top 50 events)", output, StringComparison.Ordinal);
        Assert.Contains("none", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiAnalyze_AnUnknownFormat_ListsTheOnesItKnows()
    {
        var path = MidiFileOf("4/4: C4/4 E4/4 G4/4 C5/4");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "interpretive-dance");

        Assert.NotEqual(0, exit);
        Assert.Contains("sections, summary, timeline", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sections")]
    [InlineData("summary")]
    [InlineData("timeline")]
    public void MidiAnalyze_EveryFormatWorksOnAChromaticPiece(string format)
    {
        // Chromatic notes and borrowed chords, so the colour sections have something to print.
        var path = MidiFileOf("4/4: [C4 E4 G4]/4 [C4 Eb4 G4]/4 [Db4 F4 Ab4]/4 [C4 E4 G4]/4");

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", format);

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
        Assert.DoesNotContain("Unhandled exception", output, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- midi file errors ----------

    [Fact]
    public void MidiImport_MissingFile_SaysWhichOne()
    {
        var missing = Path.Combine(_work, "nope.mid");

        var (exit, output) = Run("midi", "import", "--in", missing);

        Assert.NotEqual(0, exit);
        Assert.Contains("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiTranspose_MissingFile_SaysWhichOne()
    {
        var (exit, output) = Run(
            "midi", "transpose",
            "--in", Path.Combine(_work, "nope.mid"),
            "--out", Path.Combine(_work, "out.mid"),
            "--semitones", "2");

        Assert.NotEqual(0, exit);
        Assert.Contains("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiAnalyze_MissingFile_SaysWhichOne()
    {
        var (exit, output) = Run("midi", "analyze", "--in", Path.Combine(_work, "nope.mid"));

        Assert.NotEqual(0, exit);
        Assert.Contains("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiTranspose_OffTheEndOfTheKeyboard_IsRefused()
    {
        var source = MidiFileOf("4/4: C4/4 E4/4 G4/4 C5/4");

        var (exit, output) = Run(
            "midi", "transpose", "--in", source,
            "--out", Path.Combine(_work, "way-up.mid"), "--semitones", "90");

        Assert.NotEqual(0, exit);
        Assert.Contains("0-127", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiExport_WithoutNotes_SaysHowToUseIt()
    {
        var (exit, output) = Run("midi", "export", "--out", Path.Combine(_work, "x.mid"), "--notes", "");

        Assert.NotEqual(0, exit);
        Assert.Contains("No valid notes", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiExport_WithUnparsableNotation_SaysWhatItCouldNotRead()
    {
        var (exit, output) = Run(
            "midi", "export", "--out", Path.Combine(_work, "x.mid"), "--notes", "!!! nonsense !!!");

        Assert.NotEqual(0, exit);
        Assert.Contains("Could not parse music notation", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MidiExport_OfNothingButRests_SaysThereAreNoNotes()
    {
        var (exit, output) = Run(
            "midi", "export", "--out", Path.Combine(_work, "rests.mid"), "--notes", "4/4: R/4 R/4 R/2");

        Assert.NotEqual(0, exit);
        Assert.Contains("No valid notes", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiImport_OfALongFile_SaysHowManyItDidNotPrint()
    {
        var notation = "4/4: " + string.Join(" ", Enumerable.Range(0, 40).Select(i => $"{MusicNotation.ToNotation(48 + (i % 24))}/8"));
        var path = MidiFileOf(notation);

        var (exit, output) = Run("midi", "import", "--in", path, "--limit", "5");

        Assert.Equal(0, exit);
        Assert.Contains("more)", output, StringComparison.Ordinal);
    }
    // ---------- the remaining report branches ----------

    [Fact]
    public void Analyze_OfACluster_SaysItDoesNotRecogniseTheChord()
    {
        var (exit, output) = Run("analyze", "--notes", "C4 C#4 D4 D#4");

        Assert.Equal(0, exit);
        Assert.Contains("Chord not recognized", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_WithADirectModulation_NamesTheKind()
    {
        var (exit, output) = Run("progression", "--chords", "C,F,C,Am,D,G,Em,D,G");

        Assert.Equal(0, exit);
        Assert.Contains("DIRECT MODULATION", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Polyphony_OfCleanCounterpoint_IsRatedWell()
    {
        var (exit, output) = Run("polyphony", "--notes", "4/4: [C4 E4]/4 [B3 F4]/4 [C4 E4]/4 [B3 F4]/4");

        Assert.Equal(0, exit);
        Assert.Contains("QUALITY", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Polyphony_OfParallelFifths_IsRatedWorse()
    {
        var (exit, output) = Run("polyphony", "--notes", "4/4: [C4 G4]/4 [D4 A4]/4 [E4 B4]/4 [F4 C5]/4");

        Assert.Equal(0, exit);
        Assert.Contains("Parallel Fifths", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Polyphony_OfALongLine_TruncatesWhatItPrints()
    {
        var notes = "4/4: " + string.Join(" ", Enumerable.Range(0, 30).Select(i => $"{MusicNotation.ToNotation(48 + (i % 24))}/8"));

        var (exit, output) = Run("polyphony", "--notes", notes);

        Assert.Equal(0, exit);
        Assert.Contains("...", output, StringComparison.Ordinal);
    }

    // ---------- modal turns in every format ----------

    // Enough C major to keep the detected key there, then a flat seventh: the window with the
    // B flat in it fits C Mixolydian better than C major, which is what a modal turn is.
    private const string MixolydianRun =
        "4/4: [C4 E4 G4]/4 [C4 E4 G4]/4 [F4 A4 C5]/4 [G4 B4 D5]/4 "
        + "[C4 E4 G4]/4 [Bb3 D4 F4]/4 [F4 A4 C5]/4 [C4 E4 G4]/4";

    [Theory]
    [InlineData("sections")]
    [InlineData("summary")]
    [InlineData("timeline")]
    public void MidiAnalyze_ReportsAModalTurnInEveryFormat(string format)
    {
        // Every pitch fits C Mixolydian; only the B flat falls outside C major.
        var path = MidiFileOf(MixolydianRun);

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", format);

        Assert.Equal(0, exit);
        Assert.Contains("Mixolydian", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A#", output, StringComparison.Ordinal);   // the out-of-key pitch class
    }
    // ---------- pcset and its optional Forte catalog ----------

    [Fact]
    public void PcSet_WithNoNotes_SaysHowToUseIt()
    {
        var (exit, output) = Run("pcset", "--notes", " ");

        Assert.NotEqual(0, exit);
        Assert.Contains("celeritas pcset", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PcSet_WithAMissingCatalog_SaysTheCatalogIsMissing()
    {
        var (exit, output) = Run(
            "pcset", "--notes", "C4 E4 G4", "--catalog", Path.Combine(_work, "nope.json"));

        Assert.Equal(0, exit);
        Assert.Contains("catalog file not found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PcSet_WithACatalogHoldingTheSet_NamesIt()
    {
        var catalog = Path.Combine(_work, "forte.json");
        File.WriteAllText(catalog, """
            [ { "forte": "3-11", "primeForm": [0,3,7], "name": "Major/Minor Triad" } ]
            """);

        var (exit, output) = Run("pcset", "--notes", "C4 E4 G4", "--catalog", catalog);

        Assert.Equal(0, exit);
        Assert.Contains("Forte: 3-11", output, StringComparison.Ordinal);
        Assert.Contains("Major/Minor Triad", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PcSet_WithACatalogThatDoesNotHoldTheSet_SaysSo()
    {
        var catalog = Path.Combine(_work, "sparse.json");
        File.WriteAllText(catalog, """[ { "forte": "4-1", "primeForm": [0,1,2,3] } ]""");

        var (exit, output) = Run("pcset", "--notes", "C4 E4 G4", "--catalog", catalog);

        Assert.Equal(0, exit);
        Assert.Contains("not found in catalog", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PcSet_WithAnUnreadableCatalog_SaysSoAndFails()
    {
        // The catalog was asked for by name, so failing to read it is a failure of the run,
        // not a footnote under a successful one: this printed the reason and still exited 0,
        // so a script asking for Forte labels got none and a success code.
        var catalog = Path.Combine(_work, "broken.json");
        File.WriteAllText(catalog, "{ this is not a catalog");

        var (exit, output) = Run("pcset", "--notes", "C4 E4 G4", "--catalog", catalog);

        Assert.NotEqual(0, exit);
        Assert.Contains("catalog could not be read", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mode_WithNoNoteItCanRead_SaysSoRatherThanNamingOne()
    {
        // It printed "Detected: C Major", a scale and a character for input that held no music.
        // The 0 % beside them is easy to read past, and the prose reads as fact.
        var (exit, output) = Run("mode", "--notes", "Zzz Qqq");

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("Detected:", output, StringComparison.Ordinal);
    }

    // ---------- musicxml ----------

    [Fact]
    public void MusicXmlConvert_MissingInput_SaysWhichOne()
    {
        var (exit, output) = Run(
            "musicxml", "convert",
            "--in", Path.Combine(_work, "nope.musicxml"),
            "--out", Path.Combine(_work, "out.mid"));

        Assert.NotEqual(0, exit);
        Assert.Contains("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MusicXmlConvert_BetweenTwoFormatsItDoesNotKnow_SaysSo()
    {
        var midi = MidiFileOf("4/4: C4/4 E4/4 G4/4 C5/4");

        var (exit, output) = Run(
            "musicxml", "convert", "--in", midi, "--out", Path.Combine(_work, "copy.mid"));

        Assert.NotEqual(0, exit);
        Assert.Contains("Unsupported conversion", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MusicXmlAnalyze_MissingInput_SaysWhichOne()
    {
        var (exit, output) = Run("musicxml", "analyze", "--in", Path.Combine(_work, "nope.musicxml"));

        Assert.NotEqual(0, exit);
        Assert.Contains("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MusicXmlAnalyze_OfAScoreWithNoNotes_SaysItIsEmpty()
    {
        var path = Path.Combine(_work, "empty.musicxml");
        File.WriteAllText(path, """
            <score-partwise version="4.0">
              <part-list><score-part id="P1"/></part-list>
              <part id="P1"><measure number="1"><attributes><divisions>1</divisions></attributes></measure></part>
            </score-partwise>
            """);

        var (exit, output) = Run("musicxml", "analyze", "--in", path);

        Assert.Equal(0, exit);
        Assert.Contains("(empty score)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void MusicXmlAnalyze_OfALongScore_SaysHowMuchItDidNotPrint()
    {
        var notation = "4/4: " + string.Join(" ", Enumerable.Range(0, 30).Select(i => $"{MusicNotation.ToNotation(48 + (i % 24))}/8"));
        var midi = MidiFileOf(notation);
        var xml = Path.Combine(_work, "long.musicxml");
        Assert.Equal(0, Run("musicxml", "convert", "--in", midi, "--out", xml).ExitCode);

        var (exit, output) = Run("musicxml", "analyze", "--in", xml);

        Assert.Equal(0, exit);
        Assert.Contains("more", output, StringComparison.Ordinal);
    }

    // ---------- note tokens ----------

    [Fact]
    public void ANoteNumberOffTheKeyboard_IsRefusedByName()
    {
        var (exit, output) = Run("analyze", "--notes", "200");

        Assert.NotEqual(0, exit);
        Assert.Contains("outside the valid MIDI range 0-127", output, StringComparison.Ordinal);
    }

    // Note: polyphony and `midi export` read their --notes option without the list expander,
    // so their "no notes provided" branches need an empty option array, which the parser never
    // produces for a required option. Their reachable failure is "No valid notes parsed.",
    // asserted above.

    [Fact]
    public void Transpose_WithNoNotes_SaysHowToUseIt()
    {
        var (exit, output) = Run("transpose", "--semitones", "2", "--notes", " ");

        Assert.NotEqual(0, exit);
        Assert.Contains("celeritas transpose", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Melody_OfALongLine_SaysHowManyIntervalsItDidNotPrint()
    {
        var notes = string.Join(" ", Enumerable.Range(0, 20).Select(i => MusicNotation.ToNotation(60 + (i % 12))));

        var (exit, output) = Run("melody", "--notes", notes);

        Assert.Equal(0, exit);
        Assert.Contains("more", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_WithASymbolItCannotRead_SaysSoRatherThanAnalysingTheRest()
    {
        // The unreadable symbol used to be dropped and the report numbered the chords it kept
        // from 1, so "C Zzz G" was answered as "I - V" — an analysis of a progression the user
        // never typed, with nothing on screen to say a chord had gone missing.
        var (exit, output) = Run("progression", "--chords", "C", "Zzz", "G");

        Assert.NotEqual(0, exit);
        Assert.Contains("Zzz", output, StringComparison.Ordinal);
        Assert.DoesNotContain("I - V", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_WithChordsItCanRead_StillAnalysesThem()
    {
        var (exit, output) = Run("progression", "--chords", "C", "F", "G");

        Assert.Equal(0, exit);
        Assert.Contains("I - IV - V", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("200")]
    public void Mode_WithANumberThatIsNotAMidiNote_SaysSoRatherThanCrashing(string token)
    {
        // "-5" indexed the pitch distribution at -5 and came out of RunGuarded as an unhandled
        // IndexOutOfRangeException with a stack trace; "200" quietly counted as a G#.
        var (exit, output) = Run("mode", "--notes", token, "0", "2");

        Assert.NotEqual(0, exit);
        Assert.Contains("0 to 127", output, StringComparison.Ordinal);
        Assert.DoesNotContain("IndexOutOfRange", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_WithNotesItCanRead_StillDetectsOne()
    {
        var (exit, output) = Run("mode", "--notes", "D", "E", "F", "G", "A", "B", "C");

        Assert.Equal(0, exit);
        Assert.Contains("Dorian", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Rhythm_WithAStyleThereIsNoModelFor_SaysSoRatherThanUsingAnother()
    {
        // It used the classical model and labelled the output with the style that was asked
        // for: "Generated measure (bebop-polka style)" over rhythms trained on Bach.
        var (exit, output) = Run("rhythm", "--durations", "1/4", "1/4", "--style", "bebop-polka");

        Assert.NotEqual(0, exit);
        Assert.Contains("bebop-polka", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ToAPathThatCannotBeWritten_ReportsItRatherThanCrashing()
    {
        // An existing directory as --out raised UnauthorizedAccessException, which is not an
        // IOException and so escaped the guard that turns these into a one-line message.
        var (exit, output) = Run("midi", "export", "--notes", "C4 E4 G4", "--out", _work);

        Assert.NotEqual(0, exit);
        Assert.StartsWith("Error:", output.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("at System.", output, StringComparison.Ordinal);
    }
}
