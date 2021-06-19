#!/bin/bash -x

USAGE="Usage: $0 -l line -f source_file_path -o debug_buffer_path -w watches -t counter [-a app_args] [-p perl_args]"

while getopts "l:f:o:w:t:a:p:" opt
do
	echo "$opt $OPTARG"
	case "$opt" in
	l) line=$OPTARG ;;
	f) src_path=$OPTARG ;;
	o) buffer_path=$OPTARG ;;
	w) watches=$OPTARG ;;
	t) counter=$OPTARG ;;
	a) app_args=$OPTARG ;;
	p) perl_args=$OPTARG ;;
	esac
done

[[ -z "$line" || -z "$src_path" || -z "$buffer_path" || -z "$watches" ]] && { echo $USAGE; exit 1; }

rm -rf tmp
mkdir tmp
export TMPPATH="tmp/"

SCRIPTPATH=`dirname $0`
export VADDPATH=`realpath $SCRIPTPATH/../../`

export BREAKPOINT_SCRIPT_OPTIONS="-l $line -t $counter $perl_args"
export BREAKPOINT_SCRIPT_WATCHES="$watches"

GFX=`/opt/rocm/bin/rocminfo | grep -om1 gfx9..`
CLANG="$VADDPATH/common/debugger/dbg_clang_wrapper.sh"
CLANG_ARGS="-x assembler -target amdgcn--amdhsa -mcpu=$GFX -I$VADDPATH/gfx9/include"

CO_PATH="$TMPPATH/fp32_v_add.co"
ASM_CMD="cat $src_path | $CLANG $CLANG_ARGS -o $CO_PATH"
DBG_BUF_SIZE=4194304

"$VADDPATH/build/gfx9/fp32_v_add" \
	-asm "$ASM_CMD"               \
	-c 	 "$CO_PATH"               \
	-b   "$buffer_path"           \
	-bsz "$DBG_BUF_SIZE"      	  \
	$app_args

echo