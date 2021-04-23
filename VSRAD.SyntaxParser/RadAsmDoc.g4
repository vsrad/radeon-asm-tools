grammar RadAsmDoc;

rule    : LET  ;

LET : 'let';

/* "Structural symbols" */

COMMA      : ',' ;
LCURVEBRACKET    : '{' ;
RCURVEBRACKET    : '}' ;

IDENTIFIER
    : [a-zA-Z] [a-zA-Z0-9_:[\]]*
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

WHITESPACE
    : [ \t]+
    ;

EOL
    : '\r'? '\n'
    ;

UNKNOWN
    : ~[ \t\r\n,{}]+
    ;
