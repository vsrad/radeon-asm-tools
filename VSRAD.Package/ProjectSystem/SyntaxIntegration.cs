using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.SyntaxPackageBridge;

namespace VSRAD.Package.ProjectSystem
{
    public sealed class SelectedTargetProcessorEventArgs : EventArgs
    {
        public Task<(string Processor, string InstructionSet)> Selection { get; set; }
    }

    public interface ISyntaxIntegration
    {
        event EventHandler<SelectedTargetProcessorEventArgs> OnGetSelectedTargetProcessor;
        IEnumerable<TargetProcessor> GetPredefinedTargetProcessors();
        void NotifyTargetProcessorChanged();
    }

    [Export(typeof(ISyntaxIntegration))]
    [Export(typeof(ISyntaxPackageBridge))]
    public sealed class SyntaxIntegration : ISyntaxIntegration, ISyntaxPackageBridge
    {
        #region ISyntaxIntegration
        public event EventHandler<SelectedTargetProcessorEventArgs> OnGetSelectedTargetProcessor;

        IEnumerable<TargetProcessor> ISyntaxIntegration.GetPredefinedTargetProcessors()
        {
            var args = new TargetProcessorListEventArgs();
            PackageRequestedTargetProcessorList?.Invoke(this, args);
            var list = args.List ?? Array.Empty<(string, string)>();
            foreach (var (processor, instructionSet) in list)
                yield return new TargetProcessor(processor, instructionSet);
        }

        void ISyntaxIntegration.NotifyTargetProcessorChanged() =>
            PackageUpdatedSelectedTargetProcessor?.Invoke(this, new EventArgs());
        #endregion

        #region ISyntaxPackageBridge
        public event EventHandler<TargetProcessorListEventArgs> PackageRequestedTargetProcessorList;
        public event EventHandler<EventArgs> PackageUpdatedSelectedTargetProcessor;

        Task<(string Processor, string InstructionSet)> ISyntaxPackageBridge.GetSelectedTargetProcessor()
        {
            var args = new SelectedTargetProcessorEventArgs();
            OnGetSelectedTargetProcessor?.Invoke(this, args);
            return args.Selection;
        }
        #endregion
    }
}
