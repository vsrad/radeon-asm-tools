using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActiveCodeEditor
    {
        string GetAbsoluteSourcePath();
        uint GetCurrentLine();
        string GetActiveWord(bool matchBrackets);
    }

    [Export(typeof(IActiveCodeEditor))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ActiveCodeEditor : IActiveCodeEditor
    {
        public const string NoFilesOpenError = "No files open in the editor.";

        private readonly SVsServiceProvider _serviceProvider;
        private readonly ITextDocumentFactoryService _textDocumentService;

        // this regex matches words like `\vargs` without indices like [0]
        private static readonly Regex _activeWordWithoutBracketsRegex = new Regex(@"[\w\\$]*", RegexOptions.Compiled | RegexOptions.Singleline);
        // this regex find matches like `\vargs[kernarg_1:kernarg_2]`
        private static readonly Regex _activeWordWithBracketsRegex = new Regex(@"[\w\\$]*\[[^\[\]]*\]", RegexOptions.Compiled | RegexOptions.Singleline);
        // this regex find empty brackets
        private static readonly Regex _emptyBracketsRegex = new Regex(@"\[\s*\]", RegexOptions.Compiled);

        [ImportingConstructor]
        public ActiveCodeEditor(SVsServiceProvider serviceProvider, ITextDocumentFactoryService textDocumentService)
        {
            _serviceProvider = serviceProvider;
            _textDocumentService = textDocumentService;
        }

        string IActiveCodeEditor.GetAbsoluteSourcePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var textBuffer = GetTextViewFromVsTextView(GetActiveTextView()).TextBuffer;
            _textDocumentService.TryGetTextDocument(textBuffer, out var document);
            return document.FilePath;
        }

        uint IActiveCodeEditor.GetCurrentLine()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetActiveTextView().GetCaretPos(out var line, out _);
            return (uint)line;
        }

        string IActiveCodeEditor.GetActiveWord(bool matchBrackets)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetActiveTextView().GetSelectedText(out var activeWord);
            if (activeWord.Length == 0)
            {
                var wpfTextView = GetTextViewFromVsTextView(GetActiveTextView());
                activeWord = GetWordOnPosition(wpfTextView.TextBuffer, wpfTextView.Caret.Position.BufferPosition,
                    matchBrackets);
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

        private IVsTextView GetActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var textManager = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);

            textManager.GetActiveView2(0, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            return activeView ?? throw new InvalidOperationException(NoFilesOpenError);
        }

        private static IWpfTextView GetTextViewFromVsTextView(IVsTextView view)
        {
            ErrorHandler.ThrowOnFailure(((IVsUserData)view).GetData(DefGuidList.guidIWpfTextViewHost, out var textViewHost));
            return ((IWpfTextViewHost)textViewHost).TextView;
        }
    }
}