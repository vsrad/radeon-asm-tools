using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace VSRAD.Deborgar
{
    public interface IBreakpointManager
    {
        IDebugPendingBreakpoint2 TryCreateBreakpoint(IDebugProgram2 program, IDebugBreakpointRequest2 request);
        Breakpoint[] GetBreakpointsByLines(string file, uint[] lines);
        uint[] GetBreakpointLines(string file);
        uint GetNextBreakpointLine(string file, uint previousLine);
    }

    public sealed class BreakpointManager : IBreakpointManager
    {
        public delegate void OnBreakpointBound(Breakpoint breakpoint);

        sealed class SourceFileState
        {
            internal List<Breakpoint> Breakpoints { get; } = new List<Breakpoint>();
            internal Breakpoint PreviousBreakpoint { get; set; }
        }

        private readonly Dictionary<string, SourceFileState> _sourceFileState = new Dictionary<string, SourceFileState>();

        private readonly OnBreakpointBound _onBreakpointBound;

        public BreakpointManager(OnBreakpointBound onBreakpointBound)
        {
            _onBreakpointBound = onBreakpointBound;
        }

        public IDebugPendingBreakpoint2 TryCreateBreakpoint(IDebugProgram2 program, IDebugBreakpointRequest2 request)
        {
            var requestInfo = new BP_REQUEST_INFO[1];
            ErrorHandler.ThrowOnFailure(request.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo));
            var bpLocation = requestInfo[0].bpLocation;
            if (bpLocation.bpLocationType != (uint)enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE)
                return null;

            var documentInfo = (IDebugDocumentPosition2)Marshal.GetObjectForIUnknown(bpLocation.unionmember2);
            return new Breakpoint(this, request, program, documentInfo);
        }

        public void AddBreakpoint(Breakpoint breakpoint)
        {
            var fileState = GetSourceFileState(breakpoint.SourceContext.SourcePath);
            fileState.Breakpoints.Add(breakpoint);

            _onBreakpointBound(breakpoint);
        }

        public void RemoveBreakpoint(Breakpoint breakpoint)
        {
            var fileState = GetSourceFileState(breakpoint.SourceContext.SourcePath);
            fileState.Breakpoints.Remove(breakpoint);
        }

        public Breakpoint[] GetBreakpointsByLines(string file, uint[] lines) =>
            GetSourceFileState(file).Breakpoints.Where((b) => lines.Contains(b.SourceContext.LineNumber)).ToArray();

        public uint[] GetBreakpointLines(string file) =>
            GetSourceFileState(file).Breakpoints.Select((b) => b.SourceContext.LineNumber).ToArray();

        public uint GetNextBreakpointLine(string file, uint previousLine)
        {
            var fileState = GetSourceFileState(file);
            if (fileState.Breakpoints.Count == 0)
                // No breakpoints set but we need to pass one to the debugger anyway,
                // so we pick the end of the program as the implicit "default" breakpoint
                return (uint)File.ReadLines(file).Count() - 1;

            var breakpoints = fileState.Breakpoints.OrderBy(bp => bp.SourceContext.LineNumber);
            var nextBreakpoint = breakpoints.FirstOrDefault(bp => bp.SourceContext.LineNumber > previousLine)
                              ?? breakpoints.First();
            fileState.PreviousBreakpoint = nextBreakpoint;
            return nextBreakpoint.SourceContext.LineNumber;
        }

        private SourceFileState GetSourceFileState(string file)
        {
            if (!_sourceFileState.TryGetValue(file, out var state))
            {
                state = new SourceFileState();
                _sourceFileState.Add(file, state);
            }
            return state;
        }
    }
}
