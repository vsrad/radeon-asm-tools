using System;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public string File { get; }
        public uint[] Lines { get; }
        public bool IsStepping { get; }

        public ExecutionCompletedEventArgs(string file, uint[] lines, bool isStepping)
        {
            File = file;
            Lines = lines;
            IsStepping = isStepping;
        }
    }

    public interface IEngineIntegration
    {
        void Execute(bool step);
        void CauseBreak();
        event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;
    }
}
