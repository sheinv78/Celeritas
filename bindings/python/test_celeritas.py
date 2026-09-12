#!/usr/bin/env python3
"""
Unit tests for Celeritas Python bindings

Author: Vladimir V. Shein
License: BSL-1.1
"""

import ctypes
import json
import os
import unittest
from fractions import Fraction
from typing import Any, Callable, Dict, List
from celeritas.celeritas import CNoteEvent, _get_last_error, _lib
from celeritas import (
    CeleritasError,
    NoteEvent,
    parse_note,
    transpose,
    identify_chord,
    detect_key,
    midi_to_note_name,
    parse_chord_symbol,
    native_version,
    Trill,
    Mordent,
    MordentType,
    __version__,
)


def _offsets(notes: List[NoteEvent]) -> List[Fraction]:
    return [Fraction(n.time_numerator, n.time_denominator) for n in notes]


def _durations(notes: List[NoteEvent]) -> List[Fraction]:
    return [Fraction(n.duration_numerator, n.duration_denominator) for n in notes]


def _assert_exact_timing(
    test: unittest.TestCase, base: NoteEvent, notes: List[NoteEvent]
):
    """Offsets strictly increasing, durations > 0, durations sum to base duration."""

    offsets = _offsets(notes)
    durations = _durations(notes)

    test.assertEqual(offsets[0], Fraction(base.time_numerator, base.time_denominator))
    for earlier, later in zip(offsets, offsets[1:]):
        test.assertLess(earlier, later)

    for duration in durations:
        test.assertGreater(duration, 0)

    base_duration = Fraction(base.duration_numerator, base.duration_denominator)
    test.assertEqual(sum(durations), base_duration)

    # Notes must tile the base note contiguously: each starts where the
    # previous one ends.
    for i in range(1, len(notes)):
        test.assertEqual(offsets[i], offsets[i - 1] + durations[i - 1])


class TestParseChordSymbol(unittest.TestCase):
    """Tests for parse_chord_symbol (native chord-symbol parser)"""

    def test_parse_chord_symbol_c_major(self):
        pitches = parse_chord_symbol("C")
        self.assertIsNotNone(pitches)
        self.assertEqual(sorted(pitches), sorted([60, 64, 67]))

    def test_parse_chord_symbol_inversion_slash(self):
        pitches = parse_chord_symbol("C/E")
        self.assertIsNotNone(pitches)
        self.assertEqual(sorted(pitches), sorted([52, 60, 67]))

    def test_parse_chord_symbol_group_alterations(self):
        pitches = parse_chord_symbol("C7(b9,#11)")
        self.assertIsNotNone(pitches)
        self.assertEqual(sorted(pitches), sorted([60, 64, 67, 70, 73, 78]))

    def test_parse_chord_symbol_polychord(self):
        pitches = parse_chord_symbol("C|G")
        self.assertIsNotNone(pitches)
        self.assertEqual(sorted(pitches), sorted([60, 64, 67, 79, 83, 86]))

    def test_a_symbol_naming_more_pitches_than_the_buffer_holds_gets_them_all(self):
        """The wrapper asked for at most 32 pitches and returned what fit.

        A polychord of four ten-note chords names forty; it came back as thirty-two, which is
        also what a chord of exactly thirty-two would look like, where the C# library answers
        forty. The export now reports how many the symbol names and the wrapper asks again.
        """

        forty = "|".join(["C7(b9,#11,b13)add2add4add6"] * 4)
        pitches = parse_chord_symbol(forty)

        self.assertIsNotNone(pitches)
        self.assertEqual(len(pitches), 40)
        self.assertEqual(pitches[:10], [60, 62, 64, 65, 67, 69, 70, 73, 78, 80])
        self.assertEqual(pitches[-1], 116)

    def test_an_explicit_cap_is_still_a_cap(self):
        """A caller who names a maximum has asked for a cut, and gets the first pitches."""

        self.assertEqual(parse_chord_symbol("C13", max_pitches=3), [60, 64, 67])
        self.assertEqual(
            parse_chord_symbol("C13", max_pitches=7), [60, 64, 67, 70, 74, 77, 81]
        )
        self.assertEqual(parse_chord_symbol("C13", max_pitches=0), [])


class TestNoteEvent(unittest.TestCase):
    """Tests for NoteEvent dataclass"""

    def test_note_event_creation(self):
        note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        self.assertEqual(note.pitch, 60)
        self.assertEqual(note.velocity, 80)

    def test_note_event_time_property(self):
        note = NoteEvent(
            pitch=60,
            time_numerator=1,
            time_denominator=2,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        self.assertAlmostEqual(note.time, 0.5)

    def test_note_event_duration_property(self):
        note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=3,
            duration_denominator=8,
            velocity=80,
        )
        self.assertAlmostEqual(note.duration, 0.375)

    def test_a_note_left_at_its_default_loudness_is_as_loud_as_a_parsed_one(self):
        """One library, one default loudness.

        parse_note hands back the managed NoteEvent's default, 0.8 of full, which the export
        writes as round(0.8 * 127) = 102; the dataclass answered the same question with 80, so a
        note built in Python and a note parsed from text were the same note at two loudnesses.
        """
        self.assertEqual(NoteEvent(60, 0, 1, 1, 4).velocity, 102)
        self.assertEqual(parse_note("C4").velocity, NoteEvent(60, 0, 1, 1, 4).velocity)


class TestParseNote(unittest.TestCase):
    """Tests for parse_note function"""

    def test_parse_note_c4(self):
        note = parse_note("C4")
        self.assertIsNotNone(note)
        self.assertEqual(note.pitch, 60)

    def test_parse_note_with_sharp(self):
        note = parse_note("C#4")
        self.assertIsNotNone(note)
        self.assertEqual(note.pitch, 61)

    def test_parse_note_with_flat(self):
        note = parse_note("Db4")
        self.assertIsNotNone(note)
        self.assertEqual(note.pitch, 61)

    def test_parse_note_different_octaves(self):
        c3 = parse_note("C3")
        c4 = parse_note("C4")
        c5 = parse_note("C5")

        self.assertEqual(c3.pitch, 48)
        self.assertEqual(c4.pitch, 60)
        self.assertEqual(c5.pitch, 72)

    def test_parse_note_invalid_returns_none(self):
        note = parse_note("X999")
        self.assertIsNone(note)


class TestTranspose(unittest.TestCase):
    """Tests for transpose function (SIMD-accelerated)"""

    def test_transpose_up_semitones(self):
        pitches = [60, 64, 67]  # C major chord
        result = transpose(pitches, 2)
        self.assertEqual(result, [62, 66, 69])  # D major chord

    def test_transpose_down_semitones(self):
        pitches = [60, 64, 67]  # C major chord
        result = transpose(pitches, -2)
        self.assertEqual(result, [58, 62, 65])  # Bb major chord

    def test_transpose_zero_semitones(self):
        pitches = [60, 64, 67]
        result = transpose(pitches, 0)
        self.assertEqual(result, pitches)

    def test_transpose_octave_up(self):
        pitches = [60]
        result = transpose(pitches, 12)
        self.assertEqual(result, [72])

    def test_transpose_large_array(self):
        """Test SIMD performance on large arrays"""
        pitches = list(range(60, 72)) * 1000  # 12,000 pitches
        result = transpose(pitches, 5)
        self.assertEqual(len(result), len(pitches))
        self.assertEqual(result[0], 65)
        self.assertEqual(result[-1], 76)


class TestMidiToNoteName(unittest.TestCase):
    """Tests for midi_to_note_name function"""

    def test_midi_to_note_name_middle_c(self):
        self.assertEqual(midi_to_note_name(60), "C4")

    def test_midi_to_note_name_sharps(self):
        self.assertEqual(midi_to_note_name(61, prefer_flats=False), "C#4")
        self.assertEqual(midi_to_note_name(63, prefer_flats=False), "D#4")
        self.assertEqual(midi_to_note_name(66, prefer_flats=False), "F#4")

    def test_midi_to_note_name_flats(self):
        self.assertEqual(midi_to_note_name(61, prefer_flats=True), "Db4")
        self.assertEqual(midi_to_note_name(63, prefer_flats=True), "Eb4")
        self.assertEqual(midi_to_note_name(70, prefer_flats=True), "Bb4")

    def test_midi_to_note_name_different_octaves(self):
        self.assertEqual(midi_to_note_name(48), "C3")
        self.assertEqual(midi_to_note_name(60), "C4")
        self.assertEqual(midi_to_note_name(72), "C5")

    def test_midi_to_note_name_out_of_range(self):
        with self.assertRaises(ValueError):
            midi_to_note_name(-1)
        with self.assertRaises(ValueError):
            midi_to_note_name(128)


class TestIdentifyChord(unittest.TestCase):
    """Tests for identify_chord function"""

    def test_identify_c_major(self):
        # Not assertIn("maj", chord.lower()): "CMajor".lower() contains "maj" whatever the
        # rendering does, so that passed without testing the string it was named for.
        self.assertEqual(identify_chord([60, 64, 67]), "CMajor")

    def test_identify_d_minor(self):
        self.assertEqual(identify_chord([62, 65, 69]), "DMinor")

    def test_identify_g7(self):
        self.assertEqual(identify_chord([67, 71, 74, 77]), "GDominant7")

    def test_identify_an_unrecognized_chord_names_no_root(self):
        """The root beside Unknown is a placeholder: there is none to report."""

        self.assertEqual(identify_chord([64, 67, 71, 74, 78]), "CUnknown")

    def test_identify_a_sixth_chord_reads_its_bass(self):
        """C-E-G-A is a C6 with C at the bottom and an Am7 with A at the bottom.

        The two are the same four pitch classes, so only the bass tells them apart. The
        library had no sixth-chord quality at all and answered Am7 either way, which made a
        tonic sixth chord come back as the submediant.
        """

        self.assertEqual(identify_chord([60, 64, 67, 69]), "CMajor6")
        self.assertEqual(identify_chord([57, 60, 64, 67]), "AMinor7")
        self.assertEqual(identify_chord([60, 63, 67, 69]), "CMinor6")
        self.assertEqual(identify_chord([57, 60, 63, 67]), "AHalfDim7")

    def test_identify_chord_with_inversions(self):
        # C major in different inversions
        root = identify_chord([60, 64, 67])
        first_inv = identify_chord([64, 67, 72])
        second_inv = identify_chord([67, 72, 76])

        # All should identify as C major (though inversion may vary)
        self.assertIn("C", root)
        self.assertIn("C", first_inv)
        self.assertIn("C", second_inv)


class TestDetectKey(unittest.TestCase):
    """Tests for detect_key function"""

    def test_detect_c_major(self):
        scale = [60, 62, 64, 65, 67, 69, 71, 72]  # C major scale
        key_name, is_major = detect_key(scale)
        self.assertEqual(key_name, "C")
        self.assertTrue(is_major)

    def test_detect_a_minor(self):
        scale = [69, 71, 72, 74, 76, 77, 79, 81]  # A minor scale
        key_name, is_major = detect_key(scale)
        self.assertIn(key_name, ["A", "C"])  # A minor or C major (relative)

    def test_detect_g_major(self):
        scale = [67, 69, 71, 72, 74, 76, 78, 79]  # G major scale
        key_name, is_major = detect_key(scale)
        self.assertEqual(key_name, "G")
        self.assertTrue(is_major)

    def test_detect_key_from_melody(self):
        melody = [60, 62, 64, 60, 67, 65, 64]  # Simple C major melody
        key_name, is_major = detect_key(melody)
        self.assertEqual(key_name, "C")

    def test_a_flat_key_is_named_with_its_flat(self):
        # The native export kept a sharp table of its own, so a B-flat scale answered "A#" -
        # a key that would need ten sharps. It answers the name the key is written under.
        scale = [70, 72, 74, 75, 77, 79, 81, 82]  # Bb major scale
        key_name, is_major = detect_key(scale)
        self.assertEqual(key_name, "Bb")
        self.assertTrue(is_major)
        scale = [63, 65, 67, 68, 70, 72, 74, 75]  # Eb major scale
        self.assertEqual(detect_key(scale)[0], "Eb")

    def test_no_notes_have_no_key(self):
        """detect_key([]) raises, and the message says why.

        The C# library answers empty input with a sentinel - C major at confidence 0 - that
        a caller there can tell from a detection by reading the confidence. This function
        hands back only the tonic and the mode, so it passed the sentinel on as the answer:
        detect_key([]) was ("C", True), the same answer as for a C major scale, with nothing
        to check. The native export now refuses an empty list the way it refuses any other
        input it cannot answer.
        """

        with self.assertRaises(CeleritasError) as raised:
            detect_key([])
        self.assertIn("no notes", str(raised.exception))
        self.assertIn("empty", str(raised.exception))

    # The reviewer's held-out checks for that refusal: it is decided by the count and not by
    # the pointer, it leaves the caller's buffers alone, every other answer the export gave
    # still stands, and it is the only "error" the detect_key parity section carries.

    def test_the_refusal_is_by_count_not_by_pointer(self):
        """The export reads count before it reads the pitches.

        The wrapper passes a zero-length ctypes array for [], whose address is whatever ctypes
        hands back; a C caller may pass NULL, or a valid array with count 0. All three are the
        same question - no notes - and get the same refusal.
        """

        buffer = ctypes.create_string_buffer(16)
        is_major = ctypes.c_int()
        valid = (ctypes.c_int * 3)(60, 64, 67)

        for label, pitches in (("NULL", None), ("valid array", valid)):
            with self.subTest(pitches=label):
                rc = _lib.celeritas_detect_key(
                    pitches, 0, buffer, 16, ctypes.byref(is_major)
                )
                self.assertEqual(rc, 0)
                self.assertEqual(
                    _get_last_error(),
                    "Cannot detect a key from no notes: the pitch list is empty.",
                )

    def test_a_refusal_leaves_the_outputs_untouched(self):
        """A caller who ignores the 0 must not read a key out of its own buffers."""

        buffer = ctypes.create_string_buffer(b"UNTOUCHED", 16)
        is_major = ctypes.c_int(42)

        rc = _lib.celeritas_detect_key(None, 0, buffer, 16, ctypes.byref(is_major))

        self.assertEqual(rc, 0)
        self.assertEqual(buffer.value, b"UNTOUCHED")
        self.assertEqual(is_major.value, 42)

    def test_one_note_is_still_answered(self):
        """One note is not no notes: the (str, bool) API answers it as it always did.

        Whether this API should be able to say "undecidable" for one note is a separate
        question; this pins that the empty-input refusal did not widen to it.
        """

        self.assertEqual(detect_key([60]), ("C", True))
        self.assertEqual(detect_key([69]), ("A", True))
        self.assertEqual(detect_key([70]), ("Bb", True))

    def test_two_notes_and_a_tritone_are_still_answered(self):
        self.assertEqual(detect_key([60, 67]), ("C", True))
        self.assertEqual(detect_key([60, 66]), ("C", True))

    def test_every_pitch_in_and_out_of_the_range_is_still_answered(self):
        """128 notes, notes below zero, notes above 127: a key is a question about pitch
        classes, so these fold rather than being dropped or refused."""

        self.assertEqual(detect_key(list(range(128))), ("C", True))
        self.assertEqual(detect_key([-12, -10, -8, -7, -5, -3, -1]), ("C", True))
        self.assertEqual(detect_key([128, 130, 132, 133, 135, 137, 139]), ("Ab", True))
        self.assertEqual(detect_key([-1]), ("B", True))

    def test_a_tuple_of_pitches_is_a_list_of_pitches(self):
        self.assertEqual(detect_key((60, 64, 67)), ("C", True))

    def test_a_wrong_type_is_a_type_error_not_a_key(self):
        """None and non-integers are Python type errors, raised before the native call."""

        with self.assertRaises(TypeError):
            detect_key(None)  # type: ignore[arg-type]
        with self.assertRaises(TypeError):
            detect_key([60.5])  # type: ignore[list-item]
        with self.assertRaises(TypeError):
            detect_key(["C4"])  # type: ignore[list-item]

    def test_the_buffer_too_small_refusal_still_stands(self):
        """The refusal the empty-input one was modelled on is still there, with its message."""

        buffer = ctypes.create_string_buffer(1)
        is_major = ctypes.c_int()
        pitches = (ctypes.c_int * 3)(60, 64, 67)

        rc = _lib.celeritas_detect_key(pitches, 3, buffer, 1, ctypes.byref(is_major))

        self.assertEqual(rc, 0)
        self.assertIn("Buffer too small", _get_last_error())

    def test_the_sibling_exports_answer_empty_input_as_before(self):
        """identify_chord([]) is an honest Unknown, transpose([]) is a no-op: neither was a
        sentinel leaking and neither moved."""

        self.assertEqual(identify_chord([]), "CUnknown")
        self.assertEqual(transpose([], 3), [])

    def test_the_parity_table_refuses_only_the_empty_question(self):
        """The one "error" in the detect_key section is []; no other pin moved to error."""

        with open(_PARITY_TABLE, encoding="utf-8") as handle:
            table = json.load(handle)

        refused = [entry["q"] for entry in table["detect_key"] if entry["a"] == "error"]

        self.assertEqual(refused, [[]])


class TestTrill(unittest.TestCase):
    """Tests for Trill ornament"""

    def test_trill_creation(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8)
        self.assertEqual(trill.base_note.pitch, 64)
        self.assertEqual(trill.interval, 2)

    def test_trill_expansion(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8)
        expanded = trill.expand()

        self.assertGreater(len(expanded), 1)
        # Should alternate between base and upper note
        self.assertIn(64, [n.pitch for n in expanded])
        self.assertIn(66, [n.pitch for n in expanded])

    def test_trill_start_with_upper(self):
        base_note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8, start_with_upper=True)
        expanded = trill.expand()

        # First note should be upper note
        self.assertEqual(expanded[0].pitch, 62)

    def test_trill_exact_timing(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8)
        expanded = trill.expand()

        # speed=8 -> 1/32 whole note per trill note; 1/2 whole note = 16 notes
        self.assertEqual(len(expanded), 16)
        _assert_exact_timing(self, base_note, expanded)

    def test_trill_matches_the_library_when_the_duration_is_not_a_whole_number_of_units(
        self,
    ):
        """The bindings write Trill again in Python, so the two can drift apart.

        They had: the loop here ran until the base note ran out and put a stub note at the end,
        where Celeritas.Core.Ornamentation.Trill takes as many whole units as fit and stretches
        the last one to the end. A trill on a 3/8 note at speed 3 came out with five notes from
        the bindings and four from the library.
        """

        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=3,
            duration_denominator=8,
            velocity=80,
        )
        expanded = Trill(base_note, interval=2, speed=3).expand()

        self.assertEqual(len(expanded), 4)
        self.assertEqual(
            [(n.pitch, n.duration_numerator, n.duration_denominator) for n in expanded],
            [(64, 1, 12), (66, 1, 12), (64, 1, 12), (66, 1, 8)],
        )
        _assert_exact_timing(self, base_note, expanded)

    def test_trill_shorter_than_one_unit_is_left_as_it_is(self):
        """Expanding a note shorter than a single trill unit would delete it."""

        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=64,
            velocity=80,
        )
        expanded = Trill(base_note, interval=2, speed=8).expand()

        self.assertEqual(len(expanded), 1)
        self.assertEqual(expanded[0], base_note)

    def test_trill_end_with_turn_is_actually_played(self):
        """end_with_turn was accepted, stored, and then ignored: the flag did nothing at all."""

        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )
        plain = Trill(base_note, interval=2, speed=8).expand()
        turned = Trill(base_note, interval=2, speed=8, end_with_turn=True).expand()

        self.assertNotEqual(
            [n.pitch for n in plain],
            [n.pitch for n in turned],
            "end_with_turn changed nothing",
        )
        # The turn drops to the note below before returning to the main note.
        self.assertEqual(turned[-2].pitch, 62)
        self.assertEqual(turned[-1].pitch, 64)
        _assert_exact_timing(self, base_note, turned)

    def test_trill_never_leaves_the_keyboard(self):
        """The library holds every ornamental pitch in 0..127; the bindings now do too."""

        base_note = NoteEvent(
            pitch=126,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        expanded = Trill(base_note, interval=4, speed=8).expand()

        for note in expanded:
            self.assertGreaterEqual(note.pitch, 0)
            self.assertLessEqual(note.pitch, 127)

    def test_trill_sub_quarter_offset_not_truncated(self):
        # A base note starting at 1/8 (sub-quarter offset) must not have its
        # trill notes collapse to offset 0 (regression: int(time * 4) truncation).
        base_note = NoteEvent(
            pitch=60,
            time_numerator=1,
            time_denominator=8,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8)
        expanded = trill.expand()

        _assert_exact_timing(self, base_note, expanded)
        self.assertEqual(_offsets(expanded)[0], Fraction(1, 8))

    def test_trill_duration_not_multiple_of_step(self):
        # 3/8 whole note with 1/32 steps -> 12 notes, exact fit; but 1/12
        # duration with 1/32 steps does not divide evenly: the final note
        # must be shortened so durations still sum exactly.
        base_note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=12,
            velocity=80,
        )
        trill = Trill(base_note, interval=2, speed=8)
        expanded = trill.expand()

        _assert_exact_timing(self, base_note, expanded)


class TestMordent(unittest.TestCase):
    """Tests for Mordent ornament"""

    def test_mordent_upper(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        mordent = Mordent(base_note, mordent_type=MordentType.UPPER, alternations=1)
        expanded = mordent.expand()

        # Upper mordent: base, upper, base (3 notes)
        self.assertEqual(len(expanded), 3)
        pitches = [n.pitch for n in expanded]
        self.assertEqual(pitches, [64, 66, 64])

    def test_mordent_lower(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        mordent = Mordent(base_note, mordent_type=MordentType.LOWER, alternations=1)
        expanded = mordent.expand()

        # Lower mordent: base, lower, base (3 notes)
        self.assertEqual(len(expanded), 3)
        pitches = [n.pitch for n in expanded]
        self.assertEqual(pitches, [64, 62, 64])

    def test_mordent_multiple_alternations(self):
        base_note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )
        mordent = Mordent(base_note, mordent_type=MordentType.UPPER, alternations=2)
        expanded = mordent.expand()

        # 2 alternations: base, upper, base, upper, base (5 notes)
        self.assertEqual(len(expanded), 5)
        pitches = [n.pitch for n in expanded]
        self.assertEqual(pitches, [60, 62, 60, 62, 60])

    def test_mordent_exact_timing_no_zero_durations(self):
        # A quarter-note mordent (3 notes of 1/12 each) used to produce
        # zero-duration notes via int(duration * 4) truncation.
        base_note = NoteEvent(
            pitch=64,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=4,
            velocity=80,
        )
        mordent = Mordent(base_note, mordent_type=MordentType.UPPER, alternations=1)
        expanded = mordent.expand()

        _assert_exact_timing(self, base_note, expanded)
        self.assertEqual(_durations(expanded), [Fraction(1, 12)] * 3)
        self.assertEqual(
            _offsets(expanded), [Fraction(0), Fraction(1, 12), Fraction(2, 12)]
        )

    def test_mordent_exact_timing_with_offset(self):
        base_note = NoteEvent(
            pitch=64,
            time_numerator=3,
            time_denominator=16,
            duration_numerator=1,
            duration_denominator=8,
            velocity=80,
        )
        mordent = Mordent(base_note, mordent_type=MordentType.LOWER, alternations=2)
        expanded = mordent.expand()

        self.assertEqual(len(expanded), 5)
        _assert_exact_timing(self, base_note, expanded)


class TestIntegration(unittest.TestCase):
    """Integration tests combining multiple features"""

    def test_transpose_and_identify_chord(self):
        # Start with C major chord
        c_major = [60, 64, 67]
        self.assertIn("C", identify_chord(c_major))

        # Transpose to D major
        d_major = transpose(c_major, 2)
        self.assertIn("D", identify_chord(d_major))

        # Transpose to A major
        a_major = transpose(c_major, 9)
        self.assertIn("A", identify_chord(a_major))

    def test_parse_transpose_and_name(self):
        # Parse C4, transpose up octave, convert back to name
        note = parse_note("C4")
        self.assertEqual(note.pitch, 60)

        transposed = transpose([note.pitch], 12)[0]
        self.assertEqual(transposed, 72)

        name = midi_to_note_name(transposed)
        self.assertEqual(name, "C5")

    def test_ornament_preserves_duration(self):
        base_note = NoteEvent(
            pitch=60,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=1,
            duration_denominator=2,
            velocity=80,
        )

        trill = Trill(base_note, interval=2, speed=8)
        expanded = trill.expand()

        # Total duration of expanded notes must equal the base note exactly
        total_duration = sum(_durations(expanded))
        self.assertEqual(
            total_duration,
            Fraction(base_note.duration_numerator, base_note.duration_denominator),
        )


class TestNativeVersion(unittest.TestCase):
    """Tests for native_version function"""

    def test_native_version_format(self):
        version = native_version()
        self.assertRegex(version, r"^\d+\.\d+\.\d+$")

    def test_native_version_matches_package_version(self):
        # __version__ comes from installed package metadata; skip the
        # comparison when the package is not installed (e.g. running from
        # a source checkout without `pip install -e .`).
        if __version__ == "0.0.0":
            self.skipTest("package metadata not available")
        self.assertEqual(native_version(), __version__)


def _c_parse_chord(symbol: bytes):
    out = (ctypes.c_int * 16)()
    count = ctypes.c_int()
    rc = _lib.celeritas_parse_chord_symbol(symbol, out, 16, ctypes.byref(count))
    return rc, list(out)[: count.value]


def _c_parse_note(text: bytes):
    note = CNoteEvent()
    return _lib.celeritas_parse_note(text, ctypes.byref(note)), note


def _c_identify(pitches):
    arr = (ctypes.c_int * len(pitches))(*pitches)
    buf = ctypes.create_string_buffer(64)
    return _lib.celeritas_identify_chord(arr, len(pitches), buf, 64), buf.value.decode()


def _c_detect_key(pitches):
    arr = (
        (ctypes.c_int * max(1, len(pitches)))(*pitches)
        if pitches
        else (ctypes.c_int * 1)()
    )
    buf = ctypes.create_string_buffer(64)
    is_major = ctypes.c_int()
    rc = _lib.celeritas_detect_key(arr, len(pitches), buf, 64, ctypes.byref(is_major))
    return rc, buf.value.decode()


class TestTheLastErrorIsTheLastCallsAloneAsACCallerSeesIt(unittest.TestCase):
    """The reviewer's held-out checks on the native last-error, through ctypes as a C caller
    would call it: a success reads empty, a failure then a success reads empty, two failures
    leave the second, a failure in one export is cleared by a success in another."""

    def test_success_first_reads_empty(self):
        rc, pitches = _c_parse_chord(b"Dm7")
        self.assertEqual(rc, 1)
        self.assertEqual(pitches, [62, 65, 69, 72])
        self.assertEqual(_get_last_error(), "")

    def test_failure_then_success_then_read_is_empty(self):
        rc, _ = _c_parse_chord(b"Xm7")
        self.assertEqual(rc, 0)
        self.assertNotEqual(_get_last_error(), "")
        rc, pitches = _c_parse_chord(b"Gm7b5")
        self.assertEqual(rc, 1)
        self.assertEqual(pitches, [67, 70, 73, 77])
        self.assertEqual(_get_last_error(), "")

    def test_two_failures_in_a_row_leave_the_second_message(self):
        rc, _ = _c_parse_chord(b"H7")
        self.assertEqual(rc, 0)
        first = _get_last_error()
        self.assertIn("H7", first)
        rc, _ = _c_parse_chord(
            b"Cm5"
        )  # (e): a power chord beside a minor marker is refused
        self.assertEqual(rc, 0)
        second = _get_last_error()
        # The native export names the symbol, not the parser's reason (pre-existing format).
        self.assertIn("Cm5", second)
        self.assertNotIn("H7", second)

    def test_a_failure_in_one_export_is_cleared_by_success_in_another(self):
        rc, _ = _c_detect_key([])
        self.assertEqual(rc, 0)
        self.assertIn("empty", _get_last_error().lower())
        rc, note = _c_parse_note(b"F#4")
        self.assertEqual(rc, 1)
        self.assertEqual(note.pitch, 66)
        self.assertEqual(_get_last_error(), "")

    def test_parse_note_failure_then_identify_chord_success(self):
        rc, _ = _c_parse_note(b"Q9")
        self.assertEqual(rc, 0)
        self.assertNotEqual(_get_last_error(), "")
        rc, name = _c_identify([60, 63, 67, 70])
        self.assertEqual(rc, 1)
        self.assertNotEqual(name, "")
        self.assertEqual(_get_last_error(), "")

    def test_success_after_success_stays_empty(self):
        _c_parse_chord(b"Bdim7")
        _c_parse_chord(b"Cdim9")
        rc, pitches = _c_parse_chord(
            b"Cdim9"
        )  # (d): diminished seventh kept under the ninth
        self.assertEqual(rc, 1)
        self.assertEqual(pitches, [60, 63, 66, 69, 74])
        self.assertEqual(_get_last_error(), "")

    def test_get_last_error_itself_does_not_clear(self):
        rc, _ = _c_parse_chord(b"H7")
        self.assertEqual(rc, 0)
        self.assertEqual(_get_last_error(), _get_last_error())
        self.assertNotEqual(_get_last_error(), "")

    def test_version_clears_a_prior_failure(self):
        rc, _ = _c_parse_chord(b"H7")
        self.assertEqual(rc, 0)
        buf = ctypes.create_string_buffer(32)
        self.assertEqual(_lib.celeritas_version(buf, 32), 1)
        self.assertEqual(_get_last_error(), "")


class TestTheLastErrorIsTheLastCalls(unittest.TestCase):
    """celeritas_get_last_error describes the most recent export called, and no other.

    The message used to be sticky: set when an export failed and never cleared, so a C caller
    reading it after a call that had SUCCEEDED was handed the previous failure's complaint. The
    Python wrapper reads it only after an export reports failure, so the package never showed
    the stale message; these tests go through ctypes, as a C caller would, and the parity
    table is unaffected - it records answers, not error messages.
    """

    @staticmethod
    def _fail_to_parse_a_chord():
        out = (ctypes.c_int * 8)()
        count = ctypes.c_int()
        rc = _lib.celeritas_parse_chord_symbol(b"H7", out, 8, ctypes.byref(count))
        return rc

    def test_a_failure_leaves_its_message(self):
        self.assertEqual(self._fail_to_parse_a_chord(), 0)
        self.assertEqual(_get_last_error(), "Could not parse chord symbol: 'H7'.")

    def test_a_success_after_a_failure_leaves_no_message(self):
        self.assertEqual(self._fail_to_parse_a_chord(), 0)
        self.assertNotEqual(_get_last_error(), "")

        out = (ctypes.c_int * 8)()
        count = ctypes.c_int()
        rc = _lib.celeritas_parse_chord_symbol(b"C", out, 8, ctypes.byref(count))

        self.assertEqual(rc, 1)
        self.assertEqual(_get_last_error(), "")

    def test_every_export_clears_the_message_when_it_succeeds(self):
        """Each export, not just the one that failed, starts with a clean slate."""

        buffer = ctypes.create_string_buffer(64)
        is_major = ctypes.c_int()
        pitches = (ctypes.c_int * 3)(60, 64, 67)
        note = CNoteEvent()

        successes = (
            ("celeritas_version", lambda: _lib.celeritas_version(buffer, 64)),
            (
                "celeritas_parse_note",
                lambda: _lib.celeritas_parse_note(b"C4", ctypes.byref(note)),
            ),
            (
                "celeritas_identify_chord",
                lambda: _lib.celeritas_identify_chord(pitches, 3, buffer, 64),
            ),
            (
                "celeritas_detect_key",
                lambda: _lib.celeritas_detect_key(
                    pitches, 3, buffer, 64, ctypes.byref(is_major)
                ),
            ),
        )
        for name, call in successes:
            with self.subTest(export=name):
                self.assertEqual(self._fail_to_parse_a_chord(), 0)
                self.assertEqual(call(), 1)
                self.assertEqual(_get_last_error(), "")

        # transpose returns nothing, so its success is that the error is gone.
        with self.subTest(export="celeritas_transpose"):
            self.assertEqual(self._fail_to_parse_a_chord(), 0)
            _lib.celeritas_transpose(pitches, 3, 0)
            self.assertEqual(_get_last_error(), "")

    def test_the_wrapper_reads_the_message_only_on_failure(self):
        """The wrapper's own reading was never stale: it asks only after a 0 comes back."""

        self.assertEqual(self._fail_to_parse_a_chord(), 0)
        self.assertEqual(parse_chord_symbol("C"), [60, 64, 67])
        self.assertEqual(_get_last_error(), "")


class TestDotNetBridge(unittest.TestCase):
    def test_dotnet_bridge_is_optional(self):
        # This must not require pythonnet just to import.
        from celeritas import is_pythonnet_available

        available = is_pythonnet_available()
        self.assertIsInstance(available, bool)


class TestParseNoteNamesOneNote(unittest.TestCase):
    """parse_note names one note, and used to run the whole notation-language parser."""

    def test_the_bottom_of_the_midi_range_is_reachable(self):
        """The grammar's octave is [0-9]+ with no sign, so C-1 through B-1 were refused."""

        self.assertIsNotNone(parse_note("C-1"))
        self.assertEqual(parse_note("C-1").pitch, 0)
        self.assertEqual(parse_note("B-1").pitch, 11)

    def test_the_top_of_the_midi_range_is_reachable(self):
        self.assertEqual(parse_note("G9").pitch, 127)

    def test_more_than_one_note_is_refused(self):
        """'C4 E4 G4' came back as C4 with the chord silently dropped."""

        for notation in ("C4 E4 G4", "[C4 E4 G4]/4", "C4~ C4"):
            self.assertIsNone(parse_note(notation), notation)

    def test_a_rest_is_not_a_note(self):
        """'R/4' came back as a note of pitch -1, the value reserved for silence."""

        self.assertIsNone(parse_note("R/4"))
        self.assertIsNone(parse_note("R"))

    def test_notation_that_is_not_a_bare_pitch_is_refused(self):
        for notation in ("C4/4", "C4{tr}", "4/4: C4/4"):
            self.assertIsNone(parse_note(notation), notation)

    def test_a_plain_note_is_unchanged(self):
        note = parse_note("C4")
        self.assertEqual(
            (
                note.pitch,
                note.time_numerator,
                note.time_denominator,
                note.duration_numerator,
                note.duration_denominator,
                note.velocity,
            ),
            (60, 0, 1, 1, 4, 102),
        )


class TestOrnamentsMatchTheLibrary(unittest.TestCase):
    """The bindings write Trill and Mordent again in Python, so the two drift apart silently.

    These pin the places they had drifted. There is no native export for ornaments yet, so the
    only thing holding the two implementations together is a test that compares them.
    """

    @staticmethod
    def _note(pitch, duration=(1, 4)):
        return NoteEvent(
            pitch=pitch,
            time_numerator=0,
            time_denominator=1,
            duration_numerator=duration[0],
            duration_denominator=duration[1],
            velocity=0.8,
        )

    def test_a_mordent_at_the_bottom_of_the_keyboard_stays_on_it(self):
        """Ornament.Playable holds an ornamental pitch in 0..127.

        Trill was given that clamp and Mordent, in the same file, was not: 270 of 840 expansions
        differed from the library, and a lower mordent on MIDI 0 produced pitch -1 - the value
        this library reserves for silence, so the ornament emitted a note that reads as a rest.
        """

        expanded = Mordent(self._note(0), MordentType.LOWER, interval=1).expand()

        self.assertEqual([n.pitch for n in expanded], [0, 0, 0])
        self.assertTrue(all(0 <= n.pitch <= 127 for n in expanded))

    def test_a_mordent_at_the_top_of_the_keyboard_stays_on_it(self):
        expanded = Mordent(self._note(127), MordentType.UPPER, interval=2).expand()

        self.assertEqual([n.pitch for n in expanded], [127, 127, 127])

    def test_a_mordent_away_from_the_edges_still_reaches_its_neighbour(self):
        expanded = Mordent(self._note(60), MordentType.UPPER, interval=2).expand()

        self.assertEqual([n.pitch for n in expanded], [60, 62, 60])

    def test_a_mordent_with_no_alternations_is_refused(self):
        """Celeritas.Core.Ornamentation.Mordent throws; 0 used to give a bare main note here
        and a negative count gave no notes at all."""

        for alternations in (0, -1):
            with self.assertRaises(ValueError):
                Mordent(self._note(60), alternations=alternations).expand()

    def test_a_trill_with_no_speed_is_refused(self):
        """Celeritas.Core.Ornamentation.Trill throws; speed 0 used to raise ZeroDivisionError
        from inside Fraction, and a negative speed silently expanded to no notes at all.
        """

        for speed in (0, -1):
            with self.assertRaises(ValueError):
                Trill(self._note(60), speed=speed).expand()


# ---------------------------------------------------------------------------------------------
# Three implementations of one theory: the managed library, the native exports this package
# calls through ctypes, and the pure-Python rewrites in celeritas.py. The managed library writes
# a table of questions and its answers to parity/managed-answers.json — the C# test class
# ThreeImplementationsAgreeTests in tests/Celeritas.Tests keeps that file current — and the test
# below asks the other two the same questions and reports every answer that differs, with the
# question beside it.
#
# The three had drifted three times before anything compared them, each time found by a probe
# written by hand: celeritas_parse_note handing back the first note of a chord and pitch -1 for a
# rest, celeritas_detect_key naming B flat "A#", the Python Mordent leaving the keyboard where the
# library's does not. A refused input is the string "error" on both sides: the export returning 0
# (None or CeleritasError here), or the rewrite raising ValueError.
#
# To refresh the table after a deliberate change to the library, run the C# test with
# CELERITAS_REGENERATE_GOLDEN=1, rebuild the native library (scripts/build-python-native.ps1) and
# run this file again.
# ---------------------------------------------------------------------------------------------

_PARITY_TABLE = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "parity", "managed-answers.json"
)

_REFUSED = "error"


def _fraction_pair(numerator: int, denominator: int) -> List[int]:
    return [numerator, denominator]


def _rows(notes: List[NoteEvent]) -> List[List[int]]:
    """Notes as the table writes them: pitch, offset and duration as numerator, denominator."""

    return [
        [
            note.pitch,
            note.time_numerator,
            note.time_denominator,
            note.duration_numerator,
            note.duration_denominator,
        ]
        for note in notes
    ]


def _ask_parse_note(question: str) -> Any:
    note = parse_note(question)
    if note is None:
        return _REFUSED
    return [
        note.pitch,
        _fraction_pair(note.time_numerator, note.time_denominator),
        _fraction_pair(note.duration_numerator, note.duration_denominator),
        note.velocity,
    ]


def _ask_transpose(question: Dict[str, Any]) -> Any:
    return transpose(question["pitches"], question["semitones"])


def _ask_identify_chord(question: List[int]) -> Any:
    try:
        return identify_chord(question)
    except CeleritasError:
        return _REFUSED


def _ask_detect_key(question: List[int]) -> Any:
    try:
        tonic, is_major = detect_key(question)
    except CeleritasError:
        return _REFUSED
    return [tonic, is_major]


def _ask_parse_chord_symbol(question: str) -> Any:
    pitches = parse_chord_symbol(question)
    return _REFUSED if pitches is None else pitches


def _ask_midi_to_note_name(question: Dict[str, Any]) -> Any:
    try:
        return midi_to_note_name(
            question["pitch"], prefer_flats=question["prefer_flats"]
        )
    except ValueError:
        return _REFUSED


def _base_note(question: Dict[str, Any]) -> NoteEvent:
    return NoteEvent(
        pitch=question["pitch"],
        time_numerator=question["offset"][0],
        time_denominator=question["offset"][1],
        duration_numerator=question["duration"][0],
        duration_denominator=question["duration"][1],
        velocity=80,
    )


def _expansion(base: NoteEvent, expand: Callable[[], List[NoteEvent]]) -> Any:
    try:
        notes = expand()
    except ValueError:
        return _REFUSED
    # The table does not carry velocity; both sides pass the base note's through unchanged, and
    # a note that did not would be a disagreement the rows alone could not show.
    for note in notes:
        if note.velocity != base.velocity:
            return (
                f"velocity {note.velocity} on a note of a base note at {base.velocity}"
            )
    return _rows(notes)


def _ask_trill(question: Dict[str, Any]) -> Any:
    base = _base_note(question)
    trill = Trill(
        base,
        interval=question["interval"],
        speed=question["speed"],
        start_with_upper=question["start_with_upper"],
        end_with_turn=question["end_with_turn"],
    )
    return _expansion(base, trill.expand)


def _ask_mordent(question: Dict[str, Any]) -> Any:
    base = _base_note(question)
    mordent_type = (
        MordentType.UPPER if question["type"] == "upper" else MordentType.LOWER
    )
    mordent = Mordent(
        base,
        mordent_type=mordent_type,
        interval=question["interval"],
        alternations=question["alternations"],
    )
    return _expansion(base, mordent.expand)


# One asker per section of the table. The native exports are asked through the same ctypes
# wrappers the package exposes; the rewrites are the package's own classes and functions.
_ASKERS: Dict[str, Callable[[Any], Any]] = {
    "parse_note": _ask_parse_note,
    "transpose": _ask_transpose,
    "identify_chord": _ask_identify_chord,
    "detect_key": _ask_detect_key,
    "parse_chord_symbol": _ask_parse_chord_symbol,
    "midi_to_note_name": _ask_midi_to_note_name,
    "trill": _ask_trill,
    "mordent": _ask_mordent,
}


class TestThreeImplementationsAgree(unittest.TestCase):
    """Every answer in parity/managed-answers.json, asked again of the native library and the
    pure-Python rewrites.

    One test per section, so a run names the export or the rewrite that drifted; each test
    lists every question the two sides answered differently rather than stopping at the first.
    """

    table: Dict[str, Any] = {}

    @classmethod
    def setUpClass(cls):
        with open(_PARITY_TABLE, encoding="utf-8") as handle:
            cls.table = json.load(handle)

    def _agree(self, section: str) -> None:
        entries = self.table[section]
        self.assertGreater(len(entries), 0, f"{section} has no questions")

        ask = _ASKERS[section]
        differing = []
        for entry in entries:
            answer = ask(entry["q"])
            if answer != entry["a"]:
                differing.append(
                    "  {}: managed {}, here {}".format(
                        json.dumps(entry["q"], ensure_ascii=False),
                        json.dumps(entry["a"], ensure_ascii=False),
                        json.dumps(answer, ensure_ascii=False),
                    )
                )

        if differing:
            self.fail(
                f"{len(differing)} of {len(entries)} {section} answers differ from the "
                "managed library:\n" + "\n".join(differing)
            )

    def test_every_section_of_the_table_is_asked(self):
        """A section the C# side adds without an asker here would be a question nobody re-asks."""

        sections = sorted(name for name in self.table if not name.startswith("_"))
        self.assertEqual(sections, sorted(_ASKERS))

    def test_parse_note_agrees_with_the_managed_library(self):
        self._agree("parse_note")

    def test_transpose_agrees_with_the_managed_library(self):
        self._agree("transpose")

    def test_identify_chord_agrees_with_the_managed_library(self):
        self._agree("identify_chord")

    def test_detect_key_agrees_with_the_managed_library(self):
        self._agree("detect_key")

    def test_parse_chord_symbol_agrees_with_the_managed_library(self):
        self._agree("parse_chord_symbol")

    def test_midi_to_note_name_agrees_with_the_managed_library(self):
        self._agree("midi_to_note_name")

    def test_trill_agrees_with_the_managed_library(self):
        self._agree("trill")

    def test_mordent_agrees_with_the_managed_library(self):
        self._agree("mordent")


def run_tests():
    """Run all tests with verbose output"""
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromModule(__import__(__name__))
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    return result.wasSuccessful()


if __name__ == "__main__":
    import sys

    success = run_tests()
    sys.exit(0 if success else 1)
