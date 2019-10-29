#!/bin/bash
set -o pipefail

clang="/opt/rocm/opencl/bin/x86_64/clang -mno-code-object-v3"

if [ -n "$ASM_DBG_BUF_ADDR" ]
then
	cat > tmp_dir/tmp_gcn_src.s
	perl $DEBUGGER_DIR/breakpoint_gcnasm.pl tmp_dir/tmp_gcn_src.s -w "$BREAKPOINT_SCRIPT_WATCHES" $BREAKPOINT_SCRIPT_OPTIONS
	if [ $? -ne 0 ]; then
		echo "ERROR: breakpoint_gcnasm preprocessing failed"
		exit -1
	fi
	tmp_breakpoint=tmp_dir/tmp_gcn_breakpoint_pl.s
	cat "$tmp_breakpoint" | ${clang} $@
else
	cat | ${clang} $@
fi

exit $?
