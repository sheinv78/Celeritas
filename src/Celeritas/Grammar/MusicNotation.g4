grammar MusicNotation;

/*
 * Music Notation Grammar for Celeritas
 * Supports: notes, chords, rests, ties, time signatures (with mid-sequence changes), measures, polyphony
 */

// Parser Rules

sequence
    : timeSignature? voice (BAR timeSignature? voice)* BAR? EOF
    ;

timeSignature
    : INT SLASH INT (COLON | BAR)
    ;

voice
    : element+
    ;

element
    : note
    | chord
    | rest
    ;

note
    : pitch duration? ornament? tie?
    ;

chord
    : LBRACKET noteInChord+ RBRACKET duration?
    | LPAREN noteInChord+ RPAREN duration?
    ;

noteInChord
    : pitch duration?  // Optional individual duration in chord
    ;

rest
    : REST duration?
    ;

pitch
    : PITCH_NAME accidental? octave
    ;

accidental
    : SHARP | FLAT
    ;

octave
    : INT
    ;

duration
    : (SLASH | COLON) durationValue DOT?
    ;

durationValue
    : INT                    // Numeric: 1, 2, 4, 8, 16, 32
    | DURATION_LETTER        // Letter: w, h, q, e, s, t
    ;

tie
    : TILDE
    ;

ornament
    : LBRACE ornamentType ornamentParams? RBRACE
    ;

ornamentType
    : TRILL | MORDENT | TURN | APPOGGIATURA
    ;

ornamentParams
    : COLON INT (COLON INT)*
    ;

// Lexer Rules

// Time signature
SLASH       : '/' ;
COLON       : ':' ;
BAR         : '|' ;

// Brackets
LBRACKET    : '[' ;
RBRACKET    : ']' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
LBRACE      : '{' ;
RBRACE      : '}' ;

// Duration markers
DOT         : '.' ;
TILDE       : '~' ;

// Rest
REST        : [Rr] ([Ee][Ss][Tt])? ;

// Sharp/Flat
SHARP       : '#' | '♯' ;
FLAT        : 'b' | '♭' ;

// Duration letters (must come before PITCH_NAME - lowercase only)
DURATION_LETTER
    : [w]              // whole
    | [h]              // half
    | [q]              // quarter
    | [e]              // eighth
    | [s]              // sixteenth
    | [t]              // thirty-second
    ;

// Pitch names (A-G uppercase only)
PITCH_NAME  : [A-G] ;

// Numbers
INT         : [0-9]+ ;

// Ornament keywords
TRILL           : 'tr' | 'trill' ;
MORDENT         : 'mord' | 'mordent' ;
TURN            : 'turn' ;
APPOGGIATURA    : 'app' | 'appo' ;

// Whitespace
WS          : [ \t\r\n,;]+ -> skip ;
