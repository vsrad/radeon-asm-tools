using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Peek.DefinitionService;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.SyntaxHighlighter.IdentifiersHighliter
{
    public class HighlightWordTag : TextMarkerTag
    {
        private static string GetColorFormat()
        {
            var color = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);

            if (color.R <= 100 && color.G <= 100 && color.B <= 100)
                return PredefinedMarkerFormatNames.IdentifierDark;

            return PredefinedMarkerFormatNames.IdentifierLight;
        }

        public HighlightWordTag() : base(GetColorFormat()) { }
    }

    internal class HighlightWordTagger : ITagger<HighlightWordTag>
    {
        private ITextView View { get; set; }
        private ITextBuffer SourceBuffer { get; set; }
        private ITextSearchService2 TextSearchService { get; set; }

        private readonly DefinitionService DefinitionService;
        private readonly ParserManger parserManager;
        private readonly object updateLock = new object();
        private NormalizedSnapshotSpanCollection WordSpans { get; set; }
        private SnapshotSpan? CurrentWord { get; set; }
        private SnapshotPoint RequestedPoint { get; set; }

        internal HighlightWordTagger(ITextView view, ITextBuffer sourceBuffer, ITextSearchService2 textSearchService, DefinitionService definitionService)
        {
            this.View = view;
            this.SourceBuffer = sourceBuffer;
            this.TextSearchService = textSearchService;
            this.DefinitionService = definitionService;
            this.parserManager = this.SourceBuffer.Properties.GetOrCreateSingletonProperty(
                () => new ParserManger());

            WordSpans = new NormalizedSnapshotSpanCollection();
            CurrentWord = null;

            View.Caret.PositionChanged += CaretPositionChanged;
            View.LayoutChanged += ViewLayoutChanged;
        }

        #region Event Handlers
        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewViewState.EditSnapshot != e.OldViewState.EditSnapshot)
                UpdateAtCaretPosition(View.Caret.Position);
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) => UpdateAtCaretPosition(e.NewPosition);

        private void UpdateAtCaretPosition(CaretPosition caretPoisition)
        {
            SnapshotPoint? point = caretPoisition.Point.GetPoint(SourceBuffer, caretPoisition.Affinity);

            if (!point.HasValue)
                return;

            if (CurrentWord.HasValue &&
                CurrentWord.Value.Snapshot == View.TextSnapshot &&
                point.Value >= CurrentWord.Value.Start &&
                point.Value <= CurrentWord.Value.End)
            {
                return;
            }

            RequestedPoint = point.Value;

            ThreadPool.QueueUserWorkItem(UpdateWordAdornments);
        }

        private void UpdateWordAdornments(object threadContext)
        {
            var currentRequest = RequestedPoint;
            var wordSpans = new List<SnapshotSpan>();
            var word = currentRequest.GetExtent();

            if (!WordExtentIsValid(currentRequest, word))
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            SnapshotSpan currentWord = word.Span;

            if (CurrentWord.HasValue && currentWord == CurrentWord)
                return;

            var navigationItem = DefinitionService.GetNaviationItem((IWpfTextView)View, word);
            if (navigationItem == null)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var codeBlock = parserManager.ActualParser?.GetBlockByToken(navigationItem);
            if (codeBlock == null)
            {
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var commentsTokens = codeBlock.GetTokens().Where(token => token.TokenType == TokenType.Comment).ToList();

            var wordSpansInBlock = TextSearchService.FindAll(codeBlock.BlockActualSpan, currentWord.GetText(), FindOptions.WholeWord | FindOptions.MatchCase);

            // Check if requested word in comment
            foreach (var commentToken in codeBlock.Tokens.Where(token => token.TokenType == TokenType.Comment))
            {
                if (commentToken.SymbolSpan.Snapshot != currentWord.Snapshot)
                    return;

                if (commentToken.SymbolSpan.Contains(currentWord))
                {
                    SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
                    return;
                }
            }

            // Skip words which in comments
            foreach (var wordSpan in wordSpansInBlock)
            {
                if (!commentsTokens.Any(comment => comment.SymbolSpan.Contains(wordSpan)))
                    wordSpans.Add(wordSpan);
            }

            if (currentRequest == RequestedPoint)
                SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(wordSpans), currentWord);
        }

        static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word)
        {
            return word.IsSignificant && currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c));
        }

        private void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, SnapshotSpan? newCurrentWord)
        {
            lock (updateLock)
            {
                if (currentRequest != RequestedPoint)
                    return;

                WordSpans = newSpans;
                CurrentWord = newCurrentWord;

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0, SourceBuffer.CurrentSnapshot.Length)));
            }
        }

        #endregion

        #region ITagger<HighlightWordTag> Members

        public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (CurrentWord == null)
                yield break;

            SnapshotSpan currentWord = CurrentWord.Value;
            NormalizedSnapshotSpanCollection wordSpans = WordSpans;

            if (spans.Count == 0 || WordSpans.Count == 0)
                yield break;

            if (spans[0].Snapshot != wordSpans[0].Snapshot)
            {
                wordSpans = new NormalizedSnapshotSpanCollection(
                    wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWord = currentWord.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWord)))
                yield return new TagSpan<HighlightWordTag>(currentWord, new HighlightWordTag());

            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
                yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag());

        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion
    }
}
