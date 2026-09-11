// Copyright (c) 2025 Vladimir V. Shein

using System.Reflection;
using Celeritas.CLI;
using Celeritas.Core;
using Celeritas.Core.Midi;
using Melanchall.DryWetMidi.Core;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// <c>celeritas midi import</c> printed each note as "C4@1/4:1/8" and said the lines could be
/// pasted into other commands like polyphony. No command reads that form: the two <c>--notes</c>
/// options that take a timeline, <c>polyphony</c> and <c>midi export</c>, go through
/// <see cref="MusicNotation.Parse"/>, which refuses the first such line, and the others take
/// pitch tokens and refuse it as well. The
/// import now writes the notes as Celeritas music notation — laid out by
/// <see cref="MusicNotation.FormatNoteSequence"/> in as many voices as the file's overlaps and
/// gaps need — and that text is what <c>polyphony</c> and <c>midi export</c> accept.
/// </summary>
[Collection(nameof(CliCommandTests))]
public class AnImportIsPrintedAsNotationTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-import").FullName;

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

    /// <summary>
    /// A lead with a rest in it over a bass whose notes overlap each other and the lead: the two
    /// things a single melodic line cannot hold, spread over two tracks.
    /// </summary>
    private static readonly NoteEvent[] Lead =
    [
        new(60, Rational.Zero, Rational.Quarter),                 // C4, then a quarter of silence
        new(64, Rational.Half, Rational.Quarter),                 // E4
        new(67, new Rational(3, 4), Rational.Half),               // G4
    ];

    private static readonly NoteEvent[] Bass =
    [
        new(48, Rational.Zero, Rational.Half),                    // C3, under the lead's C4 and its rest
        new(52, Rational.Quarter, Rational.Half),                 // E3, overlapping the C3
        new(43, new Rational(5, 8), Rational.Eighth),             // G2, under the E4
    ];

    /// <summary>What the file holds, in the order the import lists it: by time, then by pitch.</summary>
    private static IEnumerable<(int Pitch, Rational Offset, Rational Duration)> Sounding(IEnumerable<NoteEvent> notes) =>
        notes.Where(n => n.Pitch != MusicNotation.RestPitch)
            .Select(n => (n.Pitch, n.Offset, n.Duration))
            .OrderBy(n => n.Offset).ThenBy(n => n.Pitch).ThenBy(n => n.Duration);

    private string TwoTrackFile()
    {
        var file = new MidiFile();
        file.AddTrack(Lead, "lead");
        file.AddTrack(Bass, "bass");
        var path = Path.Combine(_work, "two-tracks.mid");
        file.Save(path);
        return path;
    }

    /// <summary>
    /// The notation the import printed: under its "Notation" heading and the hint that follows,
    /// the line standing on its own after the blank one — the line a user copies.
    /// </summary>
    private static string NotationIn(string output)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var heading = lines.FindIndex(l => l.TrimStart().StartsWith("Notation", StringComparison.Ordinal));
        Assert.True(heading >= 0, "the import printed no Notation section:\n" + output);
        var line = lines.Skip(heading + 1)
            .SkipWhile(l => !string.IsNullOrWhiteSpace(l))
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        Assert.False(string.IsNullOrWhiteSpace(line), "the Notation section is empty:\n" + output);
        return line!.Trim();
    }

    [Fact]
    public void TheNotationOfATwoTrackFileReadsBackAsTheSameNotes()
    {
        var path = TwoTrackFile();

        var (exit, output) = Run("midi", "import", "--in", path);

        Assert.Equal(0, exit);
        var notation = NotationIn(output);
        var back = MusicNotation.Parse(notation, validateMeasures: false);
        Assert.Equal(Sounding(Lead.Concat(Bass)), Sounding(back));
    }

    [Fact]
    public void TheNotationIsWhatPolyphonyAndExportAccept()
    {
        var path = TwoTrackFile();
        var notation = NotationIn(Run("midi", "import", "--in", path).Output);

        var (polyphonyExit, polyphonyOutput) = Run("polyphony", "--notes", notation);
        Assert.Equal(0, polyphonyExit);
        Assert.Contains("POLYPHONY ANALYSIS", polyphonyOutput, StringComparison.Ordinal);

        var exported = Path.Combine(_work, "pasted.mid");
        Assert.Equal(0, Run("midi", "export", "--out", exported, "--notes", notation).ExitCode);
        using var reimported = MidiIo.Import(exported);
        var notes = Enumerable.Range(0, reimported.Count).Select(reimported.Get);
        Assert.Equal(Sounding(Lead.Concat(Bass)), Sounding(notes));
    }

    [Fact]
    public void TheListingNoLongerClaimsToBeAFormatOtherCommandsRead()
    {
        var (exit, output) = Run("midi", "import", "--in", TwoTrackFile());

        Assert.Equal(0, exit);

        // The Pitch@Offset:Duration listing stays, as a listing: the only text the import
        // offers for pasting is the notation line, and that one parses (asserted above).
        Assert.Contains("C4@0:1/4", output, StringComparison.Ordinal);
        Assert.DoesNotContain("copy/paste", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NoteBuffer format", Run("midi", "import", "--help").Output, StringComparison.Ordinal);
        Assert.Contains("music notation", Run("midi", "import", "--help").Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ALimitedImportWritesTheFirstNotesAndSaysSo()
    {
        var path = TwoTrackFile();

        var (exit, output) = Run("midi", "import", "--in", path, "--limit", "3");

        Assert.Equal(0, exit);
        Assert.Contains("first 3 of 6", output, StringComparison.Ordinal);
        var back = MusicNotation.Parse(NotationIn(output), validateMeasures: false);
        Assert.Equal(Sounding(Lead.Concat(Bass)).Take(3), Sounding(back));
    }
}
