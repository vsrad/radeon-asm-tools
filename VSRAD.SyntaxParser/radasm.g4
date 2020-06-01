grammar RadAsm;

function
    : MACRO ENDM
    ;

/* Keywords */
TEXT    : '.text'       ;
SET     : '.set'        ;
BYTE    : '.byte'       ;
SHORT   : '.short'      ;
LONG    : '.long'       ;
EXITM   : '.exitm'      ;
INCLUDE : '.include'    ;
ALTMAC  : '.altmacro'   ;
NOALTMAC: '.noaltmacro' ;
LOCAL   : '.local'      ;
LINE    : '.line'       ;
SIZE    : '.size'       ;
LN      : '.ln'         ;
NOPS    : '.nops'       ;
ERROR   : '.error'      ;
END     : '.end'        ;


MACRO   : '.macro' ;
ENDM    : '.endm' ;

IF      : '.if'         ;
IFDEF   : '.ifdef'      ;
IFNDEF  : '.ifndef'     ;
IFNOTDEF: '.ifnotdef'   ;
IFB     : '.ifb'        ;
IFC     : '.ifc'        ;
IFEQ    : '.ifeq'       ;
IFEQS   : '.ifeqs'      ;
IFGE    : '.ifge'       ;
IFGT    : '.ifgt'       ;
IFLE    : '.ifle'       ;
IFLT    : '.iflt'       ;
IFNB    : '.ifnb'       ;
IFNC    : '.ifnc'       ;
IFNE    : '.ifne'       ;
IFNES   : '.ifnes'      ;

ELSEIF  : '.elseif'     ;
ELSE    : '.else'       ;

ENDIF   : '.endif'      ;

REPT    : '.rept'       ;
ENDR    : '.endr'       ;
IRP     : '.irp'        ;
IRPC    : '.irpc'       ;
DEF     : '.def'        ;
ENDEF   : '.endef'      ;

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

/* HSA Keywords */
HSA_CO_VERSION      : '.hsa_code_object_version' ;
HSA_CO_ISA          : '.hsa_code_object_isa'     ;
AMD_HSA_KERNEL      : '.amdgpu_hsa_kernel'       ;
AMD_KERNEL_CODE     : '.amd_kernel_code_t'       ;
AMD_END_KERNEL_CODE : '.end_amd_kernel_code_t'   ;

STARTIF
    : IF
    | IFDEF
    | IFNDEF
    | IFNOTDEF
    | IFB
    | IFC
    | IFEQ
    | IFEQS
    | IFGE
    | IFGT
    | IFLE
    | IFLT
    | IFNB
    | IFNC
    | IFNE
    | IFNES
    ;

MIDDLEIF
    : ELSEIF
    | ELSE
    ;

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
LBRACKET   : '[' ;
RBRACKET   : ']' ;

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
    : [.\\_]? [a-zA-Z] [a-zA-Z0-9_]*
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
    : ~[ \t\r\n,:;()[\]=<>!&|~+*%^]+
    ;
