using VSRAD.Syntax.Parser;
using EnvDTE;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Helpers;
using System.Linq;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionList : ToolWindowPane
    {
        private const string CaptionName = "Function list";

        public static FunctionList Instance { get; private set; }
        private IVsTextManager _textManager;
        private IVsEditorAdaptersFactoryService _editorAdaptorFactory;
        private DTE _dte;
        private BaseParser parser;
        private FunctionListControl FunctionListControl => (FunctionListControl)Content;

        public FunctionList() : base(null)
        {
            this.Caption = CaptionName;
        }

        protected override void Initialize()
        {
            _textManager = GetService(typeof(VsTextManagerClass)) as IVsTextManager;
            _dte = GetService(typeof(DTE)) as DTE;
            _editorAdaptorFactory = (this.Package as Package).GetMEFComponent<IVsEditorAdaptersFactoryService>();
            var optionsEventProvider = (this.Package as Package).GetMEFComponent<OptionsEventProvider>();

            _dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;

            var commandService = (OleMenuCommandService)GetService(typeof(IMenuCommandService));
            Content = new FunctionListControl(commandService, optionsEventProvider);

            try
            {
                var activeView = GetActiveTextView();
                var parserManager = activeView.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(parserManager.ActualParser));
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }

            Instance = this;
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window LostFocus)
        {
            if (GotFocus.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
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
                            ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(parserManager.ActualParser));
                        }
                    }
                }
            }
        }

        public IWpfTextView GetActiveTextView()
        {
            Assumes.Present(_textManager);
            Assumes.Present(_editorAdaptorFactory);

            _textManager.GetActiveView(1, null, out var textViewCurrent);
            return _editorAdaptorFactory.GetWpfTextView(textViewCurrent);
        }

        private Task UpdateFunctionListAsync(object sender)
        {
            try
            {
                parser = (BaseParser)sender;
                var updatedFunctions = parser.GetFunctionTokens();
                var updatedLabels = parser.GetLabelTokens();
                return FunctionListControl.UpdateFunctionListAsync(updatedFunctions.Concat(updatedLabels));
            }
            catch (Exception e)
            {
                Error.LogError(e);
                return Task.CompletedTask;
            }
        }

        private Task HighlightCurrentFunctionAsync(ITextView textView)
        {
            var line = textView.Caret.Position.BufferPosition.GetContainingLine();

            if (line == null)
                return Task.CompletedTask;

            var function = parser.GetFunctionByLine(line);
            return function == null ? Task.CompletedTask : FunctionListControl.HighlightCurrentFunctionAsync(function.FunctionToken);
        }

        public static Task TryUpdateFunctionListAsync(object sender)
        {
            if (Instance != null)
                return Instance.UpdateFunctionListAsync(sender);

            return Task.CompletedTask;
        }

        public static Task TryHighlightCurrentFunctionAsync(CaretPositionChangedEventArgs args)
        {
            if (Instance != null)
                return Instance.HighlightCurrentFunctionAsync(args.TextView);

            return Task.CompletedTask;
        }
    }
}
