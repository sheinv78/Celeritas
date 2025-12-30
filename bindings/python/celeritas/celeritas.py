"""
Celeritas - High-Performance Music Engine for Python
Python bindings for Celeritas .NET library

Author: Vladimir V. Shein
License: BSL-1.1
"""

import ctypes
import os
import platform
from typing import List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum


_NOTE_NAMES_SHARP = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]

_NOTE_NAMES_FLAT = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"]


def _load_native_library():
    system = platform.system()
    if system == "Windows":
        lib_name = "Celeritas.Native.dll"
    elif system == "Darwin":
        lib_name = "libCeleritas.Native.dylib"
    else:  # Linux
        lib_name = "libCeleritas.Native.so"

    # Try to load from package directory
    lib_path = os.path.join(os.path.dirname(__file__), "native", lib_name)
    if os.path.exists(lib_path):
        return ctypes.CDLL(lib_path)

    # Try system path
    try:
        return ctypes.CDLL(lib_name)
    except OSError:
        raise RuntimeError(f"Could not load Celeritas native library: {lib_name}")


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
    """

    _lib.celeritas_identify_chord.argtypes = [
        ctypes.POINTER(ctypes.c_int),
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
    ]
    _lib.celeritas_identify_chord.restype = ctypes.c_byte

    n = len(pitches)
    pitch_array = (ctypes.c_int * n)(*pitches)
    buffer = ctypes.create_string_buffer(64)

    success = _lib.celeritas_identify_chord(pitch_array, n, buffer, 64)
    if success:
        return buffer.value.decode("utf-8")
    return "Unknown"


def detect_key(pitches: List[int]) -> Tuple[str, bool]:
    """Detect the key of a sequence of pitches.

    Args:
        pitches: List of MIDI pitch values

    Returns:
        Tuple of (key_name, is_major)
    """

    _lib.celeritas_detect_key.argtypes = [
        ctypes.POINTER(ctypes.c_int),
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.POINTER(ctypes.c_int),
    ]
    _lib.celeritas_detect_key.restype = ctypes.c_byte

    n = len(pitches)
    pitch_array = (ctypes.c_int * n)(*pitches)
    buffer = ctypes.create_string_buffer(16)
    is_major = ctypes.c_int()

    success = _lib.celeritas_detect_key(
        pitch_array, n, buffer, 16, ctypes.byref(is_major)
    )
    if success:
        return (buffer.value.decode("utf-8"), bool(is_major.value))
    return ("C", True)


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
        """Expand trill into sequence of notes"""

        notes = []
        note_duration = 1.0 / (self.speed * 4)
        upper_pitch = self.base_note.pitch + self.interval

        current_time = self.base_note.time
        end_time = current_time + self.base_note.duration

        use_upper = self.start_with_upper

        while current_time < end_time:
            pitch = upper_pitch if use_upper else self.base_note.pitch
            notes.append(
                NoteEvent(
                    pitch=pitch,
                    time_numerator=int(current_time * 4),
                    time_denominator=4,
                    duration_numerator=1,
                    duration_denominator=self.speed * 4,
                    velocity=self.base_note.velocity,
                )
            )
            current_time += note_duration
            use_upper = not use_upper

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
        """Expand mordent into sequence of notes"""

        notes = []
        note_count = 2 * self.alternations + 1
        note_duration = self.base_note.duration / note_count

        neighbor_pitch = (
            self.base_note.pitch + self.interval
            if self.type == MordentType.UPPER
            else self.base_note.pitch - self.interval
        )

        current_time = self.base_note.time

        for i in range(note_count):
            pitch = self.base_note.pitch if i % 2 == 0 else neighbor_pitch
            notes.append(
                NoteEvent(
                    pitch=pitch,
                    time_numerator=int(current_time * 4),
                    time_denominator=4,
                    duration_numerator=int(note_duration * 4),
                    duration_denominator=4,
                    velocity=self.base_note.velocity,
                )
            )
            current_time += note_duration

        return notes


try:
    from importlib.metadata import version as _pkg_version
except Exception:  # pragma: no cover
    _pkg_version = None

if _pkg_version is None:  # pragma: no cover
    __version__ = "0.0.0"
else:
    try:
        __version__ = _pkg_version("celeritas")
    except Exception:  # pragma: no cover
        __version__ = "0.0.0"
