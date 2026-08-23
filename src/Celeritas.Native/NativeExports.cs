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
    [ThreadStatic]
    private static string? _lastError;

    private static void SetLastError(string message) => _lastError = message;

    private static void SetLastError(Exception ex) => _lastError = ex.Message;

    /// <summary>
    /// Write a NUL-terminated UTF-8 string into a caller-provided buffer.
    /// Fails (returns false) instead of truncating when the buffer is too small.
    /// </summary>
    private static bool TryWriteUtf8(string value, IntPtr bufferPtr, int bufferSize)
    {
        if (bufferPtr == IntPtr.Zero || bufferSize <= 0)
            return false;

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        if (bytes.Length + 1 > bufferSize)
            return false;

        Marshal.Copy(bytes, 0, bufferPtr, bytes.Length);
        Marshal.WriteByte(bufferPtr, bytes.Length, 0);
        return true;
    }

    /// <summary>
    /// Copy the last error message (for the calling thread) into <paramref name="bufferPtr"/>
    /// as NUL-terminated UTF-8. Returns the number of bytes written (excluding the
    /// terminator). Truncates if the buffer is too small; returns 0 when there is
    /// no pending error or the buffer is unusable.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_get_last_error", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetLastError(IntPtr bufferPtr, int bufferSize)
    {
        try
        {
            if (bufferPtr == IntPtr.Zero || bufferSize <= 0)
                return 0;

            var message = _lastError ?? string.Empty;
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            var count = Math.Min(bytes.Length, bufferSize - 1);
            if (count > 0)
                Marshal.Copy(bytes, 0, bufferPtr, count);
            Marshal.WriteByte(bufferPtr, count, 0);
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Write the native library version (e.g. "1.2.3") into the buffer as
    /// NUL-terminated UTF-8. Returns 1 on success, 0 on failure.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_version", CallConvs = [typeof(CallConvCdecl)])]
    public static byte GetVersion(IntPtr bufferPtr, int bufferSize)
    {
        try
        {
            var version = typeof(NativeExports).Assembly.GetName().Version;
            var text = version is null ? "0.0.0" : version.ToString(3);

            if (!TryWriteUtf8(text, bufferPtr, bufferSize))
            {
                SetLastError($"Buffer too small for version string (need at least {text.Length + 1} bytes).");
                return 0;
            }

            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
            return 0;
        }
    }

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
            {
                SetLastError("Note notation string is null or empty.");
                return 0;
            }

            var notes = MusicNotation.Parse(notation);
            if (notes.Length == 0)
            {
                SetLastError($"Could not parse note notation: '{notation}'.");
                return 0;
            }

            var note = notes[0];
            var cNote = new CNoteEvent
            {
                Pitch = note.Pitch,
                TimeNumerator = (int)note.Offset.Numerator,
                TimeDenominator = (int)note.Offset.Denominator,
                DurationNumerator = (int)note.Duration.Numerator,
                DurationDenominator = (int)note.Duration.Denominator,
                Velocity = (int)MathF.Round(note.Velocity * 127)
            };

            Marshal.StructureToPtr(cNote, notePtr, false);
            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
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
        catch (Exception ex)
        {
            SetLastError(ex);
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

            if (!TryWriteUtf8(symbol, bufferPtr, bufferSize))
            {
                SetLastError($"Buffer too small for chord symbol '{symbol}' (size {bufferSize}).");
                return 0;
            }

            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
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

            if (!TryWriteUtf8(keyName, bufferPtr, bufferSize))
            {
                SetLastError($"Buffer too small for key name '{keyName}' (size {bufferSize}).");
                return 0;
            }

            Marshal.WriteInt32(isMajorPtr, result.Key.IsMajor ? 1 : 0);
            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
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
            {
                SetLastError("Chord symbol string is null or empty.");
                return 0;
            }

            var pitches = ProgressionAdvisor.ParseChordSymbol(symbol);
            if (pitches.Length == 0)
            {
                SetLastError($"Could not parse chord symbol: '{symbol}'.");
                return 0;
            }

            var count = Math.Min(pitches.Length, Math.Max(0, maxCount));
            if (count > 0)
            {
                Marshal.Copy(pitches, 0, pitchesOutPtr, count);
            }

            Marshal.WriteInt32(countOutPtr, count);
            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
            return 0;
        }
    }
}
