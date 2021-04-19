using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    internal sealed class NavigationListCommand
    {
        public static NavigationListCommand Instance { get; private set; }

        private static readonly Guid CommandSet = new Guid(Constants.NavigationListCommandSetGuid);
        private readonly AsyncPackage _package;

        private NavigationListCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this._package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, Constants.NavigationListCommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Instance = new NavigationListCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.ShowToolWindowAsync(typeof(NavigationList), 0, true, _package.DisposalToken);
                if (window?.Frame == null)
                {
                    throw new NotSupportedException("Cannot create tool window");
                }
            });
        }
    }
}
