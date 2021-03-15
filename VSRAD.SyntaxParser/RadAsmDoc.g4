grammar RadAsmDoc;

rule    : LET  ;

WHITESPACE
    : [ \t]+
    ;

EOL
    : '\r'? '\n'
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

LET : 'let';

/* "Structural symbols" */

COMMA      : ',' ;
COLON      : ':' ;

IDENTIFIER
    : [a-zA-Z] [a-zA-Z0-9_]*
    ;

UNKNOWN
    : ~[ \t\r\n,:]+
    ;
