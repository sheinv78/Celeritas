# Celeritas Python Bindings

Python bindings for the Celeritas high-performance music engine.

## Installation

```bash
pip install celeritas
```

## Quick Start

```python
from celeritas import parse_note, transpose, identify_chord, detect_key, Trill

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

- **SIMD-Accelerated Operations** - Automatic hardware acceleration detection (AVX-512, AVX2, SSE2, ARM NEON, WebAssembly SIMD)
- **Music Notation** - Parse and format music notation
- **Chord Analysis** - Identify chords and chord progressions
- **Key Detection** - Detect musical keys using Krumhansl-Schmuckler algorithm
- **Ornaments** - Trills, mordents, turns, appoggiaturas
- **MIDI Support** - Import/export MIDI files (coming soon)

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
# ~30-50ms on modern hardware (SIMD accelerated)
```

## License

Licensed under the Business Source License 1.1 (BSL-1.1).

- ✅ Free for non-commercial use
- ✅ Free for open source projects
- ⚠️ Commercial use requires a license

For commercial licensing, contact: sheinv78@gmail.com

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
