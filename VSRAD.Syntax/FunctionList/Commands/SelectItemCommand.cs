using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal sealed class SelectItemCommand : AbstractFunctionListCommand
    {
        public static SelectItemCommand Instance;

        private SelectItemCommand(AsyncPackage package, OleMenuCommandService commandService)
            : base(package, commandService, Constants.SelectItemCommandId) { }

        protected override void Execute(FunctionListWindow window)
        {
            if (window?.Frame == null || window.FunctionListControl == null) 
                return;

            window.FunctionListControl.GoToSelectedItem();
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService) =>
            Instance = new SelectItemCommand(package, commandService);
    }
}
