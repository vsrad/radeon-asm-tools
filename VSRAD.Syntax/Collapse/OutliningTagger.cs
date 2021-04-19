using VSRAD.Syntax.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core.Blocks;
using System.Threading;

namespace VSRAD.Syntax.Collapse
{
    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>, ISyntaxDisposable
    {
        private readonly IDocumentAnalysis _documentAnalysis;
        private ITextSnapshot _currentSnapshot;
        private IReadOnlyList<Span> _currentSpans;

        public OutliningTagger(IDocumentAnalysis documentAnalysis)
        {
            _currentSpans = new List<Span>();
            _documentAnalysis = documentAnalysis;
            _documentAnalysis.AnalysisUpdated += AnalysisUpdated;
            if (_documentAnalysis.CurrentResult != null) 
                AnalysisUpdated(_documentAnalysis.CurrentResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        public void OnDispose()
        {
            _documentAnalysis.AnalysisUpdated -= AnalysisUpdated;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            foreach (var span in _currentSpans)
            {
                if (_currentSnapshot.Length >= span.End)
                {
                    var hintSpan = new SnapshotSpan(_currentSnapshot, span.Start, span.Length);

                    // skip one line blocks
                    if (hintSpan.Start.GetContainingLine().LineNumber == hintSpan.End.GetContainingLine().LineNumber)
                        continue;

                    yield return new TagSpan(
                        hintSpan,
                        hintSpan
                    );
                }
            }
        }

        private void AnalysisUpdated(IAnalysisResult analysisResult, RescanReason reason, CancellationToken cancellationToken)
        {
            if (reason == RescanReason.ContentChanged)
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateTagSpansAsync(analysisResult.Snapshot, analysisResult.Scopes, cancellationToken));
        }

        private async Task UpdateTagSpansAsync(ITextSnapshot textSnapshot, IReadOnlyList<IBlock> blocks, CancellationToken cancellationToken)
        {
            var newSpanElements = blocks.AsParallel()
                .WithCancellation(cancellationToken)
                .Where(b => b.Type != BlockType.Root)
                .Select(b => b.Scope)
                .ToList();

            var oldSpanCollection = new NormalizedSpanCollection(_currentSpans);
            var newSpanCollection = new NormalizedSpanCollection(newSpanElements);

            var removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart;
            int changeEnd = -1;

            if (removed.Count > 0)
            {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }
            else
            {
                changeStart = 0;
                if (_currentSnapshot != null)
                    changeEnd = textSnapshot.Length;
            }

            if (newSpanElements.Count > 0)
            {
                changeStart = Math.Min(changeStart, newSpanElements[0].Start);
                changeEnd = Math.Max(changeEnd, newSpanElements[newSpanElements.Count - 1].End);
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _currentSpans = newSpanElements;
                _currentSnapshot = textSnapshot;
                changeEnd = changeEnd > _currentSnapshot.Length ? _currentSnapshot.Length : changeEnd;

                TagsChanged?.Invoke(this,
                    new SnapshotSpanEventArgs(
                        new SnapshotSpan(_currentSnapshot, Span.FromBounds(changeStart, changeEnd))));
            }
            catch (ArgumentOutOfRangeException e)
            {
                Error.LogError(e);
            }
        }

        private class TagSpan : ITagSpan<IOutliningRegionTag>
        {
            public TagSpan(SnapshotSpan span, SnapshotSpan? hintSpan)
            {
                Span = span;
                Tag = new OutliningTag(hintSpan ?? span.Start.GetContainingLine().Extent);
            }

            public SnapshotSpan Span { get; }

            public IOutliningRegionTag Tag { get; }
        }

        private class OutliningTag : IOutliningRegionTag
        {
            private SnapshotSpan _hintSpan;

            public OutliningTag(SnapshotSpan hintSpan)
            {
                _hintSpan = hintSpan;
            }

            public object CollapsedForm => "...";

            public object CollapsedHintForm => _hintSpan.GetText();

            public bool IsDefaultCollapsed => false;

            public bool IsImplementation => true;
        }
    }
}
