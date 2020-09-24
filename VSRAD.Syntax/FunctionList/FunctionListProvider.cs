using Microsoft.VisualStudio.Editor;
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
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class FunctionListProvider : IVsTextViewCreationListener
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly IDocumentFactory _documentFactory;

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
            if (textView != null)
            {
                textView.Caret.PositionChanged += (sender, e) => ThreadHelper.JoinableTaskFactory.RunAsync(() => CaretPositionChangedAsync(e.NewPosition.BufferPosition));
            }
        }

        private async Task CaretPositionChangedAsync(SnapshotPoint point)
        {
            if (FunctionListControl == null) return;

            var document = _documentFactory.GetOrCreateDocument(point.Snapshot.TextBuffer);
            if (document != null)
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
            }
        }

        #region update function list
        private void DocumentCreated(IDocument document)
        {
            document.DocumentAnalysis.AnalysisUpdated += (result, ct) => UpdateFunctionList(document, result, ct);
            document.CurrentSnapshot.TextBuffer.Properties.AddProperty(typeof(FunctionListWindow), true);
            UpdateFunctionList(document);
        }

        private void DocumentDisposed(IDocument document) =>
            document.DocumentAnalysis.AnalysisUpdated -= (result, ct) => UpdateFunctionList(document, result, ct);

        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            if (FunctionListControl == null) return;
            if (activeDocument == null)
            {
                FunctionListControl.ClearList();
            }
            else
            {
                if (!activeDocument.CurrentSnapshot.TextBuffer.Properties.ContainsProperty(typeof(FunctionListWindow)))
                {
                    // if document opened before function list window
                    DocumentCreated(activeDocument);
                    return;
                }

                UpdateFunctionList(activeDocument);
            }
        }

        private void UpdateFunctionList(IDocument document)
        {
            if (FunctionListControl == null) return;
            Task.Run(async () =>
            {
                var analysisResult = await document.DocumentAnalysis
                    .GetAnalysisResultAsync(document.CurrentSnapshot)
                    .ConfigureAwait(false);
                await UpdateFunctionListAsync(document, analysisResult, CancellationToken.None);
            }).RunAsyncWithoutAwait();
        }

        private void UpdateFunctionList(IDocument document, IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            if (FunctionListControl == null) return;
            ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateFunctionListAsync(document, analysisResult, cancellationToken));
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
