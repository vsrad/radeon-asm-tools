my $usage = << "ENDOFUSAGE";
Usage: $0 gcnasm_source [<options>]
    gcnasm_source          the source s file
    options
        -l <line>       line number to break (mandatory)
        -o <file>       output to the <file> rather than STDOUT
        -w <watches>    extra watches supplied colon separated in quotes;
                        watch type can be present
                        (like -w "a:b:c:i")
        -e <command>    instruction to insert after the injection
                        instead of "s_endpgm"; if "NONE" is supplied
                        then none is added
        -a              use "auto's"; the script would not look for auto
                        watch variables (kicks in by itself if -w is empty)
        -t <value>      target value for the loop counter
        -h              print usage information
ENDOFUSAGE

use Text::Balanced qw {extract_bracketed};
use List::MoreUtils qw(uniq);

my $args    = 0;
my $fo;
my @watches;
my $endpgm  = "s_endpgm";
my @lines;
my $line    = 0;
my $condit  = "1";
my $output  = 0;
my $target;

while (scalar @ARGV) {
    my $str = shift @ARGV;
    if ($str eq "-l")   {   $line    =            shift @ARGV;  next;   }
    if ($str eq "-o")   {   $_ = shift @ARGV;
                            open $fo, '>', $_ || die "$usage\nCould not open '$_': $!\n";
                            $output  = 1;                       next;   }
    if ($str eq "-w")   {   @watches = split /:/, shift @ARGV;  next;   }
    if ($str eq "-e")   {   $endpgm  =            shift @ARGV;  next;   }
    if ($str eq "-t")   {   $target  =            shift @ARGV;  next;   }
    if ($str eq "-h")   {   print "$usage\n";                   last;   }
	
	open my $df, '<', $str || die "$usage\nCould not open '$str: $!";
    push @lines, <$df>;
    close $df;
}

die $usage unless scalar (@lines) && $line; # && scalar (@inject) ;

my @done = @watches;

my $n_var   = scalar @done;
my $to_dump = join ', ', @done;

my $loopcounter = << "LOOPCOUNTER";
        s_cbranch_scc1 debug_dumping_loop_counter_lab1_\\\@
        s_add_u32       s[dbg_counter], s[dbg_counter], 1
        s_cmp_eq_u32    s[dbg_counter], $target
        s_cbranch_scc0  debug_dumping_loop_counter_lab_\\\@
debug_dumping_loop_counter_lab1_\\\@\:
        s_add_u32       s[dbg_counter], s[dbg_counter], 1
        s_cmp_lt_u32    s[dbg_counter], $target
        s_cbranch_scc1  debug_dumping_loop_counter_lab_\\\@

LOOPCOUNTER
$loopcounter = "" unless $target;

my $dump_vars = "$done[0]";
for (my $i = 1; $i < scalar @done; $i += 1) {
	$dump_vars = "$dump_vars, $done[$i]";
}

$bufsize =  defined $ENV{'ASM_DBG_BUF_SIZE'} ? $ENV{'ASM_DBG_BUF_SIZE'} : 1048576; # 1 MB
$bufaddr =  defined $ENV{'ASM_DBG_BUF_ADDR'} ? $ENV{'ASM_DBG_BUF_ADDR'} : 0;

my $plug_macro = << "PLUGMACRO";
.ifndef M_DEBUG_PLUG_DEFINED
.set M_DEBUG_PLUG_DEFINED,1

//n_var    = $n_var
//vars     = $dump_vars

.macro m_dbg_gpr_alloc
	.VGPR_ALLOC dbg_vtmp

	.if .SGPR_NEXT_FREE % 4
		.SGPR_ALLOC_ONCE dbg_soff
	.endif

	.if .SGPR_NEXT_FREE % 4
		.SGPR_ALLOC_ONCE dbg_counter
	.endif

	.if .SGPR_NEXT_FREE % 4
		.SGPR_ALLOC_ONCE dbg_stmp
	.endif

	.SGPR_ALLOC dbg_srd, 4

	.SGPR_ALLOC_ONCE dbg_soff
	.SGPR_ALLOC_ONCE dbg_counter
	.SGPR_ALLOC_ONCE dbg_stmp
	M_DBG_GPR_ALLOC_INSTANTIATED = 1
.endm

// dbg_ptr_off should be defined in main programm
.macro m_dbg_init gidx
	debug_init_start:

	s_mul_i32 s[dbg_soff], s[\\gidx], 1 //waves_in_group
	v_readfirstlane_b32 s[dbg_counter], v[tid]
	s_lshr_b32 s[dbg_counter], s[dbg_counter], 6 //wave_size_log2
	s_add_u32 s[dbg_soff], s[dbg_soff], s[dbg_counter]
	s_mul_i32 s[dbg_soff], s[dbg_soff], 64 * (1 + $n_var) * 4
	
	s_mov_b32 s[dbg_counter], 0
	M_DBG_INIT_INSTANTIATED = 1
	debug_init_end:
.endm

.macro m_debug_plug vars:vararg
		.ifndef M_DBG_INIT_INSTANTIATED
			.error "Debug macro is not instantiated (m_dbg_init)"
		.endif
		.ifndef M_DBG_GPR_ALLOC_INSTANTIATED
			.error "Debug macro is not instantiated (m_dbg_gpr_alloc)"
		.endif
//  debug dumping dongle begin
$loopcounter
		
		v_save   = dbg_vtmp
		s_srd    = dbg_srd
		s_grp    = dbg_soff

		// construct dbg_srd
		s_mov_b32 s[dbg_srd+0], 0xFFFFFFFF & $bufaddr
		s_mov_b32 s[dbg_srd+1], ($bufaddr >> 32)
		s_mov_b32 s[dbg_srd+3], 0x804fac
		// TODO: change n_var to buffer size
		s_add_u32 s[dbg_srd+1], s[dbg_srd+1], (($n_var + 1) << 18)

		s_mov_b32 s[dbg_stmp], exec_lo
		s_mov_b32 s[dbg_counter], exec_hi
		v_mov_b32       v[v_save], 0x7777777
		v_writelane_b32 v[v_save], s[s_srd+0], 1
		v_writelane_b32 v[v_save], s[s_srd+1], 2
		v_writelane_b32 v[v_save], s[s_srd+2], 3
		v_writelane_b32 v[v_save], s[s_srd+3], 4
		s_getreg_b32    s[dbg_stmp], hwreg(4, 0, 32)   //  fun stuff
		v_writelane_b32 v[v_save], s[dbg_stmp], 5
		s_getreg_b32    s[dbg_stmp], hwreg(5, 0, 32)
		v_writelane_b32 v[v_save], s[dbg_stmp], 6
		s_getreg_b32    s[dbg_stmp], hwreg(6, 0, 32)
		v_writelane_b32 v[v_save], s[dbg_stmp], 7
		v_writelane_b32 v[v_save], exec_lo, 8
		v_writelane_b32 v[v_save], exec_hi, 9
		s_mov_b64 exec, -1

		buffer_store_dword v[v_save], off, s[s_srd:s_srd+3], s[s_grp] offset:0

		//var to_dump = [$to_dump]
		.if $n_var > 0
			buf_offset\\\@ = 0
			.irp var, \\vars
				buf_offset\\\@ = buf_offset\\\@ + 4
				v_mov_b32 v[v_save], \\var
				buffer_store_dword v[v_save], off, s[s_srd:s_srd+3], s[s_grp] offset:0+buf_offset\\\@
			.endr
		.endif
		
		s_mov_b32 exec_lo, s[dbg_stmp]
		s_mov_b32 exec_hi, s[dbg_counter]

		$endpgm
	debug_dumping_loop_counter_lab_\\\@\:
//  debug dumping_dongle_end_:
.endm
.endif
PLUGMACRO

my $insert  = << "PREAMBLE";
m_debug_plug $dump_vars
PREAMBLE

my @m = @lines [0..$line-1] if $line > 0;
my @merge = ($plug_macro, @m, $insert, @lines [$line..scalar @lines - 1]);
foreach(@merge) {
	$_ = qq[m_dbg_gpr_alloc\n$_] if $_ =~ /\.GPR_ALLOC_END/;
	$_ .= qq(\nm_dbg_init gid_x\n) if $_ =~ /KERNEL_PROLOG/;
}

print $fo @merge;
