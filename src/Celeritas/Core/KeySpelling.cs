// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core.Analysis;

namespace Celeritas.Core;

/// <summary>
/// Spells a key's tonic, and the notes and chord roots inside it, the way the key is written:
/// B flat major, not A sharp major, and the chords of B major as G#m and A#dim rather than Abm
/// and Bbdim.
/// </summary>
/// <remarks>
/// Every key name this library printed came from the sharp pitch-class table, so a progression
/// in B flat was reported as "in A# Major" — a key that would need ten sharps and that no
/// musician has seen written — with the E flat chord's notes as "D#, G, A#", while the same
/// report's scale, spelled by <see cref="ModeLibrary.GetScaleNoteNames"/>, read "Bb C D Eb F G A".
/// The tonic here is the first note of that spelled scale, so a key and its scale can never
/// disagree; a key whose scale is written with flats spells its chord roots and altered notes
/// from the flat table, and every other key from the sharp one. Pitch class 6 is F sharp, as
/// the scale speller's tie rule says; a caller who wrote G flat is answered in F sharp.
/// </remarks>
internal static class KeySpelling
{
    /// <summary>Note names indexed by pitch class, using flat spellings.</summary>
    internal static readonly string[] NoteNamesFlat = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    /// <summary>The name of the key's tonic as the key is written: "Bb" for the major key on pitch class 10.</summary>
    internal static string TonicName(KeySignature key) =>
        ModeLibrary.GetScaleNoteNames(ModalKey.FromKeySignature(key))[0];

    /// <summary>
    /// The name of the modal key's tonic as its scale is written. A mode with no letter per degree
    /// — the pentatonics, blues, the symmetric scales — takes the spelling of the major or minor
    /// key its third puts it in.
    /// </summary>
    internal static string TonicName(ModalKey key)
    {
        var names = ModeLibrary.GetScaleNoteNames(key);
        return names.Length == 7 ? names[0] : TonicName(key.ToKeySignature());
    }

    /// <summary>Whether the key is written with flats, read off its spelled scale.</summary>
    internal static bool UsesFlats(KeySignature key) =>
        Array.Exists(ModeLibrary.GetScaleNoteNames(ModalKey.FromKeySignature(key)), n => n.Contains('b'));

    /// <summary>Note names indexed by pitch class as <paramref name="key"/> spells them.</summary>
    internal static IReadOnlyList<string> Names(KeySignature key) =>
        UsesFlats(key) ? NoteNamesFlat : ChordLibrary.NoteNames;
}
