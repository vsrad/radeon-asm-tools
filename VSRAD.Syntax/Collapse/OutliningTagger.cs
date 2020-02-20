using VSRAD.Syntax.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Collapse
{
    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer buffer;
        private readonly IParserManager parserManager;
        private IBaseParser currentParser;
        private ITextSnapshot currentSnapshot;
        private IList<Span> currentSpans;

        public OutliningTagger(ITextBuffer buffer, IParserManager parserManager)
        {
            this.buffer = buffer;
            this.parserManager = parserManager;
            this.currentSpans = new List<Span>();

            this.parserManager.ParserUpdatedEvent += async (sender, args) => await ParserCompletedAsync();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
                yield break;

            foreach (var span in currentSpans.ToList())
            {
                if (currentSnapshot.Length >= span.End)
                {
                    var hintSpan = new SnapshotSpan(currentSnapshot, span.Start, span.Length);
                    yield return new TagSpan(
                        hintSpan,
                        hintSpan
                    );
                }
            }
        }

        private async Task UpdateTagSpansAsync(ITextSnapshot textSnapshot)
        {
            if (currentParser.CurrentSnapshot != textSnapshot)
                return;

            var newSpanElements = currentParser.ListBlock
                                    .Where(block => block.BlockSpan.Start.GetContainingLine().LineNumber != block.BlockSpan.End.GetContainingLine().LineNumber)
                                    .Select(block => block.BlockSpan.Span).ToList();

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
                    changeEnd = currentSnapshot.Length;
            }

            if (newSpanElements.Count > 0)
            {
                changeStart = Math.Min(changeStart, newSpanElements[0].Start);
                changeEnd = Math.Max(changeEnd, newSpanElements[newSpanElements.Count - 1].End);
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                currentSpans.Clear();
                currentSpans = newSpanElements;
                currentSnapshot = textSnapshot;

                TagsChanged?.Invoke(this,
                    new SnapshotSpanEventArgs(
                        new SnapshotSpan(currentSnapshot, Span.FromBounds(changeStart, changeEnd))));
            }
            catch (ArgumentOutOfRangeException e)
            {
                Error.LogError(e);
            }
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (e.After != buffer.CurrentSnapshot)
                return;
            ThreadHelper.JoinableTaskFactory.RunAsync(() => ParserCompletedAsync());
        }

        private Task ParserCompletedAsync()
        {
            currentParser = parserManager.ActualParser;
            return UpdateTagSpansAsync(buffer.CurrentSnapshot);
        }

        internal class TagSpan : ITagSpan<IOutliningRegionTag>
        {
            public TagSpan(SnapshotSpan span, SnapshotSpan? hintSpan)
            {
                Span = span;
                Tag = new OutliningTag(hintSpan ?? span.Start.GetContainingLine().Extent);
            }

            public SnapshotSpan Span { get; }

            public IOutliningRegionTag Tag { get; }
        }

        internal class OutliningTag : IOutliningRegionTag
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
