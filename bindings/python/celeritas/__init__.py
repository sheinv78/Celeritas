"""Celeritas Python Package

Public API surface for the Celeritas native bindings.
"""

from .celeritas import (
    NoteEvent,
    ChordQuality,
    MordentType,
    TurnType,
    parse_note,
    midi_to_note_name,
    transpose,
    identify_chord,
    detect_key,
    parse_chord_symbol,
    Trill,
    Mordent,
    __version__,
)

from .dotnet import DotNetLoadResult, is_pythonnet_available, load_celeritas

__all__ = [
    "NoteEvent",
    "ChordQuality",
    "MordentType",
    "TurnType",
    "parse_note",
    "midi_to_note_name",
    "transpose",
    "identify_chord",
    "detect_key",
    "parse_chord_symbol",
    "Trill",
    "Mordent",
    "__version__",
    "DotNetLoadResult",
    "is_pythonnet_available",
    "load_celeritas",
]
