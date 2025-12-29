# Celeritas Python Bindings

Python bindings for the Celeritas high-performance music engine.

## Installation

```bash
# From this repo (recommended for now)
pip install -e ./bindings/python

# Or, if/when published to PyPI
pip install celeritas
```

## Quick Start

```python
from celeritas import parse_note, transpose, identify_chord, detect_key, parse_chord_symbol, Trill

# Parse a note
note = parse_note("C4")
print(f"Pitch: {note.pitch}, Time: {note.time}, Duration: {note.duration}")

# Transpose pitches (uses SIMD acceleration)
pitches = [60, 64, 67]  # C, E, G
transposed = transpose(pitches, 2)  # Up 2 semitones
print(f"Transposed: {transposed}")  # [62, 66, 69] = D, F#, A

# Identify chord
chord = identify_chord([60, 64, 67])
print(f"Chord: {chord}")  # "Cmaj"

# Parse chord symbols (ANTLR-backed)
print(parse_chord_symbol("C7(b9,#11)"))
print(parse_chord_symbol("C/E"))
print(parse_chord_symbol("C|G"))

# Detect key
key_name, is_major = detect_key([60, 62, 64, 65, 67, 69, 71])
print(f"Key: {key_name} {'Major' if is_major else 'Minor'}")

# Create ornaments
note = parse_note("C4")
trill = Trill(note, interval=2, speed=8)
expanded_notes = trill.expand()
print(f"Trill expanded to {len(expanded_notes)} notes")
```

## Features

- **SIMD-Accelerated Operations** - Fast pitch transposition via native SIMD
- **Basic Note Notation** - Parse single notes like `C4`, `F#5`, `Bb3`
- **Chord Identification** - Identify a chord from MIDI pitches
- **Key Detection** - Detect a key from a pitch sequence
- **Pitch Name Conversion** - Convert MIDI pitches to note names (e.g. `60 -> C4`)
- **Ornaments** - Trills and mordents (expand to note sequences)

Chord-symbol parsing is implemented in the native library and exposed in Python via `parse_chord_symbol`.

## API Reference

The Python bindings currently expose a focused subset of the Celeritas engine.
This section lists the complete public API available from:

```python
import celeritas
```

### Types

- `NoteEvent(pitch, time_numerator, time_denominator, duration_numerator, duration_denominator, velocity=80)`
    - Convenience properties: `note.time` and `note.duration` (floats)
- `ChordQuality` (enum)
- `MordentType` (enum): `UPPER`, `LOWER`
- `TurnType` (enum): `NORMAL`, `INVERTED`

### Functions

- `parse_note(notation: str) -> Optional[NoteEvent]`
    - Parses a single note like `C4`, `F#5`, `Bb3`
- `transpose(pitches: List[int], semitones: int) -> List[int]`
    - SIMD-accelerated transposition
- `midi_to_note_name(pitch: int, prefer_flats: bool = False) -> str`
    - Converts MIDI pitch (0..127) to scientific pitch notation
- `identify_chord(pitches: List[int]) -> str`
    - Returns chord symbol like `Cmaj`, `Dm7`, `G7`
- `detect_key(pitches: List[int]) -> Tuple[str, bool]`
    - Returns `(key_name, is_major)`
- `parse_chord_symbol(symbol: str, max_pitches: int = 32) -> Optional[List[int]]`
    - Parses chord symbols like `C7(b9,#11)`, `C/E`, `C|G`

### Ornaments

- `Trill(base_note: NoteEvent, interval: int = 2, speed: int = 8, start_with_upper: bool = False, end_with_turn: bool = False)`
    - `expand() -> List[NoteEvent]`
- `Mordent(base_note: NoteEvent, mordent_type: MordentType = MordentType.UPPER, interval: int = 2, alternations: int = 1)`
    - `expand() -> List[NoteEvent]`

If you want the Python bindings to cover more of the C# API, the usual path is:
1) add new NativeAOT exports in `src/Celeritas.Native`, then
2) expose them via `ctypes` in `bindings/python/celeritas/celeritas.py`.

## Full .NET API (Complete Coverage)

If you need **the entire Celeritas .NET API surface** in Python, the bindings include an **opt-in** bridge
powered by `pythonnet`.

This keeps the current NativeAOT bindings (fast + no extra deps) and additionally enables calling any public
.NET type/method directly.

### Install

```bash
pip install pythonnet
```

### Build the managed assembly

From the repo root:

```bash
dotnet build src/Celeritas/Celeritas.csproj -c Release
```

### Use

```python
from celeritas import load_celeritas

result = load_celeritas()  # loads Celeritas.dll via pythonnet
Celeritas = result.namespace

print("Loaded:", result.assembly_path)

# From here you can use the full .NET API under the Celeritas namespace.
```

You can also override the assembly location:

```bash
export CELERITAS_DOTNET_ASSEMBLY=/abs/path/to/Celeritas.dll
```

## Requirements

- Python 3.8+
- Celeritas native library (included in package)

## Performance

Python bindings maintain near-native performance for core operations:

```python
import time
from celeritas import transpose

# Benchmark: transpose 1 million notes
pitches = list(range(60, 72)) * 100000  # 1.2M pitches
start = time.time()
transposed = transpose(pitches, 5)
elapsed = time.time() - start
print(f"Transposed {len(pitches)} pitches in {elapsed*1000:.2f}ms")
# Note: timings vary widely by CPU, build flags, and Python overhead.
```

## License

Licensed under the Business Source License 1.1 (BSL-1.1).

- ✅ Free for non-commercial use
- ✅ Free for open source projects
- ⚠️ Commercial use requires a license

For commercial licensing, contact: sheinv78@gmail.com

## Testing

Run the test suite:

```bash
# Using unittest (standard library)
python test_celeritas.py

# Or with pytest
pip install pytest
pytest test_celeritas.py -v

# With coverage
pip install pytest-cov
pytest test_celeritas.py --cov=celeritas --cov-report=html
```

**Test Coverage:** 35 tests (100% passing) covering NoteEvent, parsing, transpose (SIMD), chord/key detection, ornaments (trills, mordents), and integration scenarios.

### Building Native Library

The Python bindings require a native library built with NativeAOT:

Quick path (recommended):

```bash
# From repo root
pwsh ./scripts/build-python-native.ps1 -Configuration Release -Runtime win-x64
```

```bash
# Build for Windows
dotnet publish ../../src/Celeritas.Native/Celeritas.Native.csproj -c Release -r win-x64

# Build for Linux
dotnet publish ../../src/Celeritas.Native/Celeritas.Native.csproj -c Release -r linux-x64

# Build for macOS
dotnet publish ../../src/Celeritas.Native/Celeritas.Native.csproj -c Release -r osx-x64

# Copy to package folder (Windows example)
cp ../../src/Celeritas.Native/bin/Release/net10.0/win-x64/publish/Celeritas.Native.dll celeritas/native/
```

## Documentation

Full documentation available at: https://github.com/sheinv78/Celeritas

## Examples

### Working with Notes

```python
from celeritas import NoteEvent

note = NoteEvent(
    pitch=60,  # Middle C
    time_numerator=0,
    time_denominator=1,
    duration_numerator=1,
    duration_denominator=4,  # Quarter note
    velocity=80
)
```

### Ornaments

```python
from celeritas import Trill, Mordent, MordentType

# Create a trill
note = parse_note("E5")
trill = Trill(note, interval=2, speed=8, end_with_turn=True)
notes = trill.expand()

# Create an upper mordent
mordent = Mordent(note, mordent_type=MordentType.UPPER)
notes = mordent.expand()
```

## Support

- GitHub Issues: https://github.com/sheinv78/Celeritas/issues
- Email: sheinv78@gmail.com
