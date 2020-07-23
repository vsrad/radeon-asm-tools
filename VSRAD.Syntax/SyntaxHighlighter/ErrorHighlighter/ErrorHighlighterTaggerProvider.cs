using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TagType(typeof(IErrorTag))]
    public sealed class ErrorHighlighterTaggerProvider : IViewTaggerProvider
    {
        public delegate void ErrorsUpdateDelegate(IReadOnlyDictionary<string, List<(int line, int column, string message)>> errors);

        public event ErrorsUpdateDelegate ErrorsUpdated;

        private readonly SVsServiceProvider _serviceProvider;
        private ErrorList _errorList;

        [ImportingConstructor]
        public ErrorHighlighterTaggerProvider(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Called by VSRAD.Package.ProjectSystem.ErrorListManager
        public void ErrorListUpdated()
        {
            if (_errorList == null)
            {
                var dte = (DTE2)_serviceProvider.GetService(typeof(SDTE));
                _errorList = dte.ToolWindows.ErrorList;
            }

            var errors = new Dictionary<string, List<(int line, int column, string message)>>();
            // the list doesn't contain hidden elements and we can’t affect it.
            // But we can show it and then return the state
            var showError = _errorList.ShowErrors;
            var showWarning = _errorList.ShowWarnings;
            _errorList.ShowErrors = true;
            _errorList.ShowWarnings = true;

            var errorList = _errorList.ErrorItems;
            for (int i = 1; i <= errorList.Count; i++)
            {
                var error = errorList.Item(i);

                if (!errors.ContainsKey(error.FileName))
                    errors[error.FileName] = new List<(int line, int column, string message)>();

                errors[error.FileName].Add((error.Line, error.Column, error.Description));
            }

            _errorList.ShowErrors = showError;
            _errorList.ShowWarnings = showWarning;
            ErrorsUpdated?.Invoke(errors);
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView.TextBuffer != buffer)
                return null;

            return new ErrorHighlighterTagger(this, textView, buffer) as ITagger<T>;
        }
    }
}
