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
    /// Forgets the calling thread's last error. Every export calls this first, so that
    /// <see cref="GetLastError"/> describes the most recent call and no other: a failure's
    /// message used to stay until the next failure overwrote it, and a C caller reading the
    /// error after a call that had <em>succeeded</em> was handed the previous call's complaint.
    /// (The Python wrapper reads the error only after a call reports failure, so it never saw
    /// the stale message; a caller who checks the error unconditionally did.)
    /// </summary>
    private static void ClearLastError() => _lastError = null;

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
    /// no pending error or the buffer is unusable. The message describes the most recent
    /// export called on this thread and no other: a call that succeeds leaves no message, so
    /// after a failure and then a success this writes nothing and returns 0.
    /// </summary>
    /// <remarks>
    /// The message used to be sticky — set on failure and never cleared — so a caller who read
    /// it after a successful call was handed the complaint of an earlier, unrelated one.
    /// </remarks>
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
            ClearLastError();

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
            ClearLastError();

            var notation = Marshal.PtrToStringUTF8(notationPtr);
            if (string.IsNullOrEmpty(notation))
            {
                SetLastError("Note notation string is null or empty.");
                return 0;
            }

            // TryParseNote, not MusicNotation.Parse: this export names one note, and the
            // notation parser reads a whole passage and was handing back its first event.
            // "C4 E4 G4" came back as C4 with the chord silently dropped, "R/4" came back as
            // a note of pitch -1 — the value this library reserves for silence — and the
            // grammar's octave is [0-9]+ with no sign, so every pitch in the bottom MIDI
            // octave, "C-1" through "B-1", was refused outright.
            if (!MusicNotation.TryParseNote(notation.AsSpan(), out var pitch))
            {
                SetLastError($"Could not parse note notation: '{notation}'.");
                return 0;
            }

            // The fields a bare note carries, unchanged from what the notation parser gave one.
            var note = new NoteEvent(pitch, Rational.Zero, Rational.Quarter);
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
            ClearLastError();

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
            ClearLastError();

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
    /// Detect key from pitches. Refuses an empty list (returns 0 with a last-error message
    /// saying a key needs notes): a key is a question about which notes sound, and no notes
    /// have no key.
    /// </summary>
    /// <remarks>
    /// The managed <see cref="KeyProfiler.DetectFromPitches(ReadOnlySpan{int})"/> answers empty
    /// input with the library's empty-input sentinel — C major at confidence 0, documented on
    /// its <see cref="NoteEvent"/> and notation overloads — which a C# caller can tell from a
    /// detection by reading the confidence. This export hands back only the tonic's name
    /// and whether the key is major, so it used to pass the sentinel on as if it were the answer:
    /// Python's <c>detect_key([])</c> returned <c>("C", True)</c>, indistinguishable from the same
    /// answer for a C major scale, with nothing a caller could check. An analysis entry point
    /// that would turn a sentinel into a confident wrong answer refuses instead (ADR 0002), the
    /// way this export already refused a buffer too small for the name.
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_detect_key", CallConvs = [typeof(CallConvCdecl)])]
    public static byte DetectKey(IntPtr pitchesPtr, int count, IntPtr bufferPtr, int bufferSize, IntPtr isMajorPtr)
    {
        try
        {
            ClearLastError();

            if (count <= 0)
            {
                SetLastError("Cannot detect a key from no notes: the pitch list is empty.");
                return 0;
            }

            var pitches = new int[count];
            Marshal.Copy(pitchesPtr, pitches, 0, count);

            var result = KeyProfiler.DetectFromPitches(pitches);

            // The tonic as the key is written — "Bb", not "A#" — the same name the managed
            // library's KeySignature.ToString gives; this export kept a sharp table of its own.
            var keyName = KeySpelling.TonicName(result.Key);

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
    /// <remarks>
    /// At most <paramref name="maxCount"/> pitches are written to <paramref name="pitchesOutPtr"/>;
    /// <paramref name="countOutPtr"/> receives the number the symbol names, which can be larger.
    /// A caller whose buffer was too small reads that number and asks again with a buffer that
    /// size. It used to receive the number that fit, so a caller had no way to tell a chord of
    /// exactly its buffer's size from one that had been cut to it, and the Python wrapper
    /// returned thirty-two pitches of a forty-pitch polychord with nothing to say so.
    /// </remarks>
    [UnmanagedCallersOnly(EntryPoint = "celeritas_parse_chord_symbol", CallConvs = [typeof(CallConvCdecl)])]
    public static byte ParseChordSymbol(IntPtr symbolPtr, IntPtr pitchesOutPtr, int maxCount, IntPtr countOutPtr)
    {
        try
        {
            ClearLastError();

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

            Marshal.WriteInt32(countOutPtr, pitches.Length);
            return 1;
        }
        catch (Exception ex)
        {
            SetLastError(ex);
            return 0;
        }
    }
}
