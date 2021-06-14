using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionListWindow : ToolWindowPane
    {
        public FunctionListControl FunctionListControl { get; private set; }

        public FunctionListWindow()
        {
            Caption = "Function list";
        }

        protected override void Initialize()
        {
            FunctionListControl = new FunctionListControl();
            Content = FunctionListControl;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            FunctionListProvider.FunctionListWindowCreated(FunctionListControl);
        }
    }
}
