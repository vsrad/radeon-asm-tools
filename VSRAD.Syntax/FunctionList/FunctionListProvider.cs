﻿using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;
using Task = System.Threading.Tasks.Task;

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
        private IAnalysisResult loadedResult;

        private static FunctionListControl FunctionListControl => FunctionListControl.Instance;

        [ImportingConstructor]
        public FunctionListProvider(RadeonServiceProvider serviceProvider, IDocumentFactory documentFactory, Lazy<INavigationTokenService> navigationTokenService)
        {
            _editorAdaptersFactoryService = serviceProvider.EditorAdaptersFactoryService;
            _navigationTokenService = navigationTokenService;
            _documentFactory = documentFactory;

            _documentFactory.DocumentCreated += DocumentCreated;
            _documentFactory.DocumentDisposed += DocumentDisposed;
            _documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
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
            if (FunctionListControl == null) return;

            if (TryGetDocument(point.Snapshot.TextBuffer, out var document))
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () => 
                {
                    var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(point.Snapshot);
                    var functionBlock = analysisResult.TryGetFunctionBlock(point);

                    if (functionBlock != null)
                    {
                        FunctionListControl.HighlightItemAtLine(functionBlock.Name.Span.Start.GetContainingLine().LineNumber + 1);
                    }
                    else
                    {
                        FunctionListControl.ClearHighlightItem();
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
            if (!document.CurrentSnapshot.TextBuffer.Properties.ContainsProperty(typeof(FunctionListWindow)))
            {
                document.DocumentAnalysis.AnalysisUpdated += (result, rs, ct) => UpdateFunctionList(document, result, rs, ct);
                document.CurrentSnapshot.TextBuffer.Properties.AddProperty(typeof(FunctionListWindow), true);
            }
            ActiveDocumentChanged(document);
        }

        private void DocumentCreated(IDocument document) => AssignDocumentToFunctionList(document);

        private void DocumentDisposed(IDocument document) =>
            document.DocumentAnalysis.AnalysisUpdated -= (result, rs, ct) => UpdateFunctionList(document, result, rs, ct);

        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            if (FunctionListControl == null) return;
            if (activeDocument == null)
            {
                FunctionListControl.ClearList();
            }
            else
            {
                UpdateFunctionList(activeDocument);
            }
        }

        private void UpdateFunctionList(IDocument document)
        {
            var analysisResult = document.DocumentAnalysis.CurrentResult;

            if (FunctionListControl == null || analysisResult == null) return;
            UpdateFunctionList(document, analysisResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        private void UpdateFunctionList(IDocument document, IAnalysisResult analysisResult, RescanReason reason, CancellationToken cancellationToken)
        {
            if (reason == RescanReason.ContentChanged)
                UpdateFunctionList(document, analysisResult, cancellationToken);
        }

        private void UpdateFunctionList(IDocument document, IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            if (FunctionListControl == null || analysisResult == loadedResult) return;
            loadedResult = analysisResult;
            ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(document, loadedResult, cancellationToken));
        }

        private async Task UpdateFunctionListAsync(IDocument document, IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            var tokens = analysisResult.Scopes.SelectMany(s => s.Tokens)
                .Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.FunctionName)
                .Select(t => _navigationTokenService.Value.CreateToken(t, document))
                .Select(t => new FunctionListItem(t))
                .AsParallel()
                .WithCancellation(cancellationToken)
                .ToList();

            await FunctionListControl.UpdateListAsync(tokens, cancellationToken);
        }
        #endregion
    }
}
