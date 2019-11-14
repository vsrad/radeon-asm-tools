using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System.ComponentModel.Composition;
using System.IO;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.DisassemblyCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    internal sealed class DisassemblyCommand : BaseRemoteCommand
    {
        private readonly IProject _project;
        private readonly IFileSynchronizationManager _deployManager;
        private readonly IOutputWindowManager _outputWindow;
        private readonly ICommunicationChannel _channel;

        [ImportingConstructor]
        public DisassemblyCommand(
            IProject project,
            IFileSynchronizationManager deployManager,
            IOutputWindowManager outputWindow,
            ICommunicationChannel channel,
            SVsServiceProvider serviceProvider) : base(Constants.DisassembleCommandId, serviceProvider)
        {
            _project = project;
            _deployManager = deployManager;
            _outputWindow = outputWindow;
            _channel = channel;
        }

        public override async Task RunAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var evaluator = await _project.GetMacroEvaluatorAsync(default);
            var options = await _project.Options.Profile.Disassembler.EvaluateAsync(evaluator);
            var command = new Execute { Executable = options.Executable, Arguments = options.Arguments, WorkingDirectory = options.WorkingDirectory, RunAsAdministrator = false };

            await SetStatusBarTextAsync("RAD Debug: Disassembling...");
            try
            {
                await _deployManager.SynchronizeRemoteAsync();
                var executor = new RemoteCommandExecutor("Disassembly", _channel, _outputWindow);
                var result = await executor.ExecuteWithResultAsync(command, options.RemoteOutputFile);

                if (!result.TryGetResult(out var execResult, out var error))
                    throw new System.Exception(error.Message);
                var (_, data) = execResult;

                File.WriteAllBytes(options.LocalOutputCopyPath, data);
                OpenFileInEditor(options.LocalOutputCopyPath, options.LineMarker);
            }
            finally
            {
                await ClearStatusBarAsync();
            }
        }

        private void OpenFileInEditor(string localOutputPath, string lineMarker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            dte.ItemOperations.OpenFile(localOutputPath);
            if (string.IsNullOrEmpty(lineMarker)) return;

            var lineNumber = GetMarkedLineNumber(localOutputPath, lineMarker);

            var textManager = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);

            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            activeView.SetCaretPos(lineNumber, 0);
        }

        private int GetMarkedLineNumber(string file, string lineMarker)
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                if (line == lineMarker)
                    return lineNumber;
                ++lineNumber;
            }
            return 0;
        }
    }
}
