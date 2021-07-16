using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class AddToWatchesCommand : ICommandHandler
    {
        private readonly IToolWindowIntegration _toolIntegration;
        private readonly IActiveCodeEditor _codeEditor;

        [ImportingConstructor]
        public AddToWatchesCommand(IToolWindowIntegration toolIntegration, IActiveCodeEditor codeEditor)
        {
            _toolIntegration = toolIntegration;
            _codeEditor = codeEditor;
        }

        public Guid CommandSet => Constants.AddToWatchesCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.AddArrayToWatchesFromHeaderId)
                return OLECMDF.OLECMDF_SUPPORTED;
            if (commandId >= Constants.AddArrayToWatchesToHeaderOffset
                && commandId < Constants.AddArrayToWatchesToHeaderOffset + Constants.AddArrayToWatchesIndexCount)
                return OLECMDF.OLECMDF_SUPPORTED;

            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            var watchName = _codeEditor.GetActiveWord(_toolIntegration.ProjectOptions.VisualizerOptions.MatchBracketsOnAddToWatches);
            if (string.IsNullOrEmpty(watchName))
                return;

            if (commandId == Constants.AddToWatchesCommandId)
            {
                _toolIntegration.AddWatchFromEditor(watchName);
            }
            else if (commandId >= Constants.AddArrayToWatchesToIdOffset)
            {
                var fromIndex = Math.DivRem(commandId - Constants.AddArrayToWatchesToIdOffset, Constants.AddArrayToWatchesToFromOffset, out var toIndex);
                var arrayRangeWatch = ArrayRange.FormatArrayRangeWatch(watchName, (int)fromIndex, (int)toIndex,
                                        _toolIntegration.ProjectOptions.VisualizerOptions.MatchBracketsOnAddToWatches);

                foreach (var watch in arrayRangeWatch)
                    _toolIntegration.AddWatchFromEditor(watch);
            }
        }
    }
}
