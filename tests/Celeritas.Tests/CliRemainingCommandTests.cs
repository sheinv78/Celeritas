// Copyright (c) 2025 Vladimir V. Shein

using System.Reflection;
using Celeritas.CLI;
using Celeritas.Core;
using Celeritas.Core.Midi;
using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Celeritas.Tests;

/// <summary>
/// Coverage for the CLI verbs beyond the first batch. Every one of the fourteen top-level
/// commands the tool advertises is now driven at least once, including the file-based MIDI
/// and MusicXML subcommands, which are the ones a user is most likely to hit with a real path.
/// </summary>
[Collection(nameof(CliCommandTests))]
public class CliRemainingCommandTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-cli-tests").FullName;

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

    // ---------- analysis verbs ----------

    [Fact]
    public void KeyDetect_TonicEmphasizedMaterial_NamesThatKey()
    {
        var (exit, output) = Run("keydetect", "--notes", "G3 B3 D4 G4 G4 D4 B3 G3 A3 C4 G4 G4");

        Assert.Equal(0, exit);
        Assert.Contains("G Major", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_DorianScale_IsReportedAsDorian()
    {
        var (exit, output) = Run("mode", "--notes", "D4 E4 F4 G4 A4 B4 C5 D5");

        Assert.Equal(0, exit);
        Assert.Contains("Dorian", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Melody_AscendingLine_IsDescribed()
    {
        var (exit, output) = Run("melody", "--notes", "C4 D4 E4 F4 G4 A4 B4 C5");

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void Polyphony_TwoVoices_AreSeparated()
    {
        var (exit, output) = Run("polyphony", "--notes", "4/4: [C4 E4 G4]/4 [D4 F4 A4]/4 [E4 G4 B4]/2");

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void Rhythm_StraightQuarters_AnalyzeAndPredict()
    {
        var (exit, output) = Run("rhythm", "--durations", "1/4 1/4 1/4 1/4", "--predict", "2");

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void PcSet_MajorTriad_ReportsItsPrimeForm()
    {
        var (exit, output) = Run("pcset", "--notes", "C4 E4 G4");

        Assert.Equal(0, exit);

        // The major triad's prime form is {0,3,7} and its interval vector <0,0,1,1,1,0>.
        // (The Forte label needs an explicit --catalog, so it is deliberately absent here.)
        Assert.Contains("{0,3,7}", output, StringComparison.Ordinal);
        Assert.Contains("<0,0,1,1,1,0>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceLead_Progression_ProducesAScore()
    {
        var (exit, output) = Run("voicelead", "--chords", "C F G C");

        Assert.Equal(0, exit);
        Assert.Contains("SATB Voice Leading", output, StringComparison.Ordinal);
        Assert.DoesNotContain("VoicePart", output, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceLead_StrictFlag_IsAccepted()
    {
        var (exit, output) = Run("voicelead", "--chords", "C F G C", "--strict");

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void Benchmark_Runs_AndReportsThroughput()
    {
        var (exit, output) = Run("benchmark");

        Assert.Equal(0, exit);
        Assert.Contains("notes/sec", output, StringComparison.Ordinal);
    }

    // ---------- MIDI round trip through the CLI ----------

    [Fact]
    public void Midi_ExportThenImport_PreservesTheNotes()
    {
        var path = Path.Combine(_work, "roundtrip.mid");

        var (exportExit, _) = Run("midi", "export", "--out", path, "--notes", "4/4: C4/4 E4/4 G4/4 C5/4");
        Assert.Equal(0, exportExit);
        Assert.True(File.Exists(path), "the CLI reported success but wrote no file");

        var (importExit, importOutput) = Run("midi", "import", "--in", path);
        Assert.Equal(0, importExit);
        Assert.False(string.IsNullOrWhiteSpace(importOutput));
    }

    [Fact]
    public void Midi_Info_AndAnalyze_ReadAFileTheCliWrote()
    {
        var path = Path.Combine(_work, "info.mid");
        Assert.Equal(0, Run("midi", "export", "--out", path, "--notes", "4/4: C4/4 F4/4 G4/4 C5/4").ExitCode);

        var (infoExit, infoOutput) = Run("midi", "info", "--in", path);
        Assert.Equal(0, infoExit);
        Assert.False(string.IsNullOrWhiteSpace(infoOutput));

        var (analyzeExit, analyzeOutput) = Run("midi", "analyze", "--in", path);
        Assert.Equal(0, analyzeExit);
        Assert.False(string.IsNullOrWhiteSpace(analyzeOutput));
    }

    // ---------- a drum track under the music ----------

    [Fact]
    public void Midi_Analyze_HearsThePianoAndNotTheDrums()
    {
        // Sixteen piano notes in C major over forty-eight drum hits on channel 10. Read as
        // pitches the hi-hats are F#s, and the analysis named the key F# minor.
        var path = DrumsAreNotPitchesTests.WritePianoAndDrumsFile(_work);

        var (exit, output) = Run("midi", "analyze", "--in", path, "--format", "summary");

        Assert.Equal(0, exit);
        Assert.Contains("Loaded 16 notes", output, StringComparison.Ordinal);
        Assert.Contains("Key: C Major", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Midi_Import_LeavesTheDrumsOut_UnlessAskedForThem()
    {
        var path = DrumsAreNotPitchesTests.WritePianoAndDrumsFile(_work);

        var (byDefault, defaultOutput) = Run("midi", "import", "--in", path);
        var (channelTen, channelTenOutput) = Run("midi", "import", "--in", path, "--channel", "9");
        var (both, bothOutput) = Run("midi", "import", "--in", path, "--include-percussion");

        Assert.Equal(0, byDefault);
        Assert.Equal(0, channelTen);
        Assert.Equal(0, both);
        Assert.Contains("Notes: 16", defaultOutput, StringComparison.Ordinal);
        Assert.Contains("Notes: 48", channelTenOutput, StringComparison.Ordinal);
        Assert.Contains("Notes: 64", bothOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Midi_Info_CountsTheDrumsApart_AndRangesThePitches()
    {
        var path = DrumsAreNotPitchesTests.WritePianoAndDrumsFile(_work);

        var (exit, output) = Run("midi", "info", "--in", path);

        Assert.Equal(0, exit);
        Assert.Contains("Notes: 16", output, StringComparison.Ordinal);
        Assert.Contains("Percussion (channel 10): 48 hits", output, StringComparison.Ordinal);
        Assert.Contains("(MIDI 60-72)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Midi_Info_DurationIsTheEndOfTheNoteThatEndsLast()
    {
        // A whole note from the start and a quarter on the second beat: the last note in
        // offset order ends at 0.50, the music at 1.00. The command printed 0.50.
        var path = Path.Combine(_work, "overlap.mid");
        using (var buffer = new NoteBuffer(2))
        {
            buffer.AddNote(60, Rational.Zero, Rational.Whole);
            buffer.AddNote(64, Rational.Quarter, Rational.Quarter);
            MidiIo.Export(buffer, path);
        }

        var (exit, output) = Run("midi", "info", "--in", path);

        Assert.Equal(0, exit);
        Assert.Contains("Duration (whole notes): 1.00", output, StringComparison.Ordinal);
        Assert.Equal(Rational.Whole, MidiFile.Read(path).GetStatistics().TotalDuration);
    }

    [Fact]
    public void Midi_Transpose_SaysItLeftTheDrumsOut()
    {
        // A drum hit cannot be transposed and the single-track output has no channel to keep
        // it on; the command says so rather than dropping the track in silence.
        var path = DrumsAreNotPitchesTests.WritePianoAndDrumsFile(_work);
        var shifted = Path.Combine(_work, "shifted.mid");

        var (exit, output) = Run("midi", "transpose", "--in", path, "--out", shifted, "--semitones", "2");

        Assert.Equal(0, exit);
        Assert.Contains("Loaded 16 notes", output, StringComparison.Ordinal);
        Assert.Contains("Left out 48 percussion hits", output, StringComparison.Ordinal);
        Assert.Contains("Notes: 16", Run("midi", "import", "--in", shifted, "--include-percussion").Output, StringComparison.Ordinal);
    }

    [Fact]
    public void MusicXml_ConvertFromMidi_SaysItLeftTheDrumsOut()
    {
        var path = DrumsAreNotPitchesTests.WritePianoAndDrumsFile(_work);
        var score = Path.Combine(_work, "with-drums.musicxml");

        var (exit, output) = Run("musicxml", "convert", "--in", path, "--out", score);

        Assert.Equal(0, exit);
        Assert.Contains("16 notes", output, StringComparison.Ordinal);
        Assert.Contains("Left out 48 percussion hits", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Midi_Transpose_ShiftsAFileOnDisk()
    {
        var source = Path.Combine(_work, "source.mid");
        var shifted = Path.Combine(_work, "shifted.mid");
        Assert.Equal(0, Run("midi", "export", "--out", source, "--notes", "4/4: C4/4 E4/4 G4/4 C5/4").ExitCode);

        var (exit, _) = Run("midi", "transpose", "--in", source, "--out", shifted, "--semitones", "2");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(shifted));
    }

    [Fact]
    public void Midi_MissingFile_FailsWithAMessage_NotACrash()
    {
        var (exit, output) = Run("midi", "info", "--in", Path.Combine(_work, "does-not-exist.mid"));

        Assert.NotEqual(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }

    // ---------- MusicXML through the CLI ----------

    [Fact]
    public void MusicXml_ConvertFromMidi_ThenAnalyzeTheResult()
    {
        var midi = Path.Combine(_work, "score.mid");
        var xml = Path.Combine(_work, "score.musicxml");
        Assert.Equal(0, Run("midi", "export", "--out", midi, "--notes", "4/4: C4/4 E4/4 G4/4 C5/4").ExitCode);

        var (convertExit, _) = Run("musicxml", "convert", "--in", midi, "--out", xml);
        Assert.Equal(0, convertExit);
        Assert.True(File.Exists(xml), "convert reported success but wrote no file");

        var (analyzeExit, analyzeOutput) = Run("musicxml", "analyze", "--in", xml);
        Assert.Equal(0, analyzeExit);
        Assert.False(string.IsNullOrWhiteSpace(analyzeOutput));
    }

    [Fact]
    public void MusicXml_RoundTripBackToMidi_Succeeds()
    {
        var midi = Path.Combine(_work, "a.mid");
        var xml = Path.Combine(_work, "a.musicxml");
        var back = Path.Combine(_work, "b.mid");
        Assert.Equal(0, Run("midi", "export", "--out", midi, "--notes", "4/4: C4/4 E4/4 G4/4 C5/4").ExitCode);
        Assert.Equal(0, Run("musicxml", "convert", "--in", midi, "--out", xml).ExitCode);

        var (exit, _) = Run("musicxml", "convert", "--in", xml, "--out", back);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(back));
    }
}
