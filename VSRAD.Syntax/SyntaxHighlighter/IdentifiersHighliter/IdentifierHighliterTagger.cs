using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Parser;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    public class DefinitionHighlightWordTag : TextMarkerTag
    {
        private static string GetColorFormat()
        {
            var color = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);

            if (color.R <= 100 && color.G <= 100 && color.B <= 100)
                return PredefinedMarkerFormatNames.DefinitionIdentifierDark;

            return PredefinedMarkerFormatNames.DefinitionIdentifierLight;
        }

        public DefinitionHighlightWordTag() : base(GetColorFormat()) { }
    }

    public class ReferenceHighlightWordTag : TextMarkerTag
    {
        private static string GetColorFormat()
        {
            var color = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);

            if (color.R <= 100 && color.G <= 100 && color.B <= 100)
                return PredefinedMarkerFormatNames.ReferenceIdentifierDark;

            return PredefinedMarkerFormatNames.ReferenceIdentifierLight;
        }

        public ReferenceHighlightWordTag() : base(GetColorFormat()) { }
    }

    internal class HighlightWordTagger : ITagger<TextMarkerTag>
    {
        private readonly object updateLock = new object();
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly DocumentAnalysis _documentAnalysis;
        private readonly NavigationTokenService _navigationTokenService;

        private NormalizedSnapshotSpanCollection wordSpans;
        private SnapshotSpan? navigationWordSpans;
        private SnapshotSpan? currentWord;
        private SnapshotPoint requestedPoint;
        private CancellationTokenSource indentCts;

        internal HighlightWordTagger(ITextView view, 
            ITextBuffer sourceBuffer, 
            DocumentAnalysis documentAnalysis,
            NavigationTokenService definitionService)
        {
            _view = view;
            _buffer = sourceBuffer;
            _documentAnalysis = documentAnalysis;
            _navigationTokenService = definitionService;

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
            ThreadPool.QueueUserWorkItem(UpdateWordAdornments, indentCts.Token);
        }

        private void UpdateWordAdornments(object threadContext)
        {
            try
            {
                UpdateWordAdornments((CancellationToken)threadContext);
            }
            catch (Exception)
            {
            }
        }

        private void UpdateWordAdornments(CancellationToken cancellation)
        {
            var currentRequest = requestedPoint;
            var version = currentRequest.Snapshot;
            var wordSpans = new List<SnapshotSpan>();
            var word = currentRequest.GetExtent();
            var currentTokenRequest = _documentAnalysis.GetToken(currentRequest.Position);

            if (!word.IsSignificant || currentTokenRequest.Type != _documentAnalysis.IDENTIFIER)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null, null);
                return;
            }

            var currentWord = word.Span;
            if (this.currentWord.HasValue && currentWord == this.currentWord)
                return;

            cancellation.ThrowIfCancellationRequested();
            var navigationItem = _navigationTokenService.GetNaviationItem(word);
            if (navigationItem == AnalysisToken.Empty || navigationItem.Type == RadAsmTokenType.Instruction)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null, null);
                return;
            }
            var navigationTokenSpan =  new SnapshotSpan(version, navigationItem.TrackingToken.GetSpan(version));

            cancellation.ThrowIfCancellationRequested();
            var block = _documentAnalysis.LastParserResult.GetBlockBy(navigationItem);
            var blockSpan = (block.Type == Parser.Blocks.BlockType.Root) ? new Span(0, version.Length) : block.Scope.GetSpan(version);
            var wordText = currentWord.GetText();

            var lexerTokens = _documentAnalysis.GetTokens(blockSpan).Where(t => t.Type == _documentAnalysis.IDENTIFIER);
            foreach (var token in lexerTokens)
            {
                cancellation.ThrowIfCancellationRequested();

                if (token.GetText(version) == wordText)
                    wordSpans.Add(new SnapshotSpan(version, token.GetSpan(version)));
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

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
