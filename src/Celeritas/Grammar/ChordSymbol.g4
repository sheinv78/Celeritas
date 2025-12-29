grammar ChordSymbol;

/*
 * Chord Symbol Grammar for Celeritas
 * Supports: roots with accidentals, triad qualities, extensions, alterations,
 * parenthesized alteration lists, slash bass, and polychords.
 * Examples:
 *   C, Am, G7, Dmaj7, F#m7b5, C7(b9,#11), CΔ9, C°7, Cø7, D|C, C/E
 */

// Parser rules

symbol
    : polychord EOF
    ;

polychord
    : chord (POLYSEP chord)+
    | chord
    ;

chord
    : root=note chordSuffix* bass=slashBass?
    ;

slashBass
    : SLASH note
    ;

chordSuffix
    : quality
    | extension
    | alteration
    | addTone
    | omitTone
    | modifier
    | group
    ;

group
    : LPAREN groupItem (COMMA groupItem)* RPAREN
    ;

groupItem
    : quality extension?
    | extension
    | alteration
    | addTone
    | omitTone
    | modifier
    ;

quality
    : MAJ
    | MIN
    | DIM
    | AUG
    | SUS
    | HALF_DIM
    | DELTA
    ;

extension
    : INT
    | SIX_NINE
    ;

addTone
    : ADD (INT | POWER)
    ;

omitTone
    : (NO | OMIT) (INT | POWER)
    ;

alteration
    : accidental (INT | POWER)
    ;

modifier
    : ALT
    | POWER
    ;

note
    : PITCH_NAME accidental?
    ;

accidental
    : SHARP
    | FLAT
    | MINUS
    ;

// Lexer rules

POLYSEP     : '|' ;
SLASH       : '/' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
COMMA       : ',' ;

SHARP       : '#' | '♯' ;
FLAT        : 'b' | '♭' ;
MINUS       : '-' ;

ADD         : [aA][dD][dD] ;
NO          : [nN][oO] ;
OMIT        : [oO][mM][iI][tT] ;

MAJ         : [mM][aA][jJ] | [mM][aA][jJ][oO][rR] ;
MIN         : [mM][iI][nN] | [mM][iI][nN][oO][rR] | 'm' ;
DIM         : [dD][iI][mM] | 'o' | '°' ;
AUG         : [aA][uU][gG] | '+' ;
SUS         : [sS][uU][sS] ;
HALF_DIM    : 'ø' | [hH][aA][lL][fF][dD][iI][mM] ;
DELTA       : 'Δ' ;

ALT         : [aA][lL][tT] ;
POWER       : '5' ;

SIX_NINE    : '6/9' | '69' ;

PITCH_NAME  : [A-G] ;
INT         : [0-9]+ ;

WS          : [ \t\r\n]+ -> skip ;
