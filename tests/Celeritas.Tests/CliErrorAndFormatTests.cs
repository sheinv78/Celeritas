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
    public void VoiceLead_GivenAnUnparsableChord_ReportsNoValidVoiceLeading()
    {
        var (exit, output) = Run("voicelead", "--chords", "Czz Qqq");

        Assert.Equal(0, exit);
        Assert.Contains("No valid voice leading", output, StringComparison.OrdinalIgnoreCase);
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
}
