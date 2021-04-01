using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Syntax.FunctionList.Commands
{
    internal sealed class FunctionListCommand : AbstractFunctionListCommand
    {
        public static FunctionListCommand Instance;

        private FunctionListCommand(AsyncPackage package, OleMenuCommandService commandService)
            : base(package, commandService, Constants.FunctionListCommandId) { }

        protected override void Execute(FunctionListWindow window)
        {
            if (window?.Frame == null)
                throw new NotSupportedException("Cannot create function list window");

            var windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public static void Initialize(AsyncPackage package, OleMenuCommandService commandService) =>
            Instance = new FunctionListCommand(package, commandService);
    }
}
