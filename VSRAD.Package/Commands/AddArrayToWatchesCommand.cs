using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class AddArrayToWatchesCommand : ICommandHandler
    {
        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;

        [ImportingConstructor]
        public AddArrayToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
        }

        public Guid CommandSet => Constants.AddArrayToWatchesCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            var fromHeader = commandId == Constants.AddArrayToWatchesFromHeaderId;
            var toHeader = commandId >= Constants.AddArrayToWatchesToHeaderOffset
                && commandId < Constants.AddArrayToWatchesToHeaderOffset + Constants.AddArrayToWatchesIndexCount;

            if (fromHeader || toHeader)
                return OLECMDF.OLECMDF_SUPPORTED;

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId < Constants.AddArrayToWatchesToIdOffset)
                return;

            var watchName = _codeEditor.GetActiveWord()?.Trim();

            if (string.IsNullOrEmpty(watchName))
                return;

            var fromIndex = (commandId - Constants.AddArrayToWatchesToIdOffset) / Constants.AddArrayToWatchesToFromOffset;
            var toIndex = (commandId - Constants.AddArrayToWatchesToIdOffset) % Constants.AddArrayToWatchesToFromOffset;
            var arrayRangeWatch = ArrayRange.FormatArrayRangeWatch(watchName, (int)fromIndex, (int)toIndex);

            foreach (var watch in arrayRangeWatch)
                _toolIntegration.AddWatchFromEditor(watch);
        }
    }
}
