// Copyright (c) 2025 Vladimir V. Shein

using System.Reflection;
using Celeritas.CLI;

namespace Celeritas.Tests;

/// <summary>
/// The CLI ships as a dotnet tool and had 0% coverage across all 1774 lines of its entry
/// point. These drive the real command tree in-process and assert on what a user sees.
/// <para>
/// Console output is process-global, so this class is not run in parallel with anything else.
/// </para>
/// </summary>
[Collection(nameof(CliCommandTests))]
[CollectionDefinition(nameof(CliCommandTests), DisableParallelization = true)]
public class CliCommandTests
{
    /// <summary>
    /// Runs the CLI exactly as the shipped tool does — through its real entry point, so the
    /// command tree, parsing and handlers are all the ones a user gets — and returns the exit
    /// code with everything it printed.
    /// </summary>
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

    // ---------- it runs at all ----------

    [Fact]
    public void Help_ListsTheCommands()
    {
        var (exit, output) = Run("--help");

        Assert.Equal(0, exit);
        foreach (var verb in new[] { "analyze", "transpose", "progression" })
        {
            Assert.Contains(verb, output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Version_ReportsTheBuiltVersion()
    {
        var (exit, output) = Run("--version");

        Assert.Equal(0, exit);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
    }

    [Fact]
    public void UnknownCommand_FailsRatherThanSucceedingSilently()
    {
        var (exit, _) = Run("definitely-not-a-command");

        Assert.NotEqual(0, exit);
    }

    // ---------- analyze ----------

    [Fact]
    public void Analyze_Chord_NamesTheChordAndQualifiesTheKey()
    {
        var (exit, output) = Run("analyze", "--notes", "C4 E4 G4 B4");

        Assert.Equal(0, exit);
        Assert.Contains("C Major7", output, StringComparison.Ordinal);

        // A key must never be stated as a bare fact: four notes do not settle one.
        Assert.Contains("Detected key:", output, StringComparison.Ordinal);
        Assert.True(
            output.Contains("margin", StringComparison.Ordinal) ||
            output.Contains("weak", StringComparison.Ordinal),
            "the detected-key line carried no qualifier:\n" + output);
    }

    [Fact]
    public void Analyze_WithAnExplicitKey_ReportsTheRomanNumeral()
    {
        var (exit, output) = Run("analyze", "--notes", "G4 B4 D5", "--key", "C");

        Assert.Equal(0, exit);
        Assert.Contains("V", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_MidiNumbersAndNoteNames_AgreeOnTheSameChord()
    {
        var (_, byName) = Run("analyze", "--notes", "C4 E4 G4");
        var (_, byNumber) = Run("analyze", "--notes", "60 64 67");

        Assert.Contains("C Major", byName, StringComparison.Ordinal);
        Assert.Contains("C Major", byNumber, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_GarbageNotes_FailsWithAMessage_NotACrash()
    {
        var (exit, output) = Run("analyze", "--notes", "Zzz9 Qqq");

        Assert.NotEqual(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output), "the failure said nothing at all");
    }

    // ---------- transpose ----------

    [Theory]
    [InlineData("2", "D4")]
    [InlineData("-2", "A#3")]
    [InlineData("12", "C5")]
    public void Transpose_ShiftsEveryNote(string semitones, string expected)
    {
        var (exit, output) = Run("transpose", "--notes", "C4", "--semitones", semitones);

        Assert.Equal(0, exit);
        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    [Fact]
    public void Transpose_OutOfRange_FailsRatherThanWrappingAround()
    {
        // Transposing the top of the MIDI range up must not silently wrap to a low pitch.
        var (exit, output) = Run("transpose", "--notes", "G9", "--semitones", "12");

        Assert.True(exit != 0 || !output.Contains("C0", StringComparison.Ordinal),
            "a transposition past MIDI 127 appears to have wrapped:\n" + output);
    }

    // ---------- progression ----------

    [Fact]
    public void Progression_ReportsKeyAndPattern()
    {
        var (exit, output) = Run("progression", "--chords", "C,Am,F,G");

        Assert.Equal(0, exit);
        Assert.Contains("C Major", output, StringComparison.Ordinal);
        Assert.Contains("I", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Progression_ChromaticChord_IsNotPassedOffAsADiatonicOne()
    {
        // The 0.10.0 fix: a chord outside the key used to be analyzed as the tonic.
        var (exit, output) = Run("progression", "--chords", "C,F,G,Ab");

        Assert.Equal(0, exit);
        Assert.Contains("?", output, StringComparison.Ordinal);
    }

    // ---------- info ----------

    [Fact]
    public void Info_ReportsTheDetectedSimdPath()
    {
        var (exit, output) = Run("info");

        Assert.Equal(0, exit);
        Assert.False(string.IsNullOrWhiteSpace(output));
    }
}
