using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class FunctionCompletionSource : IAsyncCompletionSource
    {
        private readonly IParserManager _parserManager;
        private bool _autocompleteFunctions;

        public FunctionCompletionSource(OptionsProvider optionsProvider, IParserManager parserManager)
        {
            _parserManager = parserManager;

            optionsProvider.OptionsUpdated += AutocompleteOptionsUpdated;
            AutocompleteOptionsUpdated(optionsProvider);
        }

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteFunctions 
                || _parserManager.ActualParser == null 
                || _parserManager.ActualParser.PointInComment(triggerLocation))
                return Task.FromResult<CompletionContext>(null);

            var parser = _parserManager.ActualParser;
            if (parser == null)
                return Task.FromResult<CompletionContext>(null);

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
                return Task.FromResult<object>(null);

            var fb = parser.GetFunction(item.DisplayText);
            if (fb == null)
                return Task.FromResult<object>(null);

            return Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(fb.FunctionToken));
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            var extent = triggerLocation.GetExtent();
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        private void AutocompleteOptionsUpdated(OptionsProvider sender) =>
            _autocompleteFunctions = sender.AutocompleteFunctions;
    }
}
