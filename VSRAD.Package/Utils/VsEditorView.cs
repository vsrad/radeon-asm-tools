using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Text.RegularExpressions;

namespace VSRAD.Package.Utils
{
    public interface IEditorView
    {
        string GetFilePath();
        (uint Line, uint Column) GetCaretPos();
        (uint FirstVisibleLine, uint VisibleLines) GetVerticalScrollWindow();
        string GetActiveWord(bool matchBrackets);
    }

    public sealed class VsEditorView : IEditorView
    {
        private readonly IVsTextView _vsTextView;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;

        // Match words like `\vargs` without indices like [0]
        private static readonly Regex _activeWordWithoutBracketsRegex = new Regex(@"[\w\\$]*", RegexOptions.Compiled | RegexOptions.Singleline);
        // Match words like `\vargs[kernarg_1:kernarg_2]`
        private static readonly Regex _activeWordWithBracketsRegex = new Regex(@"[\w\\$]*\[[^\[\]]*\]", RegexOptions.Compiled | RegexOptions.Singleline);
        // Match empty brackets
        private static readonly Regex _emptyBracketsRegex = new Regex(@"\[\s*\]", RegexOptions.Compiled);

        public VsEditorView(IVsTextView vsTextView, ITextDocumentFactoryService textDocumentFactoryService)
        {
            _vsTextView = vsTextView;
            _textDocumentFactoryService = textDocumentFactoryService;
        }

        public string GetFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var textBuffer = GetTextViewFromVsTextView(_vsTextView).TextBuffer;
            Assumes.True(_textDocumentFactoryService.TryGetTextDocument(textBuffer, out var document));
            return document.FilePath;
        }

        public (uint Line, uint Column) GetCaretPos()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(_vsTextView.GetCaretPos(out var line, out var column));
            return ((uint)line, (uint)column);
        }

        public (uint FirstVisibleLine, uint VisibleLines) GetVerticalScrollWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(_vsTextView.GetScrollInfo(1/*SB_VERT*/, out _, out _, out var visibleLines, out var firstVisibleLine));
            return ((uint)firstVisibleLine, (uint)visibleLines);
        }

        public string GetActiveWord(bool matchBrackets)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ErrorHandler.ThrowOnFailure(_vsTextView.GetSelectedText(out var activeWord));
            if (activeWord.Length == 0)
            {
                var wpfTextView = GetTextViewFromVsTextView(_vsTextView);
                activeWord = GetWordOnPosition(wpfTextView.TextBuffer, wpfTextView.Caret.Position.BufferPosition, matchBrackets);
            }
            return activeWord.Trim();
        }

        private string GetWordOnPosition(ITextBuffer textBuffer, SnapshotPoint position, bool matchBrackets)
        {
            var line = textBuffer.CurrentSnapshot.GetLineFromPosition(position);
            var lineText = line.GetText();
            var caretIndex = position - line.Start;

            // check actual word
            foreach (Match match in matchBrackets
                            ? _activeWordWithBracketsRegex.Matches(lineText)
                            : _activeWordWithoutBracketsRegex.Matches(lineText))
            {
                if (match.Index <= caretIndex && (match.Index + match.Length) >= caretIndex)
                {
                    return matchBrackets ? _emptyBracketsRegex.Replace(match.Value, "") : match.Value;
                }
            }

            // check left side of caret postion
            int indexStart;
            for (indexStart = caretIndex - 1; indexStart >= 0; indexStart--)
            {
                var ch = lineText[indexStart];
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '$' || ch == '\\'))
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
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '$' || ch == '\\'))
                {
                    break;
                }
            }

            var word = lineText.Substring(indexStart, indexEnd - indexStart);
            return word;
        }

        private static IWpfTextView GetTextViewFromVsTextView(IVsTextView view)
        {
            ErrorHandler.ThrowOnFailure(((IVsUserData)view).GetData(DefGuidList.guidIWpfTextViewHost, out var textViewHost));
            return ((IWpfTextViewHost)textViewHost).TextView;
        }
    }
}
