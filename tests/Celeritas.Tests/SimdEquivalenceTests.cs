using Celeritas.Core;
using Celeritas.Core.Simd;

namespace Celeritas.Tests;

/// <summary>
/// Verifies that the SIMD pitch transformers produce exactly the same result as a plain loop,
/// for every length from 0 to beyond the widest vector width, and that no kernel reads or
/// writes past the end of the buffer (guard sentinels around the payload).
/// </summary>
public class SimdEquivalenceTests
{
    private const int GuardSize = 32;
    private const int GuardValue = unchecked((int)0xDEADBEEF);
    private const int MaxCount = 133; // comfortably past the widest vector width + tail

    private static IEnumerable<(string Name, IPitchTransformer Impl)> AvailableTransformers()
    {
        yield return ("Scalar", new PitchTransformerScalar());
        // Portable Vector<T> kernel — the JIT widens it to the platform's widest unit.
        yield return ("Vector", new PitchTransformerVector());
    }

    [Fact]
    public unsafe void Transpose_AllKernels_MatchPlainLoop_AndStayInBounds()
    {
        var rng = new Random(12345);

        foreach (var (name, impl) in AvailableTransformers())
        {
            for (var count = 0; count < MaxCount; count++)
            {
                foreach (var semitones in new[] { 0, 2, -5, 127 })
                {
                    var source = new int[count];
                    for (var k = 0; k < count; k++)
                        source[k] = rng.Next(0, 128);

                    var expected = new int[count];
                    for (var k = 0; k < count; k++)
                        expected[k] = source[k] + semitones;

                    var padded = new int[GuardSize + count + GuardSize];
                    Array.Fill(padded, GuardValue, 0, GuardSize);
                    Array.Copy(source, 0, padded, GuardSize, count);
                    Array.Fill(padded, GuardValue, GuardSize + count, GuardSize);

                    fixed (int* p = padded)
                    {
                        impl.Transpose(p + GuardSize, count, semitones);
                    }

                    for (var k = 0; k < GuardSize; k++)
                    {
                        Assert.True(padded[k] == GuardValue,
                            $"{name}: leading guard corrupted at [{k}] for count={count}, semitones={semitones}");
                        Assert.True(padded[GuardSize + count + k] == GuardValue,
                            $"{name}: trailing guard corrupted at [+{k}] for count={count}, semitones={semitones}");
                    }

                    for (var k = 0; k < count; k++)
                    {
                        Assert.True(padded[GuardSize + k] == expected[k],
                            $"{name}: wrong result at [{k}] for count={count}, semitones={semitones}: " +
                            $"expected {expected[k]}, got {padded[GuardSize + k]}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Transpose_ViaNoteBuffer_VectorWidthBoundaryLengths()
    {
        // Lengths straddling the SIMD width / tail boundary for various vector widths (4/8/16)
        foreach (var count in new[] { 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 63, 95 })
        {
            using var buffer = new NoteBuffer(count);
            for (var i = 0; i < count; i++)
                buffer.AddNote(60 + (i % 12), new Rational(i, 4), Rational.Quarter);

            MusicMath.Transpose(buffer, 7);

            for (var i = 0; i < count; i++)
                Assert.Equal(67 + (i % 12), buffer.PitchAt(i));
        }
    }

    [Fact]
    public void ScaleVelocity_MatchesPlainLoop_AtAllBoundaryLengths()
    {
        var rng = new Random(999);

        foreach (var count in new[] { 0, 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 64, 100, 129 })
        {
            var expected = new float[count];
            using var buffer = new NoteBuffer(Math.Max(count, 1));
            for (var i = 0; i < count; i++)
            {
                var v = (float)rng.NextDouble();
                buffer.AddNote(60, new Rational(i, 4), Rational.Quarter, v);
                expected[i] = v * 0.75f;
            }

            MusicMath.ScaleVelocity(buffer, 0.75f);

            for (var i = 0; i < count; i++)
                Assert.Equal(expected[i], buffer.GetVelocity(i), precision: 6);
        }
    }

    [Fact]
    public void Quantize_NegativeOffset_RoundsToNearestGridStep()
    {
        using var buffer = new NoteBuffer(2);
        buffer.AddNote(60, new Rational(-7, 16), Rational.Quarter); // -0.4375 → nearest 1/4 step is -1/2
        buffer.AddNote(64, new Rational(-1, 16), Rational.Quarter); // -0.0625 → nearest 1/4 step is 0

        MusicMath.Quantize(buffer, Rational.Quarter);

        Assert.Equal(new Rational(-1, 2), buffer.GetOffset(0));
        Assert.Equal(Rational.Zero, buffer.GetOffset(1));
    }

    [Fact]
    public void Quantize_ZeroGrid_Throws()
    {
        using var buffer = new NoteBuffer(1);
        buffer.AddNote(60, Rational.Quarter, Rational.Quarter);

        Assert.Throws<ArgumentOutOfRangeException>(() => MusicMath.Quantize(buffer, Rational.Zero));
    }

    [Fact]
    public void Transpose_DisposedBuffer_Throws()
    {
        var buffer = new NoteBuffer(4);
        buffer.AddNote(60, Rational.Zero, Rational.Quarter);
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => MusicMath.Transpose(buffer, 2));
        Assert.Throws<ObjectDisposedException>(() => MusicMath.ScaleVelocity(buffer, 0.5f));
        Assert.Throws<ObjectDisposedException>(() => MusicMath.Quantize(buffer, Rational.Quarter));
    }
}
