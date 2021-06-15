using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal abstract class AbstractFunctionListCommand
    {
        public static readonly Guid CommandSet = new Guid(Constants.FunctionListCommandSetGuid);
        protected readonly AsyncPackage Package;
        protected readonly OleMenuCommandService CommandService;

        protected AbstractFunctionListCommand(AsyncPackage package, OleMenuCommandService commandService, int commandId)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            CommandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, commandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);

            CommandService.AddCommand(menuItem);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var window = (FunctionListWindow)Package.FindToolWindow(typeof(FunctionListWindow), 0, true);
            
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create function list window");

            Execute(window);
        }

        protected abstract void Execute(FunctionListWindow window);
    }
}
