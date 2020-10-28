using System;

namespace VSRAD.Deborgar
{
    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public string File { get; }
        public uint[] Lines { get; }
        public bool IsStepping { get; }
        public bool IsSuccessful;

        public ExecutionCompletedEventArgs(string file, uint[] lines, bool isStepping, bool isSuccessful)
        {
            File = file;
            Lines = lines;
            IsStepping = isStepping;
            IsSuccessful = isSuccessful;
        }
    }

    public interface IEngineIntegration
    {
        void Execute(bool step);
        string GetActiveSourcePath();

        event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;
    }
}
