using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighlighter
{
    public class DefinitionHighlightWordTag : TextMarkerTag
    {
        public DefinitionHighlightWordTag() : base(PredefinedMarkerFormatNames.DefinitionIdentifier) { }
    }

    public class ReferenceHighlightWordTag : TextMarkerTag
    {
        public ReferenceHighlightWordTag() : base(PredefinedMarkerFormatNames.ReferenceIdentifier) { }
    }

    internal class HighlightWordTagger : ITagger<TextMarkerTag>, ISyntaxDisposable
    {
        private readonly object updateLock = new object();
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly IDocumentAnalysis _documentAnalysis;

        private NormalizedSnapshotSpanCollection wordSpans;
        private SnapshotSpan? navigationWordSpans;
        private SnapshotSpan? currentWord;
        private SnapshotPoint requestedPoint;
        private CancellationTokenSource indentCts;

        internal HighlightWordTagger(ITextView view,
            ITextBuffer sourceBuffer,
            IDocumentAnalysis documentAnalysis)
        {
            _view = view;
            _buffer = sourceBuffer;
            _documentAnalysis = documentAnalysis;

            wordSpans = new NormalizedSnapshotSpanCollection();
            indentCts = new CancellationTokenSource();
            currentWord = null;

            _view.Caret.PositionChanged += CaretPositionChanged;
            _view.LayoutChanged += ViewLayoutChanged;
        }

        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewViewState.EditSnapshot != e.OldViewState.EditSnapshot)
                UpdateAtCaretPosition(_view.Caret.Position);
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) =>
            UpdateAtCaretPosition(e.NewPosition);

        private void UpdateAtCaretPosition(CaretPosition caretPoisition)
        {
            indentCts.Cancel();
            SnapshotPoint? point = caretPoisition.Point.GetPoint(_buffer, caretPoisition.Affinity);

            if (!point.HasValue)
                return;

            if (currentWord.HasValue &&
                currentWord.Value.Snapshot == _view.TextSnapshot &&
                point.Value >= currentWord.Value.Start &&
                point.Value <= currentWord.Value.End)
            {
                return;
            }

            requestedPoint = point.Value;
            indentCts = new CancellationTokenSource();
            Task.Run(async () => await UpdateWordAdornmentsAsync(indentCts.Token))
                .RunAsyncWithoutAwait();
        }

        private async Task UpdateWordAdornmentsAsync(CancellationToken cancellation)
        {
            var currentRequest = requestedPoint;
            var version = currentRequest.Snapshot;
            if (currentRequest == version.Length) return;

            var word = currentRequest.GetExtent();
            if (!word.IsSignificant)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null, null);
                return;
            }

            var currentWord = word.Span;
            if (this.currentWord.HasValue && currentWord == this.currentWord)
                return;

            cancellation.ThrowIfCancellationRequested();

            var analysisResult = await _documentAnalysis.GetAnalysisResultAsync(version);
            var token = analysisResult.GetToken(currentRequest);
            if (token == null)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null, null);
                return;
            }

            DefinitionToken definition;
            if (token is DefinitionToken definitionToken)
                definition = definitionToken;
            else if (token is ReferenceToken referenceToken)
                definition = referenceToken.Definition;
            else
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null, null);
                return;
            }

            var wordSpans = new List<SnapshotSpan>();
            var navigationTokenSpan = definition.Snapshot == version
                ? (SnapshotSpan?)definition.Span
                : null;

            foreach (var reference in definition.References)
            {
                cancellation.ThrowIfCancellationRequested();
                if (reference.Snapshot == version)
                    wordSpans.Add(reference.Span);
            }

            if (currentRequest == requestedPoint)
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(wordSpans), currentWord, navigationTokenSpan);
        }

        private void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, SnapshotSpan? newCurrentWord, SnapshotSpan? navigationTokenSpan)
        {
            lock (updateLock)
            {
                if (currentRequest != requestedPoint)
                    return;

                wordSpans = newSpans;
                currentWord = newCurrentWord;
                navigationWordSpans = navigationTokenSpan;

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
            }
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (this.currentWord == null)
                yield break;

            var currentWord = this.currentWord.Value;
            var wordSpans = this.wordSpans;

            if (spans.Count == 0 || this.wordSpans.Count == 0)
                yield break;

            if (spans[0].Snapshot != wordSpans[0].Snapshot)
            {
                wordSpans = new NormalizedSnapshotSpanCollection(
                    wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWord = currentWord.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWord)))
                yield return new TagSpan<ReferenceHighlightWordTag>(currentWord, new ReferenceHighlightWordTag());

            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
                yield return new TagSpan<ReferenceHighlightWordTag>(span, new ReferenceHighlightWordTag());

            if (navigationWordSpans != null)
                yield return new TagSpan<DefinitionHighlightWordTag>(navigationWordSpans.Value, new DefinitionHighlightWordTag());
        }

        public void OnDispose()
        {
            _view.Caret.PositionChanged -= CaretPositionChanged;
            _view.LayoutChanged -= ViewLayoutChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
