// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
    [UnmanagedCallersOnly(EntryPoint = "celeritas_parse_note", CallConvs = [typeof(CallConvCdecl)])]
    public static byte ParseNote(IntPtr notationPtr, IntPtr notePtr)
    {
        try
        {
            var notation = Marshal.PtrToStringUTF8(notationPtr);
            if (string.IsNullOrEmpty(notation))
                return 0;

            var notes = MusicNotation.Parse(notation);
            if (notes.Length == 0)
                return 0;

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
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Transpose an array of pitches using SIMD
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_transpose", CallConvs = [typeof(CallConvCdecl)])]
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
    [UnmanagedCallersOnly(EntryPoint = "celeritas_identify_chord", CallConvs = [typeof(CallConvCdecl)])]
    public static byte IdentifyChord(IntPtr pitchesPtr, int count, IntPtr bufferPtr, int bufferSize)
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
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Detect key from pitches
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_detect_key", CallConvs = [typeof(CallConvCdecl)])]
    public static byte DetectKey(IntPtr pitchesPtr, int count, IntPtr bufferPtr, int bufferSize, IntPtr isMajorPtr)
    {
        try
        {
            var pitches = new int[count];
            Marshal.Copy(pitchesPtr, pitches, 0, count);

            var result = KeyProfiler.DetectFromPitches(pitches);

            // Convert pitch class to note name
            string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
            var keyName = noteNames[result.Key.Root];

            if (keyName.Length >= bufferSize)
                keyName = keyName.Substring(0, bufferSize - 1);

            var bytes = System.Text.Encoding.UTF8.GetBytes(keyName + '\0');
            Marshal.Copy(bytes, 0, bufferPtr, Math.Min(bytes.Length, bufferSize));

            Marshal.WriteInt32(isMajorPtr, result.Key.IsMajor ? 1 : 0);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Parse a chord symbol (e.g. "C7(b9,#11)", "C/E", "C|G") to MIDI pitches.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_parse_chord_symbol", CallConvs = [typeof(CallConvCdecl)])]
    public static byte ParseChordSymbol(IntPtr symbolPtr, IntPtr pitchesOutPtr, int maxCount, IntPtr countOutPtr)
    {
        try
        {
            var symbol = Marshal.PtrToStringUTF8(symbolPtr);
            if (string.IsNullOrWhiteSpace(symbol))
                return 0;

            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
                return 0;

            var count = Math.Min(pitches.Length, Math.Max(0, maxCount));
            if (count > 0)
            {
                Marshal.Copy(pitches, 0, pitchesOutPtr, count);
            }

            Marshal.WriteInt32(countOutPtr, count);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}
