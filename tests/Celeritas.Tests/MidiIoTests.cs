using Celeritas.Core;
using Celeritas.Core.Midi;

namespace Celeritas.Tests;

public class MidiIoTests
{
    [Fact]
    public void ExportImport_RoundTrip_PreservesPitchAndTiming()
    {
        using var original = new NoteBuffer(4);
        original.AddNote(60, Rational.Zero, new Rational(1, 4));          // C4 @ 0, 1/4 beat
        original.AddNote(64, new Rational(1, 4), new Rational(1, 4));     // E4 @ 1/4
        original.AddNote(67, new Rational(1, 2), new Rational(1, 2));     // G4 @ 1/2
        original.AddNote(72, new Rational(1, 1), new Rational(1, 1));     // C5 @ 1
        original.Sort();

        using var ms = new MemoryStream();
        MidiIo.Export(original, ms, new MidiExportOptions(TicksPerQuarterNote: 480, Bpm: 120, Channel: 0));

        ms.Position = 0;
        using var imported = MidiIo.Import(ms, new MidiImportOptions(SortByOffset: true));

        Assert.Equal(original.Count, imported.Count);

        for (var i = 0; i < original.Count; i++)
        {
            var a = original.Get(i);
            var b = imported.Get(i);

            Assert.Equal(a.Pitch, b.Pitch);
            Assert.Equal(a.Offset, b.Offset);
            Assert.Equal(a.Duration, b.Duration);
        }
    }

    [Fact]
    public void Export_ParsedNotation_UsesWholeNoteUnits()
    {
        // Contract: MusicNotation.Parse and MidiIo share whole-note time units, so a parsed
        // quarter note ("C4/4" = Rational 1/4) must export to exactly one quarter note of ticks.
        // Historically MidiIo treated 1/4 as a quarter of a *beat*, exporting 4x too fast.
        var parsed = MusicNotation.Parse("C4/4 D4/4");

        using var buffer = new NoteBuffer(parsed.Length);
        foreach (var e in parsed)
            buffer.Add(e);

        using var ms = new MemoryStream();
        MidiIo.Export(buffer, ms, new MidiExportOptions(TicksPerQuarterNote: 480));

        ms.Position = 0;
        var midiFile = Melanchall.DryWetMidi.Core.MidiFile.Read(ms);
        var notes = Melanchall.DryWetMidi.Interaction.NotesManagingUtilities.GetNotes(midiFile).ToArray();

        Assert.Equal(2, notes.Length);
        Assert.Equal(0, notes[0].Time);
        Assert.Equal(480, notes[0].Length);   // one quarter note = 480 ticks at TPQ 480
        Assert.Equal(480, notes[1].Time);     // second note starts one quarter later
    }

    [Fact]
    public void Export_NegativeOffset_Throws()
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, new Rational(-1, 4), Rational.Quarter);

        using var ms = new MemoryStream();
        Assert.Throws<ArgumentException>(() => MidiIo.Export(buffer, ms));
    }

    [Fact]
    public void Import_MaxNotesZero_ReturnsEmptyBuffer()
    {
        using var source = new NoteBuffer(2);
        source.AddNote(60, Rational.Zero, Rational.Quarter);
        source.AddNote(64, Rational.Quarter, Rational.Quarter);

        using var ms = new MemoryStream();
        MidiIo.Export(source, ms);
        ms.Position = 0;

        using var imported = MidiIo.Import(ms, new MidiImportOptions(MaxNotes: 0));
        Assert.Equal(0, imported.Count);
    }
}
