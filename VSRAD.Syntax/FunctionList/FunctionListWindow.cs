﻿using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
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
            var optionsEventProvider = GeneralOptionProvider.Instance;
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
