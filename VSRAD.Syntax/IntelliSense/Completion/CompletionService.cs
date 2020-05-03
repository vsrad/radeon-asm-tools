using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(CompletionServiceProvider))]
    internal sealed class CompletionServiceProvider
    {
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        public CompletionServiceProvider(ICompletionBroker completionBroker)
        {
            _completionBroker = completionBroker;
        }

        public CompletionService TryCreateCompletionService(ITextView textView) =>
            new CompletionService(_completionBroker, textView);
    }

    internal sealed class CompletionService
    {
        private readonly ICompletionBroker _completionBroker;
        private readonly ITextView _textView;
        private ICompletionSession _session;

        public CompletionService(ICompletionBroker completionBroker, ITextView textView)
        {
            _completionBroker = completionBroker;
            _textView = textView;
        }

        public void TriggerCompletionSession()
        {
            if (_session == null || _session.IsDismissed)
                _session = _completionBroker.TriggerCompletion(_textView);

            _session?.Filter();
        }

        public bool TryFilterSession()
        {
            if (_session == null || _session.IsDismissed)
                return false;

            _session.Filter();
            return true;
        }

        public bool TryCommitSession()
        {
            if (_session == null || _session.IsDismissed)
                return false;

            if (!_session.SelectedCompletionSet.SelectionStatus.IsSelected)
            {
                _session.Dismiss();
                return false;
            }

            _session.Commit();
            return true;
        }
    }
}
