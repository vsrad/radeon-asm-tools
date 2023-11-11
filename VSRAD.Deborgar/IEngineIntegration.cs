using Microsoft;
using System;
using System.Collections.Generic;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public IReadOnlyList<BreakLocation> BreakLocations { get; }
        public bool IsStepping { get; }
        public bool IsSuccessful { get; }

        public ExecutionCompletedEventArgs(IReadOnlyList<BreakLocation> breakLocations, bool isStepping, bool isSuccessful)
        {
            Assumes.True(breakLocations != null && breakLocations.Count != 0, "At least one break location must be provided for the VS debugger.");

            BreakLocations = breakLocations;
            IsStepping = isStepping;
            IsSuccessful = isSuccessful;
        }
    }

    public sealed class BreakLocation
    {
        public uint LocationId { get; }

        /// The first element of the list is the top frame on the call stack.
        public IReadOnlyList<(string Name, string SourcePath, uint SourceLine)> CallStack { get; }

        public BreakLocation(uint locationId, IReadOnlyList<(string Name, string SourcePath, uint SourceLine)> callStack)
        {
            Assumes.True(callStack != null && callStack.Count != 0, "Call stack for the VS debugger must contain at least one frame.");

            LocationId = locationId;
            CallStack = callStack;
        }
    }

    public interface IEngineIntegration
    {
        void Execute(bool step);
        void CauseBreak();
        event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;
    }
}
