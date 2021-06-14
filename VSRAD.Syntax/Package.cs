using VSRAD.Syntax.FunctionList.Commands;
using VSRAD.Syntax.Options;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSRAD.Syntax.IntelliSense.Navigation.NavigationList;
using System.ComponentModel.Design;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(FunctionList.FunctionListWindow), Style = VsDockStyle.Linked, Orientation = ToolWindowOrientation.Right)]
    [ProvideToolWindow(typeof(NavigationList), Style = VsDockStyle.Linked, Orientation = ToolWindowOrientation.Bottom, Transient = true)]
    [ProvideOptionPage(typeof(GeneralOptionPage), Constants.RadeonAsmOptionsCategoryName, Constants.RadeonAsmOptionsBasePageName, 0, 0, true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(Constants.PackageGuid)]
    public sealed class Package : AsyncPackage
    {
        public static Package Instance { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;
            await base.InitializeAsync(cancellationToken, progress);
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavigationListCommand.Initialize(this, commandService);
            FunctionListWindowCommand.Initialize(this, commandService);
            InstructionSetSelector.Initialize(commandService);
        }
    }
}