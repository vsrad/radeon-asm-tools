using System;

namespace VSRAD.Deborgar
{
    public sealed class BreakTarget
    {
        public string File { get; }
        public uint[] Lines { get; }
        public bool IsStepping { get; }

        public BreakTarget(string file, uint[] lines, bool isStepping)
        {
            File = file;
            Lines = lines;
            IsStepping = isStepping;
        }
    }

    public sealed class ExecutionCompletedEventArgs : EventArgs
    {
        public BreakTarget Target { get; }
        public bool IsSuccessful;

        public ExecutionCompletedEventArgs(BreakTarget target, bool isSuccessful)
        {
            Target = target;
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
