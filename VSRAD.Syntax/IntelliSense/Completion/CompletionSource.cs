using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;
using RadCompletionItem = VSRAD.Syntax.IntelliSense.Completion.Providers.CompletionItem;
using RadCompletionContext = VSRAD.Syntax.IntelliSense.Completion.Providers.CompletionContext;
using VSRAD.Syntax.Parser.Tokens;
using System;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal class CompletionSource : IAsyncCompletionSource
    {
        public const string CompletionItemKey = nameof(CompletionItem);
        private readonly DocumentAnalysis _documentAnalysis;
        private readonly IReadOnlyList<Providers.CompletionProvider> _completionProviders;

        public CompletionSource(DocumentAnalysis documentAnalysis, IReadOnlyList<Providers.CompletionProvider> providers)
        {
            _documentAnalysis = documentAnalysis;
            _completionProviders = providers;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) =>
            ShouldTriggerCompletion(trigger, triggerLocation)
                ? new CompletionStartData(CompletionParticipation.ProvidesItems, triggerLocation.GetExtent().Span)
                : CompletionStartData.DoesNotParticipateInCompletion;

        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            var completionContexts = await ComputeNonEmptyCompletionContextsAsync(triggerLocation, applicableToSpan);

            if (completionContexts.Length == 0)
                return null;

            var completionItems = new List<CompletionItem>();
            foreach (var context in completionContexts)
            {
                foreach (var item in context.Items)
                {
                    var completionItem = new CompletionItem(item.Text, this, item.ImageElement);
                    completionItem.Properties[CompletionItemKey] = item;
                    completionItems.Add(completionItem);
                }
            }

            return new CompletionContext(completionItems.ToImmutableArray());
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            var completionItem = (RadCompletionItem)item.Properties[CompletionItemKey];
            return Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(completionItem.Tokens));
        }

        private async Task<ImmutableArray<RadCompletionContext>> ComputeNonEmptyCompletionContextsAsync(SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan)
        {
            var completionContextTasks = new List<Task<RadCompletionContext>>();
            foreach (var provider in _completionProviders)
            {
                completionContextTasks.Add(provider.GetContextAsync(_documentAnalysis, triggerLocation, applicableToSpan));
            }

            var completionContexts = await Task.WhenAll(completionContextTasks).ConfigureAwait(false);
            return completionContexts.Where(c => c.Items.Count > 0).ToImmutableArray();
        }

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation)
        {
            if (triggerLocation == triggerLocation.Snapshot.Length)
                return false;

            try
            {
                var currentTokenType = _documentAnalysis.LexerTokenToRadAsmToken(_documentAnalysis.GetToken(triggerLocation).Type);
                if (currentTokenType == RadAsmTokenType.Comment)
                {
                    return false;
                }

                if (triggerLocation > 0)
                {
                    var previousTokenType = _documentAnalysis.LexerTokenToRadAsmToken(_documentAnalysis.GetToken(triggerLocation - 1).Type);
                    if (previousTokenType == RadAsmTokenType.Keyword
                        || previousTokenType == RadAsmTokenType.Preprocessor
                        || previousTokenType == RadAsmTokenType.Number)
                    {
                        return false;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // outdated parser results so you cannot get valid tokens for the trigger location
                return false;
            }

            if (trigger.Reason == CompletionTriggerReason.Invoke
                    || trigger.Reason == CompletionTriggerReason.InvokeAndCommitIfUnique)
            {
                return true;
            }

            if (trigger.Reason == CompletionTriggerReason.Insertion && (trigger.Character == '\n' || trigger.Character == '\t')
                || trigger.Reason == CompletionTriggerReason.Deletion
                || trigger.Reason == CompletionTriggerReason.Backspace)
            {
                return false;
            }

            var extend = triggerLocation.GetExtent();
            if (extend.Span.Length < 3)
                return false;

            return true;
        }
    }
}
