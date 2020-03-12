using System;

namespace VSRAD.Deborgar
{
    public delegate void ExecutionCompleted(bool success);

    public interface IEngineIntegration
    {
        void ExecuteToLine(uint breakLine);
        string GetActiveProjectFile();
        string GetProjectRelativePath(string absoluteFilePath);
        uint GetFileLineCount(string projectFilePath);
        bool PopRunToLineIfSet(string file, out uint runToLine);

        event ExecutionCompleted ExecutionCompleted;
    }
}
