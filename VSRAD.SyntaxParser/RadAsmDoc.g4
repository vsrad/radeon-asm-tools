grammar RadAsmDoc;

rule    : LET  ;

LET     : 'let';

IDENTIFIER
    : [a-zA-Z] [a-zA-Z0-9_]*
    ;

WHITESPACE
    : [ \t\r\n]+
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

UNKNOWN
    : ~[ \t\r\n]+
    ;
