using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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
        IEnumerable<string> GetSupportedFileExtensionList();
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
            var list = args.TargetProcessors ?? Array.Empty<(string, string)>();
            return list.OrderBy(t => t).Select(t => new TargetProcessor(t.Processor, t.InstructionSet));
        }

        void ISyntaxIntegration.NotifyTargetProcessorChanged()
        {
            PackageUpdatedSelectedTargetProcessor?.Invoke(this, new EventArgs());
        }

        IEnumerable<string> ISyntaxIntegration.GetSupportedFileExtensionList()
        {
            var args = new FileExtensionListEventArgs();
            PackageRequestedSupportedFileExtensionList?.Invoke(this, args);
            return args.FileExtensions ?? Array.Empty<string>();
        }
        #endregion

        #region ISyntaxPackageBridge
        public event EventHandler<FileExtensionListEventArgs> PackageRequestedSupportedFileExtensionList;
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
