#!/bin/bash
set -o pipefail

CLANG="/opt/rocm/llvm/bin/clang"

if [ -n "$ASM_DBG_BUF_ADDR" ]
then
	SRC_PATH="$TMPPATH/tmp_src.s"
	PLUG_PATH="$TMPPATH/tmp_breakpoint_pl.s"

	cat > $SRC_PATH
	perl $VADDPATH/common/debugger/breakpoint_gcnasm.pl $SRC_PATH -w "$BREAKPOINT_SCRIPT_WATCHES" -o $PLUG_PATH $BREAKPOINT_SCRIPT_OPTIONS
	if [ $? -ne 0 ]; then
		echo "ERROR: breakpoint_gcnasm preprocessing failed"
		exit -1
	fi

	cat $PLUG_PATH | ${CLANG} $@ -
else
	cat | ${CLANG} $@ -
fi

exit $?
