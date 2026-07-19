// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Xml;
using System.Xml.Linq;

namespace Celeritas.Core.Notation;

/// <summary>
/// Imports MusicXML into the engine's <see cref="NoteBuffer"/> / <see cref="NoteEvent"/> model.
/// <para>
/// This pass handles the common core of <c>score-partwise</c>: pitched notes
/// (step/octave/alter), rests, chords (<c>&lt;chord/&gt;</c>), per-measure <c>&lt;divisions&gt;</c>,
/// multiple measures, multiple parts (merged into one time line), <c>&lt;backup&gt;</c>/
/// <c>&lt;forward&gt;</c> cursor moves, and tie merging (a <c>&lt;tie&gt;</c> chain becomes one
/// sustained note). Times are converted to the engine's whole-note units.
/// </para>
/// <para>
/// Multi-voice parts import correctly because voice timing rides on <c>&lt;backup&gt;</c>/
/// <c>&lt;forward&gt;</c>, and dynamics (<c>&lt;dynamics&gt;</c> marks and <c>&lt;sound dynamics&gt;</c>)
/// set note velocity. Not yet handled (spelled out so callers know the boundaries): grace notes,
/// tuplet time-modification, and the <c>score-timewise</c> layout. Export does not yet write
/// dynamics back. Compressed <c>.mxl</c> is not unpacked here.
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

    /// <summary>
    /// Serializes a <see cref="NoteBuffer"/> to a <c>score-partwise</c> MusicXML string.
    /// </summary>
    /// <remarks>
    /// Notes at the same onset and duration are written as a chord; gaps become rests. Overlapping
    /// notes are split into separate voices (via <c>&lt;backup&gt;</c>), so polyphony round-trips.
    /// Everything goes into a single measure and pitches are spelled with sharps.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static string ToXml(NoteBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var doc = BuildDocument(buffer);
        // A plain StringWriter is UTF-16, which would stamp encoding="utf-16" on the declaration and
        // mislead anyone who then saves the text as UTF-8. Report UTF-8 so the declaration matches.
        using var writer = new Utf8StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML to <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, string path)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        Export(buffer, stream);
    }

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML to a stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(stream);
        var doc = BuildDocument(buffer);
        var settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
        using var writer = XmlWriter.Create(stream, settings);
        doc.Save(writer);
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
        var velocity = DefaultVelocity;  // current dynamic, updated by <direction>/<sound>

        // Ties in progress, keyed by MIDI pitch: a tie-start holds a note open until the matching
        // tie-stop, so the chain is emitted once with the summed duration (and its start velocity).
        var pending = new Dictionary<int, (Rational onset, Rational duration, float velocity)>();

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

                    // A dynamic marking or a playback hint sets the velocity for later notes.
                    case "direction":
                    case "sound":
                        if (ReadDynamicVelocity(el) is float dyn)
                            velocity = dyn;
                        break;

                    case "note":
                        ReadNote(el, divisions, velocity, ref cursor, ref lastNoteOnset, pending, notes);
                        break;
                }
            }
        }

        // Flush any tie-start that never saw a matching stop (dangling/truncated input).
        foreach (var (midi, held) in pending)
            notes.Add(new NoteEvent(midi, held.onset, held.duration, held.velocity));
    }

    private static void ReadNote(
        XElement note, int divisions, float velocity, ref Rational cursor, ref Rational lastNoteOnset,
        Dictionary<int, (Rational onset, Rational duration, float velocity)> pending, List<NoteEvent> notes)
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
        var (tieStart, tieStop) = ReadTie(note);

        if (tieStop && pending.TryGetValue(midi, out var held))
        {
            // Continue or close the chain begun by an earlier tie-start (its start velocity wins).
            var total = held.duration + duration;
            if (tieStart)
                pending[midi] = (held.onset, total, held.velocity);   // middle of the chain: keep holding
            else
            {
                notes.Add(new NoteEvent(midi, held.onset, total, held.velocity));
                pending.Remove(midi);
            }
        }
        else if (tieStart)
        {
            // Begin a chain. A stale pending for this pitch (malformed) is flushed first.
            if (pending.TryGetValue(midi, out var stale))
                notes.Add(new NoteEvent(midi, stale.onset, stale.duration, stale.velocity));
            pending[midi] = (onset, duration, velocity);
        }
        else
        {
            notes.Add(new NoteEvent(midi, onset, duration, velocity));
        }

        if (!isChord)
        {
            lastNoteOnset = cursor;
            cursor += duration;
        }
    }

    // Sounding ties come from <tie type="start|stop"/> (a note may carry both). Fall back to the
    // notational <notations><tied> for files that only mark ties there.
    private static (bool start, bool stop) ReadTie(XElement note)
    {
        bool start = false, stop = false;
        foreach (var tie in Children(note, "tie"))
        {
            var type = tie.Attribute("type")?.Value;
            if (type == "start") start = true;
            else if (type == "stop") stop = true;
        }

        if (!start && !stop && note.Element("notations") is { } notations)
        {
            foreach (var tied in Children(notations, "tied"))
            {
                var type = tied.Attribute("type")?.Value;
                if (type == "start") start = true;
                else if (type == "stop") stop = true;
            }
        }

        return (start, stop);
    }

    // Reads a velocity (0..1) from a <direction> or <sound> element, or null if it sets no dynamic.
    // Prefers an explicit <sound dynamics="N"/> (N = percent of MIDI velocity 90); otherwise maps a
    // named <dynamics> mark (p, mf, ff, ...).
    private static float? ReadDynamicVelocity(XElement element)
    {
        var sound = element.Name.LocalName == "sound"
            ? element
            : element.Descendants().FirstOrDefault(e => e.Name.LocalName == "sound");
        var dynamicsAttr = sound?.Attribute("dynamics")?.Value;
        if (dynamicsAttr is not null
            && double.TryParse(dynamicsAttr.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var pct)
            && pct > 0)
        {
            return (float)Math.Clamp(pct * 0.9 / 127.0, 0.0, 1.0);
        }

        var dynamics = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "dynamics");
        if (dynamics is not null)
        {
            foreach (var mark in dynamics.Elements())
                if (NamedDynamicVelocity(mark.Name.LocalName) is float v)
                    return v;
        }

        return null;
    }

    // Standard dynamic levels as a fraction of MIDI velocity 127.
    private static float? NamedDynamicVelocity(string name) => name switch
    {
        "pppp" => 8f / 127f,
        "ppp" => 16f / 127f,
        "pp" => 33f / 127f,
        "p" => 49f / 127f,
        "mp" => 64f / 127f,
        "mf" => 80f / 127f,
        "f" => 96f / 127f,
        "ff" => 112f / 127f,
        "fff" => 120f / 127f,
        "ffff" => 127f / 127f,
        _ => null,
    };

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

    // ---- Export ----

    private static XDocument BuildDocument(NoteBuffer buffer)
    {
        var events = new List<NoteEvent>(buffer.Count);
        for (var i = 0; i < buffer.Count; i++)
            events.Add(buffer.Get(i));
        events.Sort((a, b) =>
        {
            var c = a.Offset.CompareTo(b.Offset);
            return c != 0 ? c : a.Pitch.CompareTo(b.Pitch);
        });

        // Pick divisions (per quarter note) so every offset and duration lands on an integer:
        // a value in whole notes * 4 gives quarters, whose reduced denominator must divide divisions.
        long divisions = 1;
        foreach (var e in events)
        {
            divisions = Lcm(divisions, (e.Offset * 4).Denominator);
            divisions = Lcm(divisions, (e.Duration * 4).Denominator);
        }

        // Chord units: notes sharing an onset AND a duration are one chord. Notes at the same onset
        // with different durations are independent (they go into different voices below).
        var unitMap = new Dictionary<(Rational onset, Rational duration), List<int>>();
        foreach (var e in events)
        {
            var key = (e.Offset, e.Duration);
            if (!unitMap.TryGetValue(key, out var pitches))
            {
                pitches = [];
                unitMap[key] = pitches;
            }
            pitches.Add(e.Pitch);
        }

        var units = unitMap
            .Select(kv => (onset: kv.Key.onset, duration: kv.Key.duration, pitches: kv.Value))
            .OrderBy(u => u.onset).ThenBy(u => u.duration).ToList();
        foreach (var u in units)
            u.pitches.Sort();

        // Greedy voice assignment: each unit joins the first voice free at its onset; overlapping
        // units start new voices. Monophonic / block-chordal input stays a single voice.
        var voices = new List<List<(Rational onset, Rational duration, List<int> pitches)>>();
        var voiceEnd = new List<Rational>();
        foreach (var u in units)
        {
            var target = -1;
            for (var k = 0; k < voices.Count; k++)
            {
                if (voiceEnd[k] <= u.onset)
                {
                    target = k;
                    break;
                }
            }

            if (target < 0)
            {
                voices.Add([]);
                voiceEnd.Add(Rational.Zero);
                target = voices.Count - 1;
            }

            voices[target].Add(u);
            voiceEnd[target] = u.onset + u.duration;
        }

        var measure = new XElement("measure",
            new XAttribute("number", 1),
            new XElement("attributes", new XElement("divisions", divisions)));

        var multiVoice = voices.Count > 1;
        for (var vi = 0; vi < voices.Count; vi++)
        {
            // Return the cursor to the start of the measure before writing the next voice.
            if (vi > 0)
                measure.Add(new XElement("backup", new XElement("duration", DivisionsOf(voiceEnd[vi - 1], divisions))));

            var voiceNumber = multiVoice ? vi + 1 : (int?)null;
            var cursor = Rational.Zero;
            foreach (var (onset, duration, pitches) in voices[vi])
            {
                if (onset > cursor)
                {
                    measure.Add(RestElement(onset - cursor, divisions, voiceNumber));
                    cursor = onset;
                }

                for (var p = 0; p < pitches.Count; p++)
                    measure.Add(PitchedNoteElement(pitches[p], duration, divisions, isChord: p > 0, voiceNumber));

                cursor += duration;
            }
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("score-partwise",
                new XAttribute("version", "4.0"),
                new XElement("part-list",
                    new XElement("score-part",
                        new XAttribute("id", "P1"),
                        new XElement("part-name", "Music"))),
                new XElement("part",
                    new XAttribute("id", "P1"),
                    measure)));
    }

    private static XElement RestElement(Rational duration, long divisions, int? voice)
    {
        var note = new XElement("note",
            new XElement("rest"),
            new XElement("duration", DivisionsOf(duration, divisions)));
        if (voice is int v)
            note.Add(new XElement("voice", v));
        return note;
    }

    private static XElement PitchedNoteElement(int midi, Rational duration, long divisions, bool isChord, int? voice)
    {
        var (step, alter, octave) = SpellSharp(midi);
        var pitch = new XElement("pitch", new XElement("step", step));
        if (alter != 0)
            pitch.Add(new XElement("alter", alter));
        pitch.Add(new XElement("octave", octave));

        var note = new XElement("note");
        if (isChord)
            note.Add(new XElement("chord"));
        note.Add(pitch);
        note.Add(new XElement("duration", DivisionsOf(duration, divisions)));
        if (voice is int v)
            note.Add(new XElement("voice", v));
        return note;
    }

    // Whole-note duration -> integer division count: (duration * 4 quarters) * divisions.
    private static long DivisionsOf(Rational duration, long divisions)
    {
        var quarters = duration * 4;               // whole notes -> quarter notes
        return quarters.Numerator * divisions / quarters.Denominator;
    }

    // Spell a MIDI pitch with sharps: step letter, alter (0 or +1), and MusicXML octave.
    private static (string step, int alter, int octave) SpellSharp(int midi)
    {
        var pc = ((midi % 12) + 12) % 12;
        var octave = ((midi - pc) / 12) - 1;       // MusicXML octave 4 = middle C (60)
        return pc switch
        {
            0 => ("C", 0, octave),
            1 => ("C", 1, octave),
            2 => ("D", 0, octave),
            3 => ("D", 1, octave),
            4 => ("E", 0, octave),
            5 => ("F", 0, octave),
            6 => ("F", 1, octave),
            7 => ("G", 0, octave),
            8 => ("G", 1, octave),
            9 => ("A", 0, octave),
            10 => ("A", 1, octave),
            _ => ("B", 0, octave),
        };
    }

    private static long Lcm(long a, long b)
    {
        if (a == 0 || b == 0) return 0;
        return a / Gcd(a, b) * b;
    }

    private static long Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }
        return a == 0 ? 1 : a;
    }
}
