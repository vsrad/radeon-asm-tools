using Microsoft;
using System;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public string File { get; }
        public (uint BreakLine, bool Resumable)[] Breakpoints { get; }
        public bool IsStepping { get; }
        public bool IsSuccessful { get; }

        public ExecutionCompletedEventArgs(string file, (uint BreakLine, bool Resumable)[] breakpoints, bool isStepping, bool isSuccessful)
        {
            Assumes.True(breakpoints != null && breakpoints.Length != 0, "At least one break line must be provided for the VS debugger.");

            File = file;
            Breakpoints = breakpoints;
            IsStepping = isStepping;
            IsSuccessful = isSuccessful;
        }
    }

    public interface IEngineIntegration
    {
        void Execute(bool step);
        void CauseBreak();
        event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;
    }
}
