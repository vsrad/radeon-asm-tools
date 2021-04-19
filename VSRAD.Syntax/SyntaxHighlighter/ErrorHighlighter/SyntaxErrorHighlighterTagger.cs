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
    internal sealed class SyntaxErrorHighlighterTagger : ITagger<IErrorTag>, ISyntaxDisposable
    {
        private readonly object _lock = new object();
        private readonly IDocumentAnalysis _documentAnalysis;
        private IReadOnlyList<ITagSpan<IErrorTag>> _currentErrorTags;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public SyntaxErrorHighlighterTagger(IDocumentAnalysis documentAnalysis)
        {
            _documentAnalysis = documentAnalysis;
            _documentAnalysis.AnalysisUpdated += UpdateErrorMarker;
            if (_documentAnalysis.CurrentResult != null)
                UpdateErrorMarker(_documentAnalysis.CurrentResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        public void OnDispose()
        {
            _documentAnalysis.AnalysisUpdated -= UpdateErrorMarker;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) =>
            _currentErrorTags ?? Enumerable.Empty<ITagSpan<IErrorTag>>();

        private void UpdateErrorMarker(IAnalysisResult analysisResult, RescanReason rescanReason, CancellationToken cancellationToken) =>
            Task.Run(() => UpdateSpanAdornments(analysisResult, cancellationToken), cancellationToken);

        private void UpdateSpanAdornments(IAnalysisResult analysisResult, CancellationToken cancellationToken)
        {
            if (analysisResult == null || cancellationToken.IsCancellationRequested) return;

            var errorList = analysisResult.Errors
                .AsParallel()
                .WithCancellation(cancellationToken)
                .Select(i => new TagSpan<IErrorTag>(i.Span, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, i.Message)))
                .ToList();

            if (cancellationToken.IsCancellationRequested) return;
            SynchronousUpdate(errorList, analysisResult.Snapshot);
        }

        private void SynchronousUpdate(IReadOnlyList<TagSpan<IErrorTag>> errorList, ITextSnapshot snapshot)
        {
            lock (_lock)
            {
                _currentErrorTags = errorList;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
            }
        }
    }
}
