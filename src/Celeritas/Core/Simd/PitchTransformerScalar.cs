// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.CompilerServices;

namespace Celeritas.Core.Simd;

internal sealed class PitchTransformerScalar : IPitchTransformer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Transpose(int* pitches, int count, int semitones)
    {
        var i = 0;
        var limit = count - 4;
        for (; i <= limit; i += 4)
        {
            pitches[i] = Moved(pitches[i], semitones);
            pitches[i + 1] = Moved(pitches[i + 1], semitones);
            pitches[i + 2] = Moved(pitches[i + 2], semitones);
            pitches[i + 3] = Moved(pitches[i + 3], semitones);
        }
        for (; i < count; i++)
            pitches[i] = Moved(pitches[i], semitones);
    }

    /// <summary>
    /// Transposing music moves its notes; its silences stay silent. Added to blindly, a rest's
    /// <see cref="MusicNotation.RestPitch"/> (-1) became a sounding note a fifth up.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Moved(int pitch, int semitones) =>
        pitch == MusicNotation.RestPitch ? pitch : pitch + semitones;
}

