using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public interface IOutputWindowManager
    {
        IOutputWindowWriter GetServerPane();
        IOutputWindowWriter GetExecutionResultPane();
    }

    public interface IOutputWindowWriter
    {
        Task PrintMessageAsync(string title, string contents = null);

        Task ClearAsync();
    }

    [Export(typeof(IOutputWindowManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class OutputWindowManager : IOutputWindowManager
    {
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public OutputWindowManager(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IOutputWindowWriter GetServerPane() => new OutputWindowWriter(_serviceProvider,
            Constants.OutputPaneServerGuid, Constants.OutputPaneServerTitle);

        public IOutputWindowWriter GetExecutionResultPane() => new OutputWindowWriter(_serviceProvider,
            Constants.OutputPaneExecutionResultGuid, Constants.OutputPaneExecutionResultTitle);
    }

    public sealed class OutputWindowWriter : IOutputWindowWriter
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly Guid _paneGuid;
        private readonly string _paneTitle;

        private IVsOutputWindowPane _pane;
        private IVsOutputWindowPane Pane
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_pane == null)
                {
                    var outputWindow = _serviceProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                    Assumes.Present(outputWindow);
                    outputWindow.CreatePane(_paneGuid, _paneTitle, fInitVisible: 1, fClearWithSolution: 1);
                    outputWindow.GetPane(_paneGuid, out _pane);
                }
                return _pane;
            }
        }

        public OutputWindowWriter(SVsServiceProvider provider, Guid outputPaneGuid, string outputPaneTitle)
        {
            _serviceProvider = provider;
            _paneGuid = outputPaneGuid;
            _paneTitle = outputPaneTitle;
        }

        public async Task PrintMessageAsync(string title, string contents = null)
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var message = contents == null
                ? "=== " + title + Environment.NewLine + Environment.NewLine
                : "=== " + title + Environment.NewLine + contents + Environment.NewLine + Environment.NewLine;
            Pane.OutputStringThreadSafe(message);
        }

        public async Task ClearAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            Pane.Clear();
        }
    }
}
