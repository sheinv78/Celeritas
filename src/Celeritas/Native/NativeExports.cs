// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.InteropServices;
using Celeritas.Core;
using Celeritas.Core.Analysis;
using Celeritas.Core.Simd;

namespace Celeritas.Native;

/// <summary>
/// C-compatible structure for note events
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CNoteEvent
{
    public int Pitch;
    public int TimeNumerator;
    public int TimeDenominator;
    public int DurationNumerator;
    public int DurationDenominator;
    public int Velocity;
}

/// <summary>
/// Native C exports for Python bindings via ctypes
/// </summary>
public static class NativeExports
{
    /// <summary>
    /// Parse a single note from string notation
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_parse_note")]
    public static bool ParseNote(IntPtr notationPtr, IntPtr notePtr)
    {
        try
        {
            var notation = Marshal.PtrToStringUTF8(notationPtr);
            if (string.IsNullOrEmpty(notation))
                return false;

            var notes = MusicNotation.Parse(notation);
            if (notes.Length == 0)
                return false;

            var note = notes[0];
            var cNote = new CNoteEvent
            {
                Pitch = note.Pitch,
                TimeNumerator = (int)note.Offset.Numerator,
                TimeDenominator = (int)note.Offset.Denominator,
                DurationNumerator = (int)note.Duration.Numerator,
                DurationDenominator = (int)note.Duration.Denominator,
                Velocity = (int)(note.Velocity * 127)
            };

            Marshal.StructureToPtr(cNote, notePtr, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Transpose an array of pitches using SIMD
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_transpose")]
    public static void Transpose(IntPtr pitchesPtr, int count, int semitones)
    {
        try
        {
            unsafe
            {
                int* pitches = (int*)pitchesPtr;
                PitchTransformerFactory.Best.Transpose(pitches, count, semitones);
            }
        }
        catch
        {
            // Silent fail
        }
    }

    /// <summary>
    /// Identify a chord from pitches
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_identify_chord")]
    public static bool IdentifyChord(IntPtr pitchesPtr, int count, IntPtr bufferPtr, int bufferSize)
    {
        try
        {
            var pitches = new int[count];
            Marshal.Copy(pitchesPtr, pitches, 0, count);

            var chord = ChordAnalyzer.Identify(pitches);
            var symbol = $"{chord.Root}{chord.Quality}";

            if (symbol.Length >= bufferSize)
                symbol = symbol.Substring(0, bufferSize - 1);

            var bytes = System.Text.Encoding.UTF8.GetBytes(symbol + '\0');
            Marshal.Copy(bytes, 0, bufferPtr, Math.Min(bytes.Length, bufferSize));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect key from pitches
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_detect_key")]
    public static bool DetectKey(IntPtr pitchesPtr, int count, IntPtr bufferPtr, int bufferSize, IntPtr isMajorPtr)
    {
        try
        {
            var pitches = new int[count];
            Marshal.Copy(pitchesPtr, pitches, 0, count);

            var notes = pitches.Select(p => new NoteEvent(p, Rational.Zero, new Rational(1, 4), 0.8f)).ToArray();

            var result = KeyProfiler.DetectFromPitches(pitches);
            var keyName = result.Key.Root.ToString();

            if (keyName.Length >= bufferSize)
                keyName = keyName.Substring(0, bufferSize - 1);

            var bytes = System.Text.Encoding.UTF8.GetBytes(keyName + '\0');
            Marshal.Copy(bytes, 0, bufferPtr, Math.Min(bytes.Length, bufferSize));

            Marshal.WriteInt32(isMajorPtr, result.Key.IsMajor ? 1 : 0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
