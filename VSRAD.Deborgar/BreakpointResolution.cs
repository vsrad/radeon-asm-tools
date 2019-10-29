using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Runtime.InteropServices;
using static VSRAD.Deborgar.BreakpointManager;

namespace VSRAD.Deborgar
{
    public sealed class BreakpointResolution : IDebugBreakpointResolution2
    {
        public SourceFileLineContext Context
        {
            get
            {
                var position = BreakpointPosition();
                return new SourceFileLineContext(_projectPath, position);
            }
        }

        private readonly IDebugProgram2 _program;
        private readonly IDebugDocumentPosition2 _documentInfo;
        private readonly GetProjectRelativePath _getProjectRelativePath;
        private readonly string _projectPath;

        public BreakpointResolution(IDebugProgram2 program, GetProjectRelativePath getProjectRelativePath, IDebugDocumentPosition2 documentInfo)
        {
            _program = program;
            _documentInfo = documentInfo;
            _getProjectRelativePath = getProjectRelativePath;

            ErrorHandler.ThrowOnFailure(_documentInfo.GetFileName(out var absoluteSourcePath));
            _projectPath = _getProjectRelativePath(absoluteSourcePath);
        }

        TEXT_POSITION BreakpointPosition()
        {
            var startPosition = new TEXT_POSITION[1];
            var endPosition = new TEXT_POSITION[1];

            ErrorHandler.ThrowOnFailure(_documentInfo.GetRange(startPosition, endPosition));
            return startPosition[0];
        }

        int IDebugBreakpointResolution2.GetBreakpointType(enum_BP_TYPE[] pBPType)
        {
            pBPType[0] = enum_BP_TYPE.BPT_CODE;
            return VSConstants.S_OK;
        }

        int IDebugBreakpointResolution2.GetResolutionInfo(enum_BPRESI_FIELDS dwFields, BP_RESOLUTION_INFO[] pBPResolutionInfo)
        {
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION) != 0)
            {
                pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_BPRESLOCATION;
                pBPResolutionInfo[0].bpResLocation = new BP_RESOLUTION_LOCATION
                {
                    bpType = (uint)enum_BP_TYPE.BPT_CODE,
                    // Taken from the engine sample
                    unionmember1 = Marshal.GetComInterfaceForObject(Context, typeof(IDebugCodeContext2))
                };
            }
            if ((dwFields & enum_BPRESI_FIELDS.BPRESI_PROGRAM) != 0)
            {
                pBPResolutionInfo[0].dwFields |= enum_BPRESI_FIELDS.BPRESI_PROGRAM;
                pBPResolutionInfo[0].pProgram = _program;
            }

            return VSConstants.S_OK;
        }
    }
}
