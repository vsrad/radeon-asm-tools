using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    internal class ErrorHighlighterTagger : ITagger<IErrorTag>
    {
        private static readonly Regex _activeWordWithBracketsRegular = new Regex(@"[\w\\$]*\[[^\[\]]*\]", RegexOptions.Compiled | RegexOptions.Singleline);
        private readonly ErrorHighlighterTaggerProvider _provider;
        private readonly ITextView view;
        private readonly ITextBuffer buffer;
        private readonly object updateLock;
        private readonly ITextDocument textDocument;
        private List<ErrorMessage> requestedErrorList;
        private IEnumerable<TagSpan<IErrorTag>> currentErrorSnapshotList;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal ErrorHighlighterTagger(ErrorHighlighterTaggerProvider provider, ITextView textView, ITextBuffer sourceBuffer)
        {
            view = textView;
            buffer = sourceBuffer;
            updateLock = new object();
            _provider = provider;

            var rc = buffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument);
            if (!rc) throw new InvalidOperationException("Cannot find text document for this view");

            _provider.ErrorsUpdated += ErrorsUpdatedEvent;
            view.Closed += OnClose;
        }

        private void OnClose(object sender, EventArgs e)
        {
            _provider.ErrorsUpdated -= ErrorsUpdatedEvent;
            view.Closed -= OnClose;
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (currentErrorSnapshotList == null)
                return Enumerable.Empty<ITagSpan<IErrorTag>>();

            return currentErrorSnapshotList;
        }

        private void ErrorsUpdatedEvent(object sender, IReadOnlyDictionary<string, List<ErrorMessage>> errors)
        {
            if (errors.TryGetValue(textDocument.FilePath, out requestedErrorList))
            {
                UpdateErrorMarker();
            }
            else
            {
                SynchronousUpdate(null);
            }
        }

        private void UpdateErrorMarker()
        {
            ThreadPool.QueueUserWorkItem(UpdateSpanAdornments);
        }

        private void UpdateSpanAdornments(object threadContext)
        {
            try
            {
                var errorSnapshotList = new List<TagSpan<IErrorTag>>();

                // Note that line and column numbers in the error list start at 1
                foreach (var error in requestedErrorList)
                {
                    // Reported lines may not always match the document; check to avoid an out-of-range
                    if (error.Line >= view.TextSnapshot.LineCount)
                        continue;

                    var snapshotLine = view.TextSnapshot.GetLineFromLineNumber(error.Line);
                    SnapshotSpan errorSpan;

                    if (error.Column == 0)
                    {
                        errorSpan = new SnapshotSpan(snapshotLine.Start, snapshotLine.End);
                    }
                    else
                    {
                        var (start, lenght) = GetExtentOnLine(snapshotLine, error.Column);
                        errorSpan = new SnapshotSpan(view.TextSnapshot, start + snapshotLine.Start, lenght);
                    }

                    var errorType = error.IsFatal ? PredefinedErrorTypeNames.SyntaxError : PredefinedErrorTypeNames.Warning;
                    errorSnapshotList.Add(new TagSpan<IErrorTag>(errorSpan, new ErrorTag(errorType, error.Message)));
                }

                SynchronousUpdate(errorSnapshotList);
            }
            catch (Exception e)
            {
                Microsoft.VisualStudio.Shell.ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private void SynchronousUpdate(IEnumerable<TagSpan<IErrorTag>> errorSnapshotList)
        {
            lock (updateLock)
            {
                currentErrorSnapshotList = errorSnapshotList;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)));
            }
        }

        private static (int start, int lenght) GetExtentOnLine(ITextSnapshotLine line, int index)
        {
            if (index < 0 || index > line.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "Invalid index in line");
            var lineText = line.GetText();

            // check actual word with open and close brackets
            foreach (Match match in _activeWordWithBracketsRegular.Matches(lineText))
            {
                if (match.Index <= index && (match.Index + match.Length) >= index)
                {
                    return (match.Index, match.Length);
                }
            }

            // check left side of caret postion
            int indexStart;
            for (indexStart = index - 1; indexStart >= 0; indexStart--)
            {
                var ch = lineText[indexStart];
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '$' || ch == '\\' | ch == '.'))
                {
                    indexStart++;
                    break;
                }
            }

            // check if caret on start line it might have -1 value
            indexStart = (indexStart > 0) ? indexStart : 0;

            // check right side of caret position
            int indexEnd;
            for (indexEnd = index; indexEnd < lineText.Length; indexEnd++)
            {
                var ch = lineText[indexEnd];
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '$' || ch == '\\' | ch == '.'))
                {
                    break;
                }
            }

            return (indexStart, indexEnd - indexStart);
        }
    }
}
