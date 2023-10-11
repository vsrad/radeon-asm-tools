using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.FunctionList
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class FunctionListProvider : IWpfTextViewCreationListener
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly List<IDocument> _managedDocuments;
        private Tuple<IDocument, AnalysisResult> _lastResult;

        private static FunctionListProvider _instance;
        private static FunctionListControl _functionListControl;

        [ImportingConstructor]
        public FunctionListProvider(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _managedDocuments = new List<IDocument>();

            _documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            _instance = this;
        }

        public void TextViewCreated(IWpfTextView textView) =>
            AssignViewToFunctionList(textView);

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_functionListControl == null) return;

            var position = e.NewPosition.BufferPosition;
            var snapshot = position.Snapshot;

            if (TryGetDocument(snapshot.TextBuffer, out var document))
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(snapshot);
                    var functionBlock = analysisResult.TryGetFunctionBlock(position);

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
        private void AssignViewToFunctionList(ITextView textView)
        {
            if (!TryGetDocument(textView.TextBuffer, out var document))
                return;

            AssignDocumentToFunctionList(document);

            textView.Closed += ViewClosed;
            textView.Caret.PositionChanged += CaretPositionChanged;
        }

        private void ViewClosed(object sender, EventArgs e)
        {
            var textView = (ITextView)sender;

            textView.Closed -= ViewClosed;
            textView.Caret.PositionChanged -= CaretPositionChanged;
        }

        private void AssignDocumentToFunctionList(IDocument document)
        {
            // hack to avoid IDocumentAnalysis memory leaks
            // TODO: FunctionList needs to be refactored (see https://github.com/vsrad/radeon-asm-tools/pull/220)
            if (_managedDocuments.Contains(document))
                return;

            document.DocumentClosed += DocumentClosed;
            document.DocumentAnalysis.AnalysisUpdated += UpdateFunctionList;
            _managedDocuments.Add(document);

            ActiveDocumentChanged(document);
        }

        private void DocumentClosed(IDocument document)
        {
            if (!_managedDocuments.Contains(document))
                return;

            document.DocumentClosed -= DocumentClosed;
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
            _functionListControl?.ClearList();
            _lastResult = null;
        }

        private void UpdateFunctionList(IDocument document)
        {
            var analysisResult = document.DocumentAnalysis.CurrentResult;
            var lastAnalysisResult = _lastResult?.Item2;

            // there is cases, when new document is opened, but it do not become active (see Open in Editor: Preserve active document)
            // in this case we don't want to update function list, so check that target document is in fact active
            if (analysisResult == null || analysisResult == lastAnalysisResult
                || _documentFactory.GetActiveDocumentPath() != document.Path.ToUpperInvariant()) return;
            UpdateFunctionList(document, analysisResult, CancellationToken.None);
        }

        private void UpdateFunctionList(AnalysisResult analysisResult, RescanReason reason, Span updatedTokenSpan, CancellationToken cancellationToken)
        {
            if (reason != RescanReason.ContentChanged)
                return;

            var document = _documentFactory.GetOrCreateDocument(analysisResult.Snapshot.TextBuffer);
            // there is cases, when new document is opened, but it do not become active (see Open in Editor: Preserve active document)
            // in this case we don't want to update function list, so check that target document is in fact active
            if (_documentFactory.GetActiveDocumentPath() == document.Path.ToUpperInvariant())
                UpdateFunctionList(document, analysisResult, cancellationToken);
        }

        private void UpdateFunctionList(IDocument document, AnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            _lastResult = new Tuple<IDocument, AnalysisResult>(document, analysisResult);

            // if document analyzed before Function List view initialization
            if (_functionListControl == null) return;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var tokens = analysisResult.Scopes.SelectMany(s => s.Tokens)
                    .Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.FunctionName)
                    .Select(t => new FunctionListItem(new NavigationToken(document, t)))
                    .AsParallel()
                    .WithCancellation(cancellationToken)
                    .ToList();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                _functionListControl.ReplaceListItems(tokens);
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
