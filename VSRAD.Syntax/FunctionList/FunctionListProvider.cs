using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.FunctionList
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class FunctionListProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly IDocumentFactory _documentFactory;
        private readonly List<IDocument> _managedDocuments;
        private Tuple<IDocument, IAnalysisResult> _lastResult;

        private static FunctionListProvider _instance;
        private static FunctionListControl _functionListControl;

        [ImportingConstructor]
        public FunctionListProvider(RadeonServiceProvider serviceProvider, IDocumentFactory documentFactory, Lazy<INavigationTokenService> navigationTokenService)
        {
            _editorAdaptersFactoryService = serviceProvider.EditorAdaptersFactoryService;
            _navigationTokenService = navigationTokenService;
            _documentFactory = documentFactory;
            _managedDocuments = new List<IDocument>();

            _documentFactory.DocumentCreated += DocumentCreated;
            _documentFactory.DocumentDisposed += DocumentDisposed;
            _documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            _instance = this;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = _editorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            if (textView == null) return;

            textView.Caret.PositionChanged += (sender, e) => CaretPositionChanged(e.NewPosition.BufferPosition);

            if (TryGetDocument(textView.TextBuffer, out var document))
                AssignDocumentToFunctionList(document);
        }

        private void CaretPositionChanged(SnapshotPoint point)
        {
            if (_functionListControl == null) return;

            if (TryGetDocument(point.Snapshot.TextBuffer, out var document))
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(point.Snapshot);
                    var functionBlock = analysisResult.TryGetFunctionBlock(point);

                    if (functionBlock != null)
                    {
                        _functionListControl.HighlightItemAtLine(functionBlock.Name.Span.Start.GetContainingLine().LineNumber + 1);
                    }
                    else
                    {
                        _functionListControl.ClearHighlightItem();
                    }
                });
            }
        }

        private bool TryGetDocument(ITextBuffer textBuffer, out IDocument document)
        {
            document = _documentFactory.GetOrCreateDocument(textBuffer);
            return document != null;
        }

        #region update function list
        private void AssignDocumentToFunctionList(IDocument document)
        {
            // hack to avoid IDocumentAnalysis memory leaks
            // TODO: FunctionList needs to be refactored (see https://github.com/vsrad/radeon-asm-tools/pull/220)
            if (_managedDocuments.Contains(document))
                return;

            document.DocumentAnalysis.AnalysisUpdated += UpdateFunctionList;
            _managedDocuments.Add(document);

            ActiveDocumentChanged(document);
        }

        private void DocumentCreated(IDocument document) => AssignDocumentToFunctionList(document);

        private void DocumentDisposed(IDocument document)
        {
            if (!_managedDocuments.Contains(document))
                return;

            document.DocumentAnalysis.AnalysisUpdated -= UpdateFunctionList;
            _managedDocuments.Remove(document);

            var lastDocument = _lastResult?.Item1;
            if (lastDocument == document)
                ClearFunctionList();
        }

        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            if (_functionListControl == null) return;

            if (activeDocument == null)
            {
                ClearFunctionList();
            }
            else
            {
                UpdateFunctionList(activeDocument);
            }
        }

        private void ClearFunctionList()
        {
            _functionListControl.ClearList();
            _lastResult = null;
        }

        private void UpdateFunctionList(IDocument document)
        {
            var analysisResult = document.DocumentAnalysis.CurrentResult;
            var lastAnalysisResult = _lastResult?.Item2;

            if (analysisResult == null || analysisResult == lastAnalysisResult) return;
            UpdateFunctionList(document, analysisResult, CancellationToken.None);
        }

        private void UpdateFunctionList(IAnalysisResult analysisResult, RescanReason reason, CancellationToken cancellationToken)
        {
            if (reason != RescanReason.ContentChanged)
                return;

            var document = _documentFactory.GetOrCreateDocument(analysisResult.Snapshot.TextBuffer);
            UpdateFunctionList(document, analysisResult, cancellationToken);
        }

        private void UpdateFunctionList(IDocument document, IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            _lastResult = new Tuple<IDocument, IAnalysisResult>(document, analysisResult);

            // if document analyzed before Function List view initialization
            if (_functionListControl == null) return;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var tokens = analysisResult.Scopes.SelectMany(s => s.Tokens)
                    .Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.FunctionName)
                    .Select(t => _navigationTokenService.Value.CreateToken(t, document))
                    .Select(t => new FunctionListItem(t))
                    .AsParallel()
                    .WithCancellation(cancellationToken)
                    .ToList();

                await _functionListControl.UpdateListAsync(tokens, cancellationToken);
            });
        }

        private void SetLastResultFunctionList(CancellationToken cancellationToken)
        {
            if (_lastResult == null)
                return;

            var (document, analysisResult) = _lastResult;
            UpdateFunctionList(document, analysisResult, cancellationToken);
        }

        public static void FunctionListWindowCreated(FunctionListControl functionListControl)
        {
            _functionListControl = functionListControl;
            _instance?.SetLastResultFunctionList(CancellationToken.None);
        }
        #endregion
    }
}
