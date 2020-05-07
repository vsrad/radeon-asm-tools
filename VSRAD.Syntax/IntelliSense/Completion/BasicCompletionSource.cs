using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class BasicCompletionSource : IAsyncCompletionSource
    {
        private readonly IDictionary<TokenType, IEnumerable<KeyValuePair<IBaseToken, CompletionItem>>> _completions;

        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public BasicCompletionSource(OptionsProvider optionsProvider)
        {
            _completions = new Dictionary<TokenType, IEnumerable<KeyValuePair<IBaseToken, CompletionItem>>>();

            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;
            DisplayOptionsUpdated(optionsProvider);
        }

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            var completions = Enumerable.Empty<CompletionItem>();
            if (_autocompleteLabels)
                completions = completions
                    .Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.Label));
            if (_autocompleteVariables)
                completions = completions
                    .Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.GlobalVariable))
                    .Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.LocalVariable))
                    .Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.Argument));

            return Task.FromResult(completions.Any() ? new CompletionContext(completions.OrderBy(c => c.DisplayText).ToImmutableArray()) : null);
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            if (TryGetDescription(TokenType.Label, item, out var description))
                return Task.FromResult(description);
            if (TryGetDescription(TokenType.GlobalVariable, item, out description))
                return Task.FromResult(description);
            if (TryGetDescription(TokenType.LocalVariable, item, out description))
                return Task.FromResult(description);
            if (TryGetDescription(TokenType.Argument, item, out description))
                return Task.FromResult(description);

            return Task.FromResult((object)string.Empty);
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            var extent = triggerLocation.GetExtent();
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        private bool TryGetDescription(TokenType tokenType, CompletionItem item, out object description)
        {
            try
            {
                if (_completions.TryGetValue(tokenType, out var pairs)
                    && pairs.Select(p => p.Value.DisplayText).Contains(item.DisplayText))
                {
                    description = IntellisenseTokenDescription.GetColorizedDescription(pairs.Single(p => p.Value.DisplayText == item.DisplayText).Key);
                    return true;
                }
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }

            description = null;
            return false;
        }

        private ImmutableArray<CompletionItem> GetScopedCompletions(ITextView textView, SnapshotPoint triggerPoint, TokenType type)
        {
            var scopedCompletions = ImmutableArray<CompletionItem>.Empty;
            var parserManager = textView.GetParserManager();
            var parser = parserManager?.ActualParser;

            if (parser == null)
                return scopedCompletions;

            var scopedCompletionPairs = parser
                .GetScopedTokens(triggerPoint, type)
                .Select(t => new KeyValuePair<IBaseToken, CompletionItem>(t, new CompletionItem(t.TokenName, this)));

            _completions[type] = scopedCompletionPairs;
            return scopedCompletionPairs
                .Select(p => p.Value)
                .ToImmutableArray();
        }

        private void DisplayOptionsUpdated(OptionsProvider options)
        {
            if (!(_autocompleteLabels = options.AutocompleteLabels))
                _completions.Remove(TokenType.Label);
            if (!(_autocompleteVariables = options.AutocompleteVariables))
            {
                _completions.Remove(TokenType.LocalVariable);
                _completions.Remove(TokenType.Argument);
                _completions.Remove(TokenType.GlobalVariable);
            }
        }
    }
}
