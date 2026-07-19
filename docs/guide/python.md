# Python quickstart

Celeritas ships Python bindings that call the same native engine as the .NET
API. There are two layers: a small, fast **ctypes** surface for the common
operations, and a **pythonnet** escape hatch that exposes the *entire* engine.

The ctypes snippets below were run against the bindings; their printed outputs
are real.

## Install

```bash
# From this repository (editable install; the native library is bundled)
pip install -e ./bindings/python

# Or, once published to PyPI
pip install celeritas
```

Import the package:

```python
import celeritas
```

## The fast path (ctypes)

The top-level functions cover parsing, transposition, chord ID, key detection,
chord-symbol parsing, and ornaments. Pitches are MIDI numbers (middle C = 60).

### Parse a note

```python
from celeritas import parse_note

note = parse_note("F#5")
print(note.pitch, note.duration)     # 78 0.25
```

`note.time` and `note.duration` are floats in whole-note units (a quarter is
`0.25`), matching the engine's time model.

### Transpose (SIMD-accelerated)

```python
from celeritas import transpose, midi_to_note_name

moved = transpose([60, 64, 67], 2)                 # up a whole step
print([midi_to_note_name(p) for p in moved])       # ['D4', 'F#4', 'A4']
```

`transpose` runs on the native SIMD path — it comfortably does millions of
pitches per second.

### Identify a chord

```python
from celeritas import identify_chord

print(identify_chord([60, 64, 67]))        # CMajor
print(identify_chord([60, 64, 67, 71]))    # CMajor7
```

Prefer to think in note names? Build the pitch list from names with `parse_note`:

```python
pitches = [parse_note(n).pitch for n in ["D4", "F4", "A4", "C5"]]
print(identify_chord(pitches))             # DMinor7
```

### Detect a key

```python
from celeritas import detect_key

key_name, is_major = detect_key([60, 62, 64, 65, 67, 69, 71])
print(key_name, "Major" if is_major else "Minor")   # C Major
```

> [!NOTE]
> Key detection reports the pitch content's key. A natural-minor scale shares its
> notes with the relative major, so a bare A-minor scale reports as C Major —
> supply a tonic-weighted distribution (or use the full API's mode detection) when
> the distinction matters.

### Parse a chord symbol

```python
from celeritas import parse_chord_symbol

print(parse_chord_symbol("Cmaj7"))   # [60, 64, 67, 71]
print(parse_chord_symbol("C/E"))     # [52, 60, 67]  (slash chord: E in the bass)
```

### Spelling

```python
from celeritas import midi_to_note_name

print(midi_to_note_name(66))                     # F#4  (sharps by default)
print(midi_to_note_name(66, prefer_flats=True))  # Gb4
```

### Ornaments

```python
from celeritas import parse_note, Trill, Mordent, MordentType

note = parse_note("E4")
trill = Trill(note, interval=2, speed=8)
print(len(trill.expand()))                       # 16 notes

mordent = Mordent(note, mordent_type=MordentType.UPPER, alternations=1)
print([n.pitch for n in mordent.expand()])       # [64, 66, 64]
```

## The full API (pythonnet)

The ctypes surface is a focused subset. To reach *everything* — the progression
advisor, voice-leading solver, modal analysis, MIDI I/O, and the rest — load the
managed .NET assembly through [pythonnet](https://github.com/pythonnet/pythonnet):

```bash
pip install pythonnet
# Build the managed assembly (Release recommended); the loader searches for it,
# or point CELERITAS_DOTNET_ASSEMBLY at Celeritas.dll explicitly.
dotnet build src/Celeritas/Celeritas.csproj -c Release
```

```python
from celeritas import load_celeritas, is_pythonnet_available

if is_pythonnet_available():
    Celeritas = load_celeritas().namespace     # the .NET Celeritas namespace
    Analysis = Celeritas.Core.Analysis

    report = Analysis.ProgressionAdvisor.Analyze(["Dm7", "G7", "Cmaj7"])
    print(report.Key, report.Pattern)
    for c in report.Chords:
        print(c.Symbol, c.RomanNumeral, c.Nashville)
```

Through this path you call the public .NET types directly, so the
[API reference](../api/index.md) is your guide — the C# signatures apply verbatim.

## See also

- The **[10-minute tour](tour.md)** covers the same features in C#.
- The **[API reference](../api/index.md)** documents the full engine reachable via
  pythonnet.
