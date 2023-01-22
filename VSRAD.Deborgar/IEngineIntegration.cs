using System;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public string File { get; }
        public uint[] Lines { get; }
        public bool IsStepping { get; }
        public bool CompletedSuccessfully { get; }

        public ExecutionCompletedEventArgs(string file, uint[] lines, bool isStepping, bool completedSuccessfully)
        {
            File = file;
            Lines = lines;
            IsStepping = isStepping;
            CompletedSuccessfully = completedSuccessfully;
        }
    }

    public interface IEngineIntegration
    {
        void Execute(bool step);
        void CauseBreak();
        event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;
    }
}
