using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class ScopeTokenCompletionSource : BasicCompletionSource
    {
        private static readonly ImageElement GlobalVariableIcon = GetImageElement(KnownImageIds.GlobalVariable);
        private static readonly ImageElement LocalVariableIcon = GetImageElement(KnownImageIds.LocalVariable);
        private static readonly ImageElement ArgumentIcon = GetImageElement(KnownImageIds.Parameter);
        private static readonly ImageElement LabelIcon = GetImageElement(KnownImageIds.Label);
        private readonly IDictionary<RadAsmTokenType, IEnumerable<KeyValuePair<AnalysisToken, CompletionItem>>> _completions;

        private bool _autocompleteLabels;
        private bool _autocompleteVariables;

        public ScopeTokenCompletionSource(
            OptionsProvider optionsProvider,
            DocumentAnalysis documentAnalysis) : base(optionsProvider, documentAnalysis)
        {
            _completions = new Dictionary<RadAsmTokenType, IEnumerable<KeyValuePair<AnalysisToken, CompletionItem>>>();
            DisplayOptionsUpdated(optionsProvider);
        }

        public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            var completions = Enumerable.Empty<CompletionItem>();
            if (_autocompleteLabels)
                completions = completions
                    .Concat(GetScopedCompletions(triggerLocation, RadAsmTokenType.Label, LabelIcon));
            if (_autocompleteVariables)
                completions = completions
                    .Concat(GetScopedCompletions(triggerLocation, RadAsmTokenType.GlobalVariable, GlobalVariableIcon))
                    .Concat(GetScopedCompletions(triggerLocation, RadAsmTokenType.LocalVariable, LocalVariableIcon))
                    .Concat(GetScopedCompletions(triggerLocation, RadAsmTokenType.FunctionParameter, ArgumentIcon));

            return completions.Any()
                ? Task.FromResult(new CompletionContext(completions.OrderBy(c => c.DisplayText).ToImmutableArray()))
                : Task.FromResult<CompletionContext>(null);
        }

        public override Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            if (TryGetDescription(RadAsmTokenType.Label, item, out var description))
                return Task.FromResult(description);
            if (TryGetDescription(RadAsmTokenType.GlobalVariable, item, out description))
                return Task.FromResult(description);
            if (TryGetDescription(RadAsmTokenType.LocalVariable, item, out description))
                return Task.FromResult(description);
            if (TryGetDescription(RadAsmTokenType.FunctionParameter, item, out description))
                return Task.FromResult(description);

            return Task.FromResult((object)string.Empty);
        }

        protected override void DisplayOptionsUpdated(OptionsProvider options)
        {
            if (!(_autocompleteLabels = options.AutocompleteLabels))
                _completions.Remove(RadAsmTokenType.Label);
            if (!(_autocompleteVariables = options.AutocompleteVariables))
            {
                _completions.Remove(RadAsmTokenType.LocalVariable);
                _completions.Remove(RadAsmTokenType.FunctionParameter);
                _completions.Remove(RadAsmTokenType.GlobalVariable);
            }
        }

        private bool TryGetDescription(RadAsmTokenType tokenType, CompletionItem item, out object description)
        {
            try
            {
                if (_completions.TryGetValue(tokenType, out var pairs)
                    && pairs.Select(p => p.Value.DisplayText).Contains(item.DisplayText))
                {
                    description = IntellisenseTokenDescription.GetColorizedTokenDescription(new NavigationToken(pairs.Single(p => p.Value.DisplayText == item.DisplayText).Key, DocumentAnalysis.CurrentSnapshot));
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

        private ImmutableArray<CompletionItem> GetScopedCompletions(SnapshotPoint triggerPoint, RadAsmTokenType type, ImageElement icon)
        {
            var triggerText = triggerPoint
                .GetExtent()
                .Span.GetText();

            var currentBlock = DocumentAnalysis.LastParserResult.GetBlockBy(triggerPoint);

            var scopedCompletionPairs = currentBlock
                .GetScopedTokens(type)
                .Where(t => t.TrackingToken.GetText(DocumentAnalysis.CurrentSnapshot).Contains(triggerText))
                .Select(t => new KeyValuePair<AnalysisToken, CompletionItem>(t, new CompletionItem(t.TrackingToken.GetText(DocumentAnalysis.CurrentSnapshot), this, icon)));

            _completions[type] = scopedCompletionPairs;
            return scopedCompletionPairs
                .Select(p => p.Value)
                .ToImmutableArray();
        }
    }
}
