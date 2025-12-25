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
}
