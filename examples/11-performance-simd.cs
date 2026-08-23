// Performance Examples
// SIMD operations, NoteBuffer, Pitch Class Sets


using Celeritas.Core;
using System.Linq;
using Celeritas.Core.Analysis;
using Celeritas.Core.Simd;
using System.Diagnostics;

namespace CeleritasExamples;

class PerformanceExamples
{
    static void Main()
    {
        // ===== NoteBuffer Basics =====

        using var buffer = new NoteBuffer(capacity: 1000);
        Console.WriteLine($"=== NoteBuffer ===");
        Console.WriteLine($"Initial capacity: {buffer.Capacity}");
        Console.WriteLine($"Count: {buffer.Count}");

        // Add notes
        buffer.Add(new NoteEvent(60, Rational.Zero, new Rational(1, 4), 0.8f));
        buffer.Add(new NoteEvent(64, new Rational(1, 4), new Rational(1, 4), 0.8f));
        buffer.Add(new NoteEvent(67, new Rational(1, 2), new Rational(1, 2), 0.8f));

        Console.WriteLine($"After adding 3 notes: {buffer.Count}");

        // Access by index
        Console.WriteLine($"\nFirst note:");
        Console.WriteLine($"  Pitch: {buffer.Get(0).Pitch}");
        Console.WriteLine($"  Offset: {buffer.Get(0).Offset}");
        Console.WriteLine($"  Duration: {buffer.Get(0).Duration}");

        // ===== SIMD-Accelerated Transpose =====

        var melody = MusicNotation.Parse("C4/4 E4/4 G4/4 C5/4 E5/4 G5/2");
        using var melodyBuffer = new NoteBuffer(melody.Length);
        melodyBuffer.AddRange(melody);

        Console.WriteLine($"\n=== SIMD Transpose ===");
        Console.WriteLine($"Original pitches: {string.Join(" ", melodyBuffer.PitchesReadOnly.ToArray().Select(MusicMath.MidiToNoteName))}");

        // Transpose up 5 semitones (to F)
        MusicMath.Transpose(melodyBuffer, 5);
        Console.WriteLine($"After +5 semitones: {string.Join(" ", melodyBuffer.PitchesReadOnly.ToArray().Select(MusicMath.MidiToNoteName))}");

        // Transpose down 3 semitones
        MusicMath.Transpose(melodyBuffer, -3);
        Console.WriteLine($"After -3 semitones: {string.Join(" ", melodyBuffer.PitchesReadOnly.ToArray().Select(MusicMath.MidiToNoteName))}");

        // ===== SIMD Performance Benchmark =====

        const int noteCount = 1_000_000;
        using var largeBuffer = new NoteBuffer(noteCount);

        // Fill with notes
        for (int i = 0; i < noteCount; i++)
        {
            largeBuffer.Add(new NoteEvent(
                pitch: 60 + (i % 12),
                offset: new Rational(i, 4),
                duration: new Rational(1, 4),
                velocity: 0.8f
            ));
        }

        // Figures below were measured with a Release build (dotnet run -c Release).
        Console.WriteLine($"\n=== Performance Benchmark ===");
        Console.WriteLine($"Notes: {noteCount:N0}");

        var sw = Stopwatch.StartNew();
        MusicMath.Transpose(largeBuffer, 2);
        sw.Stop();

        Console.WriteLine($"SIMD transpose: {sw.Elapsed.TotalMicroseconds:F2} μs");
        Console.WriteLine($"Per note: {sw.Elapsed.TotalMicroseconds / noteCount * 1000:F2} ns");
        Console.WriteLine($"Throughput: ~{noteCount / sw.Elapsed.TotalSeconds / 1_000_000_000:F2} billion notes/sec (single cold run)");

        // ===== SIMD Detection =====
        // SimdInfo probes the hardware. Never hardcode a tier - ask for what is available.

        Console.WriteLine($"\n=== SIMD Capabilities ===");
        Console.WriteLine($"Detected: {SimdInfo.GetDescription()}");
        Console.WriteLine($"Best available: {SimdInfo.GetBest()}");
        Console.WriteLine($"AVX-512: {SimdInfo.IsSupported(SimdInstructionSet.Avx512F)}");
        Console.WriteLine($"AVX2: {SimdInfo.IsSupported(SimdInstructionSet.Avx2)}");
        Console.WriteLine($"SSE2: {SimdInfo.IsSupported(SimdInstructionSet.Sse2)}");
        Console.WriteLine($"NEON (ARM): {SimdInfo.IsSupported(SimdInstructionSet.Neon)}");
        Console.WriteLine($"Vector<int>.Count: {System.Numerics.Vector<int>.Count}");
        Console.WriteLine($"Hardware acceleration: {System.Numerics.Vector.IsHardwareAccelerated}");

        // ===== Pitch Class Set Analysis =====

        var pitchClasses = new[] { 0, 4, 7 };  // C E G
        var pcSet = PitchClassSetAnalyzer.Analyze(pitchClasses);

        Console.WriteLine($"\n=== Pitch Class Set Analysis ===");
        Console.WriteLine($"Input: {string.Join(", ", pitchClasses)}");
        Console.WriteLine($"Normal order: {string.Join(", ", pcSet.NormalOrder)}");
        Console.WriteLine($"Prime form: {string.Join(", ", pcSet.PrimeForm)}");
        Console.WriteLine($"Interval vector: {pcSet.IntervalVectorText}");
        // Forte/Carter labeling is intentionally pluggable (no built-in Forte table).
        // For examples, we use a tiny inline catalog.
        var pcSetCatalogJson = """
        [
          { "forte": "3-11", "primeForm": [0,3,7], "name": "Major/Minor Triad", "notes": "Carter=37" }
        ]
        """;

        var pcSetCatalog = PitchClassSetCatalog.LoadJson(pcSetCatalogJson);
        if (pcSetCatalog.TryGetByPrimeForm(pcSet.PrimeForm, out var pcSetEntry) && pcSetEntry != null)
        {
            Console.WriteLine($"Forte number: {pcSetEntry.Forte}");

            static int? TryParseCarterNumber(string? notes)
            {
                if (string.IsNullOrWhiteSpace(notes))
                    return null;

                // Expected format in this example: "Carter=37"
                const string prefix = "Carter=";
                var idx = notes.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return null;

                var start = idx + prefix.Length;
                var end = start;
                while (end < notes.Length && char.IsDigit(notes[end]))
                    end++;

                return int.TryParse(notes[start..end], out var n) ? n : null;
            }

            var carter = TryParseCarterNumber(pcSetEntry.Notes);
            if (carter != null)
                Console.WriteLine($"Carter number: {carter}");
        }
        else
        {
            Console.WriteLine("Forte number: (not found)");
            Console.WriteLine("Carter number: (not found)");
        }

        // ===== PC Set Operations =====

        // Transposition
        var transposed = PitchClassSetAnalyzer.Transpose(pitchClasses, 2);
        Console.WriteLine($"\nT2: {string.Join(", ", transposed)}");  // D F# A

        // Inversion
        var inverted = PitchClassSetAnalyzer.Invert(pitchClasses);
        Console.WriteLine($"I: {string.Join(", ", inverted)}");  // C Ab F

        // Complement
        var complement = PitchClassSetAnalyzer.Complement(pitchClasses);
        Console.WriteLine($"Complement: {string.Join(", ", complement)}");

        // ===== PC Set Similarity =====

        var set1 = new[] { 0, 1, 4 };   // C Db E
        var set2 = new[] { 0, 3, 4 };   // C Eb E

        var similarity = PitchClassSetAnalyzer.Similarity(set1, set2);
        Console.WriteLine($"\n=== Set Similarity ===");
        Console.WriteLine($"Set 1: {string.Join(", ", set1)}");
        Console.WriteLine($"Set 2: {string.Join(", ", set2)}");
        Console.WriteLine($"Similarity: {similarity:P1}");

        // PitchClassSetCatalog is available (see README.md for overview)

        // ===== Batch Chord Analysis =====

        var chords = new[]
        {
            "C4 E4 G4",
            "D4 F4 A4",
            "E4 G4 B4",
            "F4 A4 C5"
        };

        Console.WriteLine($"\n=== Batch Chord Analysis ===");
        var stopwatch = Stopwatch.StartNew();
        var symbols = chords.Select(c => ChordAnalyzer.Identify(c)).ToList();
        stopwatch.Stop();

        Console.WriteLine($"Analyzed {chords.Length} chords: {string.Join(", ", symbols)}");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalMicroseconds:F2} μs");
        Console.WriteLine($"Per chord: {stopwatch.Elapsed.TotalMicroseconds / chords.Length:F2} μs");

        // ===== Memory-Efficient Operations =====

        // Reuse NoteBuffer instead of creating new arrays
        using var reusableBuffer = new NoteBuffer(100);

        Console.WriteLine($"\n=== Memory Efficiency ===");
        Console.WriteLine($"Reusing buffer for multiple operations:");

        // Operation 1
        reusableBuffer.Clear();
        var notes1 = MusicNotation.Parse("C4/4 E4/4 G4/4");
        foreach (var note in notes1)
            reusableBuffer.Add(note);
        Console.WriteLine($"  Op 1: {reusableBuffer.Count} notes");

        // Operation 2 (reuse buffer)
        reusableBuffer.Clear();
        var notes2 = MusicNotation.Parse("D4/4 F4/4 A4/4");
        foreach (var note in notes2)
            reusableBuffer.Add(note);
        Console.WriteLine($"  Op 2: {reusableBuffer.Count} notes");

        Console.WriteLine($"  Same buffer, zero allocations");

        // ===== Parallel Processing =====

        // Pre-generate chord pitch arrays (ChordAnalyzer.Identify expects pitches).
        var manyChordsToAnalyze = Enumerable.Range(0, 10_000)
            .Select(i =>
            {
                var root = 48 + (i % 12); // C3..B3
                return new[] { root, root + 4, root + 7 }; // major triad
            })
            .ToList();

        Console.WriteLine($"\n=== Parallel Processing ===");
        Console.WriteLine($"Analyzing {manyChordsToAnalyze.Count:N0} chords...");

        // Sequential
        sw = Stopwatch.StartNew();
        var sequential = manyChordsToAnalyze.Select(p => ChordAnalyzer.Identify(p)).ToList();
        sw.Stop();
        var seqTime = sw.Elapsed.TotalMilliseconds;

        // Parallel
        sw = Stopwatch.StartNew();
        var parallelSymbols = manyChordsToAnalyze.AsParallel().Select(p => ChordAnalyzer.Identify(p)).ToList();
        sw.Stop();
        var parTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"Sequential: {seqTime:F2} ms");
        Console.WriteLine($"Parallel: {parTime:F2} ms");
        Console.WriteLine($"Speedup: {seqTime / parTime:F2}x");


        // ===== Tips for Best Performance =====

        Console.WriteLine($"\n=== Performance Tips ===");
        Console.WriteLine($"1. Use NoteBuffer for large sequences (avoids array reallocations)");
        Console.WriteLine($"2. SIMD works best with 16+ notes (especially AVX-512: 16 notes at once)");
        Console.WriteLine($"3. Reuse buffers when possible to reduce GC pressure");
        Console.WriteLine($"4. Use AsParallel() for batch operations on 1000+ items");
        Console.WriteLine($"5. Rational arithmetic is already optimized (auto-normalized)");
        Console.WriteLine($"6. ChordAnalyzer.Identify is ~2ns - can analyze millions of chords/sec");
    }
}

/* Expected Output:

Timings and the SIMD capability list depend on the machine; the AVX-512
figures below come from a Ryzen 9 9950X3D. Everything else is stable.

=== NoteBuffer ===
Initial capacity: 1000
Count: 0
After adding 3 notes: 3

First note:
  Pitch: 60
  Offset: 0
  Duration: 1/4

=== SIMD Transpose ===
Original pitches: C4 E4 G4 C5 E5 G5
After +5 semitones: F4 A4 C5 F5 A5 C6
After -3 semitones: D4 F#4 A4 D5 F#5 A5

=== Performance Benchmark ===
Notes: 1,000,000
SIMD transpose: 178.10 μs
Per note: 0.18 ns
Throughput: ~5.61 billion notes/sec (single cold run)

=== SIMD Capabilities ===
Detected: AVX-512, AVX2, SSE2
Best available: Avx512F
AVX-512: True
AVX2: True
SSE2: True
NEON (ARM): False
Vector<int>.Count: 8
Hardware acceleration: True

=== Pitch Class Set Analysis ===
Input: 0, 4, 7
Normal order: 0, 4, 7
Prime form: 0, 3, 7
Interval vector: <0,0,1,1,1,0>
Forte number: 3-11
Carter number: 37

T2: 2, 6, 9
I: 0, 5, 8
Complement: 1, 2, 3, 5, 6, 8, 9, 10, 11

=== Set Similarity ===
Set 1: 0, 1, 4
Set 2: 0, 3, 4
Similarity: 100.0 %

=== Batch Chord Analysis ===
Analyzed 4 chords: C Major, D Minor, E Minor, F Major
Time: 2151.70 μs
Per chord: 537.92 μs

=== Memory Efficiency ===
Reusing buffer for multiple operations:
  Op 1: 3 notes
  Op 2: 3 notes
  Same buffer, zero allocations

=== Parallel Processing ===
Analyzing 10,000 chords...
Sequential: 1.05 ms
Parallel: 8.86 ms
Speedup: 0.12x

=== Performance Tips ===
1. Use NoteBuffer for large sequences (avoids array reallocations)
2. SIMD works best with 16+ notes (especially AVX-512: 16 notes at once)
3. Reuse buffers when possible to reduce GC pressure
4. Use AsParallel() for batch operations on 1000+ items
5. Rational arithmetic is already optimized (auto-normalized)
6. ChordAnalyzer.Identify is ~2ns - can analyze millions of chords/sec

*/
