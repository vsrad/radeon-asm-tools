grammar RadAsm2;

function
    : FUNCTION END
    ;

/* Keywords */
BUILTIN_FUNCTION
    : 'vmcnt'
    | 'expcnt'
    | 'lgkmcnt'
    | 'hwreg'
    | 'sendmsg'
    | 'asic'
    | 'type'
    | 'assert'
    | 'len'
    | 'lit'
    | 'data'
    | 'abs'
    | 'abs_lo'
    | 'abs_hi'
    | 'neg'
    | 'neg_lo'
    | 'neg_hi'
    | 'sel_lo'
    | 'sel_hi'
    | 'sel_hi_lo'
    | 'sel_lo_hi'
    | 'raw_bits'
    | 'get_dword_offset'
    | 'ones'
    | 'zeros'
    | 'zeroes'
    | 'trap_present'
    | 'user_sgpr_count'
    | 'sgpr_count'
    | 'vgpr_count'
    | 'block_size'
    | 'group_size'
    | 'group_size3d'
    | 'tidig_comp_cnt'
    | 'tg_size_en'
    | 'tgid_x_en'
    | 'tgid_y_en'
    | 'tgid_z_en'
    | 'wave_cnt_en'
    | 'scratch_en'
    | 'oc_lds_en'
    | 'z_export_en'
    | 'stencil_test_export_en'
    | 'stencil_op_export_en'
    | 'mask_export_en'
    | 'covmask_export_en'
    | 'mrtz_export_format'
    | 'kill_used'
    | 'alloc_lds'
    | 'wave_size'
    | 'assigned'
    | 'print'
    | 'set_ps'
    | 'set_vs'
    | 'set_gs'
    | 'set_es'
    | 'set_hs'
    | 'set_ls'
    | 'set_cs'
    | 'load_collision_waveid'
    | 'load_intrawave_collision'
    | 'align'
    | 'label_offset'
    | 'label_diff'
    | 'label_diff_eq'
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

fragment
SYMBOL_IDENTIFIER
    : '.'? [a-zA-Z_] [a-zA-Z0-9_]*
    ;

CLOSURE_IDENTIFIER
    : '#' SYMBOL_IDENTIFIER
    ;

IDENTIFIER
    : SYMBOL_IDENTIFIER
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
    : ~[ \t\r\n,:;?()[\]{}=<>!&|~+\\-*%^/]+
    ;