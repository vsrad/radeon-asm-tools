grammar RadAsmDoc;

rule    : LET  ;

LET     : 'let';
TARGETS : 'RadAsmTargets';

/* "Structural symbols" */

COMMA           : ',' ;
LCURVEBRACKET   : '{' ;
RCURVEBRACKET   : '}' ;

IDENTIFIER
    : [a-zA-Z] [a-zA-Z0-9_:[\]]*
    ;

IDENTIFIER_LIST
    : LCURVEBRACKET WHITESPACE? (IDENTIFIER WHITESPACE? COMMA? WHITESPACE?)* RCURVEBRACKET
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

WHITESPACE
    : [ \t]+ -> skip
    ;

EOL
    : '\r'? '\n'
    ;

UNKNOWN
    : ~[ \t\r\n,{}]+
    ;