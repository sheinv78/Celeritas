using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Celeritas.Core;
using Celeritas.Core.Analysis;

BenchmarkRunner.Run<CeleritasBenchmarks>();

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class CeleritasBenchmarks
{
    private NoteBuffer _buffer1M = null!;
    private NoteBuffer _buffer10M = null!;
    private NoteBuffer _chordBuffer = null!;
    private int[] _chordPitches = null!;
    private string _notesText = null!;
    private string[] _progressionSymbols = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1M notes buffer
        _buffer1M = new NoteBuffer(1_000_000);
        for (var i = 0; i < 1_000_000; i++)
        {
            _buffer1M.AddNote(60, new Rational(i, 4), Rational.Quarter);
        }

        // 10M notes buffer
        _buffer10M = new NoteBuffer(10_000_000);
        for (var i = 0; i < 10_000_000; i++)
        {
            _buffer10M.AddNote(60, new Rational(i, 4), Rational.Quarter);
        }

        // Chord buffer
        _chordBuffer = new NoteBuffer(3);
        _chordBuffer.AddNote(60, Rational.Zero, Rational.Quarter); // C
        _chordBuffer.AddNote(64, Rational.Zero, Rational.Quarter); // E
        _chordBuffer.AddNote(67, Rational.Zero, Rational.Quarter); // G

        _chordPitches = [60, 64, 67];

        _notesText = "C4 E4 G4 Bb4 D5 F5";
        _progressionSymbols = ["Dm7", "G7", "Cmaj7", "Am7"];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer1M?.Dispose();
        _buffer10M?.Dispose();
        _chordBuffer?.Dispose();
    }

    [Benchmark]
    public void Transpose_1M_Notes()
    {
        MusicMath.Transpose(_buffer1M, 2);
    }

    [Benchmark]
    public void Transpose_10M_Notes()
    {
        MusicMath.Transpose(_buffer10M, 2);
    }

    [Benchmark]
    public void ScaleVelocity_1M_Notes()
    {
        MusicMath.ScaleVelocity(_buffer1M, 0.99f);
    }

    [Benchmark]
    public ushort ChordAnalysis_GetMask()
    {
        return ChordAnalyzer.GetMask(_chordBuffer);
    }

    [Benchmark]
    public ChordInfo ChordAnalysis_Identify()
    {
        return ChordAnalyzer.Identify(_chordPitches);
    }

    [Benchmark]
    public int MusicNotation_ParseSingle()
    {
        return MusicNotation.ParseNote("Bb3");
    }

    [Benchmark]
    public NoteEvent[] MusicNotation_Parse()
    {
        return MusicNotation.Parse(_notesText);
    }

    [Benchmark]
    public string MusicNotation_Format()
    {
        return MusicNotation.ToNotation(78);
    }

    [Benchmark]
    public ProgressionReport Progression_Analyze()
    {
        return ProgressionAdvisor.Analyze(_progressionSymbols);
    }

    [Benchmark]
    public void Quantize_1M_Notes()
    {
        MusicMath.Quantize(_buffer1M, Rational.Sixteenth);
    }
}

