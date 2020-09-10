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
    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
    {
        private ITextSnapshot currentSnapshot;
        private IReadOnlyList<Span> currentSpans;

        public OutliningTagger(IDocumentAnalysis documentAnalysis)
        {
            currentSpans = new List<Span>();
            documentAnalysis.AnalysisUpdated += AnalysisUpdated;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            foreach (var span in currentSpans)
            {
                if (currentSnapshot.Length >= span.End)
                {
                    var hintSpan = new SnapshotSpan(currentSnapshot, span.Start, span.Length);

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

        private void AnalysisUpdated(IAnalysisResult analysisResult, CancellationToken cancellationToken) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateTagSpansAsync(analysisResult.Snapshot, analysisResult.Scopes, cancellationToken));

        private async Task UpdateTagSpansAsync(ITextSnapshot textSnapshot, IReadOnlyList<IBlock> blocks, CancellationToken cancellationToken)
        {
            var newSpanElements = blocks.AsParallel()
                .WithCancellation(cancellationToken)
                .Where(b => b.Type != BlockType.Root)
                .Select(b => b.Scope.GetSpan(textSnapshot))
                .ToList();

            var oldSpanCollection = new NormalizedSpanCollection(currentSpans);
            var newSpanCollection = new NormalizedSpanCollection(newSpanElements);

            var removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart = int.MaxValue;
            int changeEnd = -1;

            if (removed.Count > 0)
            {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }
            else
            {
                changeStart = 0;
                if (currentSnapshot != null)
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

                currentSpans = newSpanElements;
                currentSnapshot = textSnapshot;
                changeEnd = changeEnd > currentSnapshot.Length ? currentSnapshot.Length : changeEnd;

                TagsChanged?.Invoke(this,
                    new SnapshotSpanEventArgs(
                        new SnapshotSpan(currentSnapshot, Span.FromBounds(changeStart, changeEnd))));
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
            private readonly SnapshotSpan _hintSpan;

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
