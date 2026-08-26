"""
Celeritas - High-Performance Music Engine for Python
Python bindings for Celeritas .NET library

Author: Vladimir V. Shein
License: BSL-1.1
"""

import ctypes
import os
import platform
from fractions import Fraction
from typing import List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum

_NOTE_NAMES_SHARP = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]

_NOTE_NAMES_FLAT = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]


class CeleritasError(Exception):
    """Raised when a native Celeritas call fails."""


def _load_native_library() -> ctypes.CDLL:
    system = platform.system()
    if system == "Windows":
        lib_names = ["Celeritas.Native.dll"]
    elif system == "Darwin":
        # NativeAOT publishes "Celeritas.Native.dylib" (no "lib" prefix);
        # packagers may rename it to the conventional prefixed form.
        lib_names = ["libCeleritas.Native.dylib", "Celeritas.Native.dylib"]
    else:  # Linux
        lib_names = ["libCeleritas.Native.so", "Celeritas.Native.so"]

    native_dir = os.path.join(os.path.dirname(__file__), "native")
    attempted = []
    last_error: Optional[OSError] = None

    # Try to load from package directory
    for lib_name in lib_names:
        lib_path = os.path.join(native_dir, lib_name)
        attempted.append(lib_path)
        if os.path.exists(lib_path):
            try:
                return ctypes.CDLL(lib_path)
            except OSError as exc:
                last_error = exc

    # Try system path
    for lib_name in lib_names:
        attempted.append(lib_name)
        try:
            return ctypes.CDLL(lib_name)
        except OSError as exc:
            last_error = exc

    raise RuntimeError(
        "Could not load the Celeritas native library.\n"
        "Tried: " + ", ".join(attempted) + "\n"
        "Build it with:\n"
        "  dotnet publish src/Celeritas.Native/Celeritas.Native.csproj"
        " -c Release -r <rid>\n"
        "and copy the resulting shared library into the package's"
        " 'celeritas/native' directory."
    ) from last_error


_lib = _load_native_library()


@dataclass
class NoteEvent:
    """Represents a single note event with pitch, time, duration, and velocity"""

    pitch: int  # MIDI pitch (0-127)
    time_numerator: int
    time_denominator: int
    duration_numerator: int
    duration_denominator: int
    velocity: int = 80

    @property
    def time(self) -> float:
        """Get time as floating point"""

        return self.time_numerator / self.time_denominator

    @property
    def duration(self) -> float:
        """Get duration as floating point"""

        return self.duration_numerator / self.duration_denominator


class ChordQuality(Enum):
    """Chord quality types"""

    MAJOR = 0
    MINOR = 1
    DIMINISHED = 2
    AUGMENTED = 3
    DOMINANT = 4
    MAJOR_SEVENTH = 5
    MINOR_SEVENTH = 6


class MordentType(Enum):
    """Type of mordent"""

    UPPER = 0
    LOWER = 1


class TurnType(Enum):
    """Type of turn"""

    NORMAL = 0
    INVERTED = 1


class CNoteEvent(ctypes.Structure):
    _fields_ = [
        ("pitch", ctypes.c_int),
        ("time_num", ctypes.c_int),
        ("time_den", ctypes.c_int),
        ("dur_num", ctypes.c_int),
        ("dur_den", ctypes.c_int),
        ("velocity", ctypes.c_int),
    ]


_lib.celeritas_parse_note.argtypes = [ctypes.c_char_p, ctypes.POINTER(CNoteEvent)]
_lib.celeritas_parse_note.restype = ctypes.c_byte

_lib.celeritas_transpose.argtypes = [
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
]
_lib.celeritas_transpose.restype = None

_lib.celeritas_parse_chord_symbol.argtypes = [
    ctypes.c_char_p,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]
_lib.celeritas_parse_chord_symbol.restype = ctypes.c_byte

_lib.celeritas_identify_chord.argtypes = [
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_char_p,
    ctypes.c_int,
]
_lib.celeritas_identify_chord.restype = ctypes.c_byte

_lib.celeritas_detect_key.argtypes = [
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_char_p,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]
_lib.celeritas_detect_key.restype = ctypes.c_byte

# Newer exports; may be absent when an older native library is loaded.
try:
    _lib.celeritas_get_last_error.argtypes = [ctypes.c_char_p, ctypes.c_int]
    _lib.celeritas_get_last_error.restype = ctypes.c_int
    _has_get_last_error = True
except AttributeError:  # pragma: no cover - old native library
    _has_get_last_error = False

try:
    _lib.celeritas_version.argtypes = [ctypes.c_char_p, ctypes.c_int]
    _lib.celeritas_version.restype = ctypes.c_byte
    _has_version = True
except AttributeError:  # pragma: no cover - old native library
    _has_version = False


def _get_last_error() -> str:
    """Fetch the last error message recorded by the native library."""

    if not _has_get_last_error:
        return ""
    buffer = ctypes.create_string_buffer(1024)
    written = _lib.celeritas_get_last_error(buffer, len(buffer))
    if written <= 0:
        return ""
    return buffer.value.decode("utf-8", errors="replace")


def native_version() -> str:
    """Return the version string reported by the native library."""

    if not _has_version:
        raise CeleritasError(
            "The loaded native library does not export celeritas_version; "
            "rebuild the native library from the current sources."
        )
    buffer = ctypes.create_string_buffer(64)
    success = _lib.celeritas_version(buffer, len(buffer))
    if not success:
        raise CeleritasError(
            _get_last_error() or "celeritas_version failed with no error message"
        )
    return buffer.value.decode("utf-8")


def parse_note(notation: str) -> Optional[NoteEvent]:
    """Parse a single note from string notation (e.g., 'C4', 'F#5', 'Bb3').

    Args:
        notation: Note notation string

    Returns:
        NoteEvent or None if parsing failed
    """

    c_note = CNoteEvent()
    success = _lib.celeritas_parse_note(notation.encode("utf-8"), ctypes.byref(c_note))

    if success:
        return NoteEvent(
            pitch=c_note.pitch,
            time_numerator=c_note.time_num,
            time_denominator=c_note.time_den,
            duration_numerator=c_note.dur_num,
            duration_denominator=c_note.dur_den,
            velocity=c_note.velocity,
        )
    return None


def transpose(pitches: List[int], semitones: int) -> List[int]:
    """Transpose a list of pitches using SIMD acceleration.

    Args:
        pitches: List of MIDI pitch values
        semitones: Number of semitones to transpose (positive = up, negative = down)

    Returns:
        List of transposed pitches
    """

    n = len(pitches)
    pitch_array = (ctypes.c_int * n)(*pitches)
    _lib.celeritas_transpose(pitch_array, n, semitones)
    return list(pitch_array)


def midi_to_note_name(pitch: int, prefer_flats: bool = False) -> str:
    """Convert MIDI pitch (0-127) to scientific pitch notation (e.g., 60 -> 'C4').

    Args:
        pitch: MIDI pitch number (0-127)
        prefer_flats: Use flats (Bb) instead of sharps (A#) when applicable

    Returns:
        Note name like 'C4', 'F#5', 'Bb3'
    """

    if not (0 <= pitch <= 127):
        raise ValueError(f"MIDI pitch must be in [0..127], got {pitch}")

    names = _NOTE_NAMES_FLAT if prefer_flats else _NOTE_NAMES_SHARP
    pc = pitch % 12
    octave = (pitch // 12) - 1  # MIDI standard: C-1 = 0
    return f"{names[pc]}{octave}"


def identify_chord(pitches: List[int]) -> str:
    """Identify a chord from a list of pitches.

    Args:
        pitches: List of MIDI pitch values

    Returns:
        Chord symbol (e.g., 'Cmaj', 'Dm7', 'G7')

    Raises:
        CeleritasError: If the native chord identification fails.
    """

    n = len(pitches)
    pitch_array = (ctypes.c_int * n)(*pitches)
    buffer = ctypes.create_string_buffer(64)

    success = _lib.celeritas_identify_chord(pitch_array, n, buffer, 64)
    if not success:
        raise CeleritasError(_get_last_error() or "celeritas_identify_chord failed")
    return buffer.value.decode("utf-8")


def detect_key(pitches: List[int]) -> Tuple[str, bool]:
    """Detect the key of a sequence of pitches.

    Args:
        pitches: List of MIDI pitch values

    Returns:
        Tuple of (key_name, is_major)

    Raises:
        CeleritasError: If the native key detection fails.
    """

    n = len(pitches)
    pitch_array = (ctypes.c_int * n)(*pitches)
    buffer = ctypes.create_string_buffer(16)
    is_major = ctypes.c_int()

    success = _lib.celeritas_detect_key(
        pitch_array, n, buffer, 16, ctypes.byref(is_major)
    )
    if not success:
        raise CeleritasError(_get_last_error() or "celeritas_detect_key failed")
    return (buffer.value.decode("utf-8"), bool(is_major.value))


def parse_chord_symbol(symbol: str, max_pitches: int = 32) -> Optional[List[int]]:
    """Parse a chord symbol (e.g. "C7(b9,#11)", "C/E", "C|G") into MIDI pitches.

    Args:
        symbol: Chord symbol string.
        max_pitches: Maximum number of pitches to return.

    Returns:
        List of MIDI pitches, or None if parsing failed.
    """

    if symbol is None:
        return None
    if max_pitches <= 0:
        return []

    out_count = ctypes.c_int()
    out_array = (ctypes.c_int * max_pitches)()

    success = _lib.celeritas_parse_chord_symbol(
        symbol.encode("utf-8"),
        out_array,
        max_pitches,
        ctypes.byref(out_count),
    )

    if not success:
        return None

    return list(out_array)[: out_count.value]


def _playable(pitch: int) -> int:
    """Holds an ornamental pitch on the keyboard, as Ornament.Playable does in the library."""

    return max(0, min(127, pitch))


class Trill:
    """Trill ornament - rapid alternation between main note and upper note"""

    def __init__(
        self,
        base_note: NoteEvent,
        interval: int = 2,
        speed: int = 8,
        start_with_upper: bool = False,
        end_with_turn: bool = False,
    ):
        self.base_note = base_note
        self.interval = interval
        self.speed = speed
        self.start_with_upper = start_with_upper
        self.end_with_turn = end_with_turn

    def expand(self) -> List[NoteEvent]:
        """Expand trill into sequence of notes.

        Follows Celeritas.Core.Ornamentation.Trill note for note: as many whole trill units as
        fit in the base note, the last one stretched to end exactly where the base note does.

        This used to run a loop until the base note ran out, which put an extra stub note at the
        end whenever the duration was not a whole number of units - a trill on a 3/8 note at
        speed 3 came out of the bindings with five notes where the library gives four. Two
        implementations of one piece of music theory drift, and nothing compared them.
        """

        base_pitch = self.base_note.pitch
        step = Fraction(1, self.speed * 4)
        upper_pitch = _playable(base_pitch + self.interval)
        # The turn drops to the note below: a whole tone when the trill is a whole tone wide,
        # a semitone otherwise.
        lower_pitch = _playable(base_pitch - (2 if self.interval == 2 else 1))

        start_time = Fraction(
            self.base_note.time_numerator, self.base_note.time_denominator
        )
        duration = Fraction(
            self.base_note.duration_numerator, self.base_note.duration_denominator
        )
        end_time = start_time + duration

        # How many whole trill units fit. A base note shorter than one of them is left alone:
        # expanding it would delete it.
        total_notes = duration // step
        if total_notes == 0:
            return [self.base_note]

        if self.end_with_turn and total_notes >= 3:
            total_notes -= 2  # the last two belong to the turn

        notes = []
        current_time = start_time
        use_upper = self.start_with_upper

        for _ in range(int(total_notes)):
            if current_time >= end_time:
                break

            notes.append(
                NoteEvent(
                    pitch=upper_pitch if use_upper else base_pitch,
                    time_numerator=current_time.numerator,
                    time_denominator=current_time.denominator,
                    duration_numerator=step.numerator,
                    duration_denominator=step.denominator,
                    velocity=self.base_note.velocity,
                )
            )
            current_time += step
            use_upper = not use_upper

        if self.end_with_turn and current_time < end_time:
            notes.append(
                NoteEvent(
                    pitch=lower_pitch,
                    time_numerator=current_time.numerator,
                    time_denominator=current_time.denominator,
                    duration_numerator=step.numerator,
                    duration_denominator=step.denominator,
                    velocity=self.base_note.velocity,
                )
            )
            current_time += step

            if current_time < end_time:
                tail = end_time - current_time
                notes.append(
                    NoteEvent(
                        pitch=base_pitch,
                        time_numerator=current_time.numerator,
                        time_denominator=current_time.denominator,
                        duration_numerator=tail.numerator,
                        duration_denominator=tail.denominator,
                        velocity=self.base_note.velocity,
                    )
                )

        # Stretch the final note to the exact end of the base note, so the expansion sums to the
        # base duration and leaves no gap before the next melody note.
        if notes:
            last = notes[-1]
            last_start = Fraction(last.time_numerator, last.time_denominator)
            last_length = Fraction(last.duration_numerator, last.duration_denominator)
            if last_start + last_length != end_time:
                stretched = end_time - last_start
                notes[-1] = NoteEvent(
                    pitch=last.pitch,
                    time_numerator=last.time_numerator,
                    time_denominator=last.time_denominator,
                    duration_numerator=stretched.numerator,
                    duration_denominator=stretched.denominator,
                    velocity=last.velocity,
                )

        return notes


class Mordent:
    """Mordent ornament - brief alternation with upper or lower neighbor"""

    def __init__(
        self,
        base_note: NoteEvent,
        mordent_type: MordentType = MordentType.UPPER,
        interval: int = 2,
        alternations: int = 1,
    ):
        self.base_note = base_note
        self.type = mordent_type
        self.interval = interval
        self.alternations = alternations

    def expand(self) -> List[NoteEvent]:
        """Expand mordent into sequence of notes.

        Timing is computed with exact rational arithmetic (fractions of a
        whole note), so the expanded durations sum exactly to the base
        note's duration and no zero-duration notes are produced.
        """

        notes = []
        note_count = 2 * self.alternations + 1
        note_duration = (
            Fraction(
                self.base_note.duration_numerator,
                self.base_note.duration_denominator,
            )
            / note_count
        )

        neighbor_pitch = (
            self.base_note.pitch + self.interval
            if self.type == MordentType.UPPER
            else self.base_note.pitch - self.interval
        )

        current_time = Fraction(
            self.base_note.time_numerator, self.base_note.time_denominator
        )

        for i in range(note_count):
            pitch = self.base_note.pitch if i % 2 == 0 else neighbor_pitch
            notes.append(
                NoteEvent(
                    pitch=pitch,
                    time_numerator=current_time.numerator,
                    time_denominator=current_time.denominator,
                    duration_numerator=note_duration.numerator,
                    duration_denominator=note_duration.denominator,
                    velocity=self.base_note.velocity,
                )
            )
            current_time += note_duration

        return notes


def _detect_package_version() -> str:
    try:
        from importlib.metadata import version as pkg_version

        return pkg_version("celeritas")
    except Exception:  # pragma: no cover
        return "0.0.0"


__version__ = _detect_package_version()
