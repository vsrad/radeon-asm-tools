using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal sealed class ClearSearchFieldCommand : AbstractFunctionListCommand
    {
        public static ClearSearchFieldCommand Instance;

        private ClearSearchFieldCommand(AsyncPackage package, OleMenuCommandService commandService)
            : base(package, commandService, Constants.ClearSearchFieldCommandId) { }

        protected override void Execute(FunctionListWindow window)
        {
            if (window == null || window.Frame == null || window.FunctionListControl == null)
                return;

            window.FunctionListControl.ClearSearch();
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService) =>
            Instance = new ClearSearchFieldCommand(package, commandService);
    }
}
