#!/bin/bash -x

while getopts "l:f:o:w:t:p:e:v:" opt
do
	echo "$opt $OPTARG"
	case "$opt" in
	l) line=$OPTARG
	;;
	f) src_path=$OPTARG
	;;
	o) dump_path=$OPTARG
	;;
	w) watches=$OPTARG
	;;
	t) counter=$OPTARG
	;;
	v) vdk_args=$OPTARG
	;;
	p) perl_args=$OPTARG
	;;
	esac
done

rm -rf tmp_dir
mkdir tmp_dir

num_watches=`echo "${watches}" | awk -F":" '{print NF}'`

tmp=tmp_dir/tmp_gcn_breakpoint_pl.s
export BREAKPOINT_SCRIPT_OPTIONS="-l $line -o $tmp -s 96 -r s0 -t $counter $perl_args"
export BREAKPOINT_SCRIPT_WATCHES="$watches"
export ASM_DBG_BUF_SIZE=4194304
export DEBUGGER_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

"$DEBUGGER_DIR/../../build/gfx9/fp32_v_add" \
	--clang "$DEBUGGER_DIR/dbg_clang_wrapper.sh" \
	--asm "$src_path" \
	--include "$DEBUGGER_DIR/include" \
	--output_path "./tmp_dir/fp32_v_add.co" \
	--debug_path "$dump_path" \
	--debug_size "$ASM_DBG_BUF_SIZE"

echo