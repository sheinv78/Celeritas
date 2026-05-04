// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

namespace Celeritas.Tests;

/// <summary>
/// Named MIDI pitch constants for use in test assertions.
/// Standard MIDI: C4 (middle C) = 60, each semitone = +1.
/// </summary>
internal static class MidiPitch
{
    // Octave 2
    public const int C2  = 36;
    public const int G2  = 43;

    // Octave 3
    public const int C3  = 48;
    public const int E3  = 52;
    public const int G3  = 55;
    public const int B3  = 59;

    // Octave 4  (middle C = C4 = 60)
    public const int C4  = 60;
    public const int CSharp4 = 61;
    public const int D4  = 62;
    public const int DSharp4 = 63;
    public const int E4  = 64;
    public const int F4  = 65;
    public const int FSharp4 = 66;
    public const int G4  = 67;
    public const int GSharp4 = 68;
    public const int A4  = 69;
    public const int ASharp4 = 70;
    public const int B4  = 71;

    // Octave 5
    public const int C5  = 72;
    public const int D5  = 74;
    public const int E5  = 76;
    public const int G5  = 79;
}

