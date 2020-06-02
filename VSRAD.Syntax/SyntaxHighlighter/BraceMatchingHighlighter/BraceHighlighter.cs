using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.PlatformUI;
using VSRAD.Syntax.Parser;
using VSRAD.SyntaxParser;

namespace VSRAD.Syntax.SyntaxHighlighter.BraceMatchingHighlighter
{
    public class BraceHighlightWordTag : TextMarkerTag
    {
        private static string GetColorFormat()
        {
            var color = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);

            if (color.R <= 100 && color.G <= 100 && color.B <= 100)
                return PredefinedMarkerFormatNames.BraceMatchingDark;

            return PredefinedMarkerFormatNames.BraceMatchingLight;
        }

        public BraceHighlightWordTag() : base(GetColorFormat()) { }
    }

    internal class BraceHighlighter : ITagger<TextMarkerTag>
    {
        private readonly object updateLock = new object();
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly DocumentAnalysis _documentAnalysis;

        private SnapshotSpan? currentWord;
        private NormalizedSnapshotSpanCollection wordSpans;
        private CancellationTokenSource indentCts;

        internal BraceHighlighter(ITextView view, ITextBuffer sourceBuffer, DocumentAnalysis documentAnalysis)
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
            var wordSpans = new List<SnapshotSpan>();
            var bracketType = BracketType.Lbracket;
            var lbracketType = -1;
            var rbracketType = -1;

            var currentToken = _documentAnalysis.GetToken(currentRequest);
            if (currentToken.Type == _documentAnalysis.LINE_COMMENT || currentToken.Type == _documentAnalysis.BLOCK_COMMENT)
            {
                SynchronousUpdate(new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var ch = currentRequest.GetChar();
            if (ch == '(')
            {
                lbracketType = _documentAnalysis.LPAREN;
                rbracketType = _documentAnalysis.RPAREN;
            }
            else if (ch == '[')
            {
                lbracketType = _documentAnalysis.LSQUAREBRACKET;
                rbracketType = _documentAnalysis.RSQUAREBRACKET;
            }
            else if (ch == '{')
            {
                lbracketType = _documentAnalysis.LCURVEBRACKET;
                rbracketType = _documentAnalysis.RCURVEBRACKET;
            }
            else if (TryGetPreviousChar(currentRequest, out ch))
            {
                if (ch == ')')
                {
                    lbracketType = _documentAnalysis.LPAREN;
                    rbracketType = _documentAnalysis.RPAREN;
                }
                else if (ch == ']')
                {
                    lbracketType = _documentAnalysis.LSQUAREBRACKET;
                    rbracketType = _documentAnalysis.RSQUAREBRACKET;
                }
                else if (ch == '}')
                {
                    lbracketType = _documentAnalysis.LCURVEBRACKET;
                    rbracketType = _documentAnalysis.RCURVEBRACKET;
                }

                bracketType = BracketType.Rbracket;
                currentRequest -= 1;
            }

            if (lbracketType == -1 || rbracketType == -1)
            {
                SynchronousUpdate(new NormalizedSnapshotSpanCollection(), null);
                return;
            }

            var currentWord = new SnapshotSpan(currentRequest, 1);
            var bracketCounter = 1;

            if (bracketType == BracketType.Lbracket)
            {
                var searchTokens = _documentAnalysis
                    .GetTokens(new Span(currentWord.End, _documentAnalysis.CurrentSnapshot.Length - currentWord.End))
                    .Where(t => t.Type == lbracketType || t.Type == rbracketType);

                foreach (var token in searchTokens)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (token.Type == RadAsmLexer.LPAREN)
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
                var searchTokens = _documentAnalysis
                    .GetTokens(new Span(0, currentWord.Start))
                    .Where(t => t.Type == lbracketType || t.Type == rbracketType);

                foreach (var token in searchTokens.Reverse())
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (token.Type == RadAsmLexer.RPAREN)
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
    }
}
