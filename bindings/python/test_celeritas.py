#!/usr/bin/env python3
"""
Unit tests for Celeritas Python bindings

Author: Vladimir V. Shein
License: BSL-1.1
"""

import unittest
from fractions import Fraction
from typing import List
from celeritas import (
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
