using VSRAD.Syntax.FunctionList.Commands;
using VSRAD.Syntax.Options;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Syntax
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(FunctionList.FunctionList))]
    [ProvideOptionPage(typeof(OptionPage), Constants.RadeonAsmOptionsCategoryName, Constants.RadeonAsmOptionsBasePageName, 0, 0, true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(Constants.PackageGuid)]
    public sealed class Package : AsyncPackage
    {
        public static Package Instance { get; private set; }
        private IComponentModel _componentModel;

        public Package() { }

        public OptionPage OptionPage
        {
            get
            {
                return GetDialogPage(typeof(OptionPage)) as OptionPage;
            }
        }

        public T GetMEFComponent<T>() where T : class
        {
            return _componentModel.GetService<T>();
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;
            _componentModel = (await GetServiceAsync(typeof(SComponentModel))) as IComponentModel;
            await OptionPage.ChangeExtensionsAndUpdateConentTypesAsync();
            await FunctionListCommand.InitializeAsync(this);
            await ClearSearchFieldCommand.InitializeAsync(this);
            await SelectItemCommand.InitializeAsync(this);
        }
    }
}