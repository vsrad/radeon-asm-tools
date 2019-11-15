using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    internal class ErrorHighlighterTagger : ITagger<IErrorTag>
    {
        private static readonly Regex _activeWordWithBracketsRegular = new Regex(@"[\w\\$]*\[[^\[\]]*\]", RegexOptions.Compiled | RegexOptions.Singleline);
        private readonly ITextView view;
        private readonly ITextBuffer buffer;
        private readonly object updateLock;
        private readonly ITextDocument textDocument;
        private List<(int line, int column, string message)> requestedErrorList;
        private IList<(SnapshotSpan errorSpan, string message)> currentErrorSnapshotList;
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal ErrorHighlighterTagger(ErrorHighlighterTaggerProvider provider, ITextView textView, ITextBuffer sourceBuffer)
        {
            view = textView;
            buffer = sourceBuffer;
            updateLock = new object();

            var rc = buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out textDocument);
            if (!rc) throw new InvalidOperationException("Cannot find text document for this view");

            provider.ErrorsUpdated += ErrorsUpdatedEvent;
        }
        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (currentErrorSnapshotList == null)
                yield break;

            foreach (var (errorSpan, message) in currentErrorSnapshotList)
            {
                yield return new TagSpan<IErrorTag>(errorSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, message));
            }
        }

        private void ErrorsUpdatedEvent(IReadOnlyDictionary<string, List<(int line, int column, string message)>> errors)
        {
            if (errors.TryGetValue(textDocument.FilePath, out requestedErrorList))
            {
                UpdateErorMarker();
            }
            else
            {
                SynchronousUpdate(new List<(SnapshotSpan errorSpan, string message)>());
            }
        }

        private void UpdateErorMarker()
        {
            ThreadPool.QueueUserWorkItem(UpdateSpanAdornments);
        }

        private void UpdateSpanAdornments(object threadContext)
        {
            try
            {
                var errorSnapshotList = new List<(SnapshotSpan errorSpan, string message)>();

                foreach (var (line, column, message) in requestedErrorList)
                {
                    var snapshotLine = view.TextSnapshot.GetLineFromLineNumber(line - 1);
                    SnapshotSpan errorSpan;

                    if (column == 0)
                    {
                        errorSpan = new SnapshotSpan(snapshotLine.Start, snapshotLine.End);
                    }
                    else
                    {
                        try
                        {
                            var (indexStart, lenght) = GetExtentOnPosition(buffer, new SnapshotPoint(view.TextSnapshot, snapshotLine.Start + column));
                            errorSpan = new SnapshotSpan(view.TextSnapshot, snapshotLine.Start + indexStart, lenght);
                        }
                        catch (IndexOutOfRangeException) { continue; }
                    }

                    errorSnapshotList.Add((errorSpan, message));
                }

                SynchronousUpdate(errorSnapshotList);
            }
            catch (Exception e)
            {
                Microsoft.VisualStudio.Shell.ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        private void SynchronousUpdate(IList<(SnapshotSpan errorSpan, string message)> errorSnapshotList)
        {
            lock (updateLock)
            {
                currentErrorSnapshotList = errorSnapshotList;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)));
            }
        }

        private static (int indexStart, int lenght) GetExtentOnPosition(ITextBuffer textBuffer, SnapshotPoint position)
        {
            var line = textBuffer.CurrentSnapshot.GetLineFromPosition(position);
            var lineText = line.GetText();
            var caretIndex = position - line.Start;

            // check actual word with open and close brackets
            foreach (Match match in _activeWordWithBracketsRegular.Matches(lineText))
            {
                if (match.Index <= caretIndex && (match.Index + match.Length) >= caretIndex)
                {
                    return (match.Index, match.Length);
                }
            }

            // check left side of caret postion
            int indexStart;
            for (indexStart = caretIndex - 1; indexStart >= 0; indexStart--)
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
            for (indexEnd = caretIndex; indexEnd < lineText.Length; indexEnd++)
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
