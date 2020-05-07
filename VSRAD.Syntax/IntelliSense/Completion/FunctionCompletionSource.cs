using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class FunctionCompletionSource : IAsyncCompletionSource
    {
        private readonly ITextStructureNavigator _textStructureNavigator;
        private readonly IParserManager _parserManager;
        private bool _autocompleteFunctions;

        public FunctionCompletionSource(
            ITextStructureNavigator textStructureNavigator,
            OptionsProvider optionsProvider,
            IParserManager parserManager)
        {
            _textStructureNavigator = textStructureNavigator;
            _parserManager = parserManager;

            optionsProvider.OptionsUpdated += AutocompleteOptionsUpdated;
            AutocompleteOptionsUpdated(optionsProvider);
        }

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteFunctions)
                return Task.FromResult((CompletionContext)null);

            var parser = _parserManager.ActualParser;
            if (parser == null)
                return Task.FromResult((CompletionContext)null);

            var completions = parser
                .GetFunctionTokens()
                .Select(t => new CompletionItem(t.TokenName, this))
                .OrderBy(i => i.DisplayText)
                .ToImmutableArray();

            return Task.FromResult(new CompletionContext(completions));
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            var parser = _parserManager.ActualParser;
            if (parser == null)
                return Task.FromResult((object)null);

            var fb = parser.GetFunction(item.DisplayText);
            if (fb == null)
                return Task.FromResult((object)null);

            return Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(fb.FunctionToken));
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            var extent = _textStructureNavigator.GetExtentOfWord(triggerLocation - 1);
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        private void AutocompleteOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;
    }
}
