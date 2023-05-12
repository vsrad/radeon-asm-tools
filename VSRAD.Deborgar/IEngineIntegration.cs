using Microsoft;
using System;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public string File { get; }
        public uint[] Lines { get; }
        public bool IsStepping { get; }
        public bool IsSuccessful { get; }

        public ExecutionCompletedEventArgs(string file, uint[] lines, bool isStepping, bool isSuccessful)
        {
            Assumes.True(lines != null && lines.Length != 0, "At least one break line must be provided for the VS debugger.");

            File = file;
            Lines = lines;
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
