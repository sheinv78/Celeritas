# Notation interop: MusicXML

Celeritas reads and writes **MusicXML** (`score-partwise`), converting between the
interchange format the notation world speaks and the engine's
[`NoteBuffer`](xref:Celeritas.Core.NoteBuffer) / [`NoteEvent`](xref:Celeritas.Core.NoteEvent)
model. The entry point is [`MusicXmlIo`](xref:Celeritas.Core.Notation.MusicXmlIo).

```csharp
using Celeritas.Core.Notation;
```

## Importing

```csharp
using var buffer = MusicXmlIo.Import("score.musicxml");   // from a file
using var b2     = MusicXmlIo.Import(stream);             // from a stream
using var b3     = MusicXmlIo.Parse(xmlText);             // from a string
```

Compressed **`.mxl`** archives are unwrapped automatically — the score named by
`META-INF/container.xml` is read (falling back to the first score entry). The
format is detected by content, so a mis-named file still works. XML is parsed with
DTD processing off and no external resolver, so the DOCTYPE real MusicXML carries
is never fetched (no XXE, no network).

## Exporting

```csharp
string xml = MusicXmlIo.ToXml(buffer);          // as a string
MusicXmlIo.Export(buffer, "out.musicxml");      // ...or straight to a file
MusicXmlIo.Export(buffer, stream);              // ...or a stream
```

Monophonic and block-chordal material **round-trips exactly** — import → export →
import yields the same notes:

```csharp
using var original = MusicXmlIo.Import("score.musicxml");
using var again    = MusicXmlIo.Parse(MusicXmlIo.ToXml(original));
// `again` has the same pitches, offsets, and durations as `original`.
```

## What maps to what

| MusicXML | Celeritas |
| --- | --- |
| `<pitch>` step + octave + `<alter>` | MIDI pitch (`NoteEvent.Pitch`); octave 4 = middle C (60) |
| `<duration>` in `<divisions>` | whole-note [`Rational`](xref:Celeritas.Core.Rational); a quarter = `1/4` |
| `<rest>` | advances time, emits no note |
| `<chord/>` | notes sharing an onset |
| `<tie>` chain (or `<notations><tied>`) | merged into one sustained note |
| multiple `<part>`s, `<backup>`/`<forward>` | merged onto one timeline |
| voices (via `<backup>`) | overlapping notes; exported back as `<voice>` lines |
| `<dynamics>` marks / `<sound dynamics>` | note velocity (`NoteEvent.Velocity`) |

On export, pitches are spelled with **sharps**, `<divisions>` is chosen so every
duration lands on an exact integer, chords share an onset, gaps become rests, and
overlapping lines are split into voices.

## Conventions to keep in mind

- **Time is whole-note-relative.** Offsets and durations are fractions of a whole
  note, independent of meter — see [the time model](../concepts/time-model.md).
- **Pitch is a number.** MIDI pitches don't carry spelling; C♯ and D♭ are the same
  pitch — see [enharmonic spelling](../concepts/enharmonics.md).

## Boundaries

This is a working core, not a full MusicXML implementation. Remaining
approximations, spelled out:

- **Tuplet grouping metadata** (`<time-modification>`) is ignored — tuplet
  *durations* import exactly (a triplet-eighth is exactly `1/12`), only the
  notational grouping is dropped.
- **Grace notes** are approximated: the pitch is kept as a short note (`1/32`) at
  the beat of its principal note, without shifting time.
- **`score-timewise`** is transposed to partwise on import and read normally.
- **Dynamics on export** are written for single-voice music only; polyphonic
  velocity is left at the default. Export uses a single measure.

## From the command line

```bash
# Convert MusicXML <-> MIDI (direction inferred from the extensions)
celeritas musicxml convert --in score.musicxml --out score.mid
celeritas musicxml convert --in score.mid --out score.musicxml

# Summarize a score: notes, range, detected key, chord timeline
celeritas musicxml analyze --in score.musicxml
```

## See also

- [`MusicXmlIo`](xref:Celeritas.Core.Notation.MusicXmlIo) — the full API.
- The **[Cookbook](../COOKBOOK.md)** has copy-pasteable notation recipes.
