// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using System.Numerics;

namespace Celeritas.Core.Analysis;

/// <summary>
/// A note or a chord as the key judge hears it: when it starts, when it stops, which pitch
/// classes it holds and — for a single note — its pitch, so that the judge can tell a step
/// from a leap along the line. <see cref="KeyProfiler.AnalyzeModulations"/> hands the judge
/// one of these per note; <see cref="ModulationDetector"/> hands it one per note of the line
/// between its chords and, for each chord, one per group of its notes that stop together — a
/// block chord is one — with a note doubling a pitch class already in it as one more.
/// </summary>
/// <param name="Onset">When it starts.</param>
/// <param name="End">When it stops.</param>
/// <param name="PitchClasses">The pitch classes it holds, one bit each.</param>
/// <param name="Pitch">The MIDI pitch of a single note, or -1 for a chord.</param>
internal readonly record struct Sonority(Rational Onset, Rational End, ushort PitchClasses, int Pitch = -1);

/// <summary>
/// One change of key the judge heard: where the new key begins, what it left and what it
/// arrived at, and whether it held.
/// </summary>
/// <param name="Position">The start of the whole note in which the new key begins.</param>
/// <param name="From">The key the music was in.</param>
/// <param name="To">The key it moved to.</param>
/// <param name="Established">
/// Whether the new key held for a phrase — or to the end of the piece, closing on its tonic.
/// A change that did not hold is a tonicization: the music touched the key and came back.
/// </param>
/// <param name="Separation">How clearly the evidence chose <paramref name="To"/> over <paramref name="From"/>, 0-1.</param>
/// <param name="Stability">How much of the stretch that established the key belonged to it, 0-1.</param>
/// <param name="HeldUntil">
/// For an established key, where a note it does not own first sounds after <paramref name="Position"/>,
/// or the end of the music; for a tonicization, where the music is home again — the first
/// note the key does not own, or the first whole note after its tonic chord that neither is
/// that chord nor sounds a note of the key's own.
/// </param>
internal readonly record struct KeyChange(
    Rational Position,
    KeySignature From,
    KeySignature To,
    bool Established,
    float Separation,
    float Stability,
    Rational HeldUntil);

/// <summary>
/// What the judge heard: the key the music opens in, and every change of key after that.
/// </summary>
/// <param name="Opening">
/// The key the music opens in: the key given, unless the music was never in it — a change
/// placed at the first note is the opening key misjudged, not a modulation, and the key heard
/// there is the opening.
/// </param>
/// <param name="Changes">The changes of key, in order.</param>
internal readonly record struct Judgement(KeySignature Opening, List<KeyChange> Changes);

/// <summary>
/// The one place that decides where a piece changes key. Both public roads —
/// <see cref="KeyTrajectory.DetectModulations"/> over the notes of its fixed windows and
/// <see cref="ModulationDetector.Analyze(NoteBuffer, KeySignature)"/> over its chords — hand
/// their sonorities to <see cref="Judge"/>, and differ only in what a sonority is and in what
/// they layer on top of the answer.
/// </summary>
/// <remarks>
/// <para>
/// The judge applies a musician's rules rather than a window's point estimate:
/// </para>
/// <list type="bullet">
/// <item><description><b>A key holds for a phrase.</b> Evidence is read over <see cref="Phrase"/>
/// — four whole notes, a four-bar phrase in common time — never over a window shorter than
/// that. Two block chords in a two-bar window read as the key of the pitch class they share,
/// so I–V sounded as the dominant's key and IV–V7 as the subdominant's; a phrase reads as the
/// key it is in.</description></item>
/// <item><description><b>A chord is not a key.</b> A phrase whose material cannot decide a key
/// (<see cref="KeyDetectionResult.IsDecidable"/>) or does not separate its winner from the
/// runner-up says nothing; an arpeggiated IV chord read as the relative minor of its own
/// three notes.</description></item>
/// <item><description><b>A key owns its notes.</b> A phrase is named by the key that leaves the
/// least of it foreign — among keys that leave the same amount, by the Krumhansl profile. The
/// profile alone hears a dominant seventh as the key on its root: V7 I V7 I in G, half the
/// material D7, read as D major by a clear margin, though D major does not own the C natural
/// in every D7 and G owns every note.</description></item>
/// <item><description><b>A key owns its phrase.</b> Leaving the least of a phrase foreign is not
/// owning it: a phrase is a key's only when the notes the key lacks amount to less than a
/// quarter note in every bar of it — a stray note, not a chord tone (<see cref="StrayNote"/>
/// measures the passages that must pass and fail). An applied dominant is the key's chord: a
/// dominant seventh that resolves down a fifth into a chord the key owns leaves nothing
/// foreign, so I V7/V V I is owned by its key whole. Judged on what it left foreign relative
/// to the key the music was in, a passage that visits D and E for two bars each read as A
/// major, a key that was never there, because A leaves less of it foreign than D or E does; a
/// chromatic scale in eighths read as thirty-six keys one eighth apart; and D7 G C A7, the
/// phrase after the pivot of a pop verse, read as G major with the A7's C sharp called foreign
/// — an A7 that resolves to D minor, which G does not own, is no chord of G.</description></item>
/// <item><description><b>A key's chromatic chords are its own.</b> An applied chord — a major
/// triad as much as a dominant seventh, resolving down a fifth into a chord of the key that is
/// not the current key's tonic — a borrowed chord — a major key's minor subdominant, flat
/// sixth or flat seventh, resolving into a chord of the key — an augmented sixth on the flat
/// sixth degree resolving into the dominant or the tonic in six-four, and a dominant seventh on
/// the key's own tonic, the blues and pop's I7, leave nothing foreign, inside a phrase the key
/// frames with its tonic; and a minor key's tonic major triad closing the piece is its Picardy
/// third, a cadence of the minor key and no other key's note. A key cannot begin on one of its
/// own chromatic chords. With only dominant sevenths applied, C F G C | G E Am D7 | G C D7 G and
/// C F G C | G Cm D7 G | G C D7 G reached G two bars late, at the Am and the D7; Cm Fm G7 Cm | E♭
/// A♭ B♭ E♭ | Cm A♭ G7 C never came home to C minor, ending in E flat with the Picardy chord a
/// tonicization of C major; the German sixth of Cm A♭7 G7 Cm put the return two bars late, on
/// the G7; and the B♭7 closing B♭ E♭ F B♭7 was E flat's V7/IV, and B flat began a phrase
/// late.</description></item>
/// <item><description><b>A chord the key in force owns is that key's — while the key stands.</b>
/// Whatever another key might borrow it as or apply it to, a chord the key in force owns is
/// heard in the key in force: C F G C A D G, round and round, is C with a chain of secondary
/// dominants, not G with F as its flat seventh. But the resolution chain decides: an applied
/// chord that resolves into a chord the key in force does not own outright, or that sounds
/// after the key in force has been left — a chord it does not own since its tonic last sounded
/// — belongs to the key it resolves into. The G of F B♭ G C7 resolves into C7, no plain chord
/// of C's, and is V/V of F; the C of B♭ E♭ C F comes after the E flat that left C major, and is
/// V/V of B flat. Guarded on ownership alone, F began at bar 8 and B flat at bar 9, a phrase
/// after a musician hears them. A borrowed chord the key in force owns stays its own whatever
/// follows: the E major chord of C D E F♯, two bars each, is no flat seventh of F sharp
/// major's.</description></item>
/// <item><description><b>An arpeggiated chord is that chord.</b> A bar, a half bar or a quarter
/// of single notes, none longer than an eighth, whose pitch classes are exactly a major or a
/// minor triad or a dominant seventh, is one chord: its notes are the chord's tones, and a
/// phrase cannot begin in the middle of it. Heard note by note, the V/ii and the borrowed iv of
/// G's first phrase were three foreign eighths a bar, and G began two bars late in the
/// arpeggio texture where the same chords struck placed it at the bar; and on the detector
/// road, whose candidates are every onset, a window beginning on the last eighth of the
/// borrowed C minor read as E minor.</description></item>
/// <item><description><b>A non-harmonic tone is not a foreign note.</b> A note on its way —
/// short, approached and left by step — in a run that moves one way from a structural note the
/// key owns to another within a whole note, a semitone from the notes either side of it, is a
/// passing tone, and a note where the line turns is structural whatever came before it: the A
/// flat of G A A♭ G passes; a note that leaves an owned note by a semitone and returns is a
/// neighbour; a note of the line that leans on the harmony holding under it — no tone of that
/// chord, resolving by a semitone into one of its tones while the chord's harmony still holds,
/// sounding once in its bar, whatever its length and however approached — is an appoggiatura, an
/// accented passing tone or a suspension; and a note struck with a chord that leans on it — one
/// note outside a triad the chord holds, resolving by step into a tone of that triad at the
/// next note of the line — is an accented appoggiatura, no tone of the chord, so that the
/// chord is the plain triad it is. A note of the line that is a tone of the chord under it is
/// that chord's. None of them weighs anything toward what the key lacks, on either road: the
/// detector hands the judge the line between its chords for the purpose. Weighed as strays,
/// two chromatic passing eighths in a bar were a quarter of it, and a melody with two in every
/// bar of its new key named no key; a quarter-note neighbour under a D7 put G four bars late on
/// the trajectory road and at the bar on the detector's, which never saw it; a melody with a
/// chromatic appoggiatura struck on every downbeat of its new key, or a passing tone inside an
/// arch in each of its first two bars, or a half-note passing tone in its third, named no key;
/// and A struck over E G♯ B — a 4-3 suspension — made the chord no plain triad and no V/ii, so
/// G began two bars late.</description></item>
/// <item><description><b>A minor key owns its leading tone in its dominant.</b> The raised
/// seventh belongs to V, V7 and the leading-tone seventh, sounds alone in the line, or leans on
/// the tonic chord as an appoggiatura; in any other chord it is a note the key lacks. Owned as
/// a free pitch class, it made E minor the owner of any G-major phrase with an E flat in it — a
/// C minor chord borrowed, a quarter-note neighbour — and E minor was named.</description></item>
/// <item><description><b>You cannot leave a key without sounding a note it lacks.</b> The new key
/// must be separated from the current one on the profile and some note the new key owns and
/// the current key does not must actually sound. A melody in C with a chromatic passing tone
/// in every bar read as G for four bars on the strength of one F sharp, while sounding F, A
/// flat, B flat and C sharp, none of which G owns.</description></item>
/// <item><description><b>A secondary dominant is not a modulation.</b> The change is only a
/// modulation if the new key still reads from where it began through a phrase; if the music is
/// back home by then, it was a tonicization. A phrase that cannot decide is extended by a
/// phrase at a time, to the end of the piece at most, and a stretch at the end shorter than a
/// phrase establishes a key only if the piece closes on that key's tonic having already sounded
/// it — in the pivot bar or in the stretch — so that V7/V–V closing a phrase is a half cadence,
/// not a modulation to the dominant. The pivot bar must itself sound the tonic: any bar the new
/// key owned used to count, and C Am F G | C Am D7 G modulated to G at the Am, a half cadence
/// read as a modulation because vi of the old key is ii of the new.</description></item>
/// <item><description><b>A key is entered when its own notes return.</b> The notes a key owns and
/// the key before it lacks — F sharp for G from C, G sharp for A minor from C — are what tell
/// the two apart, and one bar of them is an applied dominant. They must sound in a second bar
/// before a note of the old key's own is heard again, within two phrases of the change; or the
/// phrase that establishes the new key must be framed by its tonic chord, opening on it and
/// closing within it, which is a phrase in that chord's key whatever else it holds. So C F | E7
/// Am | F G | C C is a tonicization of vi, E7 Am Dm E7 Am is A minor, Am Dm E7 Am after four
/// bars of C is A minor too, and C F G C | G C D7 G | C F G C goes to the dominant and comes
/// back. Judged by whether the new key held for a phrase alone, the applied dominants of an
/// ordinary pop verse — C Am D7 G | C A7 Dm G7 — were three modulations, and C D7 G C | E7 Am
/// A7 Dm | G7 C B7 Em | F D7 G7 C, sixteen bars in C, went to G, A minor and back.</description></item>
/// <item><description><b>The relative major is confirmed by its cadence or its frame.</b> The
/// relative major owns no note the minor lacks, so it cannot announce itself with a note: it
/// is reached when a phrase reads as the major with the minor's leading tone nowhere in it,
/// and it begins at the bar where the minor's tonic is left — provided the phrase is confirmed
/// by something the minor cannot own: the major's tonic chord framing it, opening it and
/// closing it; a loop that returns to that chord, the phrase after opening on it again; or the
/// major's own cadence, V to I, closing a phrase of the piece's count — which the minor's
/// dominant, carrying the raised seventh, cannot make. A cadence closes a phrase: the G C at
/// the seam of Am F C G | C F G C, the loop's last chord and the next phrase's first, is no
/// cadence, and asked of a window from where the minor's tonic was left it made C begin at
/// bar 5, four bars before the phrase that opens and closes on C. A phrase the minor owns
/// chord for chord, opening on the minor's tonic and closing elsewhere, confirms nothing and
/// is nothing — not a tonicization either. Named
/// by the profile alone, Am F C G, round and round, the commonest pop loop, was a modulation
/// to C at its second bar when told A minor, on both roads, and when told C the trajectory
/// road, which opens on the A minor chord, heard C at bar 2 where the detector heard none. The
/// loop is one key — C with vi first, or A minor with a major III and VII — and from the same
/// opening key the roads agree that nothing happens. Am Dm E7 Am | C F G C, framed, is C at
/// its fourth bar as before, and so are C Am F G twice, which loops on C, and Dm G C C twice,
/// which cadences in it.</description></item>
/// <item><description><b>The change is placed where the new key begins.</b> A key cannot have
/// begun before the last note it does not own, so a tonicization is placed at the first
/// sonority after that which sounds a note the new key owns and the old does not, moved back
/// to the start of its whole note across sonorities both keys own — an arpeggiated D7 begins on
/// its D, not on the F sharp an eighth later; a modulation one bar earlier still when the new
/// key owns that whole bar, which is the pivot chord: C F G C | G D7 G modulates at the G, as a
/// musician writes it, not at the D7. The pivot bar may hold the new key's own dominant
/// sevenths after the chord it opens on, so long as it opens on a chord the new key owns
/// outright: G Em Am A7 before Dm G7 C is C's bar with its V7/ii in it, and C is written at the
/// G — asked for the bar outright, the judge wrote it a bar late, at the Dm. And a key area
/// begins with a phrase: when the new key's
/// own note falls later in the phrase — phrases counted from the first note, a phrase long —
/// and the new key owns every bar from the phrase's start, the key began there, provided a
/// phrase from there establishes it and it does not open on the old key's tonic chord, which is
/// still the old key. C F G C | G C D7 G | C F G C is heard as G from its fifth bar; measured
/// from the D7 in its seventh, the phrase ran into the return to C and the G area was a
/// tonicization. C F G C | D7 G C C | C F G C, whose D7 opens the phrase, stays in C. And a key
/// is heard from where its own chords began: when the key is established from where its own
/// note falls, opens its phrase on its tonic chord and owns every bar from there to that note —
/// but for one chromatic parenthesis, a bar of a foreign key between bars it owns — the key
/// began with the phrase, and needs no second reading of bars that are its own. C F G C | G B♭
/// E♭ F B♭ D7 G | G C D7 G is G from its fifth bar with a bar of B flat quoted in its sixth;
/// measured from the D7, G began at bar 7. And G E Am D7 under a melody, on the detector road,
/// separated G from C by a hair less than the margin, so the second reading failed and G was
/// written at the Am.</description></item>
/// <item><description><b>A change at the first note is the opening key misjudged.</b> Whether the
/// opening key was read from the opening phrase or given by the caller, a change placed at the
/// first sonority means the music was never in the key it would leave; the key heard there is
/// the opening, and no modulation is reported. An A minor melody analyzed from C minor reported
/// a modulation to A minor at its first note. And an opening key guessed from the profile alone
/// — a melody opens on no chord — that never sounded a note of its own before another key was
/// read was never there either: two hundred random diatonic melodies in C, opened in A minor, E
/// minor or G by the profile, reported thirty-six modulations to C (two hundred more from
/// another seed, forty-five; now two).</description></item>
/// <item><description><b>A return to a key the music has been in is a homecoming.</b> A stretch
/// at the end shorter than a phrase is a new key only if the piece closes on its tonic having
/// already sounded it, from a key still in force, and a new key must beat the key it leaves on
/// the profile by a margin — the hysteresis that keeps a wobble from reading as a return. A
/// return to the key the piece opened in, or to a key it established since, that closes the
/// piece on that key's tonic needs neither: the ear knows the key already, its cadence is a
/// homecoming, and nothing follows a final cadence to wobble back to; it is enough that the
/// stretch reads as that key, is owned by it and names it by ownership — a stretch that stays
/// in the key from where it begins to the end of the piece, whatever its length and whichever
/// candidate reaches it. C F Dm G | Am G7 C || C D7 G Em | Am D7 G || G C D7 G | C D7 G || G Em
/// Am A7 | Dm G7 C is home at the G before the A7, as a musician writes it; asked to hear the
/// tonic before the close, as a new key must, the judge heard a one-bar tonicization of C and
/// no return, the Dm and the G7 sounding no C. And Dm G7 C closing two phrases in G reads as G
/// major on the profile still — the dominant sounded twice, the tonic once — so the plainest
/// ii V7 I home was refused for the margin. Judged only where the candidate's own window ended
/// with the piece, G Em Am D7 | G7 C C after a phrase in G was a tonicization — the candidate
/// at the Am, whose window ended a bar early, reached the return first and refused it, and its
/// tonicization blocked the candidate after it — and G7 C C C, a full phrase home, was refused
/// for the second bar of its own notes a new key must return. Em A7 D closing a piece in G is
/// G's half cadence as before: D major was never established, so its close must be a cadence
/// heard as one.</description></item>
/// <item><description><b>A chord sounds while its notes sound; its harmony holds until the next
/// chord.</b> What a chord weighs is the time its notes sound, on both roads: the detector
/// hands the judge a chord's notes from the chord's onset to where each stops, as the
/// trajectory always has, and a note held alone after a chord is the sonority there. But the
/// harmony a chord sets holds until the next chord begins, a whole note past its notes at
/// most — a staccato chord is released, and the ear keeps its harmony through the bar — and
/// that is what a note of the line leans on, is a tone of, and resolves into. Sounding until
/// the next chord on the detector road, a C major chord went on under the common tone C held
/// alone after it, A flat could not own that half bar and F minor, whose dominant C major is,
/// was named where a musician hears A flat — and where the trajectory, hearing the notes' real
/// ends, named it; and the chord as one mask lost the doubling, so the root doubled in a
/// four-voice cadence weighed once, and Dm G7 C separated C from G by less than the margin on
/// one road and by twice it on the other. Heard only while its notes sound, on both roads, a
/// D7 struck for a quarter was gone when the melody's C sharp came, the C sharp was a foreign
/// quarter and no appoggiatura, and G began four bars late — or, with a chromatic appoggiatura
/// struck on every downbeat over quarter-note chords, not at all.</description></item>
/// <item><description><b>A chord struck twice in its bar is one harmony.</b> A chord restruck
/// in its bar while its harmony holds resolves where its restrike does, no phrase begins on
/// the second strike, the key in force stands at the restrike as it stood at the chord, and a
/// Picardy third struck twice in its final bar — or restruck bar after bar to the end, under a
/// fermata — closes the piece as one struck once. Resolving
/// into its own restrike, D7 D7 G was no V7/V, and the trajectory road, which guesses its
/// opening key, opened C G | D7 G C D7 G G in G — the one key that owned the phrase — and heard
/// no modulation where the detector, told C, heard G at bar 2; asked afresh at the restrike of
/// the C in B♭ E♭ C C F F, C major stood again and the chord was C's own, not B flat's V/V,
/// and B flat began at bar 8 on both roads; the hymn closing on a Picardy third struck twice
/// ended in C major; and with the Picardy chord restruck for two more bars, A minor's close
/// was a modulation to A major.</description></item>
/// <item><description><b>A note struck with a chord and let go before it is the line's.</b> A
/// note no tone of the chord that outlasts it — the melody's eighth on the accompaniment's
/// stroke, an appoggiatura struck with the chord it leans on — is heard as the line's: a
/// passing tone, a neighbour, an appoggiatura or a foreign note, and no part of the chord.
/// Grouped into the G chord's restrike, the B flat of C5 B4 B♭4 A4 was a chord tone G major
/// lacked, and G was a one-bar tonicization on one road and nothing on the other.</description></item>
/// <item><description><b>The bars are heard from the music's accents, not from the clock.</b>
/// When more than half of all onsets fall the same eighth after the whole note — every chord
/// late by an eighth, and nothing else sounding — the bars are late by that much, and every bar
/// the judge reads begins there; a candidate on the clock is read from the nearest bar. A
/// melody on the beat over chords struck an eighth behind it keeps the melody's bars, and the
/// chords are late: a chord struck in the bar before that stops within an eighth of the bar
/// line is the harmony leaving and weighs in the bar it was struck in; a chord struck under a
/// note within an eighth of its onset is the harmony the note leans on; and a phrase opens on
/// the chord under its first note, the chord struck within an eighth after a lone first note.
/// The phrases are counted from the first bar — the first bar start after a pickup — wherever
/// in the piece's time the music begins: counted from the clock, a piece that began a bar in
/// moved G after a quarter-note pickup chord a quarter earlier than the same music at zero.
/// Read on the clock, the eighth of each F major chord that spilt into G's first bar made C
/// the owner of the phrase, and four bars each of C, F, G and C, every chord an eighth late,
/// was C at bar 8 on both roads; arpeggiated, eight bars of C then eight of G went to E minor;
/// the eighth of C major under the G sharp that begins E major's first bar was a quarter note of
/// foreign weight, and E major owned nothing; and the appoggiatura on every downbeat of G, its
/// chord an eighth behind, leaned on nothing and opened every phrase on a lone note, so G was
/// never named.</description></item>
/// <item><description><b>The piece opens on its first chord, and a given key it does not open
/// on is a guess.</b> The chord the piece opens on is the first chord within a quarter of the
/// first note — struck, or arpeggiated in quick even notes, under a pickup or an eighth behind
/// the tune. A key the caller gave that the piece does not open on is the caller's guess, and
/// like an opening guessed from the profile it is wrong if it never sounds a note of its own
/// before another key is read. Arpeggiated, C F G C | G E Am D7 | G C D7 G with its second bar
/// silent was so many notes, and the profile read C – G C as G; the chords an eighth late under
/// a held dominant pedal opened the piece on the pedal alone, and the profile guessed G; C F G
/// C | C F G7 C told G, its second bar silent, had nothing left to refute G with, and the
/// detector heard C at bar 5 where the trajectory, opening in C, heard none.</description></item>
/// <item><description><b>A chain of applied dominants is the key's whose cadence it reaches.</b>
/// An applied chord resolving into another applied chord of the key — V7/V/V into V7/V —
/// resolves into the key, when the chain lands, past its last seventh, on the key's tonic or
/// its dominant. Resolving only into a chord the key owns outright, the A7 of Am Dm E7 Am | A7
/// D7 G7 C was a foreign bar to C, and C was written at the D7, a bar late, on both roads; and
/// given to any key that owned a link of it outright, the chain B7 E7 A7 D7 of sixteen bars in
/// C was E minor's — B7 its V7, D7 its natural minor's VII7 — and the piece went to E minor at
/// bar 4 and came home at bar 9.</description></item>
/// <item><description><b>The Picardy third names the minor key it closes.</b> A span closing
/// the piece on the tonic major triad of a minor key the music has been in, and that the minor
/// key owns, is that key's, not its parallel major's — the major owns the close only by taking
/// the Picardy chord for its tonic — and the Picardy third is no chromatic chord to that minor
/// key. Named by the profile, E7 A closing Am Dm E7 Am | C F G C | C F G C | E7 A was A major, a
/// key the music had never been in, and A minor's Picardy cadence was a tonicization of A
/// major on both roads; with the Picardy counted as a chromatic chord, A minor lost the close
/// to F sharp minor, which owns both chords outright and was never heard.</description></item>
/// <item><description><b>A key is heard from where its phrases began, and a silent bar is
/// neither key's.</b> The frame that hears a key from where its own chords began reaches back
/// phrase by phrase: a phrase framed by the new key's tonic chord — opening on it and closing
/// on it — whose every bar the new key owns, running into the phrase the key is heard from, is
/// the key's too, though no note of the key's own falls in it — the phrase after names it. A
/// phrase that closes on the old key's tonic is the old key's whatever it opens on: C F G C |
/// G C G C | G C D7 G, V I V I closing on C, is G from bar 9, and was G from bar 5 when the
/// frame asked only where the phrase opened. A bar in which nothing sounds is
/// no bar of the old key's and none of the new: it neither ends a phrase nor splits its frame,
/// and a key begins at its first sounding bar, not in the silence before it. Read only from
/// the phrase that holds the F sharp, C F G C | G C R G | G C D7 G was G at bar 9 on both roads
/// — with the seventh bar silent, and with a G chord struck in it alike — where a musician
/// hears G from bar 5, the phrase G C – G being G's whatever its cadence, confirmed by the D7 G
/// after it; and C F G C | R | G C D7 G was G at bar 7, a bar late, the silent fifth bar
/// splitting the phrase from the D7 that names it. A bar in which the old key's closing chord
/// sounds on under a fermata is that chord's bar, not silence: passed over as silence, it was
/// claimed for the new key, and a chorale whose C phrase closes on a two-bar fermata began G a
/// bar early.</description></item>
/// <item><description><b>A melody moving through the tones of the closing chord is that
/// chord.</b> The last harmony of the piece is the last chord struck, its restrikes, and the
/// notes of the line after it while its harmony holds that are its own tones or short notes on
/// their way between them by step — passing and neighbour notes; a tune that arpeggiates the
/// final chord in quarters and a half ends the piece on that chord, as an arpeggio in eighths
/// does, and so does a scale run E D C sharp B A down to the final note, or a turn A B A G
/// sharp A on it — read for its tones alone, either left the chord no last harmony, and the
/// Picardy cadence was a modulation to A major. With the last harmony the last chord and the
/// melody's three notes after it, the A major chord closing Am Dm E7 Am | C F G C | Am Dm E7 A
/// under a tune ending C sharp, E, A was no Picardy third: A major owned the close outright
/// and the Picardy cadence was a modulation to A major at bar 11 on both roads, where the
/// same chords under a melody that stops with them were A minor's homecoming at bar
/// 9.</description></item>
/// </list>
/// </remarks>
internal static class KeyAreaJudge
{
    /// <summary>
    /// How long a key must hold to be a key and not a chord: four whole notes, a four-bar
    /// phrase in common time. The evidence for a change is read over this length, and a change
    /// that does not hold for it is a tonicization.
    /// </summary>
    internal static readonly Rational Phrase = new(4, 1);

    // A phrase names a key when, among the keys that own most of it, the best separates from
    // the runner-up by this margin; the margin semantics are those of
    // KeyDetectionResult.Confidence, on which a clear detection sits around 0.1-0.35 and a
    // straddle of two keys near zero. Measured on the passages in
    // RealModulationsAreHeardWhereAMusicianHearsThemTests: every phrase that begins where a
    // new key begins clears it (0.12-0.37), and the phrases that straddle two keys fall below
    // it — C D♭ G♭ A♭ reads A♭ over D♭ at 0.030, and C D7 G C reads G over C at 0.025.
    private const float MinDecisive = 0.11f;

    // The new key must beat the key the music is in by this margin, measured the same way on
    // the pair that matters. Also the hysteresis that keeps a wobble from reading as a return.
    private const float MinSeparation = 0.11f;

    // A phrase that ties two keys within this margin establishes neither. Symmetric material —
    // a tritone pair of triads — scores two keys identically, and the tie is broken by key
    // index, which a transposition does not respect; refusing to build on a tie keeps the
    // judge's answer moving with the music.
    private const float MinTold = 0.01f;

    /// <summary>
    /// A key owns a bar when the notes it lacks amount to less than this in it — a quarter
    /// note of pitch-class weight, where a pitch class weighs the time it sounds. A stray note
    /// weighs an eighth; a chord tone sounds for its chord, a half bar or a bar, and is not
    /// stray. A passing tone, a neighbour tone or an appoggiatura of the key weighs nothing at
    /// all (<see cref="Evidence.IsNonHarmonic(int, KeySignature)"/>), nor does a note of the
    /// key's own chromatic chords (<see cref="Evidence.ChromaticChordOf"/>).
    /// </summary>
    /// <remarks>
    /// Measured in whole notes of pitch-class weight per bar, on the passages that must fail:
    /// the A7 of D7 G C A7, resolving to a D minor that G does not own, leaves G major 1.0 in
    /// its bar (the C sharp for a whole bar); the G chord of D G | A D, two bars of D and two of
    /// E read as A major, leaves A 0.5 in its bar and the B chord 0.5; the same passage
    /// arpeggiated in eighths leaves A 0.25 in the G chord's bar; a chromatic scale in eighths
    /// leaves every key 0.625 in every bar, none of its notes passing because none of them
    /// lands. And on what must pass: every modulation in the fixture leaves its new key 0.0 in
    /// every bar from where it begins — four bars of D flat in thirty-second-note arpeggios after
    /// four of C included, now that the detector rounds a note onto its eighth grid only when
    /// that moves it by less than half its length: while it rounded every onset, the last C
    /// notes of the bar before fell onto the D flat downbeat and left D flat 0.0625 in that bar,
    /// two thirty-seconds each sounding its own length from the grid point (0.25 while a chord
    /// sounded until the next chord).
    /// <para>
    /// The floor is <c>&gt;=</c> a quarter. It once weighed passing tones as it weighs strays:
    /// a chromatic quarter-note neighbour — a C sharp under the D7 of a G-major melody over
    /// chords — weighed exactly 0.25 in its bar, so the trajectory, which reads the melody,
    /// placed G four bars late (at 8 where a musician says 4) while the detector, which never
    /// saw the eighths, placed it at 4; two chromatic passing eighths in one bar weighed the
    /// same and a melody with two in every bar of its new key named no key at all; and a
    /// quarter E flat neighbour in G named E minor, whose raised seventh it is, on both roads.
    /// Measured with <c>&gt;</c> instead: the arpeggiated chain of secondary dominants names F
    /// sharp minor, and over two hundred random chromatic melodies the detector's modulations
    /// rise from 16 to 173 and the trajectory's from 3 to 59. The floor stays where it is; the
    /// passing tones weigh nothing, and those shapes are heard where a musician hears them.
    /// </para>
    /// </remarks>
    private const float StrayNote = 0.25f;

    /// <summary>
    /// The pitch classes a key owns: its scale, and for a minor key its raised leading tone,
    /// so that a minor key's dominant belongs to it.
    /// </summary>
    internal static ushort Owned(KeySignature key) => OwnedByKey[KeyIndex(key)];

    /// <summary>The index of <paramref name="key"/> among the twenty-four: its root, the minor keys after the major.</summary>
    private static int KeyIndex(KeySignature key) => key.Root + (key.IsMajor ? 0 : 12);

    /// <summary>The key at <paramref name="index"/> among the twenty-four (<see cref="KeyIndex"/>).</summary>
    private static KeySignature KeyAt(int index) => new((byte)(index % 12), index < 12);

    private static readonly ushort[] OwnedByKey = BuildOwned();

    /// <summary>For each pitch class, the keys — one bit each, by <see cref="KeyIndex"/> — that do not own it.</summary>
    private static readonly uint[] KeysLacking = BuildKeysLacking();

    private static uint[] BuildKeysLacking()
    {
        var lacking = new uint[12];
        for (var pc = 0; pc < 12; pc++)
        {
            for (var k = 0; k < 24; k++)
            {
                if ((OwnedByKey[k] & (1 << pc)) == 0)
                    lacking[pc] |= 1u << k;
            }
        }

        return lacking;
    }

    private static ushort[] BuildOwned()
    {
        var owned = new ushort[24];
        for (var root = 0; root < 12; root++)
        {
            owned[root] = new KeySignature((byte)root, true).GetScaleMask();
            owned[root + 12] = (ushort)(new KeySignature((byte)root, false).GetScaleMask() | (1 << PitchMath.Fold(root + 11)));
        }

        return owned;
    }

    /// <summary>
    /// The key changes in <paramref name="sonorities"/>, judged at each of <paramref name="candidates"/>
    /// (ascending positions at which a phrase may be read), starting from <paramref name="startKey"/>
    /// or, when that is <see langword="null"/>, from the key the opening phrase is in.
    /// <paramref name="phrase"/> is the length a key must hold; callers pass <see cref="Phrase"/>
    /// or their own longer window.
    /// </summary>
    internal static Judgement Judge(
        IReadOnlyList<Sonority> sonorities,
        IReadOnlyList<Rational> candidates,
        KeySignature? startKey,
        Rational phrase)
    {
        var changes = new List<KeyChange>();
        if (sonorities.Count == 0)
            return new Judgement(startKey ?? new KeySignature(0, true), changes);

        var evidence = new Evidence(sonorities);
        var end = evidence.End;
        var firstOnset = evidence.FirstOnset;
        var openingByChord = false;
        var current = startKey ?? evidence.OpeningKey(phrase, out openingByChord);
        var opening = current;

        // An opening key read from the profile alone, with no chord to open on, is a guess —
        // and so is a key the caller gave that the piece does not open on: a musician told the
        // key takes the first chord for its tonic until the music says otherwise, and a given
        // key the music never confirms was the caller's guess, not the music's.
        var openingGuessed = startKey is null ? !openingByChord : !evidence.OpensOnTheTonicOf(current);

        // The keys the music has been in — the opening key and every key established since —
        // one bit each by KeyIndex: a return to one of them is a homecoming (see below).
        var wasIn = 1u << KeyIndex(current);
        evidence.KeysTheMusicHasBeenIn = wasIn;

        // No change may be placed before this: the change before it, or the return home after a
        // tonicization, so that one excursion is counted once.
        var floor = Rational.Zero;

        // A phrase is read from a bar: where the bars begin later than the clock — every
        // chord late by the same eighth — a candidate on the clock is read from the nearest
        // bar, which is where the harmony it would read begins. Read from the clock, the
        // trajectory's whole-note candidates took the tail of the last chord of the old key
        // into the first bar of the new, and named the old key for the phrase.
        Rational? lastRead = null;
        foreach (var candidate in candidates)
        {
            var b = evidence.NearestBar(candidate);
            if (lastRead is { } read && b == read)
                continue;
            lastRead = b;

            var to = b + phrase < end ? b + phrase : end;
            if (to <= b || to <= floor)
                continue;

            // A phrase does not begin in the middle of a chord: an arpeggiated chord is one
            // chord, and read from its last eighth — an E flat, with D7 G G C after it — a bar of
            // C minor borrowed in G named E minor on the detector road, whose candidates are
            // every onset, where the trajectory's whole-note points never fell inside the chord.
            if (evidence.BeginsInsideAChord(b))
                continue;

            var reading = evidence.Profile(b, to);
            if (!reading.Result.IsDecidable)
                continue;

            // The key in force owns every note of the phrase outright and reads best on the
            // profile: no key owns the phrase better, and the phrase is its own — which is what
            // naming it would say, at the price of weighing twenty-four keys over every note.
            // Most phrases of a piece that stays in its key are this one.
            if (reading.Result.Key == current && Evidence.OwnsEveryNoteOutright(current, reading.Mask))
                continue;

            var named = evidence.Name(reading, b, to, current);
            if (named.Margin < MinDecisive)
                continue;

            var next = named.Key;
            if (next == current)
                continue;

            // A return to a key the music has been in — the key it opened in, or one it
            // established since — in the stretch that closes the piece on that key's tonic is a
            // homecoming, and the ear that knows the key needs no profile margin to hear it: the
            // key that owns the close and names it by ownership has it. The profile reads A7 Dm
            // G7 C, a ii–V7–I home after a phrase in G, as G major still — the dominant sounded
            // twice, the tonic once — and a return that ends the piece was refused for that
            // margin, which is hysteresis against a wobble, and nothing follows the final
            // cadence to wobble back to. A return earlier in the piece faces the margin as any
            // change does. Here, before the return's own stretch is known, the candidate's
            // window stands in for it: a candidate whose window ends with the piece is reading
            // its close.
            var homeKey = (wasIn & (1u << KeyIndex(next))) != 0 && evidence.ClosesOn(next);

            var separation = Separation(reading.Result, next, current);
            if (separation < MinSeparation && !(homeKey && to == end))
                continue;

            // A key owns its phrase: the phrase that names the new key sounds nothing the new
            // key lacks beyond a stray note — not the old key's own notes, which would mean the
            // old key was never left, and not an applied dominant's altered note either, unless
            // it resolves into a chord the new key owns.
            if (!evidence.Owns(b, to, next, current))
                continue;

            // A key that owns nothing the old key lacks — the relative major, from its minor —
            // cannot announce itself with a note; it is heard when a phrase reads as it with no
            // note of the old key's own in it. Any other key must sound a note the old key lacks
            // — as its own note, not as the Picardy third that closes the old key's piece.
            var owned = Owned(next);
            var newNotes = (ushort)(owned & ~Owned(current));
            var ownsNothingNew = newNotes == 0;
            if (!ownsNothingNew && !evidence.Sounds(newNotes, b, to, next, current))
                continue;

            // A guessed opening key that never sounded a note of its own before another key was
            // read was wrong, and the music was in the other key from the start. A random
            // diatonic melody without a cadence is as much A minor as C major to the profile,
            // and read as A minor at its opening it modulated to C at the first phrase that read
            // the other way; V I IV V | I IV V I in C, opening on its dominant, read as G on the
            // profile and modulated to C at its second chord. A key that has no note of its own
            // against the new one — the major, against its relative minor — is not a guess this
            // can test.
            // A home note that is a non-harmonic tone of the other key was never the guessed
            // key's own: the F sharp in a C-major scale's E F F sharp G is no note of E minor's,
            // and a C-major melody with chromatic runs in it, opened in E minor by the profile,
            // reported a modulation to C at its fourth bar.
            var homeNotes = (ushort)(Owned(current) & ~owned);
            if (openingGuessed
                && changes.Count == 0
                && homeNotes != 0
                && !evidence.SoundsAsItsOwn(homeNotes, firstOnset, to, next, current))
            {
                opening = next;
                current = next;
                wasIn = 1u << KeyIndex(next);
                evidence.KeysTheMusicHasBeenIn = wasIn;
                continue;
            }

            // Where does the new key begin? Not before the floor, not more than a phrase before
            // the evidence, and not before the last note the new key does not own: at the first
            // sonority after that which sounds a note of the new key the old one lacks; for a
            // key with no such note, where the phrase first leaves the old key's tonic chord.
            var scanFrom = b - phrase > floor ? b - phrase : floor;
            Rational first;
            if (ownsNothingNew)
            {
                var oldTonic = TonicTriad(current);
                if (!evidence.FirstOnsetSounding((ushort)(~oldTonic & 0x0FFF), b > floor ? b : floor, afterFrom: false, to, out first))
                    continue;
            }
            else
            {
                if (evidence.LastOnsetLacking(next, current, scanFrom, b, out var lastForeign))
                    scanFrom = evidence.NextOnsetAfter(lastForeign);
                if (!evidence.FirstOnsetOfTheKey(newNotes, scanFrom, to, next, current, out first))
                    continue;
            }

            var boundary = evidence.StartOfBar(first, next, current);
            if (ownsNothingNew && !evidence.IsBarStart(boundary))
            {
                // The relative major has no note of its own to begin on; it begins with a bar,
                // not on the note after the minor's last leading tone. In a melody every note
                // leaves the tonic triad, and the return read from the middle of the minor's
                // final cadence.
                boundary = evidence.NextBarStart(boundary);
            }

            if (boundary < floor)
                boundary = floor;
            if (boundary >= to)
                continue;

            // The return itself: from where the key begins, the music stays in it to the end of
            // the piece and closes on its tonic — a homecoming whatever its length, and whichever
            // candidate reaches it. Judged by the candidate's own window instead, a candidate
            // whose window ended a bar before the piece did reached the same return first, was
            // refused the margin, and blocked the candidate after it with a tonicization: G Em
            // Am D7 | G7 C C closed a piece that had gone to G as a tonicization where G Em Am
            // D7 | G7 C came home.
            var returning = homeKey && evidence.FirstOnsetLacking(next, current, boundary, end) == end;

            // Does the new key hold from there through a phrase? A phrase that cannot decide is
            // extended, to the end at most.
            var holdTo = (b > boundary ? b : boundary) + phrase;
            var (holds, hold, heldAs) = evidence.Holds(boundary, ref holdTo, next, current, phrase, returning ? 0f : MinSeparation);

            // The phrase from the boundary reads a third key decisively: the evidence straddled
            // two keys and its compromise reading named neither. Nothing happened here — unless
            // the new key began with its phrase, judged below — and the third key is found at
            // its own candidate. C7 F B♭ F | C F G C is home at its ninth bar, but C's own note
            // is in the eleventh, and the two-bar stretch from there, G C, reads as G.
            var thirdKey = !holds
                && hold.Result.IsDecidable
                && heldAs.Key != next
                && heldAs.Key != current
                && heldAs.Margin >= MinDecisive
                && Separation(hold.Result, heldAs.Key, current) >= MinSeparation;

            // The bar before the first note the new key alone owns, when the new key owns that
            // whole bar outright: the pivot chord, a chord of both keys. Or owns it with its own
            // dominant sevenths counted, when the bar opens on a chord it owns outright: G Em Am
            // A7 before Dm G7 C is C's bar, the A7 its V7/ii, and C is written at the G, the
            // pivot, where a musician writes it — asked for the bar outright, the judge wrote it
            // a bar late, at the Dm.
            var previousBar = boundary - Rational.Whole;
            var pivot = previousBar >= floor
                && (evidence.BarIsOwned(previousBar, next, current, Exempt.Nothing)
                    || (evidence.OpensOutright(next, previousBar) && evidence.BarIsOwned(previousBar, next, current, Exempt.Sevenths)));

            // A modulation is a change from the key the music is in: a stretch at the end can
            // only be a cadence in a key the music was in if the old key was still in force just
            // before it — in the pivot bar, or in the bar before the new key begins. Two bars of
            // F sharp closing a passage that left C four bars earlier for D and E is not a
            // modulation from C.
            // A bar both keys own but for a stray note — a chromatic passing eighth in the first
            // bar of a closing phrase — is the pivot bar still, and the old key was in force.
            var fromOldKey = pivot
                || evidence.BarIsOwned(previousBar, current, null)
                || (previousBar >= floor
                    && evidence.Owns(previousBar, boundary, next, current)
                    && evidence.Owns(previousBar, boundary, current, null));

            // A stretch at the end shorter than a phrase is a key only if the piece closes on
            // its tonic and that tonic was already heard — in the pivot bar, or in the stretch
            // before the close — so that the close is a cadence in a key the music was in.
            // V7/V–V closing a phrase is a half cadence, not a modulation to the dominant, and
            // a pivot bar without the tonic in it (vi of the old key as ii of the new) is not
            // where the tonic was heard.
            var fragment = holdTo == end && holdTo - boundary < phrase;

            // A return to a key the music has been in — the key it opened in, or one it
            // established since — needs no such confirmation: the ear knows that key already,
            // and a stretch at the end that closes on its tonic is its cadence, a homecoming,
            // shorter than a phrase or not. G Em Am A7 | Dm G7 C, closing a chorale that opened
            // in C and went to G, is C again at the A7 and the cadence; asked to hear the tonic
            // before the close, as a new key must, the judge heard a one-bar tonicization of C
            // and no return home. And G7 C C C, a full phrase home, needs no second bar of C's
            // own notes either (below): asked for one, the judge heard a four-bar tonicization.
            var homecoming = returning;
            var established = holds
                && (!fragment
                    || homecoming
                    || (fromOldKey && evidence.ClosesOn(next) && evidence.SoundsTonicBeforeClose(next, pivot ? previousBar : boundary)));

            // A key is entered when its own notes return: one bar of them is an applied
            // dominant, a second bar before the old key's own notes are heard again is the key
            // — unless the phrase is framed by the new tonic chord, which is a phrase in its
            // key. A key that owns nothing the old key lacks has no note of its own to return,
            // and a homecoming at the close has no second bar to return them in.
            var held = hold.Distribution;
            if (established
                && !ownsNothingNew
                && !homecoming
                && !evidence.Returns(newNotes, homeNotes, boundary, boundary + phrase + phrase, next, current)
                && !evidence.FramedBy(next, boundary, holdTo))
            {
                established = false;
            }

            // The relative major is confirmed by its cadence or its frame. A key that owns
            // nothing the old key lacks has no note of its own to announce itself with, and
            // the profile alone cannot tell it from the minor: it is reached when the phrase
            // that reads as it is framed by its tonic chord — opens on it and closes within it,
            // or opens on it and the phrase after opens on it again, a loop that returns to
            // its first chord — or closes on its tonic from its dominant, the cadence the minor
            // cannot make (its dominant carries the raised seventh). A phrase the minor owns
            // chord for chord, opening on the minor's tonic and closing elsewhere, confirms
            // nothing and is nothing — no tonicization either, which would run until the
            // minor's own note returned, or call the F C G that closes the loop a touch of C.
            // Am F C G, round and round, is the key it started in: read as C from its second
            // bar, told A minor, and from its first chord on the trajectory road, which opens
            // in A minor and heard C at bar 2 where the detector, told C, heard none.
            if (ownsNothingNew
                && !homecoming
                && !evidence.FramedBy(next, boundary, holdTo)
                && !evidence.ReturnsToItsTonic(next, boundary, phrase)
                && !evidence.ClosesWithACadence(next, boundary, holdTo, phrase))
            {
                continue;
            }

            // A modulation is written at its pivot chord.
            var position = boundary;
            if (established && pivot)
                position = previousBar;

            // A key area begins with a phrase. When the new key's own note falls later in its
            // phrase — the phrases counted from the first note — and the new key owns every bar
            // from the phrase's start, the key began there, if a phrase from there establishes
            // it: C F G C | G C D7 G | C F G C is G from its fifth bar, though the F sharp is in
            // the seventh and a phrase from the seventh runs into the return to C. Not a phrase
            // opening on the old key's tonic chord, which is still the old key: Am A7 Dm B♭ is A
            // minor with V7/iv and the Neapolitan, not D minor from its v.
            if (!ownsNothingNew)
            {
                var phraseStart = evidence.PhraseStart(first, phrase);
                if (phraseStart >= floor
                    && phraseStart < (established ? position : boundary)
                    && !evidence.OpensOn(current, phraseStart)
                    && evidence.BarIsOwned(phraseStart, next, current, Exempt.Nothing)
                    && evidence.BarsAreOwned(phraseStart, boundary, next, current))
                {
                    var startTo = phraseStart + phrase;
                    var (startHolds, earlier, _) = evidence.Holds(phraseStart, ref startTo, next, current, phrase, returning ? 0f : MinSeparation);
                    var startFragment = startTo == end && startTo - phraseStart < phrase;
                    var startHomecoming = returning;
                    var barBefore = phraseStart - Rational.Whole;
                    var startFromOldKey = evidence.BarIsOwned(barBefore, current, null)
                        || evidence.BarIsOwned(barBefore, next, current, Exempt.Sevenths);
                    if (startHolds
                        && (!startFragment
                            || startHomecoming
                            || (startFromOldKey && evidence.ClosesOn(next) && evidence.SoundsTonicBeforeClose(next, phraseStart)))
                        && (startHomecoming
                            || evidence.Returns(newNotes, homeNotes, phraseStart, phraseStart + phrase + phrase, next, current)
                            || evidence.FramedBy(next, phraseStart, startTo)))
                    {
                        established = true;
                        position = phraseStart;
                        held = earlier.Distribution;
                    }
                }

                // A key is heard from where its own chords began. When the key is established
                // from where its own note falls, opens its phrase on its tonic chord and owns
                // every bar from there to its own note, the key began with the phrase — the bars
                // are its own already, and need no second reading: on the detector road G E Am
                // D7 under a melody separated G from C by a hair less than the margin, and G,
                // established from the D7, was written at the Am. A bar of a foreign key inside
                // a key that is entered and framed is a chromatic parenthesis, and does not move
                // the key's beginning either: C F G C | G B♭ E♭ F B♭ D7 G | G C D7 G is G from
                // its fifth bar, with a bar of B flat quoted in its sixth; measured from the D7,
                // G began at bar 7.
                if (established
                    && phraseStart >= floor
                    && phraseStart < position
                    && evidence.OpensOn(next, phraseStart)
                    && evidence.BarsAreOwnedButForAParenthesis(phraseStart, boundary, next, current))
                {
                    position = phraseStart;
                }

                // And phrase by phrase before that: a phrase framed by the new key's tonic
                // chord — opening on it and closing on it — whose every bar the new key owns, a
                // bar in which nothing begins being neither its own nor foreign, and that runs
                // into the phrase the key is heard from, is the key's too, though no note of the
                // key's own falls in it. Not a phrase that closes on the old key's tonic: C F G
                // C | G C G C | G C D7 G is G from bar 9, its V I V I being C's. The
                // phrase has no note of its own to be named for; the phrase after it, which
                // has, names it. C F G C | G C R G | G C D7 G is G from its fifth bar: the
                // phrase G C – G opens on G and closes on it, G owns every sounding bar of it,
                // and the F sharp that names G falls in the phrase after. Read only from the
                // phrase that holds the F sharp, G began at bar 9 on both roads — with the
                // seventh bar silent, and with a G chord struck in it alike. Not a phrase
                // opening on the old key's tonic chord, which is still the old key.
                if (established && position == phraseStart && evidence.OpensOn(next, phraseStart))
                {
                    for (var earlier = phraseStart - phrase;
                        earlier >= floor
                            && evidence.OpensOn(next, earlier)
                            && !evidence.OpensOn(current, earlier)
                            && evidence.FramedBy(next, earlier, earlier + phrase)
                            && evidence.BarsAreOwned(earlier, position, next, current);
                        earlier -= phrase)
                    {
                        position = earlier;
                    }
                }

                // A key begins where its music does: a phrase that begins in silence begins the
                // key at its first sounding bar. C F G C | R | G C D7 G is G from bar 6, not
                // from the silent bar 5 its phrase begins in.
                if (established)
                    position = evidence.FirstSoundingBar(position);
            }

            if (thirdKey && !established)
                continue;

            // A change placed at the first note is the opening key misjudged, not a modulation:
            // the music was never in the key it would leave.
            if (established && position <= firstOnset)
            {
                opening = next;
                current = next;
                wasIn = 1u << KeyIndex(next);
                evidence.KeysTheMusicHasBeenIn = wasIn;
                floor = evidence.NextOnsetAfter(boundary);
                continue;
            }

            var total = 0f;
            foreach (var weight in held)
                total += weight;
            var stability = total > 0f ? 1f - (Outside(held, next) / total) : 0f;

            // An established key holds until a note it does not own; an excursion lasts until
            // the music is home again — and an excursion that is home again before the new key's
            // tonic chord has sounded touched nothing: a single F sharp in an arpeggio is not a
            // tonicization of E major.
            Rational heldUntil;
            if (established)
            {
                heldUntil = evidence.FirstOnsetLacking(next, current, position, end);
            }
            else
            {
                heldUntil = evidence.HomeAgain(next, current, newNotes, homeNotes, boundary, out var tonicHeard);
                if (!tonicHeard)
                    continue;
            }

            changes.Add(new KeyChange(position, current, next, established, separation, stability, heldUntil));

            if (established)
            {
                current = next;
                wasIn |= 1u << KeyIndex(next);
                evidence.KeysTheMusicHasBeenIn = wasIn;
                floor = evidence.NextOnsetAfter(boundary);
            }
            else
            {
                // The same excursion is not counted twice: the next change waits for the music
                // coming home.
                floor = heldUntil > boundary ? heldUntil : evidence.NextOnsetAfter(boundary);
            }
        }

        return new Judgement(opening, changes);
    }

    /// <summary>A phrase's key, and how clearly it was named: the profile margin over the runner-up among the keys that own most of the phrase, 1 when no other key owns as much.</summary>
    private readonly record struct Named(KeySignature Key, float Margin);

    /// <summary>
    /// How much better the material fits <paramref name="next"/> than <paramref name="current"/>:
    /// the margin between their correlations relative to the better, clamped to 0-1 the way
    /// <see cref="KeyDetectionResult.Confidence"/> clamps its own, and zero when the better
    /// correlation is not positive.
    /// </summary>
    private static float Separation(KeyDetectionResult reading, KeySignature next, KeySignature current)
    {
        var nextCorrelation = CorrelationOf(reading, next);
        if (nextCorrelation <= 0f)
            return 0f;

        var separation = (nextCorrelation - CorrelationOf(reading, current)) / (nextCorrelation + 0.001f);
        return Math.Clamp(separation, 0f, 1f);
    }

    private static float CorrelationOf(KeyDetectionResult reading, KeySignature key)
    {
        foreach (var candidate in reading.AllCorrelations)
        {
            if (candidate.Key == key)
                return candidate.Correlation;
        }

        return 0f;
    }

    /// <summary>The pitch classes of <paramref name="key"/>'s tonic triad.</summary>
    private static ushort TonicTriad(KeySignature key) => TonicTriadByKey[KeyIndex(key)];

    private static readonly ushort[] TonicTriadByKey = BuildTonicTriads();

    private static ushort[] BuildTonicTriads()
    {
        var triads = new ushort[24];
        for (var root = 0; root < 12; root++)
        {
            triads[root] = (ushort)((1 << root) | (1 << PitchMath.Fold(root + 4)) | (1 << PitchMath.Fold(root + 7)));
            triads[root + 12] = (ushort)((1 << root) | (1 << PitchMath.Fold(root + 3)) | (1 << PitchMath.Fold(root + 7)));
        }

        return triads;
    }

    /// <summary>
    /// Whether <paramref name="pitchClasses"/> is <paramref name="key"/>'s tonic chord: it
    /// lies in the tonic triad and sounds the tonic. A lone G is a note of the C major triad,
    /// but a phrase beginning on G does not begin on the chord of C.
    /// </summary>
    private static bool IsTonicChord(KeySignature key, ushort pitchClasses) =>
        pitchClasses != 0
        && (pitchClasses & ~TonicTriad(key)) == 0
        && (pitchClasses & (1 << key.Root)) != 0;

    /// <summary>The weight of the material that <paramref name="key"/> does not own.</summary>
    private static float Outside(ReadOnlySpan<float> distribution, KeySignature key)
    {
        var owned = Owned(key);
        var sum = 0f;
        for (var pc = 0; pc < 12; pc++)
        {
            if ((owned & (1 << pc)) == 0)
                sum += distribution[pc];
        }

        return sum;
    }

    /// <summary>
    /// The pitch classes of a dominant seventh chord on <paramref name="root"/>: root, major
    /// third, fifth and minor seventh.
    /// </summary>
    private static ushort DominantSeventh(int root) => DominantSevenths[root];

    private static readonly ushort[] DominantSevenths = BuildDominantSevenths();

    private static ushort[] BuildDominantSevenths()
    {
        var sevenths = new ushort[12];
        for (var root = 0; root < 12; root++)
        {
            sevenths[root] = (ushort)((1 << root)
                | (1 << PitchMath.Fold(root + 4))
                | (1 << PitchMath.Fold(root + 7))
                | (1 << PitchMath.Fold(root + 10)));
        }

        return sevenths;
    }

    /// <summary>
    /// The key profile of a span, with the pitch classes that sound in it and the weight of
    /// each pitch class over it.
    /// </summary>
    /// <param name="Result">The reading <see cref="KeyProfiler.DetectFromBuffer"/> would give the span.</param>
    /// <param name="Mask">The pitch classes that sound in the span.</param>
    /// <param name="Distribution">The weight of each pitch class over the span: the time it sounds.</param>
    private readonly record struct Reading(KeyDetectionResult Result, ushort Mask, float[] Distribution);

    /// <summary>
    /// How far a key's chromatic chords count as its own when its material is weighed: not at
    /// all — every note outside the scale is foreign, which measures how much chromatic harmony
    /// the key needs; its dominant sevenths and Picardy third, which name themselves; or all of
    /// them, its applied triads and borrowed chords too.
    /// </summary>
    private enum Exempt
    {
        Nothing,
        Sevenths,
        Chords,
    }

    /// <summary>The sonorities in onset order, with the lookups the judge needs.</summary>
    private sealed class Evidence
    {
        private readonly Sonority[] _sonorities;
        private readonly int[] _long;

        // Each sonority's onset and end as doubles (exact, the positions being dyadic), the
        // longest ordinary sonority's length likewise, and whether any chord of two or more
        // pitch classes sounds anywhere in the music.
        private readonly double _longestD;
        private readonly double[] _onset;
        private readonly double[] _end;
        private readonly bool _anyChord;

        // Sonorities that begin together are one chord, and so are the single notes of an
        // arpeggiated chord (MarkArpeggios): for each sonority, the first index of its chord
        // and one past the last. For each sonority, the chord's pitch classes — less a note
        // struck with the chord that leans on it (_leaning, an accented appoggiatura), which is
        // no tone of the chord; the pitch classes of the next chord of three or more notes — a
        // melody's single notes between are not chords — which is what the chord resolves
        // into; and, when the chord is a major triad or a dominant seventh whose resolution is
        // a triad on the root a fifth below, that root, so that an applied chord can be told to
        // belong to the key that owns its resolution.
        private readonly int[] _groupStart;
        private readonly int[] _groupEnd;
        private readonly int[] _group;
        private readonly int _groupCount;
        private readonly ushort[] _chordPitchClasses;
        private readonly ushort[] _leaning;
        private readonly ushort[] _following;
        private readonly int[] _appliedRoot;

        // The first sonority of the chord each chord resolves into (_following), or -1: a chain
        // of applied dominants is followed from chord to chord (ChromaticChordParts).
        private readonly int[] _resolvesAt;

        // Where each sonority's harmony holds to. A chord — two pitch classes or more — is the
        // harmony under the line from its onset until the next chord begins, though its notes
        // may stop sooner: a staccato chord is released, and the ear keeps its harmony through
        // the bar; but no longer than a whole note past where its notes stop, so that a chord
        // long gone governs no melody. A note of the line holds no harmony of its own, and its
        // entry is its end. What a chord weighs is still the time its notes sound (_end).
        private readonly double[] _harmonyEnd;
        private readonly double _harmonyReachD;

        // Where the bars begin: the phase, in whole notes, of the onsets that carry the
        // harmony. When more than half the struck chords fall the same distance after the
        // whole-note grid — every chord late by an eighth — the bars are late by that much,
        // and every bar the judge reads (a bar it owns, a pivot bar, the start of a bar, a
        // downbeat) begins there; with no struck chords, the phase at which the most notes
        // group into arpeggiated chords. Zero — the clock — when the chords say nothing, and
        // always a multiple of an eighth.
        private readonly double _phase;

        // Whether each chord is a restrike of the chord before it — the same pitch classes,
        // struck again in the same bar while the earlier chord's harmony holds: one harmony
        // struck twice, which resolves into the next chord that differs (_following), and on
        // whose second strike no phrase begins (BeginsInsideAChord).
        private readonly bool[] _restrike;

        // Whether each sonority is a single note of the line — a pitch, alone at its onset —
        // and whether the chord it belongs to could be one of some key's chromatic chords: an
        // applied chord, a borrowed chord (it holds a triad) or the last chord (a Picardy
        // third) — or, for a note of the line, whether it is a tone of such a chord sounding
        // under it (_under: a sonority of that chord, or -1). Most sonorities in a melody are
        // neither, and are weighed without asking.
        private readonly bool[] _lineNote;
        private readonly bool[] _maybeChromatic;
        private readonly int[] _under;

        // Whether each sonority is a note of the line struck with a chord and let go before it,
        // no tone of the chord: the line's note, not the chord's — a passing eighth that falls on
        // the accompaniment's stroke, an appoggiatura struck with the chord it leans on. It is
        // no part of the chord's pitch classes, and the chord struck with it is the harmony
        // under it (ChordUnder, SoundingUnder).
        private readonly bool[] _overChord;

        // Whether each sonority doubles a tone another sonority struck with it already sounds:
        // it adds no weight of its own when the chord is weighed, so that a chord weighs the
        // same struck as three notes or as one sonority of three pitch classes. And the pitch
        // classes each sonority weighs when a span is weighed (Owners): its own, less a leaning
        // note; nothing for a doubling; and for a chord struck as several notes that end
        // together, the whole chord on its first note and nothing on the rest, so that the
        // chord is weighed once on either road.
        private readonly bool[] _doubling;
        private readonly ushort[] _weighs;

        // Whether a key in force still stands at each sonority — it has owned every chord
        // since its tonic chord last sounded — found once per key (StandsAt), for the keys the
        // music is ever in.
        private bool[]?[]? _stands;

        // Whether each sonority lasts no longer than a quarter note, and no longer than an eighth.
        private readonly bool[] _short;
        private readonly bool[] _eighthOrLess;

        // The keys — one bit each, by KeyIndex — a single note is a non-harmonic tone of:
        // passing, neighbour or appoggiatura. Decided once per note for all twenty-four keys,
        // because every phrase that holds the note asks the same question of it; the top bit
        // says the note has been asked.
        private uint[]? _nonHarmonic;
        private const uint Asked = 1u << 31;

        // Whether a single note is a note on its way — short, approached and left by step by
        // notes no shorter than itself — decided once per note; it does not depend on the key.
        private byte[]? _onItsWay;

        // Whether a single note of the line leans on the harmony sounding under it — an
        // appoggiatura, an accented passing tone, a suspension — decided once per note; it does
        // not depend on the key.
        private byte[]? _leans;

        // What sounds either side of each note, and the run of notes on their way it is on,
        // found once per note: none of it depends on the key, and every key asks.
        private Neighbour[]? _before;
        private Neighbour[]? _after;
        private Run[]? _run;

        public Rational End { get; }

        /// <summary>
        /// The keys the music has been in so far — the key it opened in and every key established
        /// since — one bit each by <see cref="KeyIndex"/>; <see cref="Judge"/> keeps it current.
        /// A minor key among them closing the piece on its Picardy third owns that close, not its
        /// parallel major (<see cref="Owners"/>).
        /// </summary>
        public uint KeysTheMusicHasBeenIn { get; set; }

        /// <summary>The onset of the first sonority, or zero when there is none.</summary>
        public Rational FirstOnset => _sonorities.Length > 0 ? _sonorities[0].Onset : Rational.Zero;

        public Evidence(IReadOnlyList<Sonority> sonorities)
        {
            // Positions as doubles, for the clipping and the comparisons every scan makes: a
            // position in this music is a dyadic rational, which a double holds exactly, so the
            // arithmetic is the same and a hundred times cheaper than the rational's.
            _sonorities = new Sonority[sonorities.Count];
            _onset = new double[_sonorities.Length];
            _end = new double[_sonorities.Length];
            _short = new bool[_sonorities.Length];
            _eighthOrLess = new bool[_sonorities.Length];
            var sorted = true;
            for (var i = 0; i < _sonorities.Length; i++)
            {
                _sonorities[i] = sonorities[i];
                _onset[i] = _sonorities[i].Onset.ToDouble();
                sorted &= i == 0 || _onset[i - 1] <= _onset[i];
            }

            if (!sorted)
                Array.Sort(_sonorities, static (a, b) => a.Onset.CompareTo(b.Onset));

            // The few sonorities far longer than the rest — a pedal held for the piece — are
            // kept aside, so that a scan for what sounds at a position reaches back over the
            // ordinary notes only and visits the pedal on its own: reaching back over the pedal's
            // length, every scan began at the first note, and a piece of four thousand chords
            // over a two-thousand-bar pedal took twenty times as long as the same chords alone.
            var end = Rational.Zero;
            var longest = Rational.Zero;
            var longOnes = new List<int>();
            for (var i = 0; i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (!sorted)
                    _onset[i] = s.Onset.ToDouble();
                _end[i] = s.End.ToDouble();
                if (s.End > end)
                    end = s.End;
                var length = s.End - s.Onset;
                _short[i] = length <= Rational.Quarter;
                _eighthOrLess[i] = length <= Rational.Eighth;
                if (length > Phrase * 4)
                    longOnes.Add(i);
                else if (length > longest)
                    longest = length;
            }

            if (longOnes.Count > 16)
            {
                foreach (var i in longOnes)
                {
                    var length = _sonorities[i].End - _sonorities[i].Onset;
                    if (length > longest)
                        longest = length;
                }

                longOnes.Clear();
            }

            End = end;
            _longestD = longest.ToDouble();
            _long = [.. longOnes];

            _chordPitchClasses = new ushort[_sonorities.Length];
            _leaning = new ushort[_sonorities.Length];
            _following = new ushort[_sonorities.Length];
            _appliedRoot = new int[_sonorities.Length];
            _resolvesAt = new int[_sonorities.Length];
            Array.Fill(_resolvesAt, -1);
            _lineNote = new bool[_sonorities.Length];
            _overChord = new bool[_sonorities.Length];
            _maybeChromatic = new bool[_sonorities.Length];
            _under = new int[_sonorities.Length];
            _doubling = new bool[_sonorities.Length];
            _weighs = new ushort[_sonorities.Length];
            _groupStart = new int[_sonorities.Length];
            _groupEnd = new int[_sonorities.Length];
            _group = new int[_sonorities.Length];

            // Sonorities that begin together are one chord; a sonority alone at its onset,
            // with a pitch, is a note of the line. Every onset, an eighth at a time after the
            // whole note, is counted for the phase of the bars.
            var anyChord = false;
            Span<int> phases = stackalloc int[8];
            var onsets = 0;
            for (var i = 0; i < _sonorities.Length;)
            {
                var next = NextChord(i);
                ushort seen = 0;
                for (var k = i; k < next; k++)
                {
                    _groupStart[k] = i;
                    _groupEnd[k] = next;
                    _lineNote[k] = next - i == 1 && _sonorities[k].Pitch >= 0 && BitOperations.PopCount(_sonorities[k].PitchClasses) == 1;
                    seen |= _sonorities[k].PitchClasses;

                    // A doubling is the shorter sonority of a pitch class struck twice — the
                    // melody's quarter over the chord's whole note — or, of two that end
                    // together, the one with fewer pitch classes — the chorale's doubled root
                    // beside the chord it doubles — whatever order they came in.
                    var doubling = false;
                    for (var other = i; other < next && !doubling; other++)
                    {
                        var mine = _sonorities[k].PitchClasses;
                        var theirs = _sonorities[other].PitchClasses;
                        doubling = other != k
                            && (mine & ~theirs) == 0
                            && (_end[other] > _end[k]
                                || (_end[other] == _end[k] && (mine != theirs || other < k)));
                    }

                    _doubling[k] = doubling;
                }

                // A note struck with a chord and let go before it, no tone of the chord that
                // outlasts it, is a note of the line over the chord — the melody's eighth that
                // happens to fall on the accompaniment's stroke — and is heard as the line's.
                for (var k = i; k < next; k++)
                {
                    var note = _sonorities[k];
                    if (note.Pitch < 0 || BitOperations.PopCount(note.PitchClasses) != 1)
                        continue;

                    ushort outlasting = 0;
                    for (var other = i; other < next; other++)
                    {
                        if (_end[other] > _end[k])
                            outlasting |= _sonorities[other].PitchClasses;
                    }

                    if (BitOperations.PopCount(outlasting) >= 2 && (outlasting & note.PitchClasses) == 0)
                    {
                        _overChord[k] = true;
                        _lineNote[k] = true;
                    }
                }

                anyChord |= BitOperations.PopCount(seen) > 1;
                var eighths = (_onset[i] - Math.Floor(_onset[i])) * 8;
                if (eighths == Math.Floor(eighths))
                {
                    phases[(int)eighths]++;
                    onsets++;
                }

                i = next;
            }

            _anyChord = anyChord;

            // A melody alone sets no harmony, and its scans need reach no further back.
            _harmonyReachD = anyChord ? _longestD + 1.0 : _longestD;

            _phase = anyChord ? PhaseOfTheOnsets(phases, onsets) : PhaseOfTheArpeggios();
            Phase = new Rational((long)(_phase * 8), 8);
            MarkArpeggios();

            // Each chord's ordinal, for the tables kept per chord.
            var groupCount = 0;
            for (var i = 0; i < _sonorities.Length; i = _groupEnd[i])
            {
                var groupEnd = _groupEnd[i];
                for (var k = i; k < groupEnd; k++)
                    _group[k] = groupCount;
                groupCount++;
            }

            _groupCount = groupCount;

            // Where each chord's harmony holds to: the next chord of two pitch classes or more,
            // or a whole note past the chord's own end, whichever comes first — and never
            // before its notes stop. Found from the end, chord by chord, before the leaning
            // notes are, which ask what harmony holds where they resolve; a chord of two pitch
            // classes stays one with a leaning note taken out, that being taken from four.
            _harmonyEnd = new double[_sonorities.Length];
            var nextChord = double.PositiveInfinity;
            for (var i = _sonorities.Length - 1; i >= 0; i = _groupStart[i] - 1)
            {
                var start = _groupStart[i];
                var groupEnd = _groupEnd[i];
                ushort union = 0;
                for (var k = start; k < groupEnd; k++)
                    union |= _sonorities[k].PitchClasses;

                var isChord = BitOperations.PopCount(union) > 1;
                for (var k = start; k < groupEnd; k++)
                    _harmonyEnd[k] = isChord ? Math.Max(_end[k], Math.Min(nextChord, _end[k] + 1.0)) : _end[k];
                if (isChord)
                    nextChord = _onset[start];

                if (start == 0)
                    break;
            }

            for (var i = 0; i < _sonorities.Length; i = _groupEnd[i])
            {
                var groupEnd = _groupEnd[i];

                // The chord is the notes struck together less the line's notes over it
                // (_overChord); the first of the chord's own notes carries its weight when they
                // stop together.
                ushort together = 0;
                var first = -1;
                for (var k = i; k < groupEnd; k++)
                {
                    if (_overChord[k])
                        continue;
                    together |= _sonorities[k].PitchClasses;
                    if (first < 0)
                        first = k;
                }

                var struck = _sonorities[groupEnd - 1].Onset == _sonorities[i].Onset;
                var leaning = struck ? LeaningNoteIn(i, groupEnd, together) : (ushort)0;
                var core = (ushort)(together & ~leaning);
                var endTogether = struck && leaning == 0;
                for (var k = i; k < groupEnd; k++)
                {
                    _chordPitchClasses[k] = core;
                    _leaning[k] = leaning;
                    _appliedRoot[k] = -1;
                    _maybeChromatic[k] = groupEnd == _sonorities.Length || HoldsATriad(core);
                    endTogether &= _overChord[k] || _end[k] == _end[first];
                }

                for (var k = i; k < groupEnd; k++)
                {
                    _weighs[k] = _overChord[k]
                        ? _sonorities[k].PitchClasses
                        : endTogether ? (k == first ? core : (ushort)0) : _doubling[k] ? (ushort)0 : (ushort)(_sonorities[k].PitchClasses & ~leaning);
                }
            }

            // A chord struck twice in its bar is one harmony: a chord of two pitch classes or
            // more whose pitch classes are the chord before it's, struck in the same bar while
            // that chord's harmony still holds, is a restrike of it.
            _restrike = new bool[_sonorities.Length];
            var previousChord = -1;
            for (var i = 0; i < _sonorities.Length; i = _groupEnd[i])
            {
                if (BitOperations.PopCount(_chordPitchClasses[i]) < 2)
                    continue;

                if (previousChord >= 0
                    && _chordPitchClasses[previousChord] == _chordPitchClasses[i]
                    && _harmonyEnd[previousChord] >= _onset[i]
                    && BarStartD(_onset[previousChord]) == BarStartD(_onset[i]))
                {
                    for (var k = i; k < _groupEnd[i]; k++)
                        _restrike[k] = true;
                }

                previousChord = i;
            }

            // A chord resolves into the next chord of three notes or more — a melody's single
            // notes between are not chords, and a restrike of the chord itself is the chord
            // still, so the chord resolves where its restrike does; an applied chord is a major
            // triad or a dominant seventh resolving into a triad on the root a fifth below. The
            // next chord is found in one pass from the end: found forward, chord by chord, a
            // melody of twenty thousand notes took twenty thousand scans to its end.
            var resolvesAt = _sonorities.Length;
            for (var i = _sonorities.Length - 1; i >= 0; i = _groupStart[i] - 1)
            {
                var start = _groupStart[i];
                var groupEnd = _groupEnd[i];
                if (BitOperations.PopCount(_chordPitchClasses[start]) >= 3)
                {
                    if (resolvesAt < _sonorities.Length)
                    {
                        var restruck = _restrike[resolvesAt] && _chordPitchClasses[resolvesAt] == _chordPitchClasses[start];
                        var following = restruck ? _following[resolvesAt] : _chordPitchClasses[resolvesAt];
                        var root = AppliedRootOf(_chordPitchClasses[start]);
                        if (root >= 0 && !ResolvesTo(root, following))
                            root = -1;

                        for (var k = start; k < groupEnd; k++)
                        {
                            _following[k] = following;
                            _resolvesAt[k] = restruck ? _resolvesAt[resolvesAt] : resolvesAt;
                            _appliedRoot[k] = root;
                            _maybeChromatic[k] |= root >= 0;
                        }
                    }
                    else
                    {
                        for (var k = start; k < groupEnd; k++)
                            _resolvesAt[k] = -1;
                    }

                    resolvesAt = start;
                }

                if (start == 0)
                    break;
            }

            // A note of the line that is a tone of the chord sounding under it is that chord's.
            // A melody alone has no chord to sound under anything.
            for (var i = 0; i < _sonorities.Length; i++)
            {
                _under[i] = -1;
                if (!_lineNote[i] || !_anyChord)
                    continue;

                var under = ChordUnder(i);
                if (under >= 0 && (_chordPitchClasses[under] & _sonorities[i].PitchClasses) != 0)
                {
                    _under[i] = under;
                    _maybeChromatic[i] |= _maybeChromatic[under];
                }
            }
        }

        /// <summary>
        /// A sonority of the chord whose harmony holds under sonority <paramref name="index"/>
        /// — the latest-beginning chord of two or more notes that began before it and whose
        /// harmony still holds when it begins (<see cref="_harmonyEnd"/>) — or -1.
        /// </summary>
        private int ChordUnder(int index)
        {
            var onset = _sonorities[index].Onset;
            var onsetD = _onset[index];
            var under = -1;
            foreach (var k in ReachingHarmony(onset))
            {
                // A note of the line struck with a chord (_overChord) has that chord under it.
                if (_sonorities[k].Onset > onset)
                    break;
                if (_sonorities[k].Onset == onset && (!_overChord[index] || _overChord[k]))
                    continue;
                if (_harmonyEnd[k] > onsetD && BitOperations.PopCount(_chordPitchClasses[k]) > 1 && (under < 0 || _sonorities[k].Onset >= _sonorities[under].Onset))
                    under = k;
            }

            return under;
        }

        /// <summary>
        /// An arpeggiated chord is that chord: a bar, a half bar or a quarter of single notes of
        /// the line, none longer than an eighth, whose pitch classes together are exactly a
        /// major or a minor triad or a dominant seventh is one chord, and its notes are that
        /// chord's tones.
        /// </summary>
        /// <remarks>
        /// Heard note by note, the E major chord of C F G C | G E Am D7 | G C D7 G arpeggiated
        /// in eighths was three G sharps a bar — foreign notes to G major, three eighths of them
        /// — where the same chord struck was G's V/ii, and G began two bars late; and the
        /// borrowed C minor of G Cm D7 G, arpeggiated, was three E flats. The chord is looked
        /// for in a bar first, then in each half, then in each quarter, so that a half bar of G7
        /// and a half bar of C are two chords, not a cluster. Arpeggiation is a texture of quick,
        /// even notes; a tune in quarters that happens to outline a triad is a tune: read as
        /// chords, the bars of a D Dorian folk tune that outline G and C made the piece open in
        /// C major.
        /// </remarks>
        private void MarkArpeggios() => MarkArpeggios(_phase, mark: true);

        /// <summary>
        /// Marks the arpeggiated chords with the bars beginning at <paramref name="phase"/>, or —
        /// when not <paramref name="mark"/> — only counts the notes they would hold.
        /// </summary>
        private int MarkArpeggios(double phase, bool mark, int atLeast = 0)
        {
            if (_sonorities.Length < 3)
                return 0;

            // One sweep: each bar's sonorities are found by walking on from the last. A count
            // that can no longer reach atLeast — the notes still to come would not make it —
            // stops early.
            var grouped = 0;
            var first = 0;
            while (first < _sonorities.Length)
            {
                if (!mark && grouped + (_sonorities.Length - first) < atLeast)
                    return grouped;

                var bar = Math.Floor(_onset[first] - phase) + phase;
                var last = first;
                while (last < _sonorities.Length && _onset[last] < bar + 1)
                    last++;
                grouped += MarkArpeggiosIn(first, last, bar, bar + 1, mark);
                first = last;
            }

            return grouped;
        }

        private int MarkArpeggiosIn(int first, int last, double from, double to, bool mark)
        {
            if (last - first < 3)
                return 0;

            ushort together = 0;
            var quickLineNotes = true;
            for (var k = first; k < last; k++)
            {
                quickLineNotes &= _lineNote[k] && _eighthOrLess[k];
                together |= _sonorities[k].PitchClasses;
            }

            if (quickLineNotes && IsTriadOrDominantSeventh(together))
            {
                if (mark)
                {
                    for (var k = first; k < last; k++)
                    {
                        _groupStart[k] = first;
                        _groupEnd[k] = last;
                    }
                }

                return last - first;
            }

            if (to - from > 0.25)
            {
                var middle = (from + to) / 2;
                var split = first;
                while (split < last && _onset[split] < middle)
                    split++;
                return MarkArpeggiosIn(first, split, from, middle, mark) + MarkArpeggiosIn(split, last, middle, to, mark);
            }

            return 0;
        }

        /// <summary>
        /// The phase of the bars, read from the onsets of a music with chords in it: the eighth
        /// after the whole note on which more than half of all onsets — chords and notes of the
        /// line alike — fall, or zero, the clock, when no phase has that majority.
        /// </summary>
        /// <remarks>
        /// A musician hears the bars from the music's accents, not from the clock: when every
        /// chord is late by an eighth and nothing else sounds, the bars are late by an eighth.
        /// Read on the clock, the eighth of a chord that spilt into the next whole note was that
        /// whole note's material — a tail of F major in the first bar of G — and the key that
        /// owned the tail owned the phrase outright: four bars each of C, F, G and C, every
        /// chord an eighth late, was C at bar 8 where a musician hears G, on both roads, and the
        /// roads then parted over where G began. But a melody on the beat over chords struck
        /// an eighth after it is a melody on the beat with a syncopated accompaniment: the bars
        /// are the melody's, and the chords are late. Read from the chords alone, the melody's
        /// downbeat fell at the end of the bar before, and the F sharp that begins G's bar was
        /// the last note of A minor's; C F G C | Am D7 G C D7 G under its tune, the chords an
        /// eighth late, touched G twice and never arrived. Chords on the half bar, or in 3/4 and
        /// 5/4, share no phase either, and the clock stands.
        /// </remarks>
        private static double PhaseOfTheOnsets(ReadOnlySpan<int> phases, int onsets)
        {
            var best = 0;
            for (var eighth = 1; eighth < 8; eighth++)
            {
                if (phases[eighth] > phases[best])
                    best = eighth;
            }

            return phases[best] * 2 > onsets ? best / 8.0 : 0.0;
        }

        /// <summary>
        /// The phase of the bars in a music of single notes: the eighth after the whole note
        /// at which the most notes group into arpeggiated chords (<see cref="MarkArpeggios()"/>)
        /// — more than half of them, an arpeggiated texture — the clock unless some other phase
        /// groups strictly more.
        /// </summary>
        /// <remarks>
        /// An arpeggiated accompaniment an eighth late is heard as the chords it spells, in bars
        /// that begin with each chord. Read on the clock, each whole note held the last eighth of
        /// one chord and seven eighths of the next — no chord, so many single notes — and eight
        /// bars of C then eight of G in eighths, every note an eighth late, went to E minor at
        /// the G, the E of each C chord's tail leading every bar of G. A melody is not an
        /// arpeggio: the few notes of a random chromatic line that happen to spell a triad at
        /// some phase say nothing about its bars, and moved by them the detector's tonicizations
        /// over two hundred such lines rose from six to seventeen.
        /// </remarks>
        private double PhaseOfTheArpeggios()
        {
            if (_sonorities.Length < 3)
                return 0.0;

            var best = 0.0;
            var most = MarkArpeggios(0.0, mark: false);
            if (most == _sonorities.Length)
                return best;

            for (var eighth = 1; eighth < 8; eighth++)
            {
                var atLeast = Math.Max(most, _sonorities.Length / 2) + 1;
                var grouped = MarkArpeggios(eighth / 8.0, mark: false, atLeast);
                if (grouped >= atLeast)
                {
                    most = grouped;
                    best = eighth / 8.0;
                }
            }

            return best;
        }

        /// <summary>The start of the bar holding <paramref name="position"/>: whole notes from <see cref="_phase"/>.</summary>
        private double BarStartD(double position) => Math.Floor(position - _phase) + _phase;

        /// <summary>The start of the bar holding <paramref name="position"/>, exact.</summary>
        private Rational BarStart(Rational position) => Phase + new Rational((long)Math.Floor(position.ToDouble() - _phase), 1);

        /// <summary>The bar start nearest <paramref name="position"/>, the earlier of two equally near.</summary>
        private Rational NearestBarStart(Rational position)
        {
            var before = BarStart(position);
            var after = before + Rational.Whole;
            return after - position < position - before ? after : before;
        }

        /// <summary>Whether <paramref name="onset"/> is the start of a bar.</summary>
        private bool IsDownbeat(Rational onset)
        {
            var beats = onset.ToDouble() - _phase;
            return beats == Math.Floor(beats);
        }

        /// <summary>Whether <paramref name="position"/> is the start of a bar (<see cref="_phase"/>).</summary>
        public bool IsBarStart(Rational position) => IsDownbeat(position);

        /// <summary>The first bar start strictly after <paramref name="position"/>.</summary>
        public Rational NextBarStart(Rational position) => BarStart(position) + Rational.Whole;

        /// <summary>
        /// The bar a candidate is read from: <paramref name="position"/> itself, unless it is a
        /// whole note of the clock and the bars begin elsewhere (<see cref="_phase"/>), when it
        /// is the nearest bar start.
        /// </summary>
        public Rational NearestBar(Rational position) =>
            _phase != 0.0 && position.Denominator == 1 ? NearestBarStart(position) : position;

        /// <summary>The phase of the bars (<see cref="_phase"/>), exact: a multiple of an eighth.</summary>
        public Rational Phase { get; }

        /// <summary>Whether <paramref name="chord"/> is exactly a major or a minor triad, or exactly a dominant seventh.</summary>
        private static bool IsTriadOrDominantSeventh(ushort chord) => PlainChord[chord & 0x0FFF];

        // For every set of pitch classes, whether it is exactly a major or a minor triad or a
        // dominant seventh (PlainChord), and whether it holds a triad (TriadWithin): asked for
        // every bar, half bar and quarter of a melody at each of eight phases of the bar, the
        // twelve-root loops were a third of the time to judge a twenty-thousand-note melody.
        private static readonly bool[] PlainChord = BuildPlainChords();
        private static readonly bool[] TriadWithin = BuildTriadsWithin();

        private static bool[] BuildPlainChords()
        {
            var plain = new bool[4096];
            for (var root = 0; root < 12; root++)
            {
                plain[MajorTriad(root)] = true;
                plain[MinorTriad(root)] = true;
                plain[DominantSeventh(root)] = true;
            }

            return plain;
        }

        private static bool[] BuildTriadsWithin()
        {
            var holds = new bool[4096];
            for (var chord = 0; chord < 4096; chord++)
            {
                for (var root = 0; root < 12 && !holds[chord]; root++)
                    holds[chord] = (chord & MajorTriad(root)) == MajorTriad(root) || (chord & MinorTriad(root)) == MinorTriad(root);
            }

            return holds;
        }

        /// <summary>
        /// The pitch class of a note struck with the chord [<paramref name="start"/>,
        /// <paramref name="end"/>) that leans on it — an accented appoggiatura or a suspension:
        /// one pitch class outside a triad the chord holds, resolving by step into a tone of
        /// that triad at the first note of the line after the chord, the triad still sounding
        /// there, and not sounding again in its bar — or 0.
        /// </summary>
        /// <remarks>
        /// An appoggiatura is defined by its resolution, by step into a tone of the harmony it
        /// sounds against, not by whether that harmony was struck before it or with it. Heard
        /// only when struck after the chord, the commonest kind — struck with the chord on the
        /// downbeat — was a chord tone: an A sharp struck with G B D and resolving to B made the
        /// bar a G chord with an A sharp in it, no bar of G major's, and a melody with one on
        /// every downbeat of its new key named no key; and A struck over E G sharp B — a 4-3
        /// suspension — made the chord no plain triad, so it was no longer G's V/ii and G began
        /// two bars late. On the detector road the melody's downbeat is folded into the chord's
        /// sonority when it stops with the chord, and its step is read in pitch class; on the
        /// trajectory road the note is its own sonority, and its step is read in pitch. A note
        /// struck with the chord and let go before it is the line's (<see cref="_overChord"/>): it
        /// is no part of <paramref name="together"/>, and leans on the chord as any note of the
        /// line leans on the harmony under it (<see cref="LeansOnHarmony"/>).
        /// </remarks>
        private ushort LeaningNoteIn(int start, int end, ushort together)
        {
            if (BitOperations.PopCount(together) < 4 || end >= _sonorities.Length || !_lineNote[end])
                return 0;

            var resolution = _sonorities[end];
            var to = PitchMath.Fold(resolution.Pitch);
            var sounding = HarmonyAt(resolution.Onset);

            // Of the notes that could be leaning, the one that leaves a plain chord behind — a
            // triad or a dominant seventh — before one that leaves a chord with a stranger in
            // it, and the one resolving by a semitone before one resolving by a tone: over D7
            // with a C sharp struck on it, the C sharp leans and the C is the seventh, not the
            // other way about; chosen by bit order, the reading changed with the key.
            ushort leaning = 0;
            var best = int.MaxValue;
            var remaining = together;
            while (remaining != 0)
            {
                var pc = BitOperations.TrailingZeroCount(remaining);
                remaining &= (ushort)(remaining - 1);
                var note = (ushort)(1 << pc);
                var core = (ushort)(together & ~note);
                if (!HoldsATriad(core) || (core & (1 << to)) == 0 || (sounding & core) != core)
                    continue;

                // The step is read in pitch where the road has the note, in pitch class where
                // it has only the chord.
                var struck = -1;
                for (var k = start; k < end; k++)
                {
                    if (_sonorities[k].PitchClasses == note && _sonorities[k].Pitch >= 0)
                        struck = _sonorities[k].Pitch;
                }

                var interval = struck >= 0 ? Math.Abs(resolution.Pitch - struck) : Math.Min(PitchMath.Fold(to - pc), PitchMath.Fold(pc - to));
                if (interval is 0 or > 2 || SoundsAgainInBar(start, end, note))
                    continue;

                var rank = (IsTriadOrDominantSeventh(core) ? 0 : 2) + (interval == 1 ? 0 : 1);
                if (rank < best)
                {
                    best = rank;
                    leaning = note;
                }
            }

            return leaning;
        }

        /// <summary>Whether <paramref name="chord"/> holds a major or a minor triad on some root.</summary>
        private static bool HoldsATriad(ushort chord) => TriadWithin[chord & 0x0FFF];

        /// <summary>The index of the first sonority of the chord after the one <paramref name="index"/> belongs to.</summary>
        private int NextChord(int index)
        {
            var at = _onset[index];
            while (index < _sonorities.Length && _onset[index] == at)
                index++;
            return index;
        }

        /// <summary>
        /// Whether <paramref name="chord"/> is a triad, major or minor, on the root a fifth below
        /// <paramref name="root"/> — whatever else sounds with it, so long as nothing sounds a
        /// third below that root. Containing the note is not enough: a C major chord contains
        /// E, and B7 followed by C is not V7 of E resolving. Containing the triad is not enough
        /// either: G sharp minor seventh holds B, D sharp and F sharp, and F sharp seventh
        /// followed by it is V7 falling to vi7, the deceptive cadence of B, not V7 of B
        /// resolving — a seventh chord on the sixth degree holds the tonic triad and is not the
        /// tonic chord. Read as V7/V of E resolving, it named E major for B B7 F sharp 7 G sharp
        /// minor 7, a phrase in B.
        /// </summary>
        private static bool ResolvesTo(int root, ushort chord)
        {
            var resolution = PitchMath.Fold(root + 5);
            return (chord & (1 << resolution)) != 0
                && (chord & (1 << PitchMath.Fold(resolution + 7))) != 0
                && ((chord & (1 << PitchMath.Fold(resolution + 3))) != 0 || (chord & (1 << PitchMath.Fold(resolution + 4))) != 0)
                && (chord & (1 << PitchMath.Fold(resolution - 3))) == 0
                && (chord & (1 << PitchMath.Fold(resolution - 4))) == 0;
        }

        /// <summary>
        /// The root of the applied chord <paramref name="pitchClasses"/> can be — a major triad
        /// and nothing else, or a dominant seventh with whatever sounds with it — or -1. A
        /// dominant seventh is a sonority of its own and names its root through a melody note
        /// struck with it; a plain triad with a stranger in it is not a plain triad. Only those
        /// two chords are applied: a major seventh or an added sixth on a degree rests there.
        /// </summary>
        /// <remarks>
        /// A plain triad is applied as a dominant seventh is: C F G C | G E Am D7 | G C D7 G is
        /// in G from its fifth bar, the E major triad being V/ii. With only dominant sevenths
        /// applied, the G sharp was a foreign bar and G began two bars late, at the Am.
        /// </remarks>
        private static int AppliedRootOf(ushort pitchClasses)
        {
            if (BitOperations.PopCount(pitchClasses) < 3)
                return -1;

            for (var root = 0; root < 12; root++)
            {
                var seventh = DominantSeventh(root);
                if ((pitchClasses & seventh) == seventh)
                    return root;
                var triad = MajorTriad(root);
                if (pitchClasses == triad)
                    return root;
            }

            return -1;
        }

        /// <summary>The pitch classes of the major triad on <paramref name="root"/>.</summary>
        private static ushort MajorTriad(int root) =>
            (ushort)((1 << root) | (1 << PitchMath.Fold(root + 4)) | (1 << PitchMath.Fold(root + 7)));

        /// <summary>The pitch classes of the minor triad on <paramref name="root"/>.</summary>
        private static ushort MinorTriad(int root) =>
            (ushort)((1 << root) | (1 << PitchMath.Fold(root + 3)) | (1 << PitchMath.Fold(root + 7)));

        /// <summary>
        /// The borrowed chord of the major key <paramref name="key"/> that the chord
        /// <paramref name="pitchClasses"/> is — the minor subdominant, the flat sixth or the flat
        /// seventh, whatever of the key's own sounds with it — as the triad's pitch classes, or 0.
        /// </summary>
        private static ushort BorrowedFrom(KeySignature key, ushort pitchClasses)
        {
            if (!key.IsMajor)
                return 0;

            var owned = Owned(key);
            var iv = MinorTriad(PitchMath.Fold(key.Root + 5));
            if ((pitchClasses & iv) == iv && (pitchClasses & ~iv & ~owned) == 0)
                return iv;
            var flatSixth = MajorTriad(PitchMath.Fold(key.Root + 8));
            if ((pitchClasses & flatSixth) == flatSixth && (pitchClasses & ~flatSixth & ~owned) == 0)
                return flatSixth;
            var flatSeventh = MajorTriad(PitchMath.Fold(key.Root + 10));
            if ((pitchClasses & flatSeventh) == flatSeventh && (pitchClasses & ~flatSeventh & ~owned) == 0)
                return flatSeventh;
            return 0;
        }

        /// <summary>
        /// The raised third of the Picardy third that closes the piece in the minor key
        /// <paramref name="key"/> — the chord sonority <paramref name="index"/> belongs to is the
        /// last, and is the key's tonic triad with its third raised — or 0.
        /// </summary>
        private ushort PicardyThird(int index, KeySignature key)
        {
            if (key.IsMajor)
                return 0;

            // A note of the line that is a tone of the chord under it is that chord's: the
            // melody's last note over the closing chord closes the piece on that chord.
            if (_under[index] >= 0)
                index = _under[index];
            if (!EndsThePiece(index))
                return 0;

            var chord = _chordPitchClasses[index];
            var third = (ushort)(1 << PitchMath.Fold(key.Root + 4));
            var tonic = (ushort)((1 << key.Root) | third | (1 << PitchMath.Fold(key.Root + 7)));
            return (chord & ~tonic) == 0 && (chord & (1 << key.Root)) != 0 && (chord & third) != 0 ? third : (ushort)0;
        }

        /// <summary>
        /// Whether the chord sonority <paramref name="index"/> belongs to is the last harmony of
        /// the piece: the last chord, or a chord whose restrikes (<see cref="_restrike"/>) run to
        /// the end — a Picardy third struck twice in its final bar closes the piece as much as one
        /// struck once — with notes of the line sounding while its harmony holds
        /// (<see cref="_harmonyEnd"/>) looked through when they are its own tones or short notes
        /// on their way between them (<see cref="IsOnItsWay"/>): a melody moving through the tones
        /// of the final chord, by step through passing and neighbour notes, is that chord.
        /// </summary>
        /// <remarks>
        /// The last harmony was the last chord struck, and anything struck after it ended the
        /// piece instead. A tune that arpeggiates the closing chord — C sharp, E, A over the A
        /// major chord that closes A minor's piece — struck three notes after the chord, so the
        /// chord was no Picardy third but a chromatic chord, A major owned the close outright,
        /// and the Picardy cadence was a modulation to A major on both roads. An arpeggiated
        /// chord is that chord (<see cref="MarkArpeggios()"/>) in eighths; the closing chord under
        /// a melody in quarters and a half is that chord too. Read for its tones alone, a scale
        /// run E D C sharp B A down to the final note and a turn A B A G sharp A on it each left
        /// the chord no last harmony, and the Picardy cadence was a modulation to A major again.
        /// </remarks>
        private bool EndsThePiece(int index)
        {
            // Asked of every chord in every window it falls in (Owners), and the answer does
            // not change: found once per chord.
            var ends = _endsThePiece ??= new byte[_groupCount];
            var group = _group[index];
            if (ends[group] == 0)
            {
                var chord = _chordPitchClasses[index];
                var next = _groupEnd[index];
                while (next < _sonorities.Length)
                {
                    if (_chordPitchClasses[next] == chord && BitOperations.PopCount(chord) > 1)
                        next = _groupEnd[next];
                    else if (_lineNote[next] && _harmonyEnd[index] > _onset[next]
                        && ((_sonorities[next].PitchClasses & ~chord) == 0 || IsOnItsWay(next)))
                        next++;
                    else
                        break;
                }

                ends[group] = (byte)(next >= _sonorities.Length ? 1 : 2);
            }

            return ends[group] == 1;
        }

        private byte[]? _endsThePiece;

        /// <summary>
        /// The pitch classes sonority <paramref name="index"/> sounds that <paramref name="key"/>
        /// lacks — none for the notes of the key's own material that lies outside its scale: a
        /// chromatic chord of the key (<see cref="ChromaticChordOf"/>), as far as
        /// <paramref name="exempt"/> counts those as the key's — a note of the line that is a
        /// tone of such a chord sounding under it is that chord's — or a non-harmonic tone of
        /// the line (<see cref="IsNonHarmonic(int, KeySignature)"/>).
        /// </summary>
        /// <remarks>
        /// A line note that is a tone of the chord under it is that chord's: the melody's G
        /// sharp quarter over the E major triad of C F G C | G E Am D7 was a note neither the
        /// chord's exemption nor the non-harmonic rule reached — struck after the chord, it was
        /// no appoggiatura, and it is no passing tone — so it weighed a quarter of the bar
        /// against G major, and G began two bars late, at bar 7, with A minor touched on the way.
        /// </remarks>
        private ushort Lacking(int index, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var lacking = LackingOf((ushort)(_sonorities[index].PitchClasses & ~_leaning[index]), index, key, current, exempt);
            return lacking != 0 && IsNonHarmonic(index, key) ? (ushort)0 : lacking;
        }

        /// <summary>As <see cref="Lacking"/>, for the pitch classes <paramref name="pitchClasses"/> of the chord sonority <paramref name="index"/> belongs to — a chord's notes, which no non-harmonic rule reaches.</summary>
        private ushort LackingOf(ushort pitchClasses, int index, KeySignature key, KeySignature? current, Exempt exempt)
        {
            var lacking = OutrightOf(pitchClasses, index, key);
            if (lacking != 0 && exempt != Exempt.Nothing && _maybeChromatic[index])
                lacking &= (ushort)~ChromaticChordOf(_under[index] >= 0 ? _under[index] : index, key, current, exempt);
            return lacking;
        }

        /// <summary>
        /// The notes outside <paramref name="key"/>'s scale that the chord sonority <paramref name="index"/>
        /// belongs to sounds as the key's own chromatic chord — an applied chord, a borrowed
        /// chord or the Picardy third that closes a minor piece — or 0 when the chord is none of
        /// those to the key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An applied chord — a major triad or a dominant seventh resolving down a fifth into a
        /// chord the key owns — is the key's, unless that chord is the tonic of
        /// <paramref name="current"/>, the key the music is in: a dominant resolving into the
        /// tonic is that key's dominant, not another key's applied chord. Am Dm E7 Am | A7 Dm B♭
        /// E7 is A minor with V7/iv and the Neapolitan; with its E7 taken for V7/v of D minor, D
        /// minor owned the second phrase. A borrowed chord — a major key's minor subdominant,
        /// flat sixth or flat seventh — resolving into a chord the key owns is the key's too: C F
        /// G C | G Cm D7 G | G C D7 G is in G from its fifth bar, and with the C minor chord a
        /// foreign bar G began two bars late.
        /// </para>
        /// <para>
        /// A dominant seventh names its own resolution; a plain triad, a borrowed chord, an
        /// augmented sixth or a tonic seventh is heard as the key's only against a key in force
        /// — <paramref name="current"/> given — never at the opening, where no key is yet: G | C
        /// F G C, a pickup on the dominant, opens in C, not in a G major that owns the F chord as
        /// its flat seventh. A key's applied triads and borrowed chords are heard, besides, only
        /// inside a phrase the key frames; that is <see cref="Owns"/>'s rule, which asks for them
        /// with <paramref name="exempt"/> at <see cref="Exempt.Chords"/> only then.
        /// </para>
        /// <para>
        /// A chord the key in force owns is that key's, whatever another key might borrow it as
        /// or apply it to — while the key in force stands (<see cref="Guarded"/>): C F G C A D G,
        /// round and round, is C with a chain of secondary dominants, and G major, which owned
        /// every chord of it — the F its flat seventh, the A its V/V — took the piece to the
        /// dominant. But the resolution chain decides, not the key in force alone: the G of C F
        /// G C | F B♭ G C7 | F B♭ C7 F is V/V of F because it resolves into C7, which is no plain
        /// chord of C's; and the C of C F G C | B♭ E♭ C F | B♭ E♭ F7 B♭ is V/V of B flat because
        /// C major had been left, at the E flat, before its tonic chord came round. Guarded on
        /// ownership alone, F began at bar 8 and B flat at bar 9, each a phrase after a musician
        /// hears it.
        /// </para>
        /// <para>
        /// An augmented sixth — the German sixth on the flat sixth degree, ♭6 1 ♭3 ♯4, the
        /// Italian without the ♭3, the French with 2 for ♭3 — resolving into the dominant or the
        /// tonic in six-four is the key's own chromatic chord, in minor as in major. Enharmonically
        /// a dominant seventh on the flat sixth, it resolves nowhere near a fifth below, so it was
        /// no applied chord and no borrowed one, and Cm Fm G7 Cm | E♭ A♭ B♭ E♭ | Cm A♭7 G7 Cm | Cm
        /// Fm G7 Cm came home at bar 10, on the G7, two bars after the C minor chord that begins
        /// the return.
        /// </para>
        /// <para>
        /// A dominant seventh chord on the key's own tonic, inside a phrase the key frames, is
        /// that tonic coloured — the blues and pop close phrases on I7 — not V7 of the key a
        /// fifth below: the B♭7 closing B♭ E♭ F B♭7 was V7/IV, an applied triad of E flat's, and
        /// C F G C | B♭ E♭ F B♭7 | B♭ E♭ F7 B♭ went to E flat at bar 8 and B flat at bar 9. A
        /// seventh chord that does resolve down a fifth is the dominant it sounds like: A♭7
        /// falling to D♭ minor is V7 of D flat, not A flat's tonic.
        /// </para>
        /// <para>
        /// The Picardy third: a minor key's tonic major triad closing the piece is that key's
        /// final cadence, its raised third owned as its raised seventh already is. Cm Fm G7 Cm |
        /// E♭ A♭ B♭ E♭ | Cm A♭ G7 C is home in C minor from its ninth bar; with the E natural a
        /// foreign bar, C minor never owned the closing phrase, the piece ended in E flat, and the
        /// Picardy chord was a tonicization of C major.
        /// </para>
        /// </remarks>
        private ushort ChromaticChordOf(int index, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var outside = (ushort)(_chordPitchClasses[index] & ~Owned(key));
            if (outside == 0)
                return 0;

            if (Guarded(index, current))
                return 0;

            // What the chord is to the key depends on the chord, the key and how far its
            // chromatic chords count — found once — and on the key in force in two ways asked
            // here: the guard above, and a dominant resolving into the tonic of the key in
            // force, which is that key's dominant and no applied chord.
            var triads = exempt == Exempt.Chords && current is not null;
            var (applied, other) = ChromaticChordParts(index, key, triads);
            if (applied != 0 && current is { } home && IsTonicChord(home, _following[index]))
                applied = 0;

            return (ushort)((applied | other) & outside);
        }

        // For each chord sonority, per key and exemption level, the pitch classes it sounds as
        // the key's applied chord and as its other chromatic chords, found once: over eight
        // thousand random chords each chord was asked in every window that held it, twenty-four
        // keys at a time.
        private ushort[]? _chromaticParts;
        private const ushort Found = 0x8000;

        private (ushort Applied, ushort Other) ChromaticChordParts(int index, KeySignature key, bool triads)
        {
            var start = _groupStart[index];
            var parts = _chromaticParts ??= new ushort[_groupCount * 96];
            var slot = (_group[index] * 96) + ((KeyIndex(key) * 2 + (triads ? 1 : 0)) * 2);
            if ((parts[slot] & Found) == 0)
            {
                var owned = Owned(key);
                var chord = _chordPitchClasses[start];
                var following = _following[start];

                // A chain of applied dominants is the key's: an applied chord resolving into
                // another applied chord of the key — V7/V/V into V7/V — resolves into the key.
                // Resolving only into a chord the key owns outright, A7 D7 G7 C was C's from
                // the D7, and the A7 a foreign bar; C E7 A7 D7 | G7 C C C in eighths, its
                // second bar silent, opened in G major on the trajectory road, the one key that
                // owned C – A7 D7 with a single applied chord, and went to C at its fourth bar.
                var root = _appliedRoot[start];
                var resolvesIntoTheKey = following != 0
                    && ((following & ~owned) == 0 || ((root >= 0 || triads) && ResolvesIntoAnAppliedChordOf(key, start, triads)));
                ushort applied = 0;
                ushort other = 0;

                if (root >= 0 && resolvesIntoTheKey && (triads || (chord & DominantSeventh(root)) == DominantSeventh(root)))
                    applied = DominantSeventh(root);

                if (triads)
                {
                    if (resolvesIntoTheKey)
                        other |= BorrowedFrom(key, chord);
                    other |= AugmentedSixthOf(key, chord, following);
                    if (root < 0)
                        other |= TonicSeventhOf(key, chord);
                }

                other |= PicardyThird(start, key);
                parts[slot] = (ushort)(applied | Found);
                parts[slot + 1] = other;
            }

            return ((ushort)(parts[slot] & ~Found), parts[slot + 1]);
        }

        /// <summary>
        /// Whether the chord sonority <paramref name="index"/> belongs to resolves into a dominant
        /// seventh that is itself an applied chord of <paramref name="key"/> — the next link of a
        /// chain of applied dominants, followed to the chord of the key it ends on — and the chain
        /// lands, past its last seventh, on <paramref name="key"/>'s tonic or its dominant. Only a
        /// seventh carries the chain: a run of plain major triads a fifth apart is a sequence, not
        /// a chain of dominants.
        /// </summary>
        /// <remarks>
        /// A chain of dominants drives to a cadence, and is the key's whose cadence it reaches.
        /// Given to any key that owned a link of it outright, the chain B7 E7 A7 D7 of C F G C |
        /// B7 E7 A7 D7 | G7 C F C | C F G C — sixteen bars that never leave C — was E minor's, which
        /// owns the B7 as its V7 and the D7 as its natural minor's VII7 and needed two chromatic
        /// chords where C needed four, and the piece went to E minor at its fourth bar and came
        /// home at its ninth, on both roads.
        /// </remarks>
        private bool ResolvesIntoAnAppliedChordOf(KeySignature key, int index, bool triads)
        {
            var next = _resolvesAt[_groupStart[index]];
            if (next < 0 || !IsAppliedSeventh(next))
                return false;

            if (ChromaticChordParts(next, key, triads).Applied == 0)
                return false;

            // Where the chain lands: the first chord after it that is no applied dominant seventh.
            var landing = next;
            while (landing >= 0 && IsAppliedSeventh(landing))
                landing = _resolvesAt[_groupStart[landing]];
            if (landing < 0)
                return false;

            var chord = _chordPitchClasses[landing];
            var dominant = MajorTriad(PitchMath.Fold(key.Root + 7));
            var dominantSeventh = DominantSeventh(PitchMath.Fold(key.Root + 7));
            return IsTonicChord(key, chord) || ((chord & dominant) == dominant && (chord & ~dominantSeventh) == 0);
        }

        /// <summary>Whether the chord sonority <paramref name="index"/> belongs to is a dominant seventh resolving down a fifth into a triad — a link a chain of dominants can pass through.</summary>
        private bool IsAppliedSeventh(int index)
        {
            var root = _appliedRoot[index];
            if (root < 0)
                return false;
            var seventh = DominantSeventh(root);
            return (_chordPitchClasses[index] & seventh) == seventh;
        }

        /// <summary>
        /// Whether the chord sonority <paramref name="index"/> belongs to is the key in force's
        /// own and no other key's chromatic chord: <paramref name="current"/> owns it — and, when
        /// the chord is an applied one, a major triad or a dominant seventh resolving down a
        /// fifth, it resolves into a chord <paramref name="current"/> owns outright and the key
        /// still stands at it (<see cref="StandsAt"/>). A borrowed chord the key in force owns is
        /// the key in force's whatever happens: the E major chord of C D E F♯, two bars each, is
        /// no flat seventh of F sharp's while the key it opened in owns it, and the passage, which
        /// has no key, names none.
        /// </summary>
        private bool Guarded(int index, KeySignature? current) =>
            current is { } inForce
            && OwnsChord(inForce, index)
            && (_appliedRoot[index] < 0 || ((_following[index] & ~Owned(inForce)) == 0 && StandsAt(inForce, index)));

        /// <summary>
        /// Whether <paramref name="key"/>, the key in force, still stands at sonority
        /// <paramref name="index"/>: since its tonic chord last sounded before the sonority, it
        /// has owned every sonority — its own chromatic chords counted, as a key in force needs
        /// no frame to own them. Found once per key in force, for every sonority.
        /// </summary>
        private bool StandsAt(KeySignature key, int index)
        {
            _stands ??= new bool[24][];
            var stands = _stands[KeyIndex(key)];
            if (stands is null)
            {
                stands = new bool[_sonorities.Length];
                _stands[KeyIndex(key)] = stands;
                var standing = true;
                var chordStart = -1;
                for (var i = 0; i < _sonorities.Length;)
                {
                    var end = _groupEnd[i];

                    // A chord struck twice in its bar is one chord: the key stands at its second
                    // strike as it stood at its first. Asked afresh, the restrike of the C major
                    // chord in B♭ E♭ C C F F found C major standing again — its tonic had just
                    // sounded — and was C's own chord where the first strike, after the E flat
                    // that left C, was B flat's V/V; B flat owned nothing, and F was named.
                    if (_restrike[i] && chordStart >= 0 && _chordPitchClasses[chordStart] == _chordPitchClasses[i])
                    {
                        for (var k = i; k < end; k++)
                            stands[k] = stands[chordStart];
                        i = end;
                        continue;
                    }

                    if (BitOperations.PopCount(_chordPitchClasses[i]) > 1)
                        chordStart = i;
                    for (var k = i; k < end; k++)
                        stands[k] = standing;

                    // A chord struck as several notes is asked once, by its pitch classes — what
                    // one of its notes lacks, the chord lacks — and its weights against every key
                    // (Weights) already say whether this key lacks it with its chromatic chords
                    // counted, the guard aside: a chord the key owns it does not lack. Only a
                    // chord whose notes end together is weighed whole on its first note
                    // (_weighs); struck with a shorter melody note, its notes weigh one by one,
                    // and are asked one by one — asked by onset alone, the chord was asked by its
                    // lowest note only.
                    var lacks = false;
                    if (_weighs[i] == _chordPitchClasses[i])
                    {
                        var weights = Weights(i);
                        var keyIndex = KeyIndex(key);
                        lacks = weights[keyIndex * 5] != 0
                            && (!_maybeChromatic[i] || weights[(keyIndex * 5) + 2 + (IsTonicChord(key, _following[i]) ? 2 : 0)] != 0);
                    }
                    else
                    {
                        for (var k = i; k < end && !lacks; k++)
                            lacks = Lacking(k, key, key) != 0;
                    }

                    if (lacks)
                        standing = false;
                    else if (IsTonicChordOf(key, i))
                        standing = true;
                    i = end;
                }
            }

            return stands[index];
        }

        /// <summary>
        /// The pitch classes of the augmented sixth chord of <paramref name="key"/> that
        /// <paramref name="chord"/> is — on the flat sixth degree, with the tonic and the raised
        /// fourth, and the flat third or the second besides — resolving into the dominant, plain
        /// or with its seventh, or into the tonic triad; or 0.
        /// </summary>
        private static ushort AugmentedSixthOf(KeySignature key, ushort chord, ushort following)
        {
            var root = key.Root;
            var flatSixth = (ushort)(1 << PitchMath.Fold(root + 8));
            var tonic = (ushort)(1 << root);
            var raisedFourth = (ushort)(1 << PitchMath.Fold(root + 6));
            var italian = (ushort)(flatSixth | tonic | raisedFourth);
            var german = (ushort)(italian | (1 << PitchMath.Fold(root + 3)));
            var french = (ushort)(italian | (1 << PitchMath.Fold(root + 2)));
            if ((chord & italian) != italian || (chord & ~german & ~french) != 0)
                return 0;

            var dominant = (ushort)(1 << PitchMath.Fold(root + 7));
            var dominantSeventh = DominantSeventh(PitchMath.Fold(root + 7));
            var resolves = following != 0
                && (following & dominant) != 0
                && ((following & ~dominantSeventh) == 0 || (following & ~TonicTriad(key)) == 0);
            return resolves ? (ushort)(chord & (german | french)) : (ushort)0;
        }

        /// <summary>
        /// The minor seventh of a dominant seventh chord on <paramref name="key"/>'s tonic —
        /// the tonic triad coloured, I7 — when <paramref name="chord"/> is that chord; or 0.
        /// </summary>
        private static ushort TonicSeventhOf(KeySignature key, ushort chord)
        {
            if (!key.IsMajor)
                return 0;

            var seventh = DominantSeventh(key.Root);
            return (chord & seventh) == seventh ? (ushort)(1 << PitchMath.Fold(key.Root + 10)) : (ushort)0;
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns outright every note of the chord sonority
        /// <paramref name="index"/> belongs to — for a minor key, its raised seventh only in a
        /// chord of the dominant's, as <see cref="Outright"/> reads it.
        /// </summary>
        private bool OwnsChord(KeySignature key, int index)
        {
            var chord = _chordPitchClasses[index];
            if ((chord & ~Owned(key)) != 0)
                return false;
            if (key.IsMajor)
                return true;

            var leadingTone = 1 << PitchMath.Fold(key.Root + 11);
            return (chord & leadingTone) == 0 || (chord & ~DominantChords(key)) == 0 || (chord & TonicTriad(key)) == TonicTriad(key);
        }

        /// <summary>
        /// The pitch classes sonority <paramref name="index"/> sounds that <paramref name="key"/>
        /// does not own outright: the notes outside its scale, and for a minor key its raised
        /// seventh wherever the chord it sounds in is not one of the dominant's — V, V7, the
        /// leading-tone seventh — nor holds the tonic triad, nor is the note alone. A minor key
        /// raises its seventh to make its dominant major; it does not own the note as a free
        /// pitch class of every chord. C E♭ G is no chord of E minor, and with the E flat taken
        /// for D sharp, E minor owned G Cm D7 G outright while G needed the borrowed chord, and
        /// was named for it. But the leading tone struck over the tonic chord — B natural on the
        /// downbeat over C minor, resolving to C — is the tonic chord with an appoggiatura, not a
        /// chord the key lacks: read as one, four bars of C minor with that cadence in them were
        /// no key at all. A note struck with the chord that leans on it (<see cref="LeaningNoteIn"/>)
        /// is no note of the chord's and weighs nothing here, whatever the key.
        /// </summary>
        private ushort Outright(int index, KeySignature key) =>
            OutrightOf((ushort)(_sonorities[index].PitchClasses & ~_leaning[index]), index, key);

        /// <summary>As <see cref="Outright"/>, for the pitch classes <paramref name="pitchClasses"/> of the chord sonority <paramref name="index"/> belongs to.</summary>
        private ushort OutrightOf(ushort pitchClasses, int index, KeySignature key)
        {
            var lacking = (ushort)(pitchClasses & ~Owned(key));
            if (key.IsMajor)
                return lacking;

            var leadingTone = (ushort)(1 << PitchMath.Fold(key.Root + 11));
            var chord = _chordPitchClasses[index];
            if ((chord & leadingTone) != 0 && (chord & ~DominantChords(key)) != 0 && (chord & TonicTriad(key)) != TonicTriad(key))
                lacking |= (ushort)(pitchClasses & leadingTone);
            return lacking;
        }

        /// <summary>
        /// The pitch classes of a minor key's dominant chords — the dominant triad and seventh
        /// and the diminished seventh on the raised leading tone: degrees 5, 7 (raised), 2, 4 and
        /// flat 6.
        /// </summary>
        private static ushort DominantChords(KeySignature key) => DominantChordsByRoot[key.Root];

        private static readonly ushort[] DominantChordsByRoot = BuildDominantChords();

        private static ushort[] BuildDominantChords()
        {
            var chords = new ushort[12];
            for (var root = 0; root < 12; root++)
            {
                chords[root] = (ushort)((1 << PitchMath.Fold(root + 7))
                    | (1 << PitchMath.Fold(root + 11))
                    | (1 << PitchMath.Fold(root + 2))
                    | (1 << PitchMath.Fold(root + 5))
                    | (1 << PitchMath.Fold(root + 8)));
            }

            return chords;
        }

        /// <summary>
        /// Whether sonority <paramref name="index"/>, a note <paramref name="key"/> lacks, is a
        /// non-harmonic tone of the key — a passing tone, a neighbour tone or an appoggiatura —
        /// and so weighs nothing toward what the key lacks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A chromatic passing tone fills the whole step between two notes of the scale: it lies
        /// a semitone from the note either side of it and keeps the line's direction. A
        /// chromatic neighbour tone leaves a note of the scale by a semitone and returns to it.
        /// Both are notes on their way (<see cref="IsOnItsWay"/>) — short, approached and left by
        /// step by notes no shorter than themselves — and neither falls on the downbeat of its
        /// bar, where a note is a note of the line. A run of notes on their way — a scale, a
        /// chromatic run — is passing motion when it moves one way, from a structural note the
        /// key owns to another, within a whole note; the chromatic notes in it are the passing
        /// tones: B flat between B and A, F between E and F sharp. A note where the line turns
        /// — the top or the bottom of an arch — is a structural note, whatever came before it:
        /// the A flat of G A A♭ G passes from the A down to the G, and the A, though approached
        /// by step, is where it sets out. A chromatic scale has no structural note to set out
        /// from or land on within a bar — every note in it is on its way, and none of them turns
        /// — so nothing in it passes, and the scale is what it always was to the judge: no
        /// key's. A note that leans on a harmony — a note of the line sounding against a chord
        /// that began before it, no tone of that chord, resolving by a semitone into one of its
        /// tones while the chord still sounds, and sounding once in its bar — is an
        /// appoggiatura, an accented passing tone or a suspension, whatever its length and
        /// however approached: it is defined by its resolution. A leaning note needs a chord to
        /// lean on, so in a melody alone a chromatic note approached by leap is a note of the
        /// line, and a note that sounds again in its bar is a tone of the bar's harmony. (A note
        /// struck with the chord and leaning on it is the chord's business, not the line's:
        /// <see cref="LeaningNoteIn"/>.)
        /// </para>
        /// <para>
        /// The run once had to move one way through every note on its way, so the A flat of G A
        /// A♭ G — a passing tone between two owned notes a step apart — was a foreign note
        /// because the A had been approached from below, and a melody with one such arch in
        /// each of its new key's first two bars named no key. And a passing tone once had to be
        /// no longer than a quarter: a half-note C sharp between C and D over a D7 was half its
        /// bar against G major, and killed the modulation it should at most have delayed. Its
        /// length is bounded by the chord it leans on, which must still sound when it resolves.
        /// </para>
        /// <para>
        /// Steps are read in pitch where the road has the note — a seventh is a leap — and in
        /// pitch class where it has only the chord: the detector folds a melody's downbeat note
        /// into the chord it strikes with, and the eighth after it is approached from that
        /// chord's tones.
        /// </para>
        /// <para>
        /// Weighed by duration alone, two chromatic passing eighths in a bar were a quarter of it
        /// and a melody with two in every bar of its new key named no key at all; a chromatic
        /// quarter-note neighbour under a D7 was a quarter of its bar and the trajectory road,
        /// which reads the melody, placed G four bars late while the detector, which never saw
        /// the eighths, placed it at the bar; and a passing F natural in E F F sharp G was the
        /// last note G major did not own, so G began three eighths into its bar. A passing tone
        /// is not a foreign note. The rules above are each the price of a looser reading,
        /// measured: read without direction, G F sharp A flat was a passing tone; read with steps
        /// of a tone, the F of a C-major scale's E F G was a passing tone in G major, and the
        /// scale's last bar became G's pivot bar; read on the downbeat, the F of a C-major
        /// melody's E F E sent it to G; read without the run's anchors, a chromatic scale in
        /// eighths was owned by every key and named one. Over two hundred random chromatic
        /// melodies, which have no key, the detector's modulations rose from 16 to 40 and the
        /// trajectory's from 3 to 15 with the first of those readings, and stand at 21 and 6 with
        /// the rules.
        /// </para>
        /// </remarks>
        private bool IsNonHarmonic(int index, KeySignature key) => (NonHarmonicKeys(index) >> KeyIndex(key) & 1) != 0;

        /// <summary>The keys, one bit each by their index, that single note <paramref name="index"/> is a non-harmonic tone of; 0 for a chord or a note longer than a quarter.</summary>
        private uint NonHarmonicKeys(int index)
        {
            if (!IsLineNote(index))
                return 0;

            _nonHarmonic ??= new uint[_sonorities.Length];
            var keys = _nonHarmonic[index];
            if ((keys & Asked) == 0)
            {
                keys = Asked;
                var lacking = KeysLacking[BitOperations.TrailingZeroCount(_sonorities[index].PitchClasses)];
                if (LeansOnHarmony(index))
                {
                    keys |= lacking;
                }
                else if (IsShort(index) && IsOnItsWay(index))
                {
                    for (var k = 0; k < 24; k++)
                    {
                        if ((lacking & (1u << k)) != 0 && IsPassingOrNeighbour(index, OwnedByKey[k]))
                            keys |= 1u << k;
                    }
                }

                _nonHarmonic[index] = keys;
            }

            return keys & ~Asked;
        }

        /// <summary>
        /// Whether the note on its way <paramref name="index"/> is a passing or a neighbour tone
        /// between notes <paramref name="owned"/> holds.
        /// </summary>
        private bool IsPassingOrNeighbour(int index, ushort owned)
        {
            var before = Before(index);
            var after = After(index);
            if (!before.Found || !after.Found)
                return false;

            // A passing or neighbour tone is unaccented: on the downbeat of a bar, a note is a
            // note of the line.
            if (IsDownbeat(_sonorities[index].Onset))
                return false;

            // Between two notes the key owns: back to the note it left — a neighbour — or on in
            // the same direction. G F sharp A flat is neither.
            var steps = before.StepsFrom(_sonorities[index], owned, forward: false, semitone: true);
            var resolutions = after.StepsFrom(_sonorities[index], owned, forward: true, semitone: true);
            if (!steps.Any || !resolutions.Any)
                return false;
            if (steps.Returns(resolutions))
                return true;

            // A passing tone is on a scale: the run it is on moves one way, from a structural
            // note the key owns to another, within a whole note. B B flat D flat C B flat is a
            // figure, not a scale, and its C passes nothing.
            var run = RunOf(index);
            var up = run.Rises && steps.Up && resolutions.Up;
            var down = run.Falls && steps.Down && resolutions.Down;
            if ((!up && !down) || !run.WithinAWholeNote)
                return false;

            var setsOut = Before(run.First).StepsFrom(_sonorities[run.First], owned, forward: false);
            var lands = After(run.Last).StepsFrom(_sonorities[run.Last], owned, forward: true);
            return (up && setsOut.Up && lands.Up) || (down && setsOut.Down && lands.Down);
        }

        /// <summary>
        /// Whether the note of the line <paramref name="index"/> leans on the harmony holding
        /// under it — a chord of two notes or more that began before it or with it, its harmony
        /// still holding (<see cref="_harmonyEnd"/>), or one struck under it within an eighth of
        /// its onset (<see cref="ChordArrivingUnder"/>): it is no tone of that chord, resolves by
        /// a semitone into one of its tones while the harmony still holds, and sounds once in its
        /// bar. An appoggiatura, an accented passing tone or a suspension, whatever its length
        /// and however approached. Decided once per note; it does not depend on the key.
        /// </summary>
        /// <remarks>
        /// The chord had to be sounding — its notes not yet stopped — under the note and at its
        /// resolution. A staccato D7, a quarter and then silence, was gone when the melody's C
        /// sharp came on the next beat, so the C sharp leaned on nothing and was a foreign
        /// quarter; G began four bars late on the trajectory road, and on the detector's too
        /// once its chords stopped where their notes stop. The harmony of a released chord holds
        /// until the next chord.
        /// </remarks>
        private bool LeansOnHarmony(int index)
        {
            if (!_anyChord)
                return false;

            _leans ??= new byte[_sonorities.Length];
            if (_leans[index] == 0)
            {
                var note = _sonorities[index];
                var under = SoundingUnder(index);

                // A chord struck under the note within an eighth of its onset — the
                // accompaniment late behind a melody on the beat — is the harmony the note leans
                // on as much as one struck before it: the note anticipates its chord, and
                // resolves against it. Heard only against a chord already sounding, the
                // appoggiatura on every downbeat of G's phrase was a foreign quarter when the
                // chords came an eighth late, and G was not named on either road.
                var arriving = ChordArrivingUnder(index);
                var leans = false;
                foreach (var harmony in (ReadOnlySpan<ushort>)[under, arriving])
                {
                    if (BitOperations.PopCount(harmony) < 2 || (harmony & note.PitchClasses) != 0)
                        continue;

                    var resolution = After(index).StepsFrom(note, 0x0FFF, forward: true, semitone: true);
                    leans = (resolution.PitchClasses & harmony) != 0
                        && (HarmonyAt(note.End) & harmony) == harmony
                        && !SoundsAgainInBar(index, index + 1, note.PitchClasses);
                    if (leans)
                        break;
                }

                _leans[index] = (byte)(leans ? 1 : 2);
            }

            return _leans[index] == 1;
        }

        /// <summary>
        /// Whether the single note <paramref name="index"/> is a note on its way — short, and
        /// approached and left by step by notes no shorter than itself — rather than a
        /// structural note: one that is longer than a quarter, sounds in a chord, or is
        /// approached or left by a leap, a repetition, a rest or a note shorter than itself.
        /// Decided once per note; it does not depend on the key.
        /// </summary>
        private bool IsOnItsWay(int index)
        {
            if (!IsLineNote(index) || !IsShort(index))
                return false;

            _onItsWay ??= new byte[_sonorities.Length];
            if (_onItsWay[index] == 0)
            {
                var note = _sonorities[index];
                var before = Before(index);
                var after = After(index);
                var onItsWay = before.Found && after.Found
                    && before.NoShorter && after.NoShorter
                    && before.StepsFrom(note, 0x0FFF, forward: false).Any
                    && after.StepsFrom(note, 0x0FFF, forward: true).Any;
                _onItsWay[index] = (byte)(onItsWay ? 1 : 2);
            }

            return _onItsWay[index] == 1;
        }

        /// <summary>Whether sonority <paramref name="index"/> is a single note of the line: it has a pitch and nothing else begins with it.</summary>
        private bool IsLineNote(int index) => _lineNote[index];

        /// <summary>
        /// What sounds next to a note of the line: the single note of the line ending where it
        /// begins or beginning where it ends — in several voices, the nearest in pitch — or,
        /// when no note does, the harmony sounding into it or the chord struck where it ends.
        /// </summary>
        /// <param name="Index">The neighbouring note's sonority, or -1 for a harmony.</param>
        /// <param name="Pitch">The neighbouring note's pitch, or -1 for a harmony.</param>
        /// <param name="PitchClasses">The pitch classes sounding there.</param>
        /// <param name="NoShorter">Whether the neighbour lasts at least as long as the note; a harmony always does.</param>
        private readonly record struct Neighbour(int Index, int Pitch, ushort PitchClasses, bool NoShorter)
        {
            public static readonly Neighbour None = new(-1, -1, 0, false);

            // default(Neighbour) — Index 0 and no pitch classes — marks a neighbour not yet
            // found; None, with Index -1, marks one looked for and absent.

            public bool Found => PitchClasses != 0;

            /// <summary>
            /// The steps — a semitone or a tone, up or down; a semitone only when
            /// <paramref name="semitone"/> — from <paramref name="note"/> to this neighbour's
            /// notes that <paramref name="owned"/> holds: in pitch for a note, in pitch class for
            /// a harmony. <paramref name="forward"/> reads the interval from the note to the
            /// neighbour; otherwise from the neighbour to the note, so that a passing tone's two
            /// steps point the same way.
            /// </summary>
            public Steps StepsFrom(Sonority note, ushort owned, bool forward, bool semitone = false)
            {
                var reach = semitone ? 1 : 2;
                if (Pitch >= 0)
                {
                    if (!Contains(owned, Pitch) || !IsStep(Pitch, note.Pitch) || Math.Abs(Pitch - note.Pitch) > reach)
                        return default;
                    var interval = forward ? Pitch - note.Pitch : note.Pitch - Pitch;
                    return new Steps(Steps.Bit(interval), (ushort)(1 << PitchMath.Fold(Pitch)));
                }

                // A harmony holding the note's own pitch class is the note repeated — the
                // melody's D running into the D7 it strikes with — not a step to another of
                // its tones.
                var from = PitchMath.Fold(note.Pitch);
                if ((PitchClasses & (1 << from)) != 0)
                    return default;

                byte intervals = 0;
                ushort pitchClasses = 0;
                for (var interval = -reach; interval <= reach; interval++)
                {
                    if (interval == 0)
                        continue;
                    var pc = PitchMath.Fold(from + interval);
                    if ((PitchClasses & owned & (1 << pc)) != 0)
                    {
                        intervals |= Steps.Bit(forward ? interval : -interval);
                        pitchClasses |= (ushort)(1 << pc);
                    }
                }

                return new Steps(intervals, pitchClasses);
            }
        }

        /// <summary>
        /// The steps from a note to a neighbour, as a set of signed intervals (-2, -1, 1, 2)
        /// and the pitch classes stepped to.
        /// </summary>
        private readonly record struct Steps(byte Intervals, ushort PitchClasses)
        {
            public bool Any => Intervals != 0;

            /// <summary>Whether a step here rises.</summary>
            public bool Up => (Intervals & 0b1100) != 0;

            /// <summary>Whether a step here falls.</summary>
            public bool Down => (Intervals & 0b0011) != 0;

            public static byte Bit(int interval) => interval switch { -2 => 1, -1 => 2, 1 => 4, 2 => 8, _ => 0 };

            /// <summary>Whether the note returns to the pitch class it left: a neighbour tone.</summary>
            public bool Returns(Steps resolutions) => (PitchClasses & resolutions.PitchClasses) != 0;
        }

        /// <summary>
        /// A run of notes on their way: its first and last note, whether every step in it rises
        /// or every step falls, and whether it fits within a whole note. A note not on its way
        /// is a run of one.
        /// </summary>
        private readonly record struct Run(int First, int Last, bool Rises, bool Falls, bool WithinAWholeNote);

        /// <summary>
        /// The run note <paramref name="index"/> is on, found once: the notes on their way
        /// either side of it that keep moving the way it moves. A note where the line turns is
        /// not on the run but where it sets out or lands — the A of G A A♭ G is the top of an
        /// arch, and the A flat passes from it. A note that itself turns, or whose neighbours
        /// are harmonies without a pitch to read a direction from, is a run of one, and its own
        /// two steps decide.
        /// </summary>
        private Run RunOf(int index)
        {
            _run ??= new Run[_sonorities.Length];
            if (_run[index].First == 0 && _run[index].Last == 0 && !_run[index].Rises)
            {
                var note = _sonorities[index];
                var before = Before(index);
                var after = After(index);
                var direction = before.Pitch >= 0 ? Math.Sign(note.Pitch - before.Pitch) : 0;
                var lone = false;
                if (after.Pitch >= 0)
                {
                    var onward = Math.Sign(after.Pitch - note.Pitch);
                    if (direction == 0)
                        direction = onward;
                    else if (onward != direction)
                        lone = true;
                }

                lone |= direction == 0;
                var first = index;
                var last = index;
                if (!lone)
                {
                    while (Before(first) is { Pitch: >= 0 } previous
                        && IsOnItsWay(previous.Index)
                        && Math.Sign(_sonorities[first].Pitch - previous.Pitch) == direction)
                    {
                        var beforePrevious = Before(previous.Index);
                        if (beforePrevious.Pitch >= 0 && Math.Sign(previous.Pitch - beforePrevious.Pitch) != direction)
                            break;
                        first = previous.Index;
                    }

                    while (After(last) is { Pitch: >= 0 } following
                        && IsOnItsWay(following.Index)
                        && Math.Sign(following.Pitch - _sonorities[last].Pitch) == direction)
                    {
                        var afterFollowing = After(following.Index);
                        if (afterFollowing.Pitch >= 0 && Math.Sign(afterFollowing.Pitch - following.Pitch) != direction)
                            break;
                        last = following.Index;
                    }
                }

                _run[index] = new Run(first, last, lone || direction > 0, lone || direction < 0, _sonorities[last].End - _sonorities[first].Onset <= Rational.Whole);
            }

            return _run[index];
        }

        private Neighbour Before(int index)
        {
            _before ??= new Neighbour[_sonorities.Length];
            if (_before[index].Index == 0 && _before[index].PitchClasses == 0)
            {
                var note = _sonorities[index];
                var i = LineNoteEndingAt(note.Onset, note.Pitch);
                if (i >= 0)
                {
                    _before[index] = new Neighbour(i, _sonorities[i].Pitch, _sonorities[i].PitchClasses, _sonorities[i].End - _sonorities[i].Onset >= note.End - note.Onset);
                }
                else
                {
                    ushort sounding = 0;
                    foreach (var k in ReachingHarmony(note.Onset))
                    {
                        if (_sonorities[k].Onset >= note.Onset)
                            break;
                        if (_harmonyEnd[k] > _onset[index])
                            sounding |= _sonorities[k].PitchClasses;
                    }

                    _before[index] = sounding == 0 ? Neighbour.None : new Neighbour(-1, -1, sounding, true);
                }
            }

            return _before[index];
        }

        private Neighbour After(int index)
        {
            _after ??= new Neighbour[_sonorities.Length];
            if (_after[index].Index == 0 && _after[index].PitchClasses == 0)
            {
                var note = _sonorities[index];
                var i = LineNoteBeginningAt(note.End, note.Pitch);
                if (i >= 0)
                {
                    _after[index] = new Neighbour(i, _sonorities[i].Pitch, _sonorities[i].PitchClasses, _sonorities[i].End - _sonorities[i].Onset >= note.End - note.Onset);
                }
                else
                {
                    var k = FirstIndexAt(note.End);
                    _after[index] = k < _sonorities.Length && _sonorities[k].Onset == note.End
                        ? new Neighbour(-1, -1, _chordPitchClasses[k], true)
                        : Neighbour.None;
                }
            }

            return _after[index];
        }

        /// <summary>Whether <paramref name="pitchClasses"/> holds the pitch class of <paramref name="pitch"/>.</summary>
        private static bool Contains(ushort pitchClasses, int pitch) => (pitchClasses & (1 << PitchMath.Fold(pitch))) != 0;

        /// <summary>Whether two pitches are a step apart: a semitone or a tone.</summary>
        private static bool IsStep(int a, int b) => a != b && Math.Abs(a - b) <= 2;

        /// <summary>
        /// The single note of the line beginning at <paramref name="position"/> nearest in pitch
        /// to <paramref name="pitch"/>, or -1.
        /// </summary>
        private int LineNoteBeginningAt(Rational position, int pitch)
        {
            var nearest = -1;
            for (var i = FirstIndexAt(position); i < _sonorities.Length && _sonorities[i].Onset == position; i++)
            {
                if (_sonorities[i].Pitch >= 0 && (nearest < 0 || Math.Abs(_sonorities[i].Pitch - pitch) < Math.Abs(_sonorities[nearest].Pitch - pitch)))
                    nearest = i;
            }

            return nearest;
        }

        /// <summary>
        /// The single note of the line ending at <paramref name="position"/> nearest in pitch to
        /// <paramref name="pitch"/>, or -1.
        /// </summary>
        private int LineNoteEndingAt(Rational position, int pitch)
        {
            var nearest = -1;
            foreach (var i in Reaching(position))
            {
                if (_sonorities[i].Onset >= position)
                    break;
                if (_sonorities[i].End == position && _sonorities[i].Pitch >= 0
                    && (nearest < 0 || Math.Abs(_sonorities[i].Pitch - pitch) < Math.Abs(_sonorities[nearest].Pitch - pitch)))
                {
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The pitch classes of the chords whose harmony holds under sonority <paramref name="index"/>:
        /// the sonorities that began before it, in a group of two or more, and whose harmony
        /// still holds when it begins (<see cref="_harmonyEnd"/>).
        /// </summary>
        /// <summary>
        /// The pitch classes of the first chord of two notes or more struck after the note of the
        /// line <paramref name="index"/> begins, within an eighth of its onset and before it
        /// stops: the accompaniment arriving late under a melody on the beat. 0 when none does.
        /// </summary>
        private ushort ChordArrivingUnder(int index)
        {
            var onset = _sonorities[index].Onset;
            var within = onset + Rational.Eighth;
            var stop = _sonorities[index].End;
            for (var k = FirstIndexAt(onset); k < _sonorities.Length; k++)
            {
                var at = _sonorities[k].Onset;
                if (at > within || at >= stop)
                    break;
                if (at > onset && BitOperations.PopCount(_chordPitchClasses[k]) > 1)
                    return _chordPitchClasses[k];
            }

            return 0;
        }

        private ushort SoundingUnder(int index)
        {
            var onset = _sonorities[index].Onset;
            var onsetD = _onset[index];
            ushort under = 0;
            foreach (var k in ReachingHarmony(onset))
            {
                // A note of the line struck with a chord (_overChord) has that chord under it.
                if (_sonorities[k].Onset > onset)
                    break;
                if (_sonorities[k].Onset == onset && (!_overChord[index] || _overChord[k]))
                    continue;
                if (_harmonyEnd[k] > onsetD && BitOperations.PopCount(_chordPitchClasses[k]) > 1)
                    under |= _sonorities[k].PitchClasses;
            }

            return under;
        }

        /// <summary>Whether sonority <paramref name="index"/> lasts no longer than a quarter note.</summary>
        private bool IsShort(int index) => _short[index];

        /// <summary>
        /// Whether a sonority beginning in the whole note of sonority <paramref name="start"/>,
        /// outside the sonorities [<paramref name="start"/>, <paramref name="end"/>), sounds a
        /// pitch class in <paramref name="pitchClasses"/>.
        /// </summary>
        private bool SoundsAgainInBar(int start, int end, ushort pitchClasses)
        {
            var bar = BarStart(_sonorities[start].Onset);
            var to = bar + Rational.Whole;
            for (var k = FirstIndexAt(bar); k < _sonorities.Length && _sonorities[k].Onset < to; k++)
            {
                if ((k < start || k >= end) && (_sonorities[k].PitchClasses & pitchClasses) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a sonority in [<paramref name="from"/>, <paramref name="to"/>) sounds a pitch
        /// class in <paramref name="pitchClasses"/> as a note of <paramref name="key"/>'s own — in
        /// a sonority the key owns, and not as the Picardy third of <paramref name="current"/>,
        /// which is that key's cadence and no other key's note.
        /// </summary>
        public bool Sounds(ushort pitchClasses, Rational from, Rational to, KeySignature key, KeySignature current)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & ~PicardyThird(i, current) & pitchClasses) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The key the opening phrase is in: the key of the chord the piece opens on, when that
        /// key owns as much of the phrase as any key does (<paramref name="byChord"/>); else the
        /// key the phrase sounds like on the profile. When neither decides, the opening is
        /// extended by a phrase at a time, and the key the whole sounds like is the last resort.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A piece opens on its tonic, as a rule, and a musician takes the first chord for the
        /// tonic until the phrase says otherwise. The profile alone does not: it is weighted to
        /// the root and the fifth, so C G C G reads as G major by a margin of 0.24, and C Am D7 G
        /// | C A7 Dm G7 ties C and G over eight bars and sixteen, and the whole-piece fallback
        /// opened the verse in G — from which the trajectory reported a modulation to C at bar 7
        /// of a piece that never left it. The first chord decides among the keys that own the
        /// phrase, or leave the least of it foreign, an applied dominant counted as the key's:
        /// C G C G opens in C; C D7 G C, which G owns entire and C owns through its V7/V, opens
        /// in C; and the verse, whose first phrase G owns entire but whose first two phrases C
        /// leaves less of foreign than G does, opens in C once the phrase is extended.
        /// </para>
        /// <para>
        /// Ownership without the opening chord would name the wrong key: a change is judged
        /// against the key the music is in, and a key that has to call a recurring chord tone
        /// foreign loses to one that owns every note; the opening has nothing to be judged
        /// against. Ownership and the profile disagree on the blues: its tonic is a dominant
        /// seventh chord, which no major key owns — F major owns C7 — and by ownership alone a
        /// twelve-bar blues in C began in F and modulated home at its V7. F does not open on a
        /// C7, so the blues falls to the profile, which hears it in C. A note opens no key: a
        /// melody in C may begin on its sixth degree, and taken for the tonic of A minor it did.
        /// </para>
        /// </remarks>
        public KeySignature OpeningKey(Rational phrase, out bool byChord)
        {
            // An opening that cannot decide is extended as a change's hold is, a phrase at a
            // time: a piece that modulates sounds, as a whole, like a key it was never in at
            // the start, and read from that key the opening came back as a modulation at its
            // first note.
            byChord = false;
            var to = phrase < End ? phrase : End;
            var opening = Profile(Rational.Zero, to);
            while (true)
            {
                if (opening.Result.IsDecidable)
                {
                    if (OpensIn(opening.Result, to) is { } key)
                    {
                        byChord = true;
                        return key;
                    }

                    if (opening.Result.Confidence >= MinDecisive)
                        return opening.Result.Key;
                }

                if (to >= End)
                    break;

                to = to + phrase < End ? to + phrase : End;
                opening = Profile(Rational.Zero, to);
            }

            var whole = Profile(Rational.Zero, End);
            return whole.Result.IsDecidable ? whole.Result.Key : opening.Result.Key;
        }

        /// <summary>
        /// The key whose tonic chord the piece opens on — the profile's favourite among them
        /// when several qualify — provided it owns [0, <paramref name="to"/>) or leaves as
        /// little of it foreign as any key does, an applied dominant seventh counted as the
        /// key's. Failing that, when the piece opens on a triad and exactly one major key owns
        /// the phrase outright, that key: G C F G opens in C, on its dominant.
        /// <see langword="null"/> when the piece opens on a single note, or when nothing above
        /// decides.
        /// </summary>
        /// <remarks>
        /// The owner fallback is a major key or nothing: pitch classes cannot tell A flat from G
        /// sharp, so A minor, which owns its raised leading tone, owns C F Fm C outright — the
        /// borrowed iv's A flat taken for G sharp — and by ownership a phrase in C with a
        /// borrowed chord opened in A minor. And it is not for a dominant seventh: the blues
        /// opens on one, and F major owns C7. The profile decides those. No key is in force at
        /// the opening to apply a plain triad to or to borrow against, so here only a dominant
        /// seventh is a key's beyond its scale (<see cref="ChromaticChordOf"/>): G | C F G C, a
        /// pickup on the dominant, opens in C, not in a G major that owns the F chord as its
        /// flat seventh.
        /// </remarks>
        private KeySignature? OpensIn(KeyDetectionResult reading, Rational to)
        {
            // The chord the piece opens on: struck, or arpeggiated in quick even notes
            // (MarkArpeggios) — not a melody's bar that happens to outline a triad in quarters,
            // for a tune in C may begin on A C E — and under a pickup note or an eighth late.
            var opening = OpeningChord();
            if (opening < 0)
                return null;
            var chord = _chordPitchClasses[opening];

            const float tolerance = 1e-4f;
            Span<float> foreign = stackalloc float[24];
            var least = float.MaxValue;
            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                foreign[i] = Foreign(Rational.Zero, to, reading.AllCorrelations[i].Key, current: null, Exempt.Chords);
                if (foreign[i] < least)
                    least = foreign[i];
            }

            // The key whose tonic chord opens the piece, when it owns the phrase (Owns: a stray
            // note a bar is allowed) or leaves as little of it foreign as any key does. Owning it
            // is enough: a chromatic escape eighth in the second bar of C F G7 C is no reason to
            // hear the phrase in F major, which owns the eighth but must take the cadence's G7
            // for its V7/V — and did, once a melody note over the G7 counted as that chord's, so
            // that F left nothing foreign and C an eighth. Opened in F, the piece reached G as
            // a tonicization and the trajectory road heard no modulation at all.
            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                var candidate = reading.AllCorrelations[i].Key;
                if (IsTonicChord(candidate, chord) && (foreign[i] <= least + tolerance || Owns(Rational.Zero, to, candidate, current: null)))
                    return candidate;
            }

            if (BitOperations.PopCount(chord) > 3 || least > tolerance)
                return null;

            KeySignature? owner = null;
            for (var i = 0; i < reading.AllCorrelations.Length && i < 24; i++)
            {
                var candidate = reading.AllCorrelations[i].Key;
                if (!candidate.IsMajor || foreign[i] > tolerance)
                    continue;
                if (owner is not null)
                    return null;
                owner = candidate;
            }

            return owner;
        }

        /// <summary>
        /// The first sonority of the chord the piece opens on — the first chord of two pitch
        /// classes or more, struck or arpeggiated, that begins within a quarter of the first
        /// note — or -1 when the piece opens on a line alone.
        /// </summary>
        /// <remarks>
        /// The opening chord had to be struck at the very first onset. A melody's pickup note
        /// an eighth before the first chord, or the chords all an eighth late under a melody on
        /// the beat, left the piece opening on a lone note: the profile guessed, and a passage
        /// to the dominant over a dominant pedal, its chords an eighth late, opened in G on the
        /// trajectory road and heard no modulation, while the detector, told C, heard G at its
        /// fifth bar. And an arpeggiated opening chord was so many notes: with its second bar
        /// silent, C F G C | G E Am D7 | G C D7 G in eighths read C – G C as G on the profile.
        /// </remarks>
        private int OpeningChord()
        {
            var within = _onset[0] + 0.25;
            for (var i = 0; i < _sonorities.Length && _onset[i] <= within; i = _groupEnd[i])
            {
                if (BitOperations.PopCount(_chordPitchClasses[i]) >= 2)
                    return i;
            }

            return -1;
        }

        /// <summary>Whether the chord the piece opens on (<see cref="OpeningChord"/>) is <paramref name="key"/>'s tonic chord, with or without a note above it.</summary>
        public bool OpensOnTheTonicOf(KeySignature key)
        {
            var opening = OpeningChord();
            return opening >= 0 && IsTonicChordOf(key, opening);
        }

        /// <summary>
        /// Which of the keys in <paramref name="reading"/> own [<paramref name="from"/>,
        /// <paramref name="to"/>) as well as any key does: the keys that leave the least of it
        /// foreign — their applied and borrowed chords, Picardy third and non-harmonic tones
        /// counted as their own — and, among those, the keys that need the fewest chromatic
        /// chords to do it. A key that owns a phrase outright is a better owner than one that
        /// owns it through chromatic chords: C F G C is I IV V I in C, not IV ♭VII I IV in G, and
        /// C G C F is I V I IV in C, not V/V V I IV in F. Every key is weighed in one pass over
        /// the span: weighed one key at a time, twice, a piece of four thousand chords over a
        /// two-thousand-bar pedal took twenty times as long to judge.
        /// </summary>
        private void Owners(KeyDetectionResult reading, Rational from, Rational to, KeySignature? current, Span<bool> owns)
        {
            // Ownership is weighed to a small tolerance: the weights are note durations, and two
            // keys that own the same notes must tie exactly however the durations were summed.
            const float tolerance = 1e-4f;

            // The weights are kept by key index, not by the candidate's place in the reading.
            var candidates = reading.AllCorrelations;
            var count = Math.Min(candidates.Length, 24);
            Span<float> foreign = stackalloc float[24];
            Span<float> chromatic = stackalloc float[24];
            var fromD = from.ToDouble();
            var toD = to.ToDouble();
            var opening = FirstIndexAt(fromD);
            var closing = FirstIndexAt(toD) - 1;
            var spans = opening < _sonorities.Length && _onset[opening] < toD;
            if (spans)
                opening = OpeningIndex(opening);
            var present = 0u;
            for (var k = 0; k < count; k++)
                present |= 1u << KeyIndex(candidates[k].Key);

            // The keys — one bit each, by KeyIndex — whose tonic chord frames the span, and so
            // hear their applied triads and borrowed chords in it (Exemption); a key in force
            // must be given for those to be heard at all. The key in force itself needs no
            // frame: it hears its own chromatic chords wherever it stands, as StandsAt and
            // HomeAgain already say. Framed like a rival, C minor could not own Fm G7 C Fm —
            // the C major triad is its V/iv, an applied triad — while F minor owned the same
            // four chords as i V7/V V i once the guard let the G7 go, and the detector wrote a
            // three-bar tonicization of F minor over the Picardy third and the plagal Amen that
            // close Cm Fm G7 Cm | A♭ Fm G7 C | Fm C.
            var framed = spans && current is not null ? (TonicKeysOf(opening) | TonicKeysOf(closing)) & ~Asked : 0u;
            if (spans && current is { } inForceKey)
                framed |= 1u << KeyIndex(inForceKey);

            foreach (var i in Reaching(fromD))
            {
                if (_onset[i] >= toD)
                    break;

                var start = Math.Max(_onset[i], fromD);
                var stop = Math.Min(_end[i], toD);
                if (stop <= start)
                    continue;

                // A note doubling a tone of the chord it was struck with weighs nothing of its
                // own, and a chord struck as several notes that end together is weighed once,
                // by its pitch classes, on the first of them: the chord weighs the same on
                // either road.
                var pitchClasses = _weighs[i];
                if (pitchClasses == 0)
                    continue;

                var weight = (float)(stop - start);
                if (_lineNote[i])
                {
                    // A single note is foreign to the keys that do not own its pitch class, less
                    // the keys it is a non-harmonic tone of — one bit each, in one word. A note
                    // of an arpeggiated chord, or a tone of the chord sounding under it, is that
                    // chord's: the keys the chord is a chromatic chord of own it too, and a tone
                    // of the chord under it is no more chromatic harmony than the chord already
                    // is — the melody's G sharp over G's V/ii weighed a quarter more against G
                    // than A minor's D7 weighed against A minor, and the detector, which folds the
                    // melody's downbeat into its chords, named A minor for the phrase.
                    var keys = KeysLacking[BitOperations.TrailingZeroCount(pitchClasses)] & ~NonHarmonicKeys(i) & present;
                    var chord = _under[i] >= 0 ? _under[i] : i;
                    var chordOfSome = _maybeChromatic[i] && !Guarded(chord, current);
                    while (keys != 0)
                    {
                        var keyIndex = BitOperations.TrailingZeroCount(keys);
                        keys &= keys - 1;

                        if (_under[i] < 0)
                            chromatic[keyIndex] += weight;
                        var triads = (framed >> keyIndex & 1) != 0;
                        if (!chordOfSome || (pitchClasses & ~ChromaticChordOf(chord, KeyAt(keyIndex), current, triads ? Exempt.Chords : Exempt.Sevenths)) != 0)
                            foreign[keyIndex] += weight;
                    }

                    continue;
                }

                // A chord the key in force owns is its own to every other key; only a chord
                // it does not is asked whether it is another key's chromatic chord.
                // What a chord weighs against each key is found once per chord (Weights) and
                // read here: how many of its pitch classes the key lacks outright, and how many
                // it still lacks with its chromatic chords counted — which depends on the key in
                // force in two ways asked once per chord, not once per key: the guard, and a
                // dominant resolving into the tonic in force, which is no applied chord.
                var weights = Weights(i);
                var chromaticToSome = _maybeChromatic[i] && !Guarded(i, current);
                var resolvesIntoHome = current is { } home && IsTonicChord(home, _following[i]) ? 2 : 0;

                // The Picardy third that closes the piece is no chromatic chord to a minor key
                // the music has been in: it is that key's cadence. Counted as one, A minor lost
                // E7 A to F sharp minor, which owns both chords outright and was never heard.
                var closesThePiece = EndsThePiece(i);
                for (var keyIndex = 0; keyIndex < 24; keyIndex++)
                {
                    var outright = weights[keyIndex * 5];
                    if (outright == 0)
                        continue;

                    var chromaticOutright = outright;
                    if (closesThePiece && keyIndex >= 12 && (KeysTheMusicHasBeenIn >> keyIndex & 1) != 0 && PicardyThird(i, KeyAt(keyIndex)) != 0)
                        chromaticOutright--;
                    chromatic[keyIndex] += chromaticOutright * weight;
                    var lacking = chromaticToSome
                        ? weights[(keyIndex * 5) + 1 + resolvesIntoHome + (int)(framed >> keyIndex & 1)]
                        : outright;
                    if (lacking != 0)
                        foreign[keyIndex] += lacking * weight;
                }
            }

            var least = float.MaxValue;
            for (var keyIndex = 0; keyIndex < 24; keyIndex++)
            {
                if ((present >> keyIndex & 1) != 0 && foreign[keyIndex] < least)
                    least = foreign[keyIndex];
            }

            // The Picardy third is the minor key's chord. A span that closes the piece on the
            // tonic major triad of a minor key the music has been in, and that the minor key
            // owns, is not its parallel major's: the major owns the close only by taking the
            // Picardy chord for its own tonic, and needs no chromatic chord to do it where the
            // minor needs one, so it is put out before the keys are compared on their chromatic
            // chords. Named by the profile, E7 A closing Am Dm E7 Am | C F G C | C F G C | E7 A
            // was A major — a key the music had never been in — and A minor's Picardy cadence
            // was a tonicization of A major, on both roads. Only a minor key the music has been
            // in: every G7 C closing a piece in C major is C minor's Picardy cadence by shape.
            if (spans && EndsThePiece(closing))
            {
                for (var keyIndex = 12; keyIndex < 24; keyIndex++)
                {
                    if (((present & KeysTheMusicHasBeenIn) >> keyIndex & 1) != 0
                        && foreign[keyIndex] <= least + tolerance
                        && PicardyThird(closing, KeyAt(keyIndex)) != 0)
                    {
                        present &= ~(1u << (keyIndex - 12));
                    }
                }
            }

            var leastChromatic = float.MaxValue;
            for (var keyIndex = 0; keyIndex < 24; keyIndex++)
            {
                if ((present >> keyIndex & 1) != 0 && foreign[keyIndex] <= least + tolerance && chromatic[keyIndex] < leastChromatic)
                    leastChromatic = chromatic[keyIndex];
            }

            // Among keys that own the span equally, one whose tonic chord — a chord, not a lone
            // note of the line — opens or closes it owns it better than one that does not: a
            // phrase is heard in the key of the chord it opens or closes on. G E Am D7 with a
            // melody over it needs one chromatic chord of G's (the V/ii) and one of A minor's
            // (the D7), and the detector's profile preferred A minor; the phrase opens on G.
            var framedByAChord = !spans ? 0u
                : (BitOperations.PopCount(_chordPitchClasses[opening]) >= 2 ? TonicKeysOf(opening) : 0u)
                | (BitOperations.PopCount(_chordPitchClasses[closing]) >= 2 ? TonicKeysOf(closing) : 0u);
            var anyFramed = false;
            for (var keyIndex = 0; keyIndex < 24 && framedByAChord != 0; keyIndex++)
            {
                anyFramed |= (present >> keyIndex & 1) != 0
                    && foreign[keyIndex] <= least + tolerance
                    && chromatic[keyIndex] <= leastChromatic + tolerance
                    && (framedByAChord >> keyIndex & 1) != 0;
            }

            for (var k = 0; k < count; k++)
            {
                var keyIndex = KeyIndex(candidates[k].Key);
                owns[k] = (present >> keyIndex & 1) != 0
                    && foreign[keyIndex] <= least + tolerance
                    && chromatic[keyIndex] <= leastChromatic + tolerance
                    && (!anyFramed || (framedByAChord >> keyIndex & 1) != 0);
            }
        }

        // What each chord weighs against each key, found once per chord: for each key, by
        // KeyIndex, five bytes — how many of the chord's pitch classes the key lacks outright,
        // then how many it lacks with its chromatic chords counted, in four variants: with only
        // its dominant sevenths counted or its triads too (the low bit), and with the chord
        // resolving into the tonic of the key in force or not (the high bit), which strips the
        // applied reading. Weighed afresh at every candidate, eight thousand random chords each
        // cost twenty-four keys' worth of chord reading in every window that held them.
        private byte[]? _weights;
        private bool[]? _weighed;
        private byte[]? _noteWeights;
        private bool[]? _noteWeighed;

        private ReadOnlySpan<byte> Weights(int index)
        {
            // Kept per chord for a chord struck as one, and per note for the few notes struck
            // with a chord but not ending with it, which weigh their own pitch classes.
            var whole = _weighs[index] == _chordPitchClasses[index];
            var slot = whole ? _group[index] : index;
            var table = whole
                ? (_weights ??= new byte[_groupCount * 120])
                : (_noteWeights ??= new byte[_sonorities.Length * 120]);
            var done = whole
                ? (_weighed ??= new bool[_groupCount])
                : (_noteWeighed ??= new bool[_sonorities.Length]);
            var weights = table.AsSpan(slot * 120, 120);
            if (!done[slot])
            {
                WeighAgainstEveryKey(index, weights);
                done[slot] = true;
            }

            return weights;
        }

        private void WeighAgainstEveryKey(int index, Span<byte> weights)
        {
            var pitchClasses = _weighs[index];
            var chord = _chordPitchClasses[index];
            for (var k = 0; k < 24; k++)
            {
                var key = new KeySignature((byte)(k % 12), k < 12);
                var outright = OutrightOf(pitchClasses, index, key);
                weights[k * 5] = (byte)BitOperations.PopCount(outright);
                if (outright == 0)
                    continue;

                var outside = (ushort)(chord & ~Owned(key));
                for (var variant = 0; variant < 4; variant++)
                {
                    var (applied, other) = ChromaticChordParts(index, key, triads: (variant & 1) != 0);
                    if ((variant & 2) != 0)
                        applied = 0;
                    weights[(k * 5) + 1 + variant] = (byte)BitOperations.PopCount((ushort)(outright & ~((applied | other) & outside)));
                }
            }
        }

        /// <summary>
        /// Names the key of the phrase [<paramref name="from"/>, <paramref name="to"/>): of the
        /// keys that own it as well as any key does (<see cref="Owners"/>), the one the profile
        /// correlates best with. The margin is the profile's, taken over those keys only, so
        /// that a key no other key rivals in ownership is named outright.
        /// </summary>
        /// <remarks>
        /// <see cref="KeyDetectionResult.Key"/> is the profile's choice over all twenty-four keys,
        /// and the Krumhansl profile hears a dominant seventh as the key on its root: eight chords
        /// alternating D7 and G read as D major by a margin of 0.15, though D major does not own
        /// the C natural in every D7. A key that has to call a recurring chord tone foreign is
        /// not the phrase's key when another key owns every note. Ownership is measured as the
        /// key hears its material, not by scale membership: measured by scale membership, a minor
        /// key owned its relative major's scale and one note more — its raised seventh — so any
        /// phrase in G with an E flat neighbour in it, a quarter long, was named E minor outright,
        /// the passing note taken for the leading tone.
        /// </remarks>
        public Named Name(Reading reading, Rational from, Rational to, KeySignature? current)
        {
            Span<bool> owns = stackalloc bool[24];
            Owners(reading.Result, from, to, current, owns);

            // AllCorrelations is sorted strongest first, so the first two owners met are the best
            // and the runner-up among them.
            KeySignature? best = null;
            var bestCorrelation = 0f;
            var runnerUp = float.NaN;
            for (var i = 0; i < reading.Result.AllCorrelations.Length && i < 24; i++)
            {
                if (!owns[i])
                    continue;

                if (best is null)
                {
                    best = reading.Result.AllCorrelations[i].Key;
                    bestCorrelation = reading.Result.AllCorrelations[i].Correlation;
                }
                else
                {
                    runnerUp = reading.Result.AllCorrelations[i].Correlation;
                    break;
                }
            }

            if (best is not { } key)
                return new Named(reading.Result.Key, reading.Result.Confidence);

            if (float.IsNaN(runnerUp))
                return new Named(key, 1f);

            var margin = bestCorrelation > 0f
                ? Math.Clamp((bestCorrelation - runnerUp) / (bestCorrelation + 0.001f), 0f, 1f)
                : 0f;
            return new Named(key, margin);
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns outright every pitch class in <paramref name="mask"/>,
        /// the pitch classes sounding in a span: for a major key, its scale holds them all; for a
        /// minor key, its raised seventh must not be among them, since it owns that only in
        /// certain chords (<see cref="Outright"/>).
        /// </summary>
        public static bool OwnsEveryNoteOutright(KeySignature key, ushort mask) =>
            (mask & ~Owned(key)) == 0 && (key.IsMajor || (mask & (1 << PitchMath.Fold(key.Root + 11))) == 0);

        /// <summary>
        /// The key profile of [<paramref name="from"/>, <paramref name="to"/>), each sonority
        /// weighed by how long it sounds inside the span — the reading
        /// <see cref="KeyProfiler.DetectFromBuffer"/> gives — with the pitch classes that sound
        /// there and the distribution itself.
        /// </summary>
        public Reading Profile(Rational from, Rational to)
        {
            var distribution = new float[12];
            ushort mask = 0;
            var fromD = from.ToDouble();
            var toD = to.ToDouble();

            foreach (var i in Reaching(fromD))
            {
                if (_onset[i] >= toD)
                    break;

                var start = Math.Max(_onset[i], fromD);
                var stop = Math.Min(_end[i], toD);
                if (stop <= start)
                    continue;

                var weight = (float)(stop - start);
                var pitchClasses = _sonorities[i].PitchClasses;
                var remaining = pitchClasses;
                while (remaining != 0)
                {
                    distribution[BitOperations.TrailingZeroCount(remaining)] += weight;
                    remaining &= (ushort)(remaining - 1);
                }

                mask |= pitchClasses;
            }

            return new Reading(KeyProfiler.Detect(distribution), mask, distribution);
        }

        /// <summary>
        /// Whether <paramref name="key"/> holds from <paramref name="from"/> through
        /// <paramref name="holdTo"/>: the stretch — extended by a phrase at a time while it
        /// cannot decide, to the end at most — is decidable, names the key over any rival by
        /// <see cref="MinTold"/>, separates it from <paramref name="current"/> by
        /// <paramref name="minSeparation"/> — <see cref="MinSeparation"/>, or nothing for a
        /// homecoming — and is owned by it. Returns the reading and the name the stretch was
        /// given, for the caller's use.
        /// </summary>
        public (bool Holds, Reading Hold, Named HeldAs) Holds(Rational from, ref Rational holdTo, KeySignature key, KeySignature current, Rational phrase, float minSeparation)
        {
            if (holdTo > End)
                holdTo = End;

            var hold = Profile(from, holdTo);
            var heldAs = Name(hold, from, holdTo, current);
            while ((!hold.Result.IsDecidable || heldAs.Margin < MinDecisive) && holdTo < End)
            {
                holdTo = holdTo + phrase < End ? holdTo + phrase : End;
                hold = Profile(from, holdTo);
                heldAs = Name(hold, from, holdTo, current);
            }

            var holds = hold.Result.IsDecidable
                && heldAs.Margin >= MinTold
                && heldAs.Key == key
                && Separation(hold.Result, key, current) >= minSeparation
                && Owns(from, holdTo, key, current);
            return (holds, hold, heldAs);
        }

        /// <summary>
        /// Whether <paramref name="key"/>'s tonic chord frames [<paramref name="from"/>,
        /// <paramref name="to"/>): the chord at its first onset or the chord at its last is the
        /// tonic triad, with or without a melody note above it — or, for a minor key at the
        /// close, the Picardy third.
        /// </summary>
        public bool Frames(KeySignature key, Rational from, Rational to)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length || _sonorities[i].Onset >= to)
                return false;

            return IsTonicChordOf(key, OpeningIndex(i)) || IsTonicChordOf(key, FirstIndexAt(to) - 1);
        }

        /// <summary>
        /// The sonority that carries the harmony a span opening at sonority <paramref name="index"/>
        /// opens on: the sonority itself, or — when it is a note of the line alone and a chord of
        /// two notes or more is struck within an eighth after it, before it stops — the first
        /// sonority of that chord. A phrase opens on the chord under its first note; when the
        /// accompaniment is late behind a melody on the beat, that is the chord struck an eighth
        /// after it.
        /// </summary>
        /// <remarks>
        /// Read from its first onset alone, a phrase that began with the melody's downbeat — its
        /// chord an eighth behind — opened on a lone note, no chord of any key's: the frame that
        /// hears a key's applied triads, the tonic that opens a phrase in its key, the chord a
        /// key is heard from were all lost, and a chromatic appoggiatura struck on every downbeat
        /// of G's phrase, its chords an eighth late, kept G from being named on either road.
        /// </remarks>
        private int OpeningIndex(int index)
        {
            if (!_lineNote[index] || _groupEnd[index] - _groupStart[index] > 1)
                return index;

            var next = _groupEnd[index];
            if (next < _sonorities.Length
                && _onset[next] <= _onset[index] + 0.125
                && _onset[next] < _end[index]
                && BitOperations.PopCount(_chordPitchClasses[next]) > 1)
            {
                return next;
            }

            return index;
        }

        /// <summary>Whether the chord sonority <paramref name="index"/> belongs to is <paramref name="key"/>'s tonic chord, with or without a note above it, or its Picardy third.</summary>
        private bool IsTonicChordOf(KeySignature key, int index) => (TonicKeysOf(index) >> KeyIndex(key) & 1) != 0;

        // The keys — one bit each, by KeyIndex — whose tonic chord each chord is, found once per
        // chord: every span that opens or closes on the chord asks, for every key.
        private uint[]? _tonicKeys;

        private uint TonicKeysOf(int index)
        {
            _tonicKeys ??= new uint[_sonorities.Length];
            var keys = _tonicKeys[index];
            if ((keys & Asked) == 0)
            {
                keys = Asked;
                var chord = _chordPitchClasses[index];
                for (var k = 0; k < 24; k++)
                {
                    var key = new KeySignature((byte)(k % 12), k < 12);
                    var tonic = TonicTriad(key);
                    var picardy = PicardyThird(index, key);
                    if (IsTonicChord(key, chord)
                        || (chord & tonic) == tonic
                        || (chord & ~tonic & ~picardy) == 0 && (chord & (1 << key.Root)) != 0 && picardy != 0)
                    {
                        keys |= 1u << k;
                    }
                }

                _tonicKeys[index] = keys;
            }

            return keys;
        }

        /// <summary>
        /// Whether <paramref name="key"/> owns [<paramref name="from"/>, <paramref name="to"/>):
        /// in every whole-note bar of it, clipped to the span, the notes the key lacks amount to
        /// less than <see cref="StrayNote"/>, the key's chromatic chords counted as its own as
        /// far as <paramref name="exempt"/> says — and its applied triads and borrowed chords
        /// only inside a phrase the key frames (<see cref="Frames"/>).
        /// </summary>
        /// <remarks>
        /// A key's chromatic chords are heard inside a phrase of its own: C F G C | G E Am D7 is
        /// in G, whose tonic opens the phrase with the V/ii in it. Without the frame, a phrase
        /// every key owns a half of belonged to a third: D G A D | E A B E, two bars of D and two
        /// of E, was A major's — IV ♭VII V IV, V I V/V V — and A major was reported touched for
        /// four bars in a passage that never sounds its key.
        /// </remarks>
        public bool Owns(Rational from, Rational to, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            exempt = Exemption(key, from, to, exempt);

            var fromD = from.ToDouble();
            var toD = to.ToDouble();
            var firstBar = (long)Math.Floor(fromD - _phase);
            var barCount = (int)Math.Max(0, (long)Math.Ceiling(toD - _phase) - firstBar);
            Span<float> foreign = barCount <= 16 ? stackalloc float[16] : new float[barCount];
            foreign = foreign[..barCount];
            foreign.Clear();

            foreach (var i in Reaching(fromD))
            {
                if (_onset[i] >= toD)
                    break;

                var start = Math.Max(_onset[i], fromD);
                var stop = Math.Min(_end[i], toD);
                if (stop <= start)
                    continue;

                var lacking = Lacking(i, key, current, exempt);
                if (lacking == 0)
                    continue;

                var count = BitOperations.PopCount(lacking);
                var bar = (long)Math.Floor(start - _phase);

                // A chord released late is the bar before's: a sonority struck in the bar before
                // that stops within an eighth of the bar line is the harmony leaving, as an
                // accompaniment's chords leave under a melody on the beat, and weighs in the bar
                // it was struck in. Weighed where it sounded, the eighth of C major under the
                // G sharp that begins E major's first bar was two foreign pitch classes for an
                // eighth — a quarter note of weight, the most a bar may lack — and E major owned
                // nothing.
                var barStart = bar + _phase;
                if (_onset[i] < barStart && stop <= barStart + 0.125)
                {
                    var before = (int)(bar - 1 - firstBar);
                    if (before >= 0 && before < barCount)
                    {
                        foreign[before] += count * (float)(stop - start);
                        if (foreign[before] >= StrayNote)
                            return false;
                    }

                    continue;
                }

                for (var at = start; at < stop; bar++)
                {
                    var until = Math.Min(bar + 1 + _phase, stop);
                    var index = (int)(bar - firstBar);
                    if (index >= 0 && index < barCount)
                    {
                        foreign[index] += count * (float)(until - at);
                        if (foreign[index] >= StrayNote)
                            return false;
                    }

                    at = until;
                }
            }

            return true;
        }

        /// <summary>
        /// The weight of the material in [<paramref name="from"/>, <paramref name="to"/>) that
        /// <paramref name="key"/> lacks (<see cref="Lacking"/>) — its chromatic chords counted
        /// as its own as far as <paramref name="exempt"/> says, its applied triads and borrowed
        /// chords only inside a phrase it frames (<see cref="Frames"/>); with nothing exempt the
        /// weight measures how much chromatic harmony the key needs to own the span. A
        /// non-harmonic tone weighs nothing either way.
        /// </summary>
        private float Foreign(Rational from, Rational to, KeySignature key, KeySignature? current, Exempt exempt)
        {
            exempt = Exemption(key, from, to, exempt);

            var sum = 0f;
            var fromD = from.ToDouble();
            var toD = to.ToDouble();
            foreach (var i in Reaching(fromD))
            {
                if (_onset[i] >= toD)
                    break;

                var start = Math.Max(_onset[i], fromD);
                var stop = Math.Min(_end[i], toD);
                if (stop <= start)
                    continue;

                var lacking = Lacking(i, key, current, exempt);
                if (lacking != 0)
                    sum += BitOperations.PopCount(lacking) * (float)(stop - start);
            }

            return sum;
        }

        /// <summary>
        /// The onset of the first sonority in [<paramref name="from"/>, <paramref name="to"/>) —
        /// strictly after <paramref name="from"/> when <paramref name="afterFrom"/> — that sounds
        /// a pitch class in <paramref name="pitchClasses"/>.
        /// </summary>
        public bool FirstOnsetSounding(ushort pitchClasses, Rational from, bool afterFrom, Rational to, out Rational onset)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (afterFrom && s.Onset == from)
                    continue;

                if ((s.PitchClasses & pitchClasses) != 0)
                {
                    onset = s.Onset;
                    return true;
                }
            }

            onset = default;
            return false;
        }

        /// <summary>
        /// The onset of the first sonority in [<paramref name="from"/>, <paramref name="to"/>)
        /// that sounds a pitch class in <paramref name="pitchClasses"/> in a chord of
        /// <paramref name="key"/>'s own: a chord that is not one of the key's chromatic chords,
        /// or one that holds the key's tonic triad, however coloured — a blues opens on I7. A key
        /// begins on a chord of its own: an applied or a borrowed chord is heard as the key's
        /// only once the key is in force, so it cannot be where the key begins. In a
        /// descending-fifths sequence of major triads every four chords are V/V V I IV of the
        /// third one's key, and B♭ E♭ A♭ D♭ began an A flat major area on the B flat chord; the
        /// area that begins on the E flat runs into the G flat and is nothing. A chord with a
        /// stray note in it is not a chromatic chord: while the detector's eighth-note grid rounded
        /// the last thirty-seconds of a bar of C onto the D flat downbeat, D flat began on that
        /// downbeat still — even where the strays made a D flat seventh of it.
        /// </summary>
        public bool FirstOnsetOfTheKey(ushort pitchClasses, Rational from, Rational to, KeySignature key, KeySignature current, out Rational onset)
        {
            var tonic = TonicTriad(key);
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;

                if ((s.PitchClasses & pitchClasses) != 0
                    && (ChromaticChordOf(i, key, current) == 0 || (_chordPitchClasses[i] & tonic) == tonic))
                {
                    onset = s.Onset;
                    return true;
                }
            }

            onset = default;
            return false;
        }

        /// <summary>
        /// The onset of the first sonority strictly after <paramref name="from"/> and before
        /// <paramref name="to"/> that sounds a note <paramref name="key"/> lacks, or <paramref name="to"/>.
        /// </summary>
        public Rational FirstOnsetLacking(KeySignature key, KeySignature current, Rational from, Rational to)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (s.Onset > from && Lacking(i, key, current) != 0)
                    return s.Onset;
            }

            return to;
        }

        /// <summary>
        /// The onset of the last sonority in [<paramref name="from"/>, <paramref name="to"/>)
        /// that sounds a note <paramref name="key"/> lacks.
        /// </summary>
        public bool LastOnsetLacking(KeySignature key, KeySignature current, Rational from, Rational to, out Rational onset)
        {
            var found = false;
            onset = default;
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (Lacking(i, key, current) != 0)
                {
                    onset = s.Onset;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// How many distinct whole notes in [<paramref name="from"/>, <paramref name="to"/>) hold
        /// a sonority beginning there that sounds a pitch class in <paramref name="pitchClasses"/>.
        /// </summary>
        public int BarsSounding(ushort pitchClasses, Rational from, Rational to)
        {
            var bars = 0;
            long? lastBar = null;
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & pitchClasses) == 0)
                    continue;

                var bar = (long)Math.Floor(_onset[i] - _phase);
                if (bar != lastBar)
                {
                    bars++;
                    lastBar = bar;
                }
            }

            return bars;
        }

        /// <summary>
        /// Whether a sonority in [<paramref name="from"/>, <paramref name="to"/>) sounds a pitch
        /// class in <paramref name="homeNotes"/> — a note of <paramref name="current"/>'s own —
        /// as a note and not as a non-harmonic tone of <paramref name="key"/>: a passing F sharp
        /// in a C-major scale is not E minor sounding its own note.
        /// </summary>
        public bool SoundsAsItsOwn(ushort homeNotes, Rational from, Rational to, KeySignature key, KeySignature current)
        {
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if ((s.PitchClasses & homeNotes) != 0 && Lacking(i, key, current, Exempt.Nothing) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether the new key's own notes, <paramref name="ownNotes"/>, sound in at least two
        /// whole notes from <paramref name="from"/> before <paramref name="within"/> — and before
        /// the old key's own notes, <paramref name="homeNotes"/>, are heard again as harmony: a
        /// second applied dominant after the old key has come home confirms nothing, and a chord
        /// of the old key is the old key back, whatever the new key might borrow it as; but a
        /// passing F natural in E F F♯ G is not C major coming home, and nor is a chord of the
        /// old key that resolves into a chord the old key does not own — a dominant leaving the
        /// key: the G of F B♭ G C7 resolves into C7, and is V/V of F, not C major back. Heard as
        /// C back, F was never entered and began a phrase late. The E major chord of C D E F♯,
        /// two bars each, is no chord of C's, and its E natural is C's note heard again: F sharp
        /// major, which would borrow the chord as its flat seventh, is not entered.
        /// </summary>
        public bool Returns(ushort ownNotes, ushort homeNotes, Rational from, Rational within, KeySignature key, KeySignature current)
        {
            if (within > End)
                within = End;
            if (homeNotes != 0)
            {
                for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
                {
                    var s = _sonorities[i];
                    if (s.Onset >= within)
                        break;
                    if (s.Onset > from && (s.PitchClasses & homeNotes) != 0 && Lacking(i, key, current, Exempt.Nothing) != 0 && !LeavesTheKey(i, current))
                    {
                        within = s.Onset;
                        break;
                    }
                }
            }

            return BarsSounding(ownNotes, from, within) >= 2;
        }

        /// <summary>
        /// Whether the chord sonority <paramref name="index"/> belongs to is a chord of
        /// <paramref name="key"/> — owned outright — that resolves into a chord the key does not
        /// own: a dominant leaving the key, which is not the key heard again.
        /// </summary>
        private bool LeavesTheKey(int index, KeySignature key) =>
            OwnsChord(key, index) && _following[index] != 0 && (_following[index] & ~Owned(key)) != 0;

        /// <summary>
        /// Whether [<paramref name="from"/>, <paramref name="to"/>) is framed by <paramref name="key"/>'s
        /// tonic chord: everything sounding at its first onset is that chord — or, in a melody,
        /// a single note of the tonic triad, as a tune may open on the third or the fifth — and
        /// everything sounding at its last lies within the tonic triad; an arpeggio may end on
        /// the third, and a minor key's piece may close on its Picardy third.
        /// </summary>
        public bool FramedBy(KeySignature key, Rational from, Rational to)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length || _sonorities[i].Onset >= to)
                return false;

            var opening = OpeningHarmony(i);
            var opensOnTheTonic = BitOperations.PopCount(opening) == 1
                ? (opening & ~TonicTriad(key)) == 0
                : IsTonicChord(key, opening);
            if (!opensOnTheTonic)
                return false;

            var last = FirstIndexAt(to) - 1;
            var closing = SoundingAt(_sonorities[last].Onset);
            return closing != 0 && (closing & ~TonicTriad(key) & ~PicardyThird(last, key)) == 0;
        }

        /// <summary>
        /// Whether the phrase beginning at <paramref name="from"/> — a phrase of the piece's own
        /// count (<see cref="PhraseStart"/>) — opens on <paramref name="key"/>'s tonic chord and
        /// the phrase after it opens on that chord again: a loop that returns to its first chord
        /// is the key of that chord. Counted from the piece's phrases, not from any chord: the C
        /// of Am F C G comes round every four bars too, but no phrase of the loop begins on it.
        /// </summary>
        public bool ReturnsToItsTonic(KeySignature key, Rational from, Rational phrase) =>
            PhraseStart(from, phrase) == from && OpensOn(key, from) && OpensOn(key, from + phrase);

        /// <summary>
        /// Whether [<paramref name="from"/>, <paramref name="to"/>) closes with <paramref name="key"/>'s
        /// authentic cadence: the last chord in it is the key's tonic chord — with or without a
        /// melody note above it — closing a phrase of the piece's count (<see cref="PhraseStart"/>)
        /// or the piece, and the chord before that, a chord of two notes or more that differs from
        /// it, is the key's dominant, plain or with its seventh. A cadence in chords; a melody
        /// alone closes in its key by its frame (<see cref="FramedBy"/>).
        /// </summary>
        public bool ClosesWithACadence(KeySignature key, Rational from, Rational to, Rational phrase)
        {
            var last = FirstIndexAt(to) - 1;
            if (last < FirstIndexAt(from))
                return false;

            // The melody's last note over the closing chord closes on that chord.
            var tonic = _groupStart[_lineNote[last] && _under[last] >= 0 ? _under[last] : last];
            if (BitOperations.PopCount(_chordPitchClasses[tonic]) < 2 || !IsTonicChordOf(key, tonic))
                return false;

            // A cadence closes a phrase: the tonic chord's bar is the last of a phrase of the
            // piece's own count (PhraseStart), or of the piece. The G C at the seam of Am F C G
            // | C F G C — the loop's last chord and the next phrase's first — is no cadence,
            // and C begins where its phrase does, not four bars before it.
            var after = BarStart(_sonorities[tonic].Onset) + Rational.Whole;
            if (after < End && PhraseStart(after, phrase) != after)
                return false;

            var dominant = MajorTriad(PitchMath.Fold(key.Root + 7));
            var dominantSeventh = DominantSeventh(PitchMath.Fold(key.Root + 7));
            for (var i = tonic - 1; i >= 0 && _sonorities[i].Onset >= from; i = _groupStart[i] - 1)
            {
                var chord = _chordPitchClasses[i];
                if (BitOperations.PopCount(chord) < 2 || chord == _chordPitchClasses[tonic])
                    continue;
                return (chord & dominant) == dominant && (chord & ~dominantSeventh) == 0;
            }

            return false;
        }

        /// <summary>
        /// The harmony a span opening at sonority <paramref name="index"/> opens on: everything
        /// sounding at its onset, or — when it is a lone note of the line whose chord is struck
        /// within an eighth after it (<see cref="OpeningIndex"/>) — that chord, the note over it
        /// being the line's.
        /// </summary>
        private ushort OpeningHarmony(int index)
        {
            var opening = OpeningIndex(index);
            return opening == index ? SoundingAt(_sonorities[index].Onset) : _chordPitchClasses[opening];
        }

        /// <summary>
        /// Whether everything sounding at the first onset at or after <paramref name="from"/> —
        /// or the chord struck within an eighth after a lone first note (<see cref="OpeningIndex"/>)
        /// — is <paramref name="key"/>'s tonic chord.
        /// </summary>
        public bool OpensOn(KeySignature key, Rational from)
        {
            var i = FirstIndexAt(from);
            return i < _sonorities.Length && IsTonicChord(key, OpeningHarmony(i));
        }

        /// <summary>
        /// Whether the chord at the first onset at or after <paramref name="from"/> is owned by
        /// <paramref name="key"/> outright — every note of it in the key's scale, no chromatic
        /// chord of the key's counted.
        /// </summary>
        public bool OpensOutright(KeySignature key, Rational from)
        {
            var i = FirstIndexAt(from);
            if (i >= _sonorities.Length)
                return false;

            i = OpeningIndex(i);
            var onset = _sonorities[i].Onset;
            for (var k = i; k < _sonorities.Length && _sonorities[k].Onset == onset; k++)
            {
                if (Outright(k, key) != 0)
                    return false;
            }

            return true;
        }

        /// <summary>The pitch classes of every sonority sounding at <paramref name="position"/> — less a note leaning on the chord it was struck with, or a note of the line struck over it (<see cref="_overChord"/>), which is no note of the harmony.</summary>
        private ushort SoundingAt(Rational position)
        {
            ushort together = 0;
            foreach (var i in Reaching(position))
            {
                if (_sonorities[i].Onset > position)
                    break;
                if (_sonorities[i].End > position && !_overChord[i])
                    together |= (ushort)(_sonorities[i].PitchClasses & ~_leaning[i]);
            }

            return together;
        }

        /// <summary>
        /// The pitch classes of every sonority whose harmony holds at <paramref name="position"/>
        /// (<see cref="_harmonyEnd"/>): what <see cref="SoundingAt"/> gives, with a chord that
        /// was released before <paramref name="position"/> still counted until the next chord.
        /// A note of the line leans on this, and an accented appoggiatura resolves into it.
        /// </summary>
        private ushort HarmonyAt(Rational position)
        {
            var positionD = position.ToDouble();
            ushort together = 0;
            foreach (var i in ReachingHarmony(position))
            {
                if (_sonorities[i].Onset > position)
                    break;
                if (_harmonyEnd[i] > positionD)
                    together |= _sonorities[i].PitchClasses;
            }

            return together;
        }

        /// <summary>
        /// The start of the phrase holding <paramref name="position"/>: phrases of
        /// <paramref name="phrase"/> counted from the first bar — the bar the first note begins,
        /// or, when the bars begin later than the clock (<see cref="_phase"/>) and the first note
        /// is a pickup before the first bar, that first bar.
        /// </summary>
        public Rational PhraseStart(Rational position, Rational phrase)
        {
            var anchor = FirstBar;
            var phrases = (long)Math.Floor(((position - anchor) / phrase).ToDouble());
            return anchor + (phrase * phrases);
        }

        /// <summary>
        /// The bar the music begins in: the first note, or — when the bars begin later than the
        /// clock (<see cref="_phase"/>) and the first note is a pickup before a bar — the first
        /// bar start after it, wherever in the piece's time the music begins.
        /// </summary>
        /// <remarks>
        /// Anchored on the phase itself, the phrases of a piece that began a bar in — a count-in
        /// bar, a MIDI file's first bar empty — were counted from the clock while its bars began
        /// an eighth or a quarter later, and G after a quarter-note pickup chord moved a quarter
        /// earlier when the same music started a bar later.
        /// </remarks>
        private Rational FirstBar => _phase != 0.0 && !IsBarStart(FirstOnset) ? NextBarStart(FirstOnset) : FirstOnset;

        /// <summary>
        /// Where the music is home again after an excursion to <paramref name="key"/> that began
        /// at <paramref name="from"/>: the first onset sounding a note the key does not own, or —
        /// once a whole note of the key's tonic chord has been heard — the first whole note that
        /// neither is the tonic nor sounds one of the key's own notes, <paramref name="ownNotes"/>;
        /// the end when neither comes. An excursion to a key with no note of its own — the
        /// relative major, from its minor — lasts until the old key's own notes,
        /// <paramref name="homeNotes"/>, return. <paramref name="tonicHeard"/> says whether the
        /// key's tonic chord sounded before the music was home — as a chord, or as a whole note
        /// of nothing but its notes.
        /// </summary>
        /// <remarks>
        /// A tonicization is an applied dominant and the chord it resolves to: C F | E7 Am | F G |
        /// C C touches A minor for the E7 and the Am, and is home at the F. Measured to the first
        /// note the new key did not own, the excursion ran to the end of the piece, seven bars
        /// for one E7 Am, because A minor owns every note of C major. The excursion is read by
        /// whole notes, from where it began, because an arpeggiated D7 sounds its A and C after
        /// its F sharp, and read note by note the excursion ended inside the chord that began it.
        /// </remarks>
        public Rational HomeAgain(KeySignature key, KeySignature current, ushort ownNotes, ushort homeNotes, Rational from, out bool tonicHeard)
        {
            var tonic = TonicTriad(key);
            tonicHeard = false;

            var until = End;
            if (ownNotes == 0 && homeNotes != 0 && FirstOnsetSounding(homeNotes, from, afterFrom: true, End, out var home))
                until = home;

            for (var bar = from; bar < until; bar += Rational.Whole)
            {
                var barTo = bar + Rational.Whole;
                ushort together = 0;
                for (var i = FirstIndexAt(bar); i < _sonorities.Length && _sonorities[i].Onset < barTo; i++)
                {
                    if (Lacking(i, key, current) != 0)
                        return _sonorities[i].Onset;
                    together |= (ushort)(_sonorities[i].PitchClasses & ~_leaning[i]);
                    if (BitOperations.PopCount(_chordPitchClasses[i]) >= 2 && IsTonicChord(key, _chordPitchClasses[i]))
                        tonicHeard = true;
                }

                if (together == 0)
                    continue;
                if (IsTonicChord(key, together))
                    tonicHeard = true;
                else if (ownNotes != 0 && (together & ownNotes) == 0 && tonicHeard)
                    return bar;
            }

            return until;
        }

        /// <summary>
        /// Whether the whole note starting at <paramref name="bar"/> holds at least one sonority
        /// beginning in it and every such sonority sounds only notes <paramref name="key"/> owns.
        /// A bar with a note the key lacks is not a chord of the key, however brief the note: the
        /// last bar of a C major scale is not the pivot into G for having only one F in it.
        /// </summary>
        public bool BarIsOwned(Rational bar, KeySignature key, KeySignature? current, Exempt exempt = Exempt.Chords)
        {
            var any = false;
            var to = bar + Rational.Whole;
            for (var i = FirstIndexAt(bar); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= to)
                    break;
                if (Lacking(i, key, current, exempt) != 0)
                    return false;
                any = true;
            }

            return any;
        }

        /// <summary>
        /// Whether the whole note starting at <paramref name="bar"/> is silent: nothing begins in
        /// it and nothing sounds into it. A silent bar is no key's — neither the old key's nor the
        /// new — and is passed over when a stretch of bars is asked for (<see cref="BarsAreOwned"/>,
        /// <see cref="BarsAreOwnedButForAParenthesis"/>): it neither ends a phrase nor splits its
        /// frame. A bar in which a chord held from the bar before still sounds is that chord's
        /// bar, not silence.
        /// </summary>
        /// <remarks>
        /// A silent bar was no bar of the key's — no sonority began in it — so a phrase with a
        /// bar of silence inside it was never owned bar by bar, and the frame that hears a key
        /// from where its own chords began stopped at the silence: C F G C | G C R G | G C D7 G
        /// was G at bar 9 on both roads, where a musician hears G from bar 5, the silent seventh
        /// bar being neither the old key's nor the new key's. A bar the old key's phrase held
        /// silent was passed over already, by the chromatic parenthesis: G R D7 G reached G at
        /// bar 5 by taking the silence for a quoted bar of another key. Passed over as silence, a
        /// bar in which nothing begins but the old key's closing chord sounds on under a fermata
        /// was claimed for the new key, and a chorale whose C phrase closes on a two-bar fermata
        /// began G a bar early, in the fermata.
        /// </remarks>
        private bool BarRests(Rational bar)
        {
            var i = FirstIndexAt(bar);
            return (i >= _sonorities.Length || _sonorities[i].Onset >= bar + Rational.Whole) && SoundingAt(bar) == 0;
        }

        /// <summary>
        /// The bar the music sounds in from <paramref name="position"/>: <paramref name="position"/>
        /// itself, unless its bar is silent (<see cref="BarRests"/>), when it is the start of the
        /// bar of the first onset after it. A key begins where its music does: a bar of silence
        /// before its first chord is no bar of its own — C F G C | R | G C D7 G is G from its
        /// sixth bar, not from the silence in its fifth, though the frame that hears the key from
        /// where its phrase begins reaches the silence.
        /// </summary>
        public Rational FirstSoundingBar(Rational position)
        {
            var bar = position;
            while (bar < End && BarRests(bar))
            {
                var next = BarStart(NextOnsetAfter(bar));
                if (next <= bar)
                    break;
                bar = next;
            }

            return bar;
        }

        /// <summary>
        /// Whether every whole note from <paramref name="from"/> to <paramref name="to"/> is
        /// <see cref="BarIsOwned"/> by <paramref name="key"/> — a bar in which nothing begins
        /// being passed over (<see cref="BarRests"/>) — its applied triads and borrowed chords
        /// counted only when it frames the span (<see cref="Exemption"/>).
        /// </summary>
        public bool BarsAreOwned(Rational from, Rational to, KeySignature key, KeySignature current)
        {
            var exempt = Exemption(key, from, to, Exempt.Chords);
            for (var bar = from; bar < to; bar += Rational.Whole)
            {
                if (!BarIsOwned(bar, key, current, exempt) && !BarRests(bar))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether every whole note from <paramref name="from"/> to <paramref name="to"/> is
        /// <see cref="BarIsOwned"/> by <paramref name="key"/> — a bar in which nothing begins
        /// being passed over (<see cref="BarRests"/>) — allowing one chromatic parenthesis: a bar
        /// the key does not own between a bar it owns and the bar at <paramref name="to"/>, which
        /// the caller vouches for.
        /// </summary>
        public bool BarsAreOwnedButForAParenthesis(Rational from, Rational to, KeySignature key, KeySignature current)
        {
            var exempt = Exemption(key, from, to, Exempt.Chords);
            var parenthesis = false;
            var previousOwned = false;
            for (var bar = from; bar < to; bar += Rational.Whole)
            {
                if (BarIsOwned(bar, key, current, exempt))
                {
                    previousOwned = true;
                    continue;
                }

                if (BarRests(bar))
                    continue;

                if (parenthesis || !previousOwned || bar + Rational.Whole != to && !BarIsOwned(bar + Rational.Whole, key, current, exempt))
                    return false;
                parenthesis = true;
                previousOwned = false;
            }

            return true;
        }

        /// <summary>
        /// The exemption <paramref name="key"/> gets over [<paramref name="from"/>,
        /// <paramref name="to"/>): what the caller asked for, save that the key's applied triads
        /// and borrowed chords (<see cref="Exempt.Chords"/>) are heard only inside a span the
        /// key frames (<see cref="Frames"/>) — elsewhere its dominant sevenths alone. The one
        /// place the frame rule is stated: <see cref="Owns"/>, <see cref="Foreign"/>,
        /// <see cref="BarsAreOwned"/> and <see cref="Owners"/> all ask here. A single bar is no
        /// phrase and cannot be framed, so <see cref="BarIsOwned"/> takes the exemption its
        /// caller decided; and a key in force needs no frame to own its chromatic chords
        /// (<see cref="FirstOnsetLacking"/>, <see cref="HomeAgain"/>, <see cref="StandsAt"/>).
        /// </summary>
        private Exempt Exemption(KeySignature key, Rational from, Rational to, Exempt exempt) =>
            exempt == Exempt.Chords && !Frames(key, from, to) ? Exempt.Sevenths : exempt;

        /// <summary>
        /// Whether the first sonority at or after <paramref name="position"/> belongs to a chord
        /// that began before it: an arpeggiated chord, or a chord struck twice in its bar
        /// (<see cref="_restrike"/>), whose second strike is the middle of one harmony.
        /// </summary>
        public bool BeginsInsideAChord(Rational position)
        {
            var i = FirstIndexAt(position);
            return i < _sonorities.Length && (_sonorities[_groupStart[i]].Onset < _sonorities[i].Onset || _restrike[i]);
        }

        /// <summary>The first onset strictly after <paramref name="position"/>, or the end.</summary>
        public Rational NextOnsetAfter(Rational position)
        {
            for (var i = FirstIndexAt(position); i < _sonorities.Length; i++)
            {
                if (_sonorities[i].Onset > position)
                    return _sonorities[i].Onset;
            }

            return End;
        }

        /// <summary>
        /// The start of the whole note holding <paramref name="onset"/>, or — when a sonority
        /// earlier in that whole note sounds a note <paramref name="key"/> lacks — the first
        /// onset after the last such sonority. The new key begins with the bar unless the bar
        /// began in the old.
        /// </summary>
        public Rational StartOfBar(Rational onset, KeySignature key, KeySignature current)
        {
            var bar = BarStart(onset);
            var start = bar;
            var afterForeign = false;

            var i = FirstIndexAt(bar);
            while (i < _sonorities.Length && _sonorities[i].Onset <= onset)
            {
                var at = _sonorities[i].Onset;
                var lacking = false;
                while (i < _sonorities.Length && _sonorities[i].Onset == at)
                    lacking |= Lacking(i++, key, current) != 0;

                if (afterForeign)
                {
                    start = at;
                    afterForeign = false;
                }

                if (at < onset && lacking)
                    afterForeign = true;
            }

            return start;
        }

        /// <summary>
        /// Whether a sonority beginning at or after <paramref name="from"/> and before the last
        /// onset sounds <paramref name="key"/>'s tonic pitch class.
        /// </summary>
        public bool SoundsTonicBeforeClose(KeySignature key, Rational from)
        {
            if (_sonorities.Length == 0)
                return false;

            var last = _sonorities[^1].Onset;
            var tonic = (ushort)(1 << key.Root);
            for (var i = FirstIndexAt(from); i < _sonorities.Length; i++)
            {
                var s = _sonorities[i];
                if (s.Onset >= last)
                    break;
                if ((s.PitchClasses & tonic) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>Whether the music ends on <paramref name="key"/>'s tonic triad — a minor key's Picardy third included — and nothing else.</summary>
        public bool ClosesOn(KeySignature key)
        {
            if (_sonorities.Length == 0)
                return false;

            var final = _chordPitchClasses[^1];
            return final != 0 && (final & ~TonicTriad(key) & ~PicardyThird(_sonorities.Length - 1, key)) == 0;
        }

        /// <summary>The index of the first sonority whose onset is at or after <paramref name="position"/>.</summary>
        private int FirstIndexAt(Rational position) => FirstIndexAt(position.ToDouble());

        private int FirstIndexAt(double position)
        {
            var lo = 0;
            var hi = _sonorities.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (_onset[mid] < position)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }

        /// <summary>
        /// The indices of the sonorities that may be sounding at <paramref name="position"/> or
        /// begin after it: the long ones kept aside that reach it, then every sonority from the
        /// first whose ordinary length could, in onset order. A loop over them stops itself where
        /// its onsets pass what it is looking for.
        /// </summary>
        private ReachingIndices Reaching(Rational position) => new(this, position.ToDouble(), _longestD, harmony: false);

        private ReachingIndices Reaching(double position) => new(this, position, _longestD, harmony: false);

        /// <summary>
        /// The indices of the sonorities whose harmony may hold at <paramref name="position"/>
        /// or that begin after it (<see cref="_harmonyEnd"/>): <see cref="Reaching(Rational)"/>
        /// reaching a whole note further back, for a chord released before the note that
        /// leans on it.
        /// </summary>
        private ReachingIndices ReachingHarmony(Rational position) => new(this, position.ToDouble(), _harmonyReachD, harmony: true);

        private struct ReachingIndices(Evidence evidence, double position, double reach, bool harmony)
        {
            private readonly int _mainStart = evidence.FirstIndexAt(position - reach);
            private int _long = 0;
            private int _main = -1;

            public int Current { get; private set; }

            public ReachingIndices GetEnumerator() => this;

            public bool MoveNext()
            {
                while (_long < evidence._long.Length)
                {
                    var i = evidence._long[_long++];
                    if (i < _mainStart && (harmony ? evidence._harmonyEnd[i] : evidence._end[i]) > position)
                    {
                        Current = i;
                        return true;
                    }
                }

                if (_main < 0)
                    _main = _mainStart;
                if (_main < evidence._sonorities.Length)
                {
                    Current = _main++;
                    return true;
                }

                return false;
            }
        }
    }
}
