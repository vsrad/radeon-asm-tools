using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using System;
using VSRAD.Syntax.IntelliSense.Completion.Providers;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal class CompletionSource : IAsyncCompletionSource
    {
        private readonly IDocument _document;
        private readonly IIntelliSenseDescriptionBuilder _descriptionBuilder;
        private readonly IReadOnlyList<RadCompletionProvider> _completionProviders;

        public CompletionSource(IDocument document,
            IIntelliSenseDescriptionBuilder descriptionBuilder,
            IReadOnlyList<RadCompletionProvider> providers)
        {
            _document = document;
            _descriptionBuilder = descriptionBuilder;
            _completionProviders = providers;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) =>
            ShouldTriggerCompletion(trigger, triggerLocation)
                ? new CompletionStartData(CompletionParticipation.ProvidesItems, triggerLocation.GetExtent().Span)
                : CompletionStartData.DoesNotParticipateInCompletion;

        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            var completionContextTasks = _completionProviders.Select(p => p.GetContextAsync(_document, triggerLocation, applicableToSpan, cancellationToken));
            var completionContexts = await Task.WhenAll(completionContextTasks).ConfigureAwait(false);

            var completionItemCount = completionContexts.Sum(c => c.Items.Count);
            var completionItemBuilder = ImmutableArray.CreateBuilder<CompletionItem>(completionItemCount);
            foreach (var context in completionContexts)
                foreach (var item in context.Items)
                    completionItemBuilder.Add(item.CreateVsCompletionItem(this));

            return new CompletionContext(completionItemBuilder.MoveToImmutable());
        }

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            _descriptionBuilder.GetDescriptionAsync(RadCompletionItem.GetFromVsCompletionItem(item).Info, token);

        private bool ShouldTriggerCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation)
        {
            if (triggerLocation == triggerLocation.Snapshot.Length)
                return false;

            try
            {
                var currentToken = _document.DocumentTokenizer.CurrentResult.GetToken(triggerLocation);
                var currentTokenType = _document.DocumentTokenizer.GetTokenType(currentToken.Type);
                if (currentTokenType == RadAsmTokenType.Comment)
                {
                    return false;
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
