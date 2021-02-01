grammar RadAsm2;

function
    : FUNCTION END
    ;

/* Keywords */
VAR     : 'var' ;
VMCNT   : 'vmcnt' ;
EXPCNT  : 'expcnt' ;
LGKMCNT : 'lgkmcnt' ;
HWREG   : 'hwreg' ;
SENDMSG : 'sendmsg' ;
ASIC    : 'asic' ;
TYPE    : 'type' ;
ASSERT  : 'assert' ;
SHADER  : 'shader' ;
RETURN  : 'return' ;

FUNCTION: 'function' ;

IF      : 'if' ;
ELSIF   : 'elsif' ;
ELSE    : 'else' ;

FOR     : 'for' ;
WHILE   : 'while' ;

END     : 'end' ;

REPEAT  : 'repeat' ;
UNTIL   : 'until' ;

/* Preprocessor Keywords */
PP_INCLUDE  : '#include' ;
PP_DEFINE   : '#define'  ;
PP_UNDEF    : '#undef'   ;
PP_PRAGMA   : '#pragma'  ;
PP_ERROR    : '#error'   ;
PP_WARNING  : '#warning' ;
PP_IMPORT   : '#import'  ;
PP_LINE     : '#line'    ;
PP_INCLUDE_NEXT : '#include_next' ;

PP_IF       : '#if'      ;
PP_IFDEF    : '#ifdef'   ;
PP_IFNDEF   : '#ifndef'  ;

PP_ELSE     : '#else'    ;
PP_ELSIF    : '#elsif'   ;
PP_ELIF     : '#elif'    ;

PP_ENDIF    : '#endif'   ;

/* Expression-operator symbols */
EQ      : '='   ;
LT      : '<'   ;
LE      : '<='  ;
EQEQ    : '=='  ;
NE      : '!='  ;
GE      : '>='  ;
GT      : '>'   ;
ANDAND  : '&&'  ;
OROR    : '||'  ;
NOT     : '!'   ;
TILDE   : '~'   ;
PLUS    : '+'   ;
MINUS   : '-'   ;
STAR    : '*'   ;
SLASH   : '/'   ;
PERCENT : '%'   ;
CARET   : '^'   ;
AND     : '&'   ;
OR      : '|'   ;
SHL     : '<<'  ;
SHR     : '>>'  ;

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
LSQUAREBRACKET   : '[' ;
RSQUAREBRACKET   : ']' ;
LCURVEBRACKET    : '{' ;
RCURVEBRACKET    : '}' ;

// Literals

CONSTANT
    : INT_CONSTANT
    ;

fragment
INT_CONSTANT
    : [+-]? '0' [bB] [0-1]+
    | [+-]? '0' [oO] [0-7]+
    | [+-]? '0' [xX] [0-9a-fA-F]+
    | [+-]? [0-9] [0-9]*
    ;

STRING_LITERAL
    : '"' .*? ('"'| EOF)
    ;

IDENTIFIER
    : '.'? [a-zA-Z_] [a-zA-Z0-9_]*
    ;

WHITESPACE
    : [ \t]+
    ;

EOL
    : '\r'? '\n'
    ;

LINE_COMMENT
    : '//' ~[\r\n]*
    ;

BLOCK_COMMENT
    : '/*' .*? ('*/' | EOF)
    ;

UNKNOWN
    : ~[ \t\r\n,:;()[\]{}=<>!&|~+*%^]+
    ;
