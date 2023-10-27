using Microsoft;
using System;
using System.Collections.Generic;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public IReadOnlyList<BreakInstance> BreakInstances { get; }
        public bool IsStepping { get; }
        public bool IsSuccessful { get; }

        public ExecutionCompletedEventArgs(IReadOnlyList<BreakInstance> breakInstances, bool isStepping, bool isSuccessful)
        {
            Assumes.True(breakInstances != null && breakInstances.Count != 0, "At least one break instance must be provided for the VS debugger.");

            BreakInstances = breakInstances;
            IsStepping = isStepping;
            IsSuccessful = isSuccessful;
        }
    }

    public sealed class BreakInstance
    {
        public uint InstanceId { get; }

        /// The first element of the list is the top frame on the call stack.
        public IReadOnlyList<(string Name, string SourcePath, uint SourceLine)> CallStack { get; }

        public BreakInstance(uint instanceId, IReadOnlyList<(string Name, string SourcePath, uint SourceLine)> callStack)
        {
            Assumes.True(callStack != null && callStack.Count != 0, "Call stack for the VS debugger must contain at least one frame.");

            InstanceId = instanceId;
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
