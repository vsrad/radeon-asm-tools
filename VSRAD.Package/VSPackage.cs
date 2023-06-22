using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using VSRAD.Deborgar;
using VSRAD.Package.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Registry;
using VSRAD.Package.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package
{
    [ProvideDebugEngine(Deborgar.Constants.DebugEngineName, typeof(DebugEngine),
        PortSupplierGuids = new[] {
            Deborgar.Constants.VisualStudioLocalPortSupplierId,
            Deborgar.Constants.RemotePortSupplierId
        })]
    [ProvideDebugPortSupplier(Deborgar.Constants.RemotePortSupplierName, Deborgar.Constants.RemotePortSupplierId,
        typeof(Deborgar.Remote.RemotePortSupplier))]
    [ProvideToolWindow(typeof(VisualizerWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideToolWindow(typeof(SliceVisualizerWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideToolWindow(typeof(OptionsWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // Required to load packaged dlls (referenced via nuget)
    [ProvideBindingPath]
    // Required for the custom project template to show up in New Project dialog
    [ProvideService(typeof(VSLanguageInfo), ServiceName = nameof(VSLanguageInfo))]
    [ProvideLanguageService(typeof(VSLanguageInfo), Deborgar.Constants.LanguageName, 106)]
    [ProvideService(typeof(DebugVisualizer.FontAndColorService))]
    [ProvideFontAndColorsCategory("VSRAD", Constants.FontAndColorsCategoryId, typeof(DebugVisualizer.FontAndColorService))]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(Constants.PackageId)]
    public sealed class VSPackage : AsyncPackage, IOleCommandTarget
    {
        public static VisualizerWindow VisualizerToolWindow { get; private set; }
        public static SliceVisualizerWindow SliceVisualizerToolWindow { get; private set; }
        public static OptionsWindow OptionsToolWindow { get; private set; }

        private ICommandRouter _commandRouter;
        private SolutionManager _solutionManager;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            AddService(typeof(DebugVisualizer.FontAndColorService),
                (c, ct, st) => Task.FromResult<object>(new DebugVisualizer.FontAndColorService()), promote: true);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsMonitorSelection = (IVsMonitorSelection)await GetServiceAsync(typeof(IVsMonitorSelection));
            var dte = (DTE)await GetServiceAsync(typeof(DTE));
            _solutionManager = new SolutionManager(vsMonitorSelection);
            _solutionManager.ProjectLoaded += (s, e) => ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(() => ProjectLoadedAsync(s, e));

#pragma warning disable CS4014     // LoadCurrentSolution needs to be invoked _after_ we leave InitializeAsync,
#pragma warning disable VSTHRD001  // otherwise we enter a deadlock waiting for the package to finish loading
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                (Action)(() => _solutionManager.LoadCurrentSolution(dte)), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
#pragma warning restore CS4014

#if DEBUG
            DebugVisualizer.FontAndColorService.ClearFontAndColorCache(this);
#endif

            try
            {
                // https://stackoverflow.com/a/22748659
                var rerunDebugCommand = dte.Commands.Item(Constants.ActionsMenuCommandSet.ToString("B"), Constants.RerunDebugCommandId);
                rerunDebugCommand.Bindings = "Global::F6";
            }
            catch (Exception e)
            {
                Errors.ShowWarning("Unable to set F6 as the keyboard shortcut for Tools.RadDebug.RerunDebug." +
                    " Please configure it manually in Tools -> Options -> Environment -> Keyboard." +
                    "\n\nException: " + e.Message);
            }
        }

        public async Task ProjectLoadedAsync(object sender, ProjectLoadedEventArgs e)
        {
            _commandRouter = e.CommandRouter;

            VisualizerToolWindow = (VisualizerWindow)await FindToolWindowAsync(
                typeof(VisualizerWindow), 0, true, CancellationToken.None);
            SliceVisualizerToolWindow = (SliceVisualizerWindow)await FindToolWindowAsync(
                typeof(SliceVisualizerWindow), 0, true, CancellationToken.None);
            OptionsToolWindow = (OptionsWindow)await FindToolWindowAsync(
                typeof(OptionsWindow), 0, true, CancellationToken.None);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            VisualizerToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
            SliceVisualizerToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
            OptionsToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
        }

        public static void SolutionUnloaded()
        {
            VisualizerToolWindow?.OnProjectUnloaded();
            OptionsToolWindow?.OnProjectUnloaded();
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _commandRouter?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText) ?? VSConstants.S_OK;
        }

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _commandRouter?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) ?? VSConstants.S_OK;
        }
    }
}
