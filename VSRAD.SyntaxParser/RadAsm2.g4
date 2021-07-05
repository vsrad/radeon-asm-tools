grammar RadAsm2;

function
    : FUNCTION END
    ;

/* Keywords */
BUILDIN_FUNCTION
    : 'vmcnt'
    | 'expcnt'
    | 'hwreg'
    | 'sendmsg'
    | 'asic'
    | 'type'
    | 'assert'
    | 'shader'
    | 'len'
    | 'abs'
    | 'ones'
    | 'zeros'
    | 'zeroes'
    | 'user_sgpr_count'
    | 'sgpr_count'
    | 'vgpr_count'
    | 'block_size'
    | 'group_size'
    | 'tg_size_en'
    | 'tgid_x_en'
    | 'tgid_y_en'
    | 'tgid_z_en'
    | 'alloc_lds'
    | 'wave_size'
    | 'assigned'
    | 'label'
    | 'print'
    ;

SHADER      : 'shader' ;
LABEL       : 'label' ;

VAR         : 'var' ;
RETURN      : 'return' ;
FUNCTION    : 'function' ;

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
PLUSEQ  : '+='  ;
LE      : '<='  ;
EQEQ    : '=='  ;
NE      : '!='  ;
GE      : '>='  ;
SHL     : '<<'  ;
SHR     : '>>'  ;
LOGOR   : '||'  ;
LOGAND  : '&&'  ;
LOGXOR  : '^^'  ;
LOGNOT  : '!'   ;
REDMIN  : '<:'  ;
REDMAX  : '>:'  ;
EQ      : '='   ;
LT      : '<'   ;
GT      : '>'   ;
PLUS    : '+'   ;
MINUS   : '-'   ;
PROD    : '*'   ;
DIV     : '/'   ;
MOD     : '%'   ;
BITOR   : '|'   ;
BITAND  : '&'   ;
BITXOR  : '^'   ;
BITNOT  : '~'   ;

/* "Structural symbols" */

COMMA      : ',' ;
SEMI       : ';' ;
COLON      : ':' ;
QUEST      : '?' ;
SHARP      : '#' ;
LPAREN     : '(' ;
RPAREN     : ')' ;
LSQUAREBRACKET   : '[' ;
RSQUAREBRACKET   : ']' ;
LCURVEBRACKET    : '{' ;
RCURVEBRACKET    : '}' ;

// Literals

CONSTANT
    : INT_CONSTANT
    | FLOAT_CONSTANT
    ;

fragment
INT_CONSTANT
    : [+-]? '0' [bB] [0-1]+
    | [+-]? '0' [oO] [0-7]+
    | [+-]? '0' [xX] [0-9a-fA-F]+
    | [+-]? [0-9] [0-9]*
    ;

fragment
FLOAT_CONSTANT
    : [+-]? ([0-9]*[.]) [0-9]+
    ;

STRING_LITERAL
    : '"' .*? ('"'| EOF)
    ;

IDENTIFIER
    : '.'? [a-zA-Z_] [a-zA-Z0-9_]*
    ;

LINE_COMMENT
    : '//' ~[\r\n]*
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
    : ~[ \t\r\n,:;#?()[\]{}=<>!&|~+\\-*%^/]+
    ;