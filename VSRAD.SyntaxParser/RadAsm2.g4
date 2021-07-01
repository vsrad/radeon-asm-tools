grammar RadAsm2;

function
    : FUNCTION END
    ;

/* Keywords */
VMCNT       : 'vmcnt' ;
EXPCNT      : 'expcnt' ;
LGKMCNT     : 'lgkmcnt' ;
HWREG       : 'hwreg' ;
SENDMSG     : 'sendmsg' ;
ASIC        : 'asic' ;
TYPE        : 'type' ;
ASSERT      : 'assert' ;
SHADER      : 'shader' ;
LEN         : 'len' ;
ABS         : 'abs' ;
ONES        : 'ones' ;
ZEROS       : 'zeros' ;
ZEROES      : 'zeroes' ;
USGPR_COUNT : 'user_sgpr_count' ;
SGPR_COUNT  : 'sgpr_count' ;
VGPR_COUNT  : 'vgpr_count' ;
BLOCK_SIZE  : 'block_size' ;
GROUP_SIZE  : 'group_size' ;
TG_SIZE_EN  : 'tg_size_en' ;
TGID_X_EN   : 'tgid_x_en' ;
TGID_Y_EN   : 'tgid_y_en' ;
TGID_Z_EN   : 'tgid_z_en' ;
ALLOC_LDS   : 'alloc_lds' ;
WAVE_SIZE   : 'wave_size' ;
ASSIGNED    : 'assigned' ;
LABEL       : 'label' ;
PRINT       : 'print' ;

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