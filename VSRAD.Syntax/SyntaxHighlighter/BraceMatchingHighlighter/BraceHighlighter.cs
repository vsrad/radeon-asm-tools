using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.SyntaxHighlighter.BraceMatchingHighlighter
{
    public class BraceHighlightWordTag : TextMarkerTag
    {
        public BraceHighlightWordTag() : base(PredefinedMarkerFormatNames.BraceMatching) { }
    }

    internal class BraceHighlighter : ITagger<TextMarkerTag>
    {
        private readonly object updateLock = new object();
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly IDocumentTokenizer _tokenizer;

        private SnapshotSpan? currentWord;
        private NormalizedSnapshotSpanCollection wordSpans;
        private CancellationTokenSource indentCts;

        internal BraceHighlighter(ITextView view, ITextBuffer sourceBuffer, IDocumentTokenizer documentTokenizer)
        {
            _view = view;
            _buffer = sourceBuffer;
            _tokenizer = documentTokenizer;

            wordSpans = new NormalizedSnapshotSpanCollection();
            indentCts = new CancellationTokenSource();
            currentWord = null;

            _view.Caret.PositionChanged += CaretPositionChanged;
            _view.LayoutChanged += ViewLayoutChanged;
            _view.Closed += ViewClosedEventHandler;
        }

        private void ViewClosedEventHandler(object sender, EventArgs e)
        {
            _view.Caret.PositionChanged -= CaretPositionChanged;
            _view.LayoutChanged -= ViewLayoutChanged;
            _view.Closed -= ViewClosedEventHandler;

            indentCts?.Dispose();
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

            if (currentWord.HasValue 
                && currentWord.Value.Snapshot == _view.TextSnapshot
                && currentWord.Value.Start < point.Value.Position
                && currentWord.Value.End > point.Value.Position)
            {
                return;
            }

            indentCts = new CancellationTokenSource();
            ThreadPool.QueueUserWorkItem(UpdateWordAdornments, new object[] { point.Value, indentCts.Token });
        }

        private void UpdateWordAdornments(object values)
        {
            try
            {
                var valuesArray = (object[])values;
                UpdateWordAdornments((SnapshotPoint)valuesArray[0], (CancellationToken)valuesArray[1]);
            }
            catch (Exception)
            {
            }
        }

        private void UpdateWordAdornments(SnapshotPoint currentRequest, CancellationToken cancellation)
        {
            var version = currentRequest.Snapshot;
            if (currentRequest == version.Length)
                return;

            var wordSpans = new List<SnapshotSpan>();
            var bracketType = BracketType.Lbracket;
            var lbracketType = RadAsmTokenType.Unknown;
            var rbracketType = RadAsmTokenType.Unknown;

            cancellation.ThrowIfCancellationRequested();
            var currentResult = _tokenizer.CurrentResult;
            var currentToken = currentResult.GetToken(currentRequest);
            if (_tokenizer.GetTokenType(currentToken.Type) == RadAsmTokenType.Comment)
            {
                SynchronousUpdate(new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var ch = currentRequest.GetChar();
            if (ch == '(')
            {
                lbracketType = RadAsmTokenType.Lparen;
                rbracketType = RadAsmTokenType.Rparen;
            }
            else if (ch == '[')
            {
                lbracketType = RadAsmTokenType.LsquareBracket;
                rbracketType = RadAsmTokenType.RsquareBracket;
            }
            else if (ch == '{')
            {
                lbracketType = RadAsmTokenType.LcurveBracket;
                rbracketType = RadAsmTokenType.RcurveBracket;
            }
            else if (TryGetPreviousChar(currentRequest, out ch))
            {
                if (ch == ')')
                {
                    lbracketType = RadAsmTokenType.Lparen;
                    rbracketType = RadAsmTokenType.Rparen;
                }
                else if (ch == ']')
                {
                    lbracketType = RadAsmTokenType.LsquareBracket;
                    rbracketType = RadAsmTokenType.RsquareBracket;
                }
                else if (ch == '}')
                {
                    lbracketType = RadAsmTokenType.LcurveBracket;
                    rbracketType = RadAsmTokenType.RcurveBracket;
                }

                bracketType = BracketType.Rbracket;
                currentRequest -= 1;
            }

            if (lbracketType == RadAsmTokenType.Unknown || rbracketType == RadAsmTokenType.Unknown)
            {
                SynchronousUpdate(new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var currentWord = new SnapshotSpan(currentRequest, 1);
            var bracketCounter = 1;

            if (bracketType == BracketType.Lbracket)
            {
                var searchTokens = currentResult
                    .GetTokens(new Span(currentWord.End, currentWord.Snapshot.Length - currentWord.End))
                    .Where(t => IsLeftOrRightBracket(t.Type, lbracketType, rbracketType));

                foreach (var token in searchTokens)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (_tokenizer.GetTokenType(token.Type) == lbracketType)
                    {
                        bracketCounter++;
                    }
                    else
                    {
                        if (--bracketCounter == 0)
                        {
                            wordSpans.Add(new SnapshotSpan(version, token.GetSpan(version)));
                            break;
                        }
                    }
                }
            }
            else if (bracketType == BracketType.Rbracket)
            {
                var searchTokens = currentResult
                    .GetTokens(new Span(0, currentWord.Start))
                    .Where(t => IsLeftOrRightBracket(t.Type, lbracketType, rbracketType));

                foreach (var token in searchTokens.Reverse())
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (_tokenizer.GetTokenType(token.Type) == rbracketType)
                    {
                        bracketCounter++;
                    }
                    else
                    {
                        if (--bracketCounter == 0)
                        {
                            wordSpans.Add(new SnapshotSpan(version, token.GetSpan(version)));
                            break;
                        }
                    }
                }
            }

            if (bracketCounter != 0)
            {
                SynchronousUpdate(new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            cancellation.ThrowIfCancellationRequested();
            SynchronousUpdate(new NormalizedSnapshotSpanCollection(wordSpans), currentWord);
        }

        private void SynchronousUpdate(NormalizedSnapshotSpanCollection newSpans, SnapshotSpan? newCurrentWord)
        {
            lock (updateLock)
            {
                wordSpans = newSpans;
                currentWord = newCurrentWord;

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
                yield return new TagSpan<BraceHighlightWordTag>(currentWord, new BraceHighlightWordTag());

            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
                yield return new TagSpan<BraceHighlightWordTag>(span, new BraceHighlightWordTag());
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private enum BracketType
        {
            Lbracket,
            Rbracket,
        }

        private static bool TryGetPreviousChar(SnapshotPoint point, out char ch)
        {
            if (point > 0)
            {
                ch = (point - 1).GetChar();
                return true;
            }

            ch = char.MinValue;
            return false;
        }

        private bool IsLeftOrRightBracket(int type, RadAsmTokenType lBracket, RadAsmTokenType rBracket)
        {
            var tokenType = _tokenizer.GetTokenType(type);
            return tokenType == lBracket || tokenType == rBracket;
        }
    }
}
