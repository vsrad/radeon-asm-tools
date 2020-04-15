namespace VSRAD.Deborgar
{
    public delegate void ExecutionCompleted(bool success);

    public enum BreakMode
    {
        SingleRoundRobin, SingleRerun, Multiple
    }

    public interface IEngineIntegration
    {
        void Execute(uint[] breakLines);
        string GetActiveProjectFile();
        string GetProjectRelativePath(string absoluteFilePath);
        uint GetFileLineCount(string projectFilePath);
        BreakMode GetBreakMode();
        bool PopRunToLineIfSet(string file, out uint runToLine);

        event ExecutionCompleted ExecutionCompleted;
    }
}
