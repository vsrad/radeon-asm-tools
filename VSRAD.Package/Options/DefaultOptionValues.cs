using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Options
{
    internal static class DefaultOptionValues
    {
        #region General
        public const string DeployDirectory = "";
        public const string RemoteMachineAdredd = "127.0.0.1";
        public const int Port = 9339;
        public const DocumentSaveType AutosaveSource = DocumentSaveType.ActiveDocument;
        public const string AdditionalSources = "";
        public const bool CopySources = true;
        #endregion
        #region Debugger
        public const string DebuggerExecutable = "python.exe";
        public const string DebuggerArguments = "script.py -w $(" + RadMacros.Watches + ") -l $(" + RadMacros.BreakLine + ") -v \"$(" + RadMacros.DebugAppArgs + ")\" -t $(" + RadMacros.Counter + ") -p \"$(" + RadMacros.DebugBreakArgs + ")\" -f \"$(" + RadMacros.ActiveSourceFile + ")\" -o \"$(" + RadMacros.DebuggerOutputPath + ")\"";
        public const string DebuggerWorkingDirectory = "$(" + RadMacros.DeployDirectory + ")";
        public const string DebuggerOutputPath = "";
        public const bool DebuggerBinaryOutput = false;
        public const bool DebuggerParseValidWatches = false;
        public const string DebuggerValidWatchesFilePath = "";
        public const bool DebuggerRunAsAdmin = false;
        public const int DebuggerTimeoutSecs = 0;
        public const int OutputOffset = 0;
        public const string PreprocessedSource = "";
        #endregion
        #region Disassembler
        public const string DisassemblerExecutable = "";
        public const string DisassemblerArguments = "";
        public const string DisassemblerWorkingDirectory = "$(" + RadMacros.DeployDirectory + ")";
        public const string DisassemblerOutputPath = "";
        public const string DisassemblerLocalOutputCopyPath = "";
        public const string DisassemblerLineMaker = "";
        #endregion
        #region Profiler
        public const string ProfilerExecutable = "";
        public const string ProfilerArguments = "";
        public const string ProfilerWorkingDirectory = "$(" + RadMacros.DeployDirectory + ")";
        public const string ProfilerOutputPath = "";
        public const string ProfilerViewerExecutable = "";
        public const string ProfilerViewerArguments = "";
        public const string ProfilerLocalOutputCopyPath = "";
        public const bool ProfilerRunAsAdmin = false;
        #endregion
        #region Build
        public const string BuildExecutable = "";
        public const string BuildArguments = "";
        public const string BuildWorkingDirectory = "";
        #endregion
    }
}
