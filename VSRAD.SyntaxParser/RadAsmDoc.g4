grammar RadAsmDoc;

rule    : LET  ;

LET     : 'let';

/* "Structural symbols" */

COMMA      : ',' ;
COLON      : ':' ;

IDENTIFIER
    : [a-zA-Z] [a-zA-Z0-9_]*
    ;

WHITESPACE
    : [ \t]+
    ;

EOL
    : '\r'? '\n'
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

UNKNOWN
    : ~[ \t\r\n,:]+
    ;
