using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using VSRAD.Deborgar;
using VSRAD.Package.Commands;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Registry;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;
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
        Style = VsDockStyle.Tabbed, MultiInstances = false)]
    [ProvideToolWindow(typeof(SliceVisualizerWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false)]
    [ProvideToolWindow(typeof(OptionsWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideToolWindow(typeof(FloatInspectorWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Width = 600, Height = 520)]
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
        public static FloatInspectorWindow FloatInspectorToolWindow { get; private set; }

        private ICommandRouter _commandRouter;
        private SolutionManager _solutionManager;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            AddService(typeof(DebugVisualizer.FontAndColorService),
                (c, ct, st) => Task.FromResult<object>(new DebugVisualizer.FontAndColorService()), promote: true);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsMonitorSelection = (IVsMonitorSelection)await GetServiceAsync(typeof(IVsMonitorSelection));
            var dte = (DTE2)await GetServiceAsync(typeof(DTE));
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
                var reverseDebugCommand = dte.Commands.Item(Constants.ActionsMenuCommandSet.ToString("B"), Constants.ReverseDebugCommandId);
                reverseDebugCommand.Bindings = "Global::F7";
            }
            catch (Exception e)
            {
                Errors.ShowWarning("Unable to set F6 and F7 as the keyboard shortcuts for Tools.RadDebug.RerunDebug and Tools.RadDebug.ReverseDebug." +
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
            FloatInspectorToolWindow = (FloatInspectorWindow)await FindToolWindowAsync(
                typeof(FloatInspectorWindow), 0, true, CancellationToken.None);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            VisualizerToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
            SliceVisualizerToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
            OptionsToolWindow.OnProjectLoaded(e.ToolWindowIntegration);
            FloatInspectorToolWindow.OnProjectLoaded(e.ToolWindowIntegration);

            await ApplyWorkaroundForErrorInMiscFilesAfterProjectLoadAsync();
        }

        public static void SolutionUnloaded()
        {
            VisualizerToolWindow?.OnProjectUnloaded();
            OptionsToolWindow?.OnProjectUnloaded();
        }

        public static Version GetExtensionVersion(SVsServiceProvider serviceProvider)
        {
            var dte = (DTE2)serviceProvider.GetService(typeof(DTE));
            Assumes.Present(dte);
            var dteVersion = Version.Parse(dte.Version.Split(' ')[0]);
            var extManagerVersion = new Version(dteVersion.Major == -1 ? 0 : dteVersion.Major, dteVersion.Minor == -1 ? 0 : dteVersion.Minor, 0, 0);
            var extManagerAssembly = Assembly.Load($"Microsoft.VisualStudio.ExtensionManager, Version={extManagerVersion}, PublicKeyToken=b03f5f7f11d50a3a");
            var extManagerType = extManagerAssembly.GetType("Microsoft.VisualStudio.ExtensionManager.SVsExtensionManager");
            var extManagerIType = extManagerAssembly.GetType("Microsoft.VisualStudio.ExtensionManager.IVsExtensionManager");
            var extManager = serviceProvider.GetService(extManagerType);
            Assumes.Present(extManager);
            var extManagerGetExtension = extManagerIType.GetMethod("GetInstalledExtension");
            var extension = extManagerGetExtension.Invoke(extManager, new[] { "ba666db81-7abd-4a75-b906-895d8cc0616e" }); // ProductId from source.extension.vsixmanifest
            var header = extension.GetType().GetProperty("Header").GetValue(extension);
            var version = header.GetType().GetProperty("Version").GetValue(header);
            return (Version)version;
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

        // After reloading the project, Visual Studio sometimes fails to open files located outside the project tree:
        // * https://developercommunity.visualstudio.com/t/VS-fails-to-reopen-a-file-in-MiscFiles/10439129
        // * https://developercommunity.visualstudio.com/t/An-error-occurred-in-Miscellaneous-File/10316041
        // To reproduce, edit the .vcxproj file while the project is open in VS, go back to VS and click "Reload All". Repeat if necessary.
        // This can be detected programmatically by iterating over EnvDTE.Documents: when the errors are present, this results in E_FAIL.
        // As a workaround, we can force the documents to load properly by walking editor windows and calling OpenDocument on each of them.
        // Unfortunately, this has the side effect of resetting tab groups, but it still seems better than having to close and reopen each file manually.
        private async Task ApplyWorkaroundForErrorInMiscFilesAfterProjectLoadAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = (DTE2)await GetServiceAsync(typeof(DTE));
            try
            {
                foreach (Document document in dte.Documents) { }
            }
            catch (COMException documentLoadExc) when (documentLoadExc.HResult == VSConstants.E_FAIL)
            {
                foreach (var documentWindow in VsEditor.GetOpenEditorWindows(this))
                {
                    var documentPath = (string)((dynamic)documentWindow).EffectiveDocumentMoniker;
                    if (!string.IsNullOrEmpty(documentPath))
                        VsShellUtilities.OpenDocument(this, documentPath);
                }
            }
        }
    }
}
