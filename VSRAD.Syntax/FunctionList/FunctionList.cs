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
using VSRAD.Syntax.Core;
using System.Collections.Generic;
using VSRAD.Syntax.Core.Blocks;
using Microsoft.VisualStudio;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionList : ToolWindowPane
    {
        private const string CaptionName = "Function list";

        private IVsTextManager _textManager;
        private IVsEditorAdaptersFactoryService _editorAdaptorFactory;
        private DocumentAnalysisProvoder _documentAnalysisProvider;
        private FunctionBlock lastSelectedFunction;

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

            if (_textManager.GetActiveView(1, null, out var textViewCurrent) != VSConstants.S_OK)
                return null;

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
            var activeView = GetActiveTextView();
            if (activeView != null)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(activeView.TextBuffer));

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
                        ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(textBuffer));
                    else
                        ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(version: null, blocks: new List<IBlock>()));
                }
            }
        }

        private async Task UpdateFunctionListAsync(ITextBuffer buffer)
        {
            try
            {
                lastSelectedFunction = null;
                await FunctionListControl.ClearHighlightCurrentFunctionAsync();

                var documentAnalysis = _documentAnalysisProvider.CreateDocumentAnalysis(buffer);
                await UpdateFunctionListAsync(documentAnalysis.CurrentSnapshot, documentAnalysis.LastParserResult);
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
                    .Where(t => t.Type == Core.Tokens.RadAsmTokenType.Label);

                var functionListTokens = functionNames
                    .Concat(labels)
                    .Select(t => new FunctionListItem(t.Type, t.TrackingToken.GetText(version), t.TrackingToken.Start.GetPoint(version).GetContainingLine().LineNumber));

                return FunctionListControl.UpdateFunctionListAsync(version, functionListTokens);
            }
            catch (Exception e)
            {
                Error.LogError(e);
                return Task.CompletedTask;
            }
        }

        private async Task HighlightCurrentFunctionAsync(SnapshotPoint position)
        {
            try
            {
                var documentAnalysis = _documentAnalysisProvider.CreateDocumentAnalysis(position.Snapshot.TextBuffer);
                var functions = documentAnalysis.LastParserResult.GetFunctions();
                var currentFunction = GetFunctionBy(functions, position);

                if (currentFunction == lastSelectedFunction) return;

                lastSelectedFunction = currentFunction;
                if (currentFunction == null)
                {
                    await FunctionListControl.ClearHighlightCurrentFunctionAsync();
                    return;
                }

                var functionToken = currentFunction.Name;
                var lineNumber = functionToken
                    .TrackingToken.Start
                    .GetPoint(documentAnalysis.CurrentSnapshot)
                    .GetContainingLine().LineNumber;

                await FunctionListControl.HighlightCurrentFunctionAsync(functionToken.Type, lineNumber + 1 /* numbering starts from 1 */);
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        public static void TryHighlightCurrentFunction(SnapshotPoint point)
        {
            if (Instance != null)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => Instance.HighlightCurrentFunctionAsync(point));
        }

        public static void TryUpdateFunctionList(ITextSnapshot version, IReadOnlyList<IBlock> blocks)
        {
            if (Instance != null)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => Instance.UpdateFunctionListAsync(version, blocks));
        }

        private static FunctionBlock GetFunctionBy(IEnumerable<FunctionBlock> blocks, SnapshotPoint position) =>
            blocks.FirstOrDefault(func => PointInFunction(func, position));

        private static bool PointInFunction(FunctionBlock func, SnapshotPoint position)
        {
            var scope = func.GetActualScope(position.Snapshot);
            return scope.Contains(position);
        }
    }
}
