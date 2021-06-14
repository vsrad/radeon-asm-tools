using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.FunctionList
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class FunctionListProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly IDocumentFactory _documentFactory;
        private readonly Dictionary<IDocument, ITextView> _documentTextViews;
        private readonly FunctionListModel _model;
        private IAnalysisResult _lastResult;

        private static FunctionListProvider _instance;
        private static FunctionListControl _functionListControl;

        [ImportingConstructor]
        public FunctionListProvider(RadeonServiceProvider serviceProvider, IDocumentFactory documentFactory, Lazy<INavigationTokenService> navigationTokenService)
        {
            _editorAdaptersFactoryService = serviceProvider.EditorAdaptersFactoryService;
            _navigationTokenService = navigationTokenService;
            _documentFactory = documentFactory;
            _documentTextViews = new Dictionary<IDocument, ITextView>();

            _documentFactory.DocumentDisposed += DocumentDisposed;
            _documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            _model = FunctionListModel.CurrentModel;
            _instance = this;
        }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = _editorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            if (textView == null) return;

            var document = _documentFactory.GetOrCreateDocument(textView.TextBuffer);
            AssignDocumentToFunctionList(textView, document);
            ActiveDocumentChanged(document);
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (_functionListControl == null) return;
            var point = e.NewPosition.BufferPosition;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var document = _documentFactory.GetOrCreateDocument(point.Snapshot.TextBuffer);
                var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(point.Snapshot);

                var functionBlock = analysisResult.TryGetFunctionBlock(point);
                if (functionBlock == null)
                {
                    _model.SelectedItem = null;
                    return;
                }

                var lineNumber = analysisResult.Snapshot.GetLineNumberFromPosition(functionBlock.Name.Span.Start) + 1;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _model.SelectedItem = _model.Items.FirstOrDefault(f => f.LineNumber == lineNumber);
            });
        }

        #region document assignment
        private void AssignDocumentToFunctionList(ITextView textView, IDocument document)
        {
            // if opened text view associated with the same document (eg peek definition)
            // then we should not care about it
            if (_documentTextViews.ContainsKey(document))
                return;

            _documentTextViews.Add(document, textView);
            document.DocumentAnalysis.AnalysisUpdated += UpdateFunctionList;
            document.CurrentSnapshot.TextBuffer.Properties.AddProperty(typeof(FunctionListWindow), true);
            textView.Caret.PositionChanged += OnCaretPositionChanged;
        }

        private void TryRemoveDocument(IDocument document)
        {
            if (!_documentTextViews.TryGetValue(document, out var textView)) return;

            document.DocumentAnalysis.AnalysisUpdated -= UpdateFunctionList;
            document.CurrentSnapshot.TextBuffer.Properties.RemoveProperty(typeof(FunctionListWindow));
            textView.Caret.PositionChanged -= OnCaretPositionChanged;
            if (_lastResult != null && _lastResult.Snapshot.TextBuffer == document.CurrentSnapshot.TextBuffer)
                CleanContext();

            _documentTextViews.Remove(document);
        }

        private void DocumentDisposed(IDocument document) => TryRemoveDocument(document);
        #endregion

        #region update function list
        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            if (_functionListControl == null) return;

            if (activeDocument == null)
            {
                CleanContext();
            }
            else
            {
                UpdateFunctionList(activeDocument);
            }
        }

        private void CleanContext() =>
            _model.UpdateItems(Enumerable.Empty<FunctionListItem>());

        private void UpdateFunctionList(IDocument document)
        {
            var analysisResult = document.DocumentAnalysis.CurrentResult;
            if (analysisResult == null || analysisResult == _lastResult) return;
            UpdateFunctionList(analysisResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        private void UpdateFunctionList(IAnalysisResult analysisResult, RescanReason reason, CancellationToken cancellationToken)
        {
            if (reason == RescanReason.ContentChanged)
                UpdateFunctionList(analysisResult, cancellationToken);
        }

        private void UpdateFunctionList(IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            _lastResult = analysisResult;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var items = analysisResult.Scopes.SelectMany(s => s.Tokens)
                    .Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.FunctionName)
                    .Cast<IDefinitionToken>()
                    .Select(t => _navigationTokenService.Value.CreateToken(t, _lastResult.Document))
                    .Select(n => new FunctionListItem(n))
                    .AsParallel()
                    .WithCancellation(cancellationToken)
                    .ToList();

                
                FunctionListItem selectedItem = null;
                if (_documentTextViews.TryGetValue(analysisResult.Document, out var textView))
                {
                    var point = textView.Caret.Position.BufferPosition;
                    if (point.Snapshot == analysisResult.Snapshot)
                    {
                        var functionBlock = analysisResult.TryGetFunctionBlock(point);
                        if (functionBlock != null)
                        {
                            var lineNumber = analysisResult.Snapshot.GetLineNumberFromPosition(functionBlock.Name.Span.Start) + 1;
                            selectedItem = items.FirstOrDefault(f => f.LineNumber == lineNumber);
                        }
                    }
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _model.UpdateItems(items);
                _model.SelectedItem = selectedItem;
            });
        }

        private void SetLastResultFunctionList(CancellationToken cancellationToken)
        {
            if (_lastResult == null) return;
            UpdateFunctionList(_lastResult, cancellationToken);
        }

        public static void FunctionListWindowCreated(FunctionListControl functionListControl)
        {
            _functionListControl = functionListControl;
            _instance?.SetLastResultFunctionList(CancellationToken.None);
        }
        #endregion
    }
}
