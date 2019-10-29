using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace VSRAD.Deborgar
{
    public interface IBreakpointManager
    {
        IDebugPendingBreakpoint2 TryCreateBreakpoint(IDebugProgram2 program, IDebugBreakpointRequest2 request);
        bool AnyBreakpointsSet(string projectFile);
        uint GetNextBreakpointLine(string file, uint currentBreakLine);
        bool TryGetBreakpointAtLine(string projectFile, uint line, out Breakpoint breakpoint);
    }

    public sealed class BreakpointManager : IBreakpointManager
    {
        public delegate uint GetFileLineCount(string projectFilePath);
        public delegate string GetProjectRelativePath(string absoluteFilePath);
        public delegate void OnBreakpointBound(Breakpoint breakpoint);

        sealed class SourceFileState
        {
            internal List<Breakpoint> Breakpoints { get; } = new List<Breakpoint>();
            internal Breakpoint PreviousBreakpoint { get; set; }
        }

        private readonly Dictionary<string, SourceFileState> _sourceFileState = new Dictionary<string, SourceFileState>();
        private readonly GetFileLineCount _getFileLineCount;
        private readonly GetProjectRelativePath _getProjectRelativePath;
        private readonly OnBreakpointBound _onBreakpointBound;

        public BreakpointManager(GetFileLineCount getFileLineCount, GetProjectRelativePath getProjectRelativePath,
                                 OnBreakpointBound onBreakpointBound)
        {
            _getFileLineCount = getFileLineCount;
            _getProjectRelativePath = getProjectRelativePath;
            _onBreakpointBound = onBreakpointBound;
        }

        public IDebugPendingBreakpoint2 TryCreateBreakpoint(IDebugProgram2 program, IDebugBreakpointRequest2 request)
        {
            var requestInfo = new BP_REQUEST_INFO[1];
            ErrorHandler.ThrowOnFailure(request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo));
            var bpLocation = requestInfo[0].bpLocation;

            var documentInfo = (IDebugDocumentPosition2)Marshal.GetObjectForIUnknown(bpLocation.unionmember2);

            if (bpLocation.bpLocationType == (uint)enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE)
            {
                var resolution = new BreakpointResolution(program, _getProjectRelativePath, documentInfo);
                return new Breakpoint(this, request, resolution);
            }

            return null;
        }

        public void AddBreakpoint(Breakpoint breakpoint)
        {
            var context = breakpoint.Resolution.Context;
            var fileState = GetSourceFileState(context.ProjectPath);
            fileState.Breakpoints.Add(breakpoint);

            _onBreakpointBound(breakpoint);
        }

        public void RemoveBreakpoint(Breakpoint breakpoint)
        {
            var context = breakpoint.Resolution.Context;
            var fileState = GetSourceFileState(context.ProjectPath);
            fileState.Breakpoints.Remove(breakpoint);
        }

        public bool TryGetBreakpointAtLine(string projectFile, uint line, out Breakpoint breakpoint)
        {
            if (_sourceFileState.TryGetValue(projectFile, out var fileState))
            {
                breakpoint = fileState.Breakpoints.Where(bp => bp.Resolution.Context.LineNumber == line).FirstOrDefault();
                if (breakpoint != null)
                    return true;
            }
            breakpoint = null;
            return false;
        }

        public uint GetNextBreakpointLine(string file, uint currentBreakLine)
        {
            var fileState = GetSourceFileState(file);

            /* The user may not set any breakpoints but we need to pass one to the debugger anyway,
             * so we pick the end of the program as the implicit "default" breakpoint */
            var defaultBreakpointLine = _getFileLineCount(file) - 1;
            Breakpoint breakpointByMinLine = null;
            foreach (var breakpoint in fileState.Breakpoints.OrderBy(bp => bp.Resolution.Context.LineNumber))
            {
                var lineNumber = breakpoint.Resolution.Context.LineNumber;
                if ((lineNumber > currentBreakLine || currentBreakLine == defaultBreakpointLine) && breakpoint != fileState.PreviousBreakpoint)
                {
                    fileState.PreviousBreakpoint = breakpoint;
                    return lineNumber;
                }
                breakpointByMinLine = (breakpointByMinLine == null || lineNumber < breakpointByMinLine.Resolution.Context.LineNumber) ? breakpoint : breakpointByMinLine;
            }
            fileState.PreviousBreakpoint = breakpointByMinLine;
            return (breakpointByMinLine != null) ? breakpointByMinLine.Resolution.Context.LineNumber : defaultBreakpointLine;
        }

        public bool AnyBreakpointsSet(string projectFile)
        {
            if (_sourceFileState.TryGetValue(projectFile, out var fileState))
            {
                return fileState.Breakpoints.Count > 0;
            }
            return false;
        }

        private SourceFileState GetSourceFileState(string projectFile)
        {
            if (!_sourceFileState.TryGetValue(projectFile, out var state))
            {
                state = new SourceFileState();
                _sourceFileState.Add(projectFile, state);
            }
            return state;
        }
    }
}
