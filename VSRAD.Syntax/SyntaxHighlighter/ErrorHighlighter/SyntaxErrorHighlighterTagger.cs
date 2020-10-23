using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    internal sealed class SyntaxErrorHighlighterTagger : ITagger<IErrorTag>
    {
        private readonly object _lock = new object();
        private IReadOnlyList<ITagSpan<IErrorTag>> currentErrorTags;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public SyntaxErrorHighlighterTagger(IDocumentAnalysis documentAnalysis)
        {
            documentAnalysis.AnalysisUpdated += (result, rs, cancellation) => UpdateErorMarker(result, cancellation);
            if (documentAnalysis.CurrentResult != null)
                UpdateErorMarker(documentAnalysis.CurrentResult, CancellationToken.None);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) =>
            currentErrorTags ?? Enumerable.Empty<ITagSpan<IErrorTag>>();

        private void UpdateErorMarker(IAnalysisResult analysisResult, CancellationToken cancellationToken) =>
            Task.Run(() => UpdateSpanAdornments(analysisResult, cancellationToken));

        private void UpdateSpanAdornments(IAnalysisResult analysisResult, CancellationToken cancellationToken)
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
    }
}
