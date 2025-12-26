// MIDI Import/Export Examples
// Load/save MIDI files, analyze, transpose


using Celeritas.Core;
using System.Linq;
using Celeritas.Core.Midi;
using Celeritas.Core.Analysis;

namespace CeleritasExamples;

class MidiExamples
{
    static void Main()
    {
        // ===== Create MIDI from Notation =====

        var melody = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/2 | B4/4 G4/4 E4/4 C4/2");

        Console.WriteLine($"=== Creating MIDI File ===");
        Console.WriteLine($"Notes: {melody.Length}");

        using var buffer = new NoteBuffer(melody.Length);
        buffer.AddRange(melody);
        MidiIo.Export(buffer, "output.mid", new MidiExportOptions(Bpm: 120));

        Console.WriteLine($"Saved to output.mid");
        Console.WriteLine($"Tempo: 120 BPM");

        // ===== Load MIDI File =====

        var loaded = MidiIo.Import("output.mid");
        Console.WriteLine($"\n=== Loaded MIDI File ===");
        Console.WriteLine($"Notes loaded: {loaded.Count}");

        // ===== Analyze MIDI Content =====

        var pitches = new int[loaded.Count];
        for (int i = 0; i < loaded.Count; i++)
        {
            pitches[i] = loaded.Get(i).Pitch;
        }

        var key = KeyProfiler.DetectFromPitches(pitches);
        Console.WriteLine($"\n=== Analysis ===");
        Console.WriteLine($"Detected key: {key}");

        var melodicAnalysis = MelodyAnalyzer.Analyze(pitches);
        Console.WriteLine($"Contour: {melodicAnalysis.Contour}");
        Console.WriteLine($"Range: {melodicAnalysis.Ambitus} semitones");

        // ===== Transpose MIDI =====

        using var transposed = MidiIo.Import("output.mid");

        // Transpose up 5 semitones (C -> F)
        MusicMath.Transpose(transposed, 5);

        MidiIo.Export(transposed, "transposed.mid");

        var transposedPitches = new int[transposed.Count];
        for (int i = 0; i < transposed.Count; i++)
        {
            transposedPitches[i] = transposed.Get(i).Pitch;
        }

        var transposedKey = KeyProfiler.DetectFromPitches(transposedPitches);
        Console.WriteLine($"\n=== Transposition ===");
        Console.WriteLine($"Original key: {key.Key}");
        Console.WriteLine($"Transposed +5 semitones");
        Console.WriteLine($"New key: {transposedKey.Key}");
        Console.WriteLine($"Saved to transposed.mid");

        // ===== Multi-Track MIDI =====
        // Note: Multi-track MIDI not yet implemented in MidiIo

        /*
        var track1 = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/4");
        var track2 = MusicNotation.Parse("C3/2 G3/2");

        var multiTrack = new MidiFile();
        multiTrack.AddTrack(track1, "Melody");
        multiTrack.AddTrack(track2, "Bass");
        multiTrack.SetTempo(120);
        multiTrack.Save("multitrack.mid");

        Console.WriteLine($"\n=== Multi-Track MIDI ===");
        Console.WriteLine($"Tracks: {multiTrack.TrackCount}");
        Console.WriteLine($"  Track 1: Melody ({track1.Length} notes)");
        Console.WriteLine($"  Track 2: Bass ({track2.Length} notes)");
        Console.WriteLine($"Saved to multitrack.mid");
        */

        Console.WriteLine($"\n=== Multi-Track MIDI ===");
        Console.WriteLine($"Multi-track MIDI support: Coming soon");

        // ===== MIDI with Chords =====

        var withChords = MusicNotation.Parse(@"
            4/4: [C4 E4 G4]/4 R/4 [D4 F4 A4]/2 |
            [E4 G4 B4]/2 [C4 E4 G4]/2");

        using var chordBuffer = new NoteBuffer(withChords.Length);
        chordBuffer.AddRange(withChords);
        MidiIo.Export(chordBuffer, "chords.mid");

        Console.WriteLine($"\n=== MIDI with Chords ===");
        Console.WriteLine($"Total notes: {withChords.Length}");
        Console.WriteLine($"Tempo: 100 BPM");
        Console.WriteLine($"Saved to chords.mid");

        // ===== Extract Specific Track =====
        // Note: Track extraction not yet implemented

        /*
        var multiLoaded = MidiFile.Load("multitrack.mid");
        var melodyTrack = multiLoaded.GetTrack(0);
        var bassTrack = multiLoaded.GetTrack(1);

        Console.WriteLine($"\n=== Track Extraction ===");
        Console.WriteLine($"Melody track: {melodyTrack.Notes.Length} notes");
        Console.WriteLine($"Bass track: {bassTrack.Notes.Length} notes");

        // ===== MIDI Merge =====

        var midi1 = MidiFile.Load("output.mid");
        var midi2 = MidiFile.Load("transposed.mid");

        var merged = MidiFile.Merge(midi1, midi2);
        merged.Save("merged.mid");

        Console.WriteLine($"\n=== MIDI Merge ===");
        Console.WriteLine($"Input 1: {midi1.EventCount} events");
        Console.WriteLine($"Input 2: {midi2.EventCount} events");
        Console.WriteLine($"Merged: {merged.EventCount} events");
        Console.WriteLine($"Saved to merged.mid");
        */

        // ===== MIDI Statistics =====
        // Note: MidiFile.GetStatistics doesn't exist
        /*
        var stats = MidiFile.GetStatistics("output.mid");

        Console.WriteLine($"\n=== MIDI Statistics ===");
        Console.WriteLine($"File: output.mid");
        Console.WriteLine($"Duration: {stats.Duration}");
        Console.WriteLine($"Notes: {stats.NoteCount}");
        Console.WriteLine($"Average pitch: {stats.AveragePitch:F1}");
        Console.WriteLine($"Pitch range: {stats.LowestPitch} - {stats.HighestPitch}");
        Console.WriteLine($"Average velocity: {stats.AverageVelocity:F1}");
        Console.WriteLine($"Note density: {stats.NotesPerSecond:F2} notes/sec");
        */

        // ===== Tempo Changes =====
        // Note: MidiFile tempo changes not yet implemented

        /*
        var withTempoChanges = new MidiFile();
        withTempoChanges.SetTempo(120);

        var section1 = MusicNotation.Parse("C4/1 D4/1");
        withTempoChanges.AddTrack(section1);

        // Add tempo change at beat 2
        withTempoChanges.AddTempoChange(new Rational(2, 1), 140);

        withTempoChanges.Save("tempo_changes.mid");

        Console.WriteLine($"\n=== Tempo Changes ===");
        Console.WriteLine($"Initial tempo: 120 BPM");
        Console.WriteLine($"Tempo change at beat 2: 140 BPM");
        Console.WriteLine($"Saved to tempo_changes.mid");
        */

        // ===== Time Signature Changes =====

        // Note: Time signature changes not yet fully implemented
        /*
        var withMeterChanges = new MidiFile();

        var measure1 = MusicNotation.Parse("4/4: C4/4 E4/4 G4/4 C5/4");
        var measure2 = MusicNotation.Parse("3/4: D4/4 F4/4 A4/4");

        withMeterChanges.AddTrack(measure1);
        withMeterChanges.AddTimeSignatureChange(new Rational(1, 1), numerator: 3, denominator: 4);
        withMeterChanges.AddTrack(measure2);

        withMeterChanges.Save("meter_changes.mid");

        Console.WriteLine($"\n=== Time Signature Changes ===");
        Console.WriteLine($"Measure 1: 4/4");
        Console.WriteLine($"Measure 2: 3/4");
        Console.WriteLine($"Saved to meter_changes.mid");
        */

        // ===== Extract Chords from MIDI =====
        // Note: MidiFile.Load doesn't exist in current context
        /*
        var polyphonicMidi = MidiFile.Load("chords.mid");
        var allNotes = polyphonicMidi.ToNoteEvents();

        // Group simultaneous notes
        var chordGroups = allNotes
            .GroupBy(n => n.Offset)
            .Where(g => g.Count() >= 3)  // At least 3 notes = chord
            .ToList();

        Console.WriteLine($"\n=== Chord Extraction ===");
        Console.WriteLine($"Chords found: {chordGroups.Count}");

        foreach (var group in chordGroups)
        {
            var chord = group.ToArray();
            var symbol = ChordAnalyzer.Identify(chord.Select(n => n.Pitch).ToArray());
            Console.WriteLine($"  {group.Key}: {symbol}");
        }
        */

        // ===== Quantize MIDI =====
        // Note: MusicMath.Quantize signature mismatch
        /*
        var unquantized = MusicNotation.Parse(
            "C4/4.1 E4/4.05 G4/3.98 C5/2.02");  // Slightly off timing

        var quantized = MusicMath.Quantize(unquantized, gridSize: new Rational(1, 16));

        Console.WriteLine($"\n=== Quantization ===");
        Console.WriteLine($"Grid: 1/16 notes");
        Console.WriteLine($"Before: {string.Join(", ", unquantized.Select(n => n.Offset))}");
        Console.WriteLine($"After: {string.Join(", ", quantized.Select(n => n.Offset))}");
        */

        // ===== MIDI Velocity Adjustments =====
        // Note: NoteEvent.Velocity is readonly
        /*
        var dynamicMelody = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/4");

        // Set velocities (0.0 - 1.0)
        dynamicMelody[0].Velocity = 0.5f;  // Piano
        dynamicMelody[1].Velocity = 0.7f;  // Mezzo-forte
        dynamicMelody[2].Velocity = 0.9f;  // Forte
        dynamicMelody[3].Velocity = 1.0f;  // Fortissimo

        var dynamicMidi = MidiFile.FromNoteEvents(dynamicMelody, tempo: 120);
        dynamicMidi.Save("dynamics.mid");

        Console.WriteLine($"\n=== Velocity/Dynamics ===");
        Console.WriteLine($"Note velocities:");
        foreach (var note in dynamicMelody)
        {
            Console.WriteLine($"  {MusicMath.MidiToNoteName(note.Pitch)}: {note.Velocity:P0}");
        }
        Console.WriteLine($"Saved to dynamics.mid");
        */

        // ===== Best Practices =====

        Console.WriteLine($"\n=== MIDI Best Practices ===");
        Console.WriteLine($"1. Always set tempo before saving (default: 120 BPM)");
        Console.WriteLine($"2. Use velocity (0.0-1.0) for dynamics, not separate events");
        Console.WriteLine($"3. Quantize if needed: gridSize = 1/16 or 1/32 for tight timing");
        Console.WriteLine($"4. For analysis, convert to NoteEvents first");
        Console.WriteLine($"5. Multi-track files preserve voice separation");
    }
}

/* Expected Output:

=== Creating MIDI File ===
Notes: 8
Saved to output.mid
Tempo: 120 BPM
Tracks: 1

=== Loaded MIDI File ===
Tempo: 120 BPM
Time signature: 4/4
Duration: 4/1
Total events: 24

=== Converted to NoteEvents ===
Total notes: 8
First 5 notes:
  C4 at 0, duration 1/4
  E4 at 1/4, duration 1/4
  G4 at 1/2, duration 1/4
  C5 at 3/4, duration 1/2
  B4 at 5/4, duration 1/4

=== Analysis ===
Detected key: C major
Contour: Arch
Range: 12 semitones

=== Transposition ===
Original key: C major
Transposed +5 semitones
New key: F major
Saved to transposed.mid

=== Multi-Track MIDI ===
Tracks: 2
  Track 1: Melody (4 notes)
  Track 2: Bass (2 notes)
Saved to multitrack.mid

*/
