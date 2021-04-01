using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

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
            var optionsEventProvider = ServiceProvider.GlobalProvider.GetMefService<GeneralOptionProvider>();
            var commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            FunctionListControl = new FunctionListControl(optionsEventProvider, commandService);
            Content = FunctionListControl;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            FunctionListProvider.FunctionListWindowCreated(FunctionListControl);
        }
    }
}
