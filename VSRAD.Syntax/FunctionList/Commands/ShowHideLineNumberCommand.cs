using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal class ShowHideLineNumberCommand : AbstractFunctionListCommand
    {
        public static ShowHideLineNumberCommand Instance;

        private ShowHideLineNumberCommand(AsyncPackage package, OleMenuCommandService commandService)
            : base(package, commandService, Constants.ShowHideLineNumberCommandId) { }

        protected override void Execute(FunctionListWindow window)
        {
            if (window?.Frame == null || window.FunctionListControl == null) 
                return;

            window.FunctionListControl.ShowHideLineNumber();
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService) =>
            Instance = new ShowHideLineNumberCommand(package, commandService);
    }
}
