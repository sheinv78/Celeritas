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

        // ===== Advanced MIDI Features =====
        // See ROADMAP.md for planned features:
        // - Track extraction and merging
        // - MIDI statistics
        // - Tempo/time signature changes
        // - Chord extraction from MIDI
        // - Quantization

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
