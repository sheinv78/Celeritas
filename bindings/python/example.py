#!/usr/bin/env python3
"""
Example usage of Celeritas Python bindings
"""

from celeritas import (
    parse_note,
    transpose,
    identify_chord,
    detect_key,
    Trill,
    Mordent,
    MordentType,
    NoteEvent
)


def main():
    print("=== Celeritas Python Bindings Examples ===\n")

    # Example 1: Parse notes
    print("1. Parsing notes:")
    note = parse_note("C4")
    if note:
        print(f"   C4 -> Pitch: {note.pitch}, Duration: {note.duration}")
    
    # Example 2: Transpose
    print("\n2. Transposing pitches:")
    pitches = [60, 64, 67]  # C major chord
    print(f"   Original: {pitches}")
    transposed = transpose(pitches, 2)  # Up 2 semitones
    print(f"   Transposed +2: {transposed}")

    # Example 3: Chord identification
    print("\n3. Chord identification:")
    c_major = [60, 64, 67]
    d_minor = [62, 65, 69]
    g7 = [67, 71, 74, 77]
    
    print(f"   {c_major} -> {identify_chord(c_major)}")
    print(f"   {d_minor} -> {identify_chord(d_minor)}")
    print(f"   {g7} -> {identify_chord(g7)}")

    # Example 4: Key detection
    print("\n4. Key detection:")
    c_major_scale = [60, 62, 64, 65, 67, 69, 71, 72]  # C D E F G A B C
    key_name, is_major = detect_key(c_major_scale)
    print(f"   Scale: {c_major_scale}")
    print(f"   Detected key: {key_name} {'Major' if is_major else 'Minor'}")

    # Example 5: Ornaments - Trill
    print("\n5. Trill ornament:")
    base_note = NoteEvent(
        pitch=64,  # E4
        time_numerator=0,
        time_denominator=1,
        duration_numerator=1,
        duration_denominator=2,  # Half note
        velocity=80
    )
    trill = Trill(base_note, interval=2, speed=8)
    expanded = trill.expand()
    print(f"   Base note: E4 (pitch 64), duration: 1/2")
    print(f"   Trill expanded to {len(expanded)} notes")
    print(f"   First 3 pitches: {[n.pitch for n in expanded[:3]]}")

    # Example 6: Ornaments - Mordent
    print("\n6. Mordent ornament:")
    mordent = Mordent(base_note, mordent_type=MordentType.UPPER, alternations=1)
    expanded = mordent.expand()
    print(f"   Upper mordent expanded to {len(expanded)} notes")
    print(f"   Pitches: {[n.pitch for n in expanded]}")

    # Example 7: Performance test
    print("\n7. Performance test (SIMD acceleration):")
    import time
    large_list = list(range(60, 72)) * 10000  # 120,000 pitches
    start = time.time()
    transposed_large = transpose(large_list, 5)
    elapsed = (time.time() - start) * 1000
    print(f"   Transposed {len(large_list):,} pitches in {elapsed:.2f}ms")
    print(f"   Rate: {len(large_list)/elapsed*1000:,.0f} pitches/second")


if __name__ == "__main__":
    main()
