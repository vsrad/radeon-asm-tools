using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal abstract class AbstractFunctionListCommand
    {
        public static readonly Guid CommandSet = new Guid(Constants.FunctionListCommandSetGuid);
        protected readonly AsyncPackage package;

        public AbstractFunctionListCommand(AsyncPackage package, OleMenuCommandService commandService, int commandId)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, commandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);

            commandService.AddCommand(menuItem);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = (FunctionListWindow)package.FindToolWindow(typeof(FunctionListWindow), 0, true);
            Execute(window);
        }

        protected abstract void Execute(FunctionListWindow window);
    }
}
