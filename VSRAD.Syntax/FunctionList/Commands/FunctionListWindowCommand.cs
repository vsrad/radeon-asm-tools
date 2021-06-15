using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal sealed class FunctionListWindowCommand : AbstractFunctionListCommand
    {
        public static FunctionListWindowCommand Instance;

        private FunctionListWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
            : base(package, commandService, Constants.FunctionListCommandId) { }

        protected override void Execute(FunctionListWindow window)
        {
            var windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService) =>
            Instance = new FunctionListWindowCommand(package, commandService);
    }
}