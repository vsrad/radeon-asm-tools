using EnvDTE;
using System;
using System.ComponentModel.Design;
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
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Parser;
using System.Collections.Generic;
using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionList : ToolWindowPane
    {
        private const string CaptionName = "Function list";

        private IVsTextManager _textManager;
        private IVsEditorAdaptersFactoryService _editorAdaptorFactory;
        private DocumentAnalysisProvoder _documentAnalysisProvider;

        public static FunctionList Instance { get; private set; }
        public FunctionListControl FunctionListControl => (FunctionListControl)Content;

        public FunctionList() : base(null)
        {
            Caption = CaptionName;
        }

        public IWpfTextView GetActiveTextView()
        {
            Assumes.Present(_textManager);
            Assumes.Present(_editorAdaptorFactory);

            _textManager.GetActiveView(1, null, out var textViewCurrent);
            return _editorAdaptorFactory.GetWpfTextView(textViewCurrent);
        }

        protected override void Initialize()
        {
            _textManager = GetService(typeof(VsTextManagerClass)) as IVsTextManager;
            _editorAdaptorFactory = Syntax.Package.Instance.GetMEFComponent<IVsEditorAdaptersFactoryService>();
            _documentAnalysisProvider = Syntax.Package.Instance.GetMEFComponent<DocumentAnalysisProvoder>();

            var optionsEventProvider = Syntax.Package.Instance.GetMEFComponent<OptionsProvider>();
            var commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Content = new FunctionListControl(commandService, optionsEventProvider);

            Instance = this;
            UpdateFunctionList(GetActiveTextView()?.TextBuffer);

            var dte = GetService(typeof(DTE)) as DTE;
            dte.Events.WindowEvents.WindowActivated += OnChangeActivatedWindow;
        }

        private void OnChangeActivatedWindow(Window GotFocus, Window LostFocus)
        {
            if (GotFocus.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase))
            {
                var openWindowPath = System.IO.Path.Combine(GotFocus.Document.Path, GotFocus.Document.Name);
                if (Utils.IsDocumentOpen(this, openWindowPath, out var buffer))
                {
                    var textBuffer = _editorAdaptorFactory.GetDataBuffer(buffer);
                    var asmType = textBuffer.CurrentSnapshot.GetAsmType();

                    if (asmType == AsmType.RadAsm || asmType == AsmType.RadAsm2)
                        UpdateFunctionList(textBuffer);
                }
            }
        }

        private void UpdateFunctionList(ITextBuffer buffer)
        {
            try
            {
                var documentAnalysis = _documentAnalysisProvider.CreateDocumentAnalysis(buffer);
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(documentAnalysis.CurrentSnapshot, documentAnalysis.LastParserResult));
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private Task UpdateFunctionListAsync(ITextSnapshot version, IReadOnlyList<IBlock> blocks)
        {
            try
            {
                var functionNames = blocks.GetFunctions().Select(b => b.Name);

                var labels = blocks
                    .SelectMany(b => b.Tokens)
                    .Where(t => t.Type == Parser.Tokens.RadAsmTokenType.Label);

                var functionListTokens = functionNames
                    .Concat(labels)
                    .Select(t => new FunctionListToken(t.Type, t.TrackingToken.GetText(version), t.TrackingToken.Start.GetPoint(version).GetContainingLine().LineNumber));

                return FunctionListControl.UpdateFunctionListAsync(functionListTokens);
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

            var documentAnalysis = _documentAnalysisProvider.CreateDocumentAnalysis(textView.TextBuffer);
            var functions = documentAnalysis.LastParserResult.GetFunctions();
            var currentFunction = GetFunctionBy(functions, line);

            if (currentFunction == null)
            {
                return Task.CompletedTask;
            }
            else
            {
                var functionToken = currentFunction.Name;
                var text = functionToken.TrackingToken.GetText(documentAnalysis.CurrentSnapshot);
                var lineNumber = functionToken
                    .TrackingToken.Start
                    .GetPoint(documentAnalysis.CurrentSnapshot)
                    .GetContainingLine().LineNumber;

                return FunctionListControl.HighlightCurrentFunctionAsync(new FunctionListToken(functionToken.Type, text, lineNumber));
            }
        }

        public static void TryHighlightCurrentFunction(ITextView textView)
        {
            if (Instance != null)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => Instance.HighlightCurrentFunctionAsync(textView));
        }

        public static void TryUpdateFunctionList(ITextSnapshot version, IReadOnlyList<IBlock> blocks)
        {
            if (Instance != null)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => Instance.UpdateFunctionListAsync(version, blocks));
        }

        private static FunctionBlock GetFunctionBy(IEnumerable<FunctionBlock> blocks, ITextSnapshotLine line)
        {
            foreach (var func in blocks)
            {
                if (func.Scope.GetSpan(line.Snapshot).Contains(line.Start))
                    return func;
            }

            return null;
        }
    }
}
