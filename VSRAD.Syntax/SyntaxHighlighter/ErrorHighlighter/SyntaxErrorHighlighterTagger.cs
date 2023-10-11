using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    internal sealed class SyntaxErrorHighlighterTagger : DocumentObserver, ITagger<IErrorTag>
    {
        private readonly object _lock = new object();
        private IReadOnlyList<ITagSpan<IErrorTag>> currentErrorTags;
        private readonly IDocumentAnalysis _documentAnalysis;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public SyntaxErrorHighlighterTagger(IDocument document) : base(document)
        {
            _documentAnalysis = document.DocumentAnalysis;
            _documentAnalysis.AnalysisUpdated += UpdateErrorMarker;

            if (_documentAnalysis.CurrentResult != null)
                UpdateErrorMarker(_documentAnalysis.CurrentResult, RescanReason.ContentChanged, document.DocumentTokenizer.CurrentResult.UpdatedTokenSpan, CancellationToken.None);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) =>
            currentErrorTags ?? Enumerable.Empty<ITagSpan<IErrorTag>>();

        private void UpdateErrorMarker(AnalysisResult analysisResult, RescanReason reason, Span updatedTokenSpan, CancellationToken cancellationToken) =>
            Task.Run(() => UpdateSpanAdornments(analysisResult, cancellationToken));

        private void UpdateSpanAdornments(AnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            if (analysisResult == null || cancellationToken.IsCancellationRequested) return;

            var errorList = analysisResult.Errors
                .Select(i => new TagSpan<IErrorTag>(i.Span, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, i.Message)))
                .ToList();

            if (cancellationToken.IsCancellationRequested) return;
            SynchronousUpdate(errorList, analysisResult.Snapshot);
        }

        private void SynchronousUpdate(List<TagSpan<IErrorTag>> errorList, ITextSnapshot snapshot)
        {
            lock (_lock)
            {
                currentErrorTags = errorList;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }

        protected override void OnClosingDocument(IDocument document)
        {
            _documentAnalysis.AnalysisUpdated -= UpdateErrorMarker;
        }
    }
}
