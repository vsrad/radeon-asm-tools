using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [ExportCommandGroup(Constants.AddArrayToWatchesCommandSet)]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class AddArrayToWatchesCommand : BaseCommand
    {
        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;

        [ImportingConstructor]
        public AddArrayToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
        }

        public override Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
        {
            var fromHeader = commandId == Constants.AddArrayToWatchesFromHeaderId;
            var toHeader = commandId >= Constants.AddArrayToWatchesToHeaderOffset
                && commandId < Constants.AddArrayToWatchesToHeaderOffset + Constants.AddArrayToWatchesIndexCount;

            if (fromHeader || toHeader)
                return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Supported));

            return Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
        }

        public async override Task<bool> RunAsync(long commandId)
        {
            if (commandId < Constants.AddArrayToWatchesToIdOffset) return true;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var watchName = _codeEditor.GetActiveWord()?.Trim();

            if (string.IsNullOrEmpty(watchName)) return true;

            var fromIndex = (commandId - Constants.AddArrayToWatchesToIdOffset) / Constants.AddArrayToWatchesToFromOffset;
            var toIndex = (commandId - Constants.AddArrayToWatchesToIdOffset) % Constants.AddArrayToWatchesToFromOffset;
            var arrayRangeWatch = ArrayRange.FormatArrayRangeWatch(watchName, (int)fromIndex, (int)toIndex);

            foreach (var watch in arrayRangeWatch)
                _toolIntegration.AddWatchFromEditor(watch);

            return true;
        }
    }
}
