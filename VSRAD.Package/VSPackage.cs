using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using VSRAD.Deborgar;
using VSRAD.Package.ProjectSystem;
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
    [ProvideToolWindow(typeof(ToolWindows.VisualizerWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideToolWindow(typeof(ToolWindows.OptionsWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideToolWindow(typeof(ToolWindows.EvaluateSelectedWindow),
        Style = VsDockStyle.Tabbed, MultiInstances = false, Transient = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // Required to load packaged dlls (referenced via nuget)
    [ProvideBindingPath]
    // Required for the custom project template to show up in New Project dialog
    [ProvideService(typeof(VSLanguageInfo), ServiceName = nameof(VSLanguageInfo))]
    [ProvideLanguageService(typeof(VSLanguageInfo), Deborgar.Constants.LanguageName, 106)]
    [Guid(Constants.PackageId)]
    public sealed class VSPackage : AsyncPackage
    {
        public static VisualizerWindow VisualizerToolWindow { get; private set; }
        public static OptionsWindow OptionsToolWindow { get; private set; }
        public static EvaluateSelectedWindow EvaluateSelectedWindow { get; private set; }

        private static JoinableTaskFactory _taskFactoryOverride;
        public static JoinableTaskFactory TaskFactory
        {
            get => _taskFactoryOverride ?? ThreadHelper.JoinableTaskFactory;
            set => _taskFactoryOverride = value;
        }

        public async Task ProjectLoadedAsync(IToolWindowIntegration toolWindowIntegration)
        {
            VisualizerToolWindow = (VisualizerWindow)await FindToolWindowAsync(
                typeof(VisualizerWindow), 0, true, CancellationToken.None);
            OptionsToolWindow = (OptionsWindow)await FindToolWindowAsync(
                typeof(OptionsWindow), 0, true, CancellationToken.None);
            EvaluateSelectedWindow = (EvaluateSelectedWindow)await FindToolWindowAsync(
                typeof(EvaluateSelectedWindow), 0, true, CancellationToken.None);

            await TaskFactory.SwitchToMainThreadAsync();
            VisualizerToolWindow.OnProjectLoaded(toolWindowIntegration);
            OptionsToolWindow.OnProjectLoaded(toolWindowIntegration);
            EvaluateSelectedWindow.OnProjectLoaded(toolWindowIntegration);
        }

        public static void ProjectUnloaded()
        {
            VisualizerToolWindow.OnProjectUnloaded();
            OptionsToolWindow.OnProjectUnloaded();
        }
    }
}
