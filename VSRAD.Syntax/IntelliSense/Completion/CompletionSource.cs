using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(CompletionSourceProvider))]
    internal class CompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsEventProvider _optionsEventProvider;

        [ImportingConstructor]
        public CompletionSourceProvider(
            ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService,
            OptionsEventProvider optionsEventProvider,
            InstructionListManager instructionListManager)
        {
            _textStructureNavigatorSelector = textStructureNavigatorSelectorService;
            _instructionListManager = instructionListManager;
            _optionsEventProvider = optionsEventProvider;
        }

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (textView.TextBuffer == null)
                throw new ArgumentNullException(nameof(textView));

            var textStructureNavigator = _textStructureNavigatorSelector.GetTextStructureNavigator(textView.TextBuffer);
            return new CompletionSource(textStructureNavigator, _optionsEventProvider);
        }
    }

    internal sealed class CompletionSource : IAsyncCompletionSource
    {
        private static readonly ImageElement CompletionItemIcon = new ImageElement(new ImageId(new Guid("D0DBB6F5-765C-4582-8CE3-F412F0830FA1"), 1), "Hello Icon");
        private readonly ITextStructureNavigator _textStructureNavigator;
        private readonly IDictionary<TokenType, IEnumerable<KeyValuePair<IBaseToken, CompletionItem>>> _completions;

        private bool _autocompleteFunctions;
        private bool _autocompleteLabels;
        private bool _autocompleteGlobalVariables;

        public CompletionSource(
            ITextStructureNavigator textStructureNavigator,
            OptionsEventProvider optionsProvider)
        {
            _textStructureNavigator = textStructureNavigator;
            _completions = new Dictionary<TokenType, IEnumerable<KeyValuePair<IBaseToken, CompletionItem>>>();

            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;
            DisplayOptionsUpdated(optionsProvider);
        }

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            var completions = Enumerable.Empty<CompletionItem>();
            if (_autocompleteFunctions)
                completions = completions.Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.Function));
            if (_autocompleteLabels)
                completions = completions.Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.Label));
            if (_autocompleteGlobalVariables)
                completions = completions.Concat(GetScopedCompletions(session.TextView, triggerLocation, TokenType.GlobalVariable));

            return Task.FromResult(completions.Any() ? new CompletionContext(completions.ToImmutableArray()) : null);
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            if (TryGetDescription(TokenType.Function, item, out var description))
                return Task.FromResult(description);
            if (TryGetDescription(TokenType.Label, item, out description))
                return Task.FromResult(description);
            if (TryGetDescription(TokenType.GlobalVariable, item, out description))
                return Task.FromResult(description);

            return Task.FromResult((object)string.Empty);
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            var extent = _textStructureNavigator.GetExtentOfWord(triggerLocation - 1);
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
                .Select(t => new KeyValuePair<IBaseToken, CompletionItem>(t, new CompletionItem(t.TokenName, this, CompletionItemIcon)));

            _completions[type] = scopedCompletionPairs;
            return scopedCompletionPairs
                .Select(p => p.Value)
                .ToImmutableArray();
        }

        private void DisplayOptionsUpdated(OptionsEventProvider options)
        {
            if (!(_autocompleteFunctions = options.AutocompleteFunctions))
                _completions.Remove(TokenType.Function);
            if (!(_autocompleteLabels = options.AutocompleteLabels))
                _completions.Remove(TokenType.Label);
            if (!(_autocompleteGlobalVariables = options.AutocompleteVariables))
                _completions.Remove(TokenType.GlobalVariable);
        }
    }
}
