// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Midi;
using CsCheck;
using Melanchall.DryWetMidi.Core;
using NoteEvent = Celeritas.Core.NoteEvent;

namespace Celeritas.Tests;

/// <summary>
/// Properties of the operations that move notes around — transposition, velocity scaling,
/// quantization, sorting, merging — and of how a chord spells itself in a key. These are the
/// places where a corruption is invisible: a buffer that loses a note, or a chord that spells
/// a pitch class it does not contain, still produces music.
/// </summary>
public class PropertyBufferAndSpellingTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("celeritas-prop-buffer").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_work, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static readonly Gen<int> MidiPitch = Gen.Int[24, 96];

    private static NoteBuffer BufferOf(int[] pitches, int[] beats)
    {
        var buffer = new NoteBuffer(pitches.Length);
        for (var i = 0; i < pitches.Length; i++)
            buffer.AddNote(pitches[i], new Rational(beats[i % beats.Length], 8), Rational.Quarter);
        return buffer;
    }

    private static (int Pitch, Rational Offset, Rational Duration)[] Contents(NoteBuffer buffer) =>
        [.. Enumerable.Range(0, buffer.Count)
            .Select(i => (buffer.Get(i).Pitch, buffer.Get(i).Offset, buffer.Get(i).Duration))];

    // ---------- transposition ----------

    [Fact]
    public void Transposing_IsAdditive()
    {
        (from pitches in MidiPitch.Array[1, 12]
         from a in Gen.Int[-12, 12]
         from b in Gen.Int[-12, 12]
         select (pitches, a, b)).Sample(t =>
        {
            using var twice = BufferOf(t.pitches, [0]);
            MusicMath.Transpose(twice, t.a);
            MusicMath.Transpose(twice, t.b);

            using var once = BufferOf(t.pitches, [0]);
            MusicMath.Transpose(once, t.a + t.b);

            return Contents(twice).SequenceEqual(Contents(once));
        }, iter: 500);
    }

    [Fact]
    public void Transposing_LeavesTimeAlone()
    {
        (from pitches in MidiPitch.Array[1, 12]
         from beats in Gen.Int[0, 16].Array[1, 8]
         from n in Gen.Int[-24, 24]
         select (pitches, beats, n)).Sample(t =>
        {
            using var buffer = BufferOf(t.pitches, t.beats);
            var before = Contents(buffer);

            MusicMath.Transpose(buffer, t.n);
            var after = Contents(buffer);

            return before.Length == after.Length
                && before.Zip(after, (x, y) => x.Offset == y.Offset && x.Duration == y.Duration).All(same => same);
        }, iter: 500);
    }

    // ---------- velocity ----------

    [Fact]
    public void ScalingVelocity_StaysInsideTheAudibleRange()
    {
        (from pitches in MidiPitch.Array[1, 8]
         from factor in Gen.Float[-2f, 20f]
         select (pitches, factor)).Sample(t =>
        {
            using var buffer = BufferOf(t.pitches, [0]);

            MusicMath.ScaleVelocity(buffer, t.factor);

            return Enumerable.Range(0, buffer.Count).All(i => buffer.Get(i).Velocity is >= 0f and <= 1f);
        }, iter: 500);
    }

    [Fact]
    public void ScalingVelocityByNothing_IsRefusedRatherThanStored()
    {
        // NaN survives both Math.Clamp and Vector.Min/Max, so scaling by it wrote NaN into
        // every velocity and the method's unconditional promise of 0..1 quietly stopped
        // being true. There is no loudness that is not a number.
        using var buffer = BufferOf([60, 64, 67], [0]);

        Assert.Throws<ArgumentException>(() => MusicMath.ScaleVelocity(buffer, float.NaN));
        Assert.All(Enumerable.Range(0, buffer.Count), i => Assert.False(float.IsNaN(buffer.Get(i).Velocity)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(37)]
    public void AVelocityThatWasAlreadyNotANumber_ComesOutInsideTheRange(int count)
    {
        // Nothing validates what AddNote is given, so a NaN can already be in the buffer.
        // Both the vector loop and its tail have to answer for it, so this runs at a length
        // below one vector width and at one well above it.
        var buffer = new NoteBuffer(count);
        for (var i = 0; i < count; i++)
            buffer.AddNote(60 + (i % 12), new Rational(i, 8), Rational.Quarter, i % 3 == 0 ? float.NaN : 0.8f);

        using (buffer)
        {
            MusicMath.ScaleVelocity(buffer, 1.5f);

            Assert.All(Enumerable.Range(0, buffer.Count),
                i => Assert.InRange(buffer.Get(i).Velocity, 0f, 1f));
        }
    }

    // ---------- quantization ----------

    [Fact]
    public void Quantizing_IsIdempotent()
    {
        (from pitches in MidiPitch.Array[1, 8]
         from beats in Gen.Int[0, 32].Array[1, 8]
         from grid in Gen.Int[1, 8]
         select (pitches, beats, grid)).Sample(t =>
        {
            var step = new Rational(1, t.grid * 4);

            using var once = BufferOf(t.pitches, t.beats);
            MusicMath.Quantize(once, step);
            var after = Contents(once);

            MusicMath.Quantize(once, step);

            return Contents(once).SequenceEqual(after);
        }, iter: 500);
    }

    [Fact]
    public void Quantizing_KeepsEveryNoteAndItsPitch()
    {
        (from pitches in MidiPitch.Array[1, 8]
         from beats in Gen.Int[0, 32].Array[1, 8]
         from grid in Gen.Int[1, 8]
         select (pitches, beats, grid)).Sample(t =>
        {
            using var buffer = BufferOf(t.pitches, t.beats);
            var before = Contents(buffer);

            MusicMath.Quantize(buffer, new Rational(1, t.grid * 4));
            var after = Contents(buffer);

            return before.Length == after.Length
                && before.Zip(after, (x, y) => x.Pitch == y.Pitch).All(same => same);
        }, iter: 500);
    }

    [Fact]
    public void Quantizing_NeverMovesANoteByMoreThanHalfAStep()
    {
        (from pitches in MidiPitch.Array[1, 8]
         from beats in Gen.Int[0, 32].Array[1, 8]
         from grid in Gen.Int[1, 8]
         select (pitches, beats, grid)).Sample(t =>
        {
            var step = new Rational(1, t.grid * 4);

            using var buffer = BufferOf(t.pitches, t.beats);
            var before = Contents(buffer);

            MusicMath.Quantize(buffer, step);
            var after = Contents(buffer);

            return before.Zip(after, (x, y) =>
            {
                var moved = Math.Abs((y.Offset - x.Offset).ToDouble());
                return moved <= (step.ToDouble() / 2) + 1e-9;
            }).All(near => near);
        }, iter: 500);
    }

    // ---------- sorting ----------

    [Fact]
    public void Sorting_KeepsExactlyTheNotesItWasGiven()
    {
        (from pitches in MidiPitch.Array[1, 16]
         from beats in Gen.Int[0, 32].Array[1, 16]
         select (pitches, beats)).Sample(t =>
        {
            using var buffer = BufferOf(t.pitches, t.beats);
            var before = Contents(buffer).OrderBy(n => n.Offset.ToDouble()).ThenBy(n => n.Pitch).ToArray();

            buffer.Sort();
            var after = Contents(buffer);

            return after.Zip(after.Skip(1), (a, b) => a.Offset <= b.Offset).All(ordered => ordered)
                && before.SequenceEqual(after.OrderBy(n => n.Offset.ToDouble()).ThenBy(n => n.Pitch));
        }, iter: 500);
    }

    // ---------- merging MIDI files ----------

    [Fact]
    public void MergingTwoFiles_KeepsEveryNote()
    {
        (from a in MidiPitch.Array[1, 6]
         from b in MidiPitch.Array[1, 6]
         select (a, b)).Sample(t =>
        {
            var first = new MidiFile();
            first.AddTrack([.. t.a.Select((p, i) => new NoteEvent(p, new Rational(i, 4), Rational.Quarter))]);
            var second = new MidiFile();
            second.AddTrack([.. t.b.Select((p, i) => new NoteEvent(p, new Rational(i, 4), Rational.Quarter))]);

            var merged = first.MergeToSingleTrack(second);

            var noteOns = merged.GetTrackChunks()
                .Sum(track => track.Events.OfType<NoteOnEvent>().Count());

            return noteOns == t.a.Length + t.b.Length;
        }, iter: 200);
    }

    // ---------- how a roman numeral spells itself ----------

    [Fact]
    public void ARomanNumeralSpellsOnlyPitchClasses()
    {
        (from degree in Gen.Int[0, 6]
         from quality in Gen.Int[0, 8]
         from root in Gen.Int[0, 11]
         from major in Gen.Bool
         select (degree, quality, root, major)).Sample(t =>
        {
            ScaleDegree[] degrees =
            [
                ScaleDegree.I, ScaleDegree.Ii, ScaleDegree.Iii, ScaleDegree.Iv,
                ScaleDegree.V, ScaleDegree.Vi, ScaleDegree.Vii,
            ];
            ChordQuality[] qualities =
            [
                ChordQuality.Major, ChordQuality.Minor, ChordQuality.Diminished, ChordQuality.Augmented,
                ChordQuality.Major7, ChordQuality.Minor7, ChordQuality.Dominant7, ChordQuality.HalfDim7,
                ChordQuality.Diminished7,
            ];

            var chord = new RomanNumeralChord(degrees[t.degree], qualities[t.quality], HarmonicFunction.Tonic);
            var key = new KeySignature((byte)t.root, t.major);

            var pitchClasses = chord.GetPitchClasses(key);

            return pitchClasses.Length > 0
                && pitchClasses.All(pc => pc <= 11)
                && pitchClasses[0] == chord.GetRootPitchClass(key)
                && !string.IsNullOrWhiteSpace(chord.ToRomanNumeral())
                && !string.IsNullOrWhiteSpace(chord.ToNashville());
        }, iter: 1000);
    }

    [Fact]
    public void ARomanNumeralSpellsTheSameChordInEveryKey()
    {
        (from degree in Gen.Int[0, 6]
         from root in Gen.Int[0, 11]
         from n in Gen.Int[1, 11]
         select (degree, root, n)).Sample(t =>
        {
            ScaleDegree[] degrees =
            [
                ScaleDegree.I, ScaleDegree.Ii, ScaleDegree.Iii, ScaleDegree.Iv,
                ScaleDegree.V, ScaleDegree.Vi, ScaleDegree.Vii,
            ];

            var chord = new RomanNumeralChord(degrees[t.degree], ChordQuality.Major, HarmonicFunction.Tonic);

            var here = chord.GetPitchClasses(new KeySignature((byte)t.root, true));
            var there = chord.GetPitchClasses(new KeySignature((byte)PitchMath.Fold(t.root + t.n), true));

            return here.Length == there.Length
                && here.Zip(there, (a, b) => PitchMath.Fold(a + t.n) == b).All(same => same);
        }, iter: 500);
    }
}
