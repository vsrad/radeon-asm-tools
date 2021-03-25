using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(IErrorTag))]
    public sealed class ErrorHighlighterTaggerProvider : IViewTaggerProvider
    {
        public event EventHandler<IReadOnlyDictionary<string, List<ErrorMessage>>> ErrorsUpdated;

        // Called by VSRAD.Package.ProjectSystem.ErrorListManager
        public void ErrorListUpdated(IEnumerable<ErrorTask> errorList)
        {
            var errorsPerFile = new Dictionary<string, List<ErrorMessage>>();

            foreach (var error in errorList)
            {
                if (!errorsPerFile.TryGetValue(error.Document, out var fileErrors))
                {
                    fileErrors = new List<ErrorMessage>();
                    errorsPerFile[error.Document] = fileErrors;
                }
                fileErrors.Add(new ErrorMessage
                {
                    Line = error.Line,
                    Column = error.Column,
                    Message = error.Text,
                    IsFatal = error.ErrorCategory == TaskErrorCategory.Error
                });
            }

            ErrorsUpdated?.Invoke(this, errorsPerFile);
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer) return null;
            return new ErrorHighlighterTagger(this, textView, buffer) as ITagger<T>;
        }
    }

    public sealed class ErrorMessage
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public bool IsFatal { get; set; }
    }
}
