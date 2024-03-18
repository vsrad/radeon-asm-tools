using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VSRAD.SyntaxPackageBridge;

namespace VSRAD.Package.ProjectSystem
{
    public sealed class TargetProcessorSelectionEventArgs : EventArgs
    {
        public Task<(string TargetProcessor, string SyntaxName)> Selection { get; set; }
    }

    public interface ISyntaxIntegration
    {
        event EventHandler<TargetProcessorSelectionEventArgs> OnGetSelectedTargetProcessor;
        IReadOnlyList<(string TargetProcessor, string SyntaxName)> GetTargetProcessorList();
        void NotifyTargetProcessorChanged();
    }

    [Export(typeof(ISyntaxIntegration))]
    [Export(typeof(ISyntaxPackageBridge))]
    public sealed class SyntaxIntegration : ISyntaxIntegration, ISyntaxPackageBridge
    {
        #region ISyntaxIntegration
        public event EventHandler<TargetProcessorSelectionEventArgs> OnGetSelectedTargetProcessor;

        IReadOnlyList<(string TargetProcessor, string SyntaxName)> ISyntaxIntegration.GetTargetProcessorList()
        {
            var args = new TargetProcessorListEventArgs();
            PackageRequestedTargetProcessorList?.Invoke(this, args);
            return args.List ?? Array.Empty<(string, string)>();
        }

        void ISyntaxIntegration.NotifyTargetProcessorChanged() =>
            PackageUpdatedSelectedTargetProcessor?.Invoke(this, new EventArgs());
        #endregion

        #region ISyntaxPackageBridge
        public event EventHandler<TargetProcessorListEventArgs> PackageRequestedTargetProcessorList;
        public event EventHandler<EventArgs> PackageUpdatedSelectedTargetProcessor;

        Task<(string TargetProcessor, string SyntaxName)> ISyntaxPackageBridge.GetSelectedTargetProcessor()
        {
            var args = new TargetProcessorSelectionEventArgs();
            OnGetSelectedTargetProcessor?.Invoke(this, args);
            return args.Selection;
        }
        #endregion
    }
}
