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
    : directive
    | polyphonicBlock
    | note
    | chord
    | rest
    ;

polyphonicBlock
    : LANGLE LANGLE voice (BAR voice)* RANGLE RANGLE
    ;

directive
    : bpmDirective
    | tempoDirective
    | characterDirective
    | sectionDirective
    | partDirective
    | dynamicsDirective
    ;

bpmDirective
    : AT BPM (EQUALS)? INT (ARROW INT duration?)?
    ;

tempoDirective
    : AT TEMPO (EQUALS)? directiveValue
    ;

characterDirective
    : AT CHARACTER (EQUALS)? directiveValue
    ;

sectionDirective
    : AT SECTION (EQUALS)? directiveValue
    ;

partDirective
    : AT PART (EQUALS)? directiveValue
    ;

dynamicsDirective
    : AT DYNAMICS (EQUALS)? dynamicsValue
    | AT CRESC (TO dynamicsValue)?
    | AT DIM (TO dynamicsValue)?
    ;

dynamicsValue
    : STRING
    | IDENT
    | DYNAMICS_LEVEL
    ;

directiveValue
    : STRING
    | IDENT
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

// Directives
AT          : '@' ;
ARROW       : '->' ;
EQUALS      : '=' ;

// Brackets
LBRACKET    : '[' ;
RBRACKET    : ']' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
LBRACE      : '{' ;
RBRACE      : '}' ;
LANGLE      : '<' ;
RANGLE      : '>' ;

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

// Quoted strings for directive values
STRING      : '"' (~["\r\n])* '"' ;

// Directive keywords
BPM         : [bB][pP][mM] ;
TEMPO       : [tT][eE][mM][pP][oO] ;
CHARACTER   : [cC][hH][aA][rR][aA][cC][tT][eE][rR] ;
SECTION     : [sS][eE][cC][tT][iI][oO][nN] ;
PART        : [pP][aA][rR][tT] ;
DYNAMICS    : [dD][yY][nN][aA][mM][iI][cC][sS] ;
CRESC       : [cC][rR][eE][sS][cC] | [cC][rR][eE][sS][cC][eE][nN][dD][oO] ;
DIM         : [dD][iI][mM] | [dD][iI][mM][iI][nN][uU][eE][nN][dD][oO] ;
TO          : [tT][oO] ;

// Dynamics levels (must come before IDENT to have priority)
DYNAMICS_LEVEL
    : [pP][pP][pP][pP]    // pppp
    | [pP][pP][pP]        // ppp
    | [pP][pP]            // pp
    | [pP]                // p
    | [mM][pP]            // mp
    | [mM][fF]            // mf
    | [fF]                // f
    | [fF][fF]            // ff
    | [fF][fF][fF]        // fff
    | [fF][fF][fF][fF]    // ffff
    | [sS][fF]            // sf (sforzando)
    | [sS][fF][zZ]        // sfz
    | [fF][pP]            // fp (forte-piano)
    | [rR][fF]            // rf (rinforzando)
    ;

// Ornament keywords (must come before IDENT)
TRILL           : 'tr' | 'trill' ;
MORDENT         : 'mord' | 'mordent' ;
TURN            : 'turn' ;
APPOGGIATURA    : 'app' | 'appo' ;

// Identifiers for directive values (lowercase/underscore only to avoid conflicts with PITCH_NAME + accidentals)
IDENT       : [a-z_][a-z_]* ;

// Whitespace
WS          : [ \t\r\n,;]+ -> skip ;
