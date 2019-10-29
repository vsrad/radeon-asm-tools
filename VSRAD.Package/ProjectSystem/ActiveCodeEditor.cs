using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
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
        string GetActiveWord();
    }

    [Export(typeof(IActiveCodeEditor))]
    [AppliesTo(Constants.ProjectCapability)]
    public sealed class ActiveCodeEditor : IActiveCodeEditor
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly ITextDocumentFactoryService _textDocumentService;

        // this regular find matches like `\vargs[kernarg_1:kernarg_2]`
        private static readonly Regex _activeWordWithBracketsRegular = new Regex(@"[\w\\$]*\[[^\[\]]*\]", RegexOptions.Compiled | RegexOptions.Singleline);

        [ImportingConstructor]
        public ActiveCodeEditor(SVsServiceProvider serviceProvider, ITextDocumentFactoryService textDocumentService)
        {
            _serviceProvider = serviceProvider;
            _textDocumentService = textDocumentService;
        }

        string IActiveCodeEditor.GetAbsoluteSourcePath()
        {
            var textBuffer = GetTextViewFromVsTextView(GetActiveTextView()).TextBuffer;
            _textDocumentService.TryGetTextDocument(textBuffer, out var document);
            return document.FilePath;
        }

        uint IActiveCodeEditor.GetCurrentLine()
        {
            GetActiveTextView().GetCaretPos(out var line, out var column);
            return (uint)line;
        }

        string IActiveCodeEditor.GetActiveWord()
        {
            var activeWord = GetSelection();
            if (activeWord.Length == 0)
            {
                var wpfTextView = GetTextViewFromVsTextView(GetActiveTextView());
                activeWord = GetWordOnPosition(wpfTextView.TextBuffer, wpfTextView.Caret.Position.BufferPosition);
            }
            return activeWord;
        }

        private string GetWordOnPosition(ITextBuffer textBuffer, SnapshotPoint position)
        {
            var line = textBuffer.CurrentSnapshot.GetLineFromPosition(position);
            var lineText = line.GetText();
            var caretIndex = position - line.Start;

            // check actual word with open and close brackets
            foreach (Match match in _activeWordWithBracketsRegular.Matches(lineText))
            {
                if (match.Index <= caretIndex && (match.Index + match.Length) >= caretIndex)
                {
                    return match.Value;
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

        private string GetSelection()
        {
            GetActiveTextView().GetSelectedText(out var text);
            return text;
        }

        private IVsTextView GetActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var textManager = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);

            textManager.GetActiveView2(0, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out var activeView);
            return activeView ?? throw new InvalidOperationException("No source files opened in the editor.");
        }

        private static IWpfTextView GetTextViewFromVsTextView(IVsTextView view)
        {
            ErrorHandler.ThrowOnFailure(((IVsUserData)view).GetData(DefGuidList.guidIWpfTextViewHost, out var textViewHost));
            return ((IWpfTextViewHost)textViewHost).TextView;
        }
    }
}