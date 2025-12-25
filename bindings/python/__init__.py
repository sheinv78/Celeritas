"""
Celeritas Python Package
"""

from .celeritas import (
    NoteEvent,
    ChordQuality,
    MordentType,
    TurnType,
    parse_note,
    transpose,
    identify_chord,
    detect_key,
    Trill,
    Mordent,
    __version__,
)

__all__ = [
    "NoteEvent",
    "ChordQuality",
    "MordentType",
    "TurnType",
    "parse_note",
    "transpose",
    "identify_chord",
    "detect_key",
    "Trill",
    "Mordent",
    "__version__",
]
