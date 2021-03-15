#!/bin/bash -x

while getopts "l:f:o:w:t:a:p:" opt
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
	a) app_args=$OPTARG
	;;
	p) perl_args=$OPTARG
	;;
	esac
done

rm -rf tmp_dir
mkdir tmp_dir
tmp=tmp_dir/tmp_gcn_breakpoint_pl.s

num_watches=`echo "${watches}" | awk -F":" '{print NF}'`

export BREAKPOINT_SCRIPT_OPTIONS="-l $line -o $tmp -t $counter $perl_args"
export BREAKPOINT_SCRIPT_WATCHES="$watches"
export ASM_DBG_BUF_SIZE=4194304
export VADD_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )/../../"

"$VADD_DIR/build/gfx9/fp32_v_add" \
	-asm "$VADD_DIR/common/debugger/dbg_clang_wrapper.sh" \
	-s "$src_path" \
	-I "$VADD_DIR/gfx9/include" \
	-o "./tmp_dir/fp32_v_add.co" \
	-b "$dump_path" \
	-bsz "$ASM_DBG_BUF_SIZE" \
	$app_args

echo