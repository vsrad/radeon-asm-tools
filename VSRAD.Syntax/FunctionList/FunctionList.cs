using VSRAD.Syntax.Parser;
using EnvDTE;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionList : ToolWindowPane
    {
        public static FunctionList Instance { get; private set; }
        internal const string CaptionName = "Function list";
        private IVsTextManager _textManager;
        private IVsEditorAdaptersFactoryService _editorAdaptorFactory;
        private DTE _dte;

        public FunctionList() : base(null)
        {
            this.Caption = CaptionName;
        }

        protected override void Initialize()
        {
            ThreadPool.QueueUserWorkItem(InitializeComponent);

            var commandService = (OleMenuCommandService)GetService(typeof(IMenuCommandService));
            Content = new FunctionListControl(commandService);
        }

        private void InitializeComponent(object value)
        {
            _textManager = GetService(typeof(VsTextManagerClass)) as IVsTextManager;
            _dte = GetService(typeof(DTE)) as DTE;
            _editorAdaptorFactory = (this.Package as Package).GetMEFComponent<IVsEditorAdaptersFactoryService>();

            _dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
            Instance = this;
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window LostFocus)
        {
            if (GotFocus.Kind.Equals("Document"))
            {
                var openWindowPath = Path.Combine(GotFocus.Document.Path, GotFocus.Document.Name);

                if (VsShellUtilities.IsDocumentOpen(
                  this,
                  openWindowPath,
                  Guid.Empty,
                  out var uiHierarchy,
                  out var itemID,
                  out var windowFrame))
                {
                    var view = VsShellUtilities.GetTextView(windowFrame);
                    if (view.GetBuffer(out var lines) == 0)
                    {
                        if (lines is IVsTextBuffer buffer)
                        {
                            var textBuffer = _editorAdaptorFactory.GetDataBuffer(buffer);
                            var parserManager = textBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
                            ThreadHelper.JoinableTaskFactory.Run(() => FunctionListControl.UpdateFunctionListAsync(parserManager.ActualParser));
                        }
                    }
                }
            }
        }

        public IWpfTextView GetWpfTextView()
        {
            if (_textManager == null || _editorAdaptorFactory == null)
                return null;

            _textManager.GetActiveView(1, null, out var textViewCurrent);
            return (textViewCurrent != null) ? _editorAdaptorFactory.GetWpfTextView(textViewCurrent) : null;
        }
    }
}
