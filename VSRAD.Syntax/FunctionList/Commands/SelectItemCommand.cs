using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal sealed class SelectItemCommand
    {

        internal static readonly Guid CommandSet = new Guid(Constants.FunctionListCommandSetGuid);

        private readonly AsyncPackage package;

        private SelectItemCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, Constants.SelectItemCommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);

            commandService.AddCommand(menuItem);
        }

        public static SelectItemCommand Instance
        {
            get;
            private set;
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new SelectItemCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = (FunctionList)package.FindToolWindow(typeof(FunctionList), 0, true);
            if ((null == window) || (null == window.Frame)) 
                return;

            window.FunctionListControl.GoToSelectedItem();
        }
    }
}
