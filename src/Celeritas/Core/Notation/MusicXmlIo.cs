// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.IO.Compression;
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
/// set note velocity (and single-voice export writes velocity back as <c>&lt;sound dynamics&gt;</c>).
/// Compressed <c>.mxl</c> archives are unwrapped and the <c>score-timewise</c> layout is transposed
/// to partwise on import; grace notes are approximated as short notes at the following beat.
/// Remaining boundary: tuplet grouping metadata (<c>&lt;time-modification&gt;</c>) is ignored, though
/// tuplet <em>durations</em> import exactly. Export writes a single measure.
/// </para>
/// </summary>
public static class MusicXmlIo
{
    private const float DefaultVelocity = 0.8f;

    // Nominal length given to grace notes on import (they carry no duration of their own).
    private static readonly Rational GraceDuration = new(1, 32);

    // Default meter used when export is not given one: notes are barred into 4/4 measures.
    private static readonly TimeSignature CommonTime = new(4, 4);

    /// <summary>Reads and imports a MusicXML file from <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The document is not valid, importable MusicXML.</exception>
    public static NoteBuffer Import(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.OpenRead(path);
        return Import(stream);
    }

    /// <summary>
    /// Reads and imports MusicXML from a stream. Plain XML and compressed <c>.mxl</c> (a ZIP whose
    /// score is named by <c>META-INF/container.xml</c>) are both accepted, detected by content.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The document is not valid, importable MusicXML.</exception>
    public static NoteBuffer Import(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Buffer so we can sniff the format and re-read it (MusicXML files are small).
        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        buffered.Position = 0;

        // A .mxl archive starts with the ZIP local-file signature "PK\x03\x04".
        var isZip = buffered.Length >= 2 && buffered.ReadByte() == 'P' && buffered.ReadByte() == 'K';
        buffered.Position = 0;

        return BuildFromDocument(isZip
            ? LoadFromMxl(buffered)
            : LoadSafely(reader => XDocument.Load(reader), (Stream)buffered));
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
    /// Serializes a <see cref="NoteBuffer"/> to a <c>score-partwise</c> MusicXML string, barred into
    /// 4/4 measures.
    /// </summary>
    /// <remarks>
    /// Notes at the same onset and duration are written as a chord; gaps become rests; overlapping
    /// notes are split into separate voices (via <c>&lt;backup&gt;</c>). The timeline is divided into
    /// measures of the given meter, notes crossing a barline are split and tied, and pitches are
    /// spelled with sharps — so import → export → import round-trips.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public static string ToXml(NoteBuffer buffer) => ToXml(buffer, CommonTime);

    /// <inheritdoc cref="ToXml(NoteBuffer)"/>
    /// <param name="buffer">The notes to serialize.</param>
    /// <param name="timeSignature">The meter to bar the notes into.</param>
    public static string ToXml(NoteBuffer buffer, TimeSignature timeSignature)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var doc = BuildDocument(buffer, timeSignature);
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

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML (4/4) to <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, string path) => Export(buffer, path, CommonTime);

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML, barred into <paramref name="timeSignature"/>, to a file.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, string path, TimeSignature timeSignature)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(path);
        using var stream = File.Create(path);
        Export(buffer, stream, timeSignature);
    }

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML (4/4) to a stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, Stream stream) => Export(buffer, stream, CommonTime);

    /// <summary>Writes a <see cref="NoteBuffer"/> as MusicXML, barred into <paramref name="timeSignature"/>, to a stream.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public static void Export(NoteBuffer buffer, Stream stream, TimeSignature timeSignature)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(stream);
        var doc = BuildDocument(buffer, timeSignature);
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

    // Unwraps a compressed .mxl archive: read the score named by META-INF/container.xml, or fall
    // back to the first non-META-INF .xml/.musicxml entry.
    private static XDocument LoadFromMxl(Stream zipStream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Not a readable .mxl (ZIP) archive: {ex.Message}", ex);
        }

        using (archive)
        {
            var entry = FindScoreEntry(archive)
                ?? throw new InvalidDataException("The .mxl archive contains no MusicXML score.");
            using var entryStream = entry.Open();
            return LoadSafely(reader => XDocument.Load(reader), entryStream);
        }
    }

    private static ZipArchiveEntry? FindScoreEntry(ZipArchive archive)
    {
        // Preferred: the rootfile path declared in META-INF/container.xml.
        var container = archive.GetEntry("META-INF/container.xml");
        if (container is not null)
        {
            try
            {
                using var containerStream = container.Open();
                var doc = LoadSafely(reader => XDocument.Load(reader), containerStream);
                var fullPath = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "rootfile")
                    ?.Attribute("full-path")?.Value;

                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    var named = archive.GetEntry(fullPath)
                        ?? archive.Entries.FirstOrDefault(e =>
                            e.FullName.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
                    if (named is not null)
                        return named;
                }
            }
            catch (InvalidDataException)
            {
                // Unusable container.xml — fall through to the heuristic below.
            }
        }

        // Fallback: the first score-looking entry outside META-INF.
        return archive.Entries.FirstOrDefault(e =>
            !e.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)
            && (e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                || e.FullName.EndsWith(".musicxml", StringComparison.OrdinalIgnoreCase)));
    }

    private static NoteBuffer BuildFromDocument(XDocument doc)
    {
        var root = doc.Root
            ?? throw new InvalidDataException("MusicXML document has no root element.");

        // Element names are compared without namespace: MusicXML is conventionally unqualified,
        // but tolerate a default namespace by matching on LocalName. score-timewise nests
        // measures over parts; transpose it to the part-over-measure shape the reader expects.
        var parts = root.Name.LocalName switch
        {
            "score-partwise" => Children(root, "part"),
            "score-timewise" => TimewiseToParts(root),
            _ => throw new InvalidDataException(
                $"Expected a score-partwise or score-timewise root, found '{root.Name.LocalName}'."),
        };

        var notes = new List<NoteEvent>();
        foreach (var part in parts)
            ReadPart(part, notes);

        var buffer = new NoteBuffer(Math.Max(notes.Count, 1));
        if (notes.Count > 0)
        {
            buffer.AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(notes));
            buffer.Sort();
        }
        return buffer;
    }

    // Rebuilds score-timewise (measures over parts) into partwise <part> elements (part over
    // measures), so the single partwise reader handles both layouts.
    private static List<XElement> TimewiseToParts(XElement root)
    {
        var partIds = new List<string>();
        foreach (var measure in Children(root, "measure"))
            foreach (var part in Children(measure, "part"))
            {
                var id = part.Attribute("id")?.Value ?? "";
                if (!partIds.Contains(id))
                    partIds.Add(id);
            }

        var result = new List<XElement>(partIds.Count);
        foreach (var id in partIds)
        {
            var partEl = new XElement("part", new XAttribute("id", id));
            foreach (var measure in Children(root, "measure"))
            {
                var slice = Children(measure, "part")
                    .FirstOrDefault(p => (p.Attribute("id")?.Value ?? "") == id);
                if (slice is null)
                    continue;

                var measureEl = new XElement("measure");
                if (measure.Attribute("number") is { } number)
                    measureEl.Add(new XAttribute("number", number.Value));
                foreach (var child in slice.Elements())
                    measureEl.Add(new XElement(child));   // deep clone
                partEl.Add(measureEl);
            }
            result.Add(partEl);
        }

        return result;
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
        var durationEl = note.Element("duration");
        if (durationEl is null)
        {
            // Grace notes carry no <duration>. Preserve the pitch at the current position with a
            // short nominal length, without advancing time — an approximation (the engine has no
            // dedicated grace-note concept). A non-grace note without a duration is skipped.
            if (note.Element("grace") is not null && note.Element("pitch") is { } gracePitch)
                notes.Add(new NoteEvent(PitchToMidi(gracePitch), cursor, GraceDuration, velocity));
            return;
        }

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

    // A note segment confined to one measure, carrying tie flags for barline splits.
    private readonly record struct Segment(
        int Measure, Rational Onset, Rational Duration, List<int> Pitches, float Velocity, bool TieStart, bool TieStop);

    private static XDocument BuildDocument(NoteBuffer buffer, TimeSignature timeSignature)
    {
        var measureLen = timeSignature.MeasureDuration;
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
        // with different durations are independent (they go into different voices below). The unit's
        // velocity is taken from its first note (chords are assumed uniform in dynamics).
        var unitMap = new Dictionary<(Rational onset, Rational duration), (List<int> pitches, float velocity)>();
        foreach (var e in events)
        {
            var key = (e.Offset, e.Duration);
            if (!unitMap.TryGetValue(key, out var unit))
            {
                unit = ([], e.Velocity);
                unitMap[key] = unit;
            }
            unit.pitches.Add(e.Pitch);
        }

        var units = unitMap
            .Select(kv => (kv.Key.onset, kv.Key.duration, kv.Value.pitches, kv.Value.velocity))
            .OrderBy(u => u.onset).ThenBy(u => u.duration).ToList();
        foreach (var u in units)
            u.pitches.Sort();

        // Greedy voice assignment: each unit joins the first voice free at its onset; overlapping
        // units start new voices. Monophonic / block-chordal input stays a single voice.
        var voices = new List<List<(Rational onset, Rational duration, List<int> pitches, float velocity)>>();
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

        // Total length and how many measures cover it.
        var totalEnd = Rational.Zero;
        foreach (var vlist in voices)
            foreach (var (onset, duration, _, _) in vlist)
            {
                var end = onset + duration;
                if (end > totalEnd)
                    totalEnd = end;
            }

        var measureCount = 1;
        while (measureLen * measureCount < totalEnd)
            measureCount++;

        // Split each voice's units at barlines into per-measure, tied segments.
        var segmentsByVoice = new List<List<Segment>>(voices.Count);
        foreach (var vlist in voices)
        {
            var segs = new List<Segment>();
            foreach (var (onset, duration, pitches, velocity) in vlist)
            {
                var segStart = onset;
                var end = onset + duration;
                while (segStart < end)
                {
                    var m = MeasureIndexOf(segStart, measureLen);
                    var barline = measureLen * (m + 1);
                    var segEnd = end < barline ? end : barline;
                    segs.Add(new Segment(m, segStart, segEnd - segStart, pitches, velocity,
                        TieStart: segEnd < end, TieStop: segStart > onset));
                    segStart = segEnd;
                }
            }
            segmentsByVoice.Add(segs);
        }

        var multiVoice = voices.Count > 1;
        var partEl = new XElement("part", new XAttribute("id", "P1"));
        var currentVelocity = DefaultVelocity;   // single-voice dynamic, tracked across measures

        for (var m = 0; m < measureCount; m++)
        {
            var mStart = measureLen * m;
            var mEnd = measureLen * (m + 1);
            var measureEl = new XElement("measure", new XAttribute("number", m + 1));
            if (m == 0)
                measureEl.Add(new XElement("attributes",
                    new XElement("divisions", divisions),
                    TimeElement(timeSignature)));

            var wroteAVoice = false;
            for (var vi = 0; vi < segmentsByVoice.Count; vi++)
            {
                var voiceSegs = segmentsByVoice[vi].Where(s => s.Measure == m).OrderBy(s => s.Onset).ToList();
                if (voiceSegs.Count == 0)
                    continue;

                // Each voice fills the measure, so returning to its start is a full-measure backup.
                if (wroteAVoice)
                    measureEl.Add(new XElement("backup", new XElement("duration", DivisionsOf(measureLen, divisions))));
                wroteAVoice = true;

                var voiceNumber = multiVoice ? vi + 1 : (int?)null;
                var cursor = mStart;
                foreach (var s in voiceSegs)
                {
                    if (s.Onset > cursor)
                    {
                        measureEl.Add(RestElement(s.Onset - cursor, divisions, voiceNumber));
                        cursor = s.Onset;
                    }

                    if (!multiVoice && Math.Abs(s.Velocity - currentVelocity) > 0.001f)
                    {
                        measureEl.Add(DynamicsElement(s.Velocity));
                        currentVelocity = s.Velocity;
                    }

                    for (var p = 0; p < s.Pitches.Count; p++)
                        measureEl.Add(PitchedNoteElement(
                            s.Pitches[p], s.Duration, divisions, isChord: p > 0, voiceNumber, s.TieStart, s.TieStop));

                    cursor += s.Duration;
                }

                // Pad to the barline so measures are full (the reader advances a continuous cursor).
                if (cursor < mEnd)
                    measureEl.Add(RestElement(mEnd - cursor, divisions, voiceNumber));
            }

            // A wholly empty measure still advances time by one measure.
            if (!wroteAVoice)
                measureEl.Add(RestElement(measureLen, divisions, null));

            partEl.Add(measureEl);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("score-partwise",
                new XAttribute("version", "4.0"),
                new XElement("part-list",
                    new XElement("score-part",
                        new XAttribute("id", "P1"),
                        new XElement("part-name", "Music"))),
                partEl));
    }

    private static int MeasureIndexOf(Rational time, Rational measureLen)
    {
        var q = time / measureLen;               // >= 0 for note times
        return (int)(q.Numerator / q.Denominator);
    }

    private static XElement TimeElement(TimeSignature ts) =>
        new("time",
            new XElement("beats", ts.BeatsPerMeasure),
            new XElement("beat-type", ts.BeatUnit));

    // A measure-level playback dynamic: velocity (0..1) -> <sound dynamics="N"/>, the inverse of
    // import's N * 0.9 / 127. Rounded for readable output; round-trip stays within ~0.001.
    private static XElement DynamicsElement(float velocity)
    {
        var percent = Math.Round(velocity * 127.0 / 0.9, 2);
        return new XElement("sound",
            new XAttribute("dynamics", percent.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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

    private static XElement PitchedNoteElement(
        int midi, Rational duration, long divisions, bool isChord, int? voice, bool tieStart, bool tieStop)
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
        if (tieStop)
            note.Add(new XElement("tie", new XAttribute("type", "stop")));
        if (tieStart)
            note.Add(new XElement("tie", new XAttribute("type", "start")));
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
