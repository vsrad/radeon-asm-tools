using SnapshotExtension = VSRAD.Syntax.Helpers.TextSnapshotExtension;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Operations;

namespace VSRAD.Syntax.Helpers
{
    public static class TextViewExtension
    {
        private const double OffsetLineFromTextView = 60.0;

        public static TextExtent GetTextExtentOnCursor(this ITextView view) =>
            view.Caret.Position.BufferPosition.GetExtent();

        public static void ChangeCaretPosition(this ITextView textView, int lineNumber)
        {
            try
            {
                if (textView == null)
                    return;

                var position = textView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Start;
                textView.Caret.MoveTo(position);
                textView.DisplayTextLineContainingBufferPosition(position, OffsetLineFromTextView, ViewRelativePosition.Top);
            }
            catch (Exception e)
            {
                Microsoft.VisualStudio.Shell.ActivityLog.LogWarning(Constants.RadeonAsmSyntaxContentType, e.Message);
            }
        }

        public static string GetPath(this ITextView textView)
        {
            textView.TextBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer bufferAdapter);
            var persistFileFormat = bufferAdapter as IPersistFileFormat;

            if (persistFileFormat == null)
                return null;

            persistFileFormat.GetCurFile(out var filePath, out _);
            return filePath;
        }

        public static bool CommentUncommentBlock(this ITextView textView, bool comment)
        {
            SnapshotPoint start, end;
            SnapshotPoint? mappedStart, mappedEnd;

            if (textView.Selection.IsActive && !textView.Selection.IsEmpty)
            {
                start = textView.Selection.Start.Position;
                end = textView.Selection.End.Position;
                mappedStart = MapPoint(textView, start);

                var endLine = end.GetContainingLine();
                if (endLine.Start == end)
                {
                    end = end.Snapshot.GetLineFromLineNumber(endLine.LineNumber - 1).End;
                }

                mappedEnd = MapPoint(textView, end);
            }
            else
            {
                // comment the current line
                start = end = textView.Caret.Position.BufferPosition;
                mappedStart = mappedEnd = MapPoint(textView, start);
            }

            if (mappedStart != null && mappedEnd != null &&
                mappedStart.Value <= mappedEnd.Value)
            {
                if (comment)
                {
                    CommentRegion(mappedStart.Value, mappedEnd.Value);
                }
                else
                {
                    UncommentRegion(mappedStart.Value, mappedEnd.Value);
                }

                if (textView.TextSnapshot.GetAsmType() == AsmType.RadAsm || textView.TextSnapshot.GetAsmType() == AsmType.RadAsm2)
                {
                    UpdateSelection(textView, start, end);
                }
                return true;
            }

            return false;
        }

        private static SnapshotPoint? MapPoint(ITextView view, SnapshotPoint point)
        {
            return view.BufferGraph.MapDownToFirstMatch(
               point,
               PointTrackingMode.Positive,
               (snap) => snap.GetAsmType() == AsmType.RadAsm || snap.GetAsmType() == AsmType.RadAsm2,
               PositionAffinity.Successor
            );
        }

        private static void CommentRegion(SnapshotPoint start, SnapshotPoint end)
        {
            var snapshot = start.Snapshot;

            using (var edit = snapshot.TextBuffer.CreateEdit())
            {
                int minColumn = Int32.MaxValue;
                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++)
                {
                    var curLine = snapshot.GetLineFromLineNumber(i);
                    var text = curLine.GetText();

                    int firstNonWhitespace = IndexOfNonWhitespaceCharacter(text);
                    if (firstNonWhitespace >= 0 && firstNonWhitespace < minColumn)
                    {
                        // ignore blank lines
                        minColumn = firstNonWhitespace;
                    }
                }

                // second pass, place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++)
                {
                    var curLine = snapshot.GetLineFromLineNumber(i);
                    if (String.IsNullOrWhiteSpace(curLine.GetText()))
                    {
                        continue;
                    }

                    edit.Insert(curLine.Start.Position + minColumn, "//");
                }

                edit.Apply();
            }
        }

        private static int IndexOfNonWhitespaceCharacter(string text)
        {
            for (int j = 0; j < text.Length; j++)
            {
                if (!Char.IsWhiteSpace(text[j]))
                    return j;
            }
            return -1;
        }

        private static void UncommentRegion(SnapshotPoint start, SnapshotPoint end)
        {
            var snapshot = start.Snapshot;

            using (var edit = snapshot.TextBuffer.CreateEdit())
            {

                // first pass, determine the position to place the comment
                for (int i = start.GetContainingLine().LineNumber; i <= end.GetContainingLine().LineNumber; i++)
                {
                    var curLine = snapshot.GetLineFromLineNumber(i);

                    DeleteFirstCommentChar(edit, curLine);
                }

                edit.Apply();
            }
        }

        private static void DeleteFirstCommentChar(ITextEdit edit, ITextSnapshotLine curLine)
        {
            var text = curLine.GetText();
            for (int j = 0; j < text.Length - 1; j++)
            {
                if (!Char.IsWhiteSpace(text[j]))
                {
                    if (text[j] == '/' && text[j + 1] == '/')
                        edit.Delete(curLine.Start.Position + j, 2);

                    break;
                }
            }
        }

        private static void UpdateSelection(ITextView view, SnapshotPoint start, SnapshotPoint end)
        {
            view.Selection.Select(
                new SnapshotSpan(
                    // translate to the new snapshot version:
                    start.GetContainingLine().Start.TranslateTo(view.TextBuffer.CurrentSnapshot, PointTrackingMode.Negative),
                    end.GetContainingLine().End.TranslateTo(view.TextBuffer.CurrentSnapshot, PointTrackingMode.Positive)
                ),
                false
            );
        }
    }
}
