grammar RadAsm;

function
    : MACRO ENDM
    ;

/* Keywords */
TEXT    : '.text' ;
SET     : '.set' ;
MACRO   : '.macro' ;
ENDM    : '.endm' ;
IF      : '.if' ;
IFDEF   : '.ifdef' ;
ENDIF   : '.endif' ;

/* Expression-operator symbols */
EQ      : '=' ;
LT      : '<' ;
LE      : '<=' ;
EQEQ    : '==' ;
NE      : '!=' ;
GE      : '>=' ;
GT      : '>' ;
ANDAND  : '&&' ;
OROR    : '||' ;
NOT     : '!' ;
TILDE   : '~' ;
PLUS    : '+' ;
MINUS   : '-' ;
STAR    : '*' ;
SLASH   : '/' ;
PERCENT : '%' ;
CARET   : '^' ;
AND     : '&' ;
OR      : '|' ;
SHL     : '<<' ;
SHR     : '>>' ;

BINOP
    : PLUS
    | SLASH
    | MINUS
    | STAR
    | PERCENT
    | CARET
    | AND
    | OR
    | SHL
    | SHR
    ;

/* "Structural symbols" */

COMMA      : ',' ;
SEMI       : ';' ;
COLON      : ':' ;
LPAREN     : '(' ;
RPAREN     : ')' ;
LBRACKET   : '[' ;
RBRACKET   : ']' ;
LBRACE     : '{' ;
RBRACE     : '}' ;
POUND      : '#';
DOLLAR     : '$' ;
UNDERSCORE : '_' ;

// Literals



CONSTANT
    : INT_CONSTANT
    ;

fragment
INT_CONSTANT
    : '0' [bB] [0-1]+
    | '0' [oO] [0-7]+
    | '0' [xX] [0-9a-fA-F]+
    | [1-9] [0-9]*
    ;

STRING_LITERAL
    : '"' .*? ('"'| EOF)
    ;

WHITESPACE
    : [ \t\r\n]+
    ;

LINE_COMMENT
    : '//' ~[\r\n]*
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

UNKNOWN
    : ~[ \t\r\n]+
    ;
