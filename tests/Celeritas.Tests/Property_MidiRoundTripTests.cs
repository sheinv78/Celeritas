using Celeritas.Core;
using Celeritas.Core.Midi;
using CsCheck;

namespace Celeritas.Tests;

/// <summary>
/// Property-based tests (CsCheck) for MIDI export/import round-tripping of pitch and timing.
/// </summary>
public class PropertyMidiRoundTripTests
{
    // Denominators divide 4 * 480 = 1920 ticks, so tick rounding at TPQ 480 is exact.
    private static readonly Gen<long> Denominator =
        Gen.Int[0, 4].Select(i => i switch { 0 => 1L, 1 => 2L, 2 => 4L, 3 => 8L, _ => 16L });

    // Non-negative whole-note offsets (MIDI cannot represent time < 0).
    private static readonly Gen<Rational> Offset =
        from num in Gen.Int[0, 16]
        from den in Denominator
        select new Rational(num, den);

    // Strictly positive whole-note durations.
    private static readonly Gen<Rational> Duration =
        from num in Gen.Int[1, 16]
        from den in Denominator
        select new Rational(num, den);

    private static readonly Gen<(int Pitch, Rational Off, Rational Dur)[]> Notes =
        (from pitch in Gen.Int[0, 127]
         from off in Offset
         from dur in Duration
         select (pitch, off, dur)).Array[1, 64];

    [Fact]
    public void ExportImport_PreservesPitchOffsetDuration()
    {
        Notes.Sample(specs =>
        {
            // Distinct pitches only: two same-pitch notes on one channel create ambiguous
            // note-on/off pairing on import. Last spec wins per pitch.
            var byPitch = new Dictionary<int, (Rational Off, Rational Dur)>();
            foreach (var (pitch, off, dur) in specs)
            {
                byPitch[pitch] = (off, dur);
            }

            using var buffer = new NoteBuffer(byPitch.Count);
            foreach (var kv in byPitch)
            {
                buffer.AddNote(kv.Key, kv.Value.Off, kv.Value.Dur);
            }

            using var ms = new MemoryStream();
            MidiIo.Export(buffer, ms, new MidiExportOptions(TicksPerQuarterNote: 480, Bpm: 120, Channel: 0));

            ms.Position = 0;
            using var imported = MidiIo.Import(ms, new MidiImportOptions(SortByOffset: true));

            Assert.Equal(byPitch.Count, imported.Count);

            var importedByPitch = new Dictionary<int, (Rational Off, Rational Dur)>();
            for (var i = 0; i < imported.Count; i++)
            {
                var e = imported.Get(i);
                importedByPitch[e.Pitch] = (e.Offset, e.Duration);
            }

            foreach (var kv in byPitch)
            {
                Assert.True(importedByPitch.TryGetValue(kv.Key, out var got), "pitch missing after round-trip");
                Assert.Equal(kv.Value.Off, got.Off);
                Assert.Equal(kv.Value.Dur, got.Dur);
            }
        });
    }
}
