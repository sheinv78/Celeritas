// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Xml;
using System.Xml.Linq;

namespace Celeritas.Core.Notation;

/// <summary>
/// Imports MusicXML into the engine's <see cref="NoteBuffer"/> / <see cref="NoteEvent"/> model.
/// <para>
/// This first pass handles the common core of <c>score-partwise</c>: pitched notes
/// (step/octave/alter), rests, chords (<c>&lt;chord/&gt;</c>), per-measure <c>&lt;divisions&gt;</c>,
/// multiple measures, multiple parts (merged into one time line), and <c>&lt;backup&gt;</c>/
/// <c>&lt;forward&gt;</c> cursor moves. Times are converted to the engine's whole-note units.
/// </para>
/// <para>
/// Not yet handled (spelled out so callers know the boundaries): tie merging (tied notes are
/// emitted as separate events), voices, grace notes, tuplet time-modification, dynamics/velocity,
/// and the <c>score-timewise</c> layout. Compressed <c>.mxl</c> is not unpacked here.
/// </para>
/// </summary>
public static class MusicXmlIo
{
    private const float DefaultVelocity = 0.8f;

    /// <summary>Reads and imports a MusicXML file from <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The document is not valid, importable MusicXML.</exception>
    public static NoteBuffer Import(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Import(stream);
    }

    /// <summary>Reads and imports MusicXML from a stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The document is not valid, importable MusicXML.</exception>
    public static NoteBuffer Import(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return BuildFromDocument(LoadSafely(reader => XDocument.Load(reader), stream));
    }

    /// <summary>Imports MusicXML held in a string.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The document is not valid, importable MusicXML.</exception>
    public static NoteBuffer Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var textReader = new StringReader(xml);
        return BuildFromDocument(LoadSafely(reader => XDocument.Load(reader), textReader));
    }

    // Loads with DTD processing off and no external resolver: MusicXML files carry a DOCTYPE,
    // and resolving it would fetch an external DTD (an XXE vector and a needless network hit).
    private static XDocument LoadSafely<T>(Func<XmlReader, XDocument> load, T input)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var reader = input switch
            {
                Stream s => XmlReader.Create(s, settings),
                TextReader t => XmlReader.Create(t, settings),
                _ => throw new InvalidOperationException("Unsupported input."),
            };
            return load(reader);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"Not well-formed XML: {ex.Message}", ex);
        }
    }

    private static NoteBuffer BuildFromDocument(XDocument doc)
    {
        var root = doc.Root
            ?? throw new InvalidDataException("MusicXML document has no root element.");

        // Element names are compared without namespace: MusicXML is conventionally unqualified,
        // but tolerate a default namespace by matching on LocalName.
        if (root.Name.LocalName == "score-timewise")
            throw new InvalidDataException("score-timewise is not supported yet; convert to score-partwise.");
        if (root.Name.LocalName != "score-partwise")
            throw new InvalidDataException($"Expected a score-partwise root, found '{root.Name.LocalName}'.");

        var notes = new List<NoteEvent>();
        foreach (var part in Children(root, "part"))
            ReadPart(part, notes);

        var buffer = new NoteBuffer(Math.Max(notes.Count, 1));
        if (notes.Count > 0)
        {
            buffer.AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(notes));
            buffer.Sort();
        }
        return buffer;
    }

    private static void ReadPart(XElement part, List<NoteEvent> notes)
    {
        var cursor = Rational.Zero;      // current time position within this part (whole notes)
        var lastNoteOnset = Rational.Zero; // onset of the most recent non-chord note (for <chord/>)
        var divisions = 0;               // divisions per quarter note; set by <attributes>

        foreach (var measure in Children(part, "measure"))
        {
            foreach (var el in measure.Elements())
            {
                switch (el.Name.LocalName)
                {
                    case "attributes":
                        var div = el.Element("divisions");
                        if (div is not null && int.TryParse(div.Value, out var d) && d > 0)
                            divisions = d;
                        break;

                    case "backup":
                        cursor -= DurationOf(el, divisions);
                        if (cursor < Rational.Zero) cursor = Rational.Zero;
                        break;

                    case "forward":
                        cursor += DurationOf(el, divisions);
                        break;

                    case "note":
                        ReadNote(el, divisions, ref cursor, ref lastNoteOnset, notes);
                        break;
                }
            }
        }
    }

    private static void ReadNote(
        XElement note, int divisions, ref Rational cursor, ref Rational lastNoteOnset, List<NoteEvent> notes)
    {
        // Grace notes carry no <duration>; skip them in this pass.
        var durationEl = note.Element("duration");
        if (durationEl is null)
            return;

        var duration = DurationToWholeNotes(ParseInt(durationEl.Value, "duration"), divisions);
        var isChord = note.Element("chord") is not null;
        var isRest = note.Element("rest") is not null;

        if (isRest)
        {
            // A rest occupies time but produces no note. A chord-rest is unusual; treat as time only.
            if (!isChord)
            {
                lastNoteOnset = cursor;
                cursor += duration;
            }
            return;
        }

        var pitch = note.Element("pitch")
            ?? throw new InvalidDataException("A sounding note has neither <pitch> nor <rest>.");
        var midi = PitchToMidi(pitch);

        var onset = isChord ? lastNoteOnset : cursor;
        notes.Add(new NoteEvent(midi, onset, duration, DefaultVelocity));

        if (!isChord)
        {
            lastNoteOnset = cursor;
            cursor += duration;
        }
    }

    private static int PitchToMidi(XElement pitch)
    {
        var step = pitch.Element("step")?.Value?.Trim().ToUpperInvariant()
            ?? throw new InvalidDataException("<pitch> is missing <step>.");

        var semitone = step switch
        {
            "C" => 0,
            "D" => 2,
            "E" => 4,
            "F" => 5,
            "G" => 7,
            "A" => 9,
            "B" => 11,
            _ => throw new InvalidDataException($"Unknown pitch step '{step}'."),
        };

        var octaveEl = pitch.Element("octave")
            ?? throw new InvalidDataException("<pitch> is missing <octave>.");
        var octave = ParseInt(octaveEl.Value, "octave");

        var alter = 0;
        var alterEl = pitch.Element("alter");
        if (alterEl is not null && !string.IsNullOrWhiteSpace(alterEl.Value))
            alter = (int)Math.Round(ParseDouble(alterEl.Value, "alter"));

        // MusicXML octave 4 places middle C at 60: (4 + 1) * 12 + 0 = 60.
        return ((octave + 1) * 12) + semitone + alter;
    }

    private static Rational DurationOf(XElement el, int divisions)
    {
        var durationEl = el.Element("duration");
        return durationEl is null
            ? Rational.Zero
            : DurationToWholeNotes(ParseInt(durationEl.Value, "duration"), divisions);
    }

    // MusicXML <duration> is in divisions, where <divisions> is divisions-per-quarter-note.
    // A quarter note is 1/4 of a whole note, so wholeNotes = duration / (divisions * 4).
    private static Rational DurationToWholeNotes(int duration, int divisions)
    {
        if (divisions <= 0)
            throw new InvalidDataException("A note or move appears before a positive <divisions> was declared.");
        return new Rational(duration, (long)divisions * 4);
    }

    private static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);

    private static int ParseInt(string value, string field) =>
        int.TryParse(value?.Trim(), out var n)
            ? n
            : throw new InvalidDataException($"'{value}' is not a valid integer for <{field}>.");

    private static double ParseDouble(string value, string field) =>
        double.TryParse(value?.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : throw new InvalidDataException($"'{value}' is not a valid number for <{field}>.");
}
