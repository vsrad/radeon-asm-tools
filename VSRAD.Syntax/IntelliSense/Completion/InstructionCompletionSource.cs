using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class InstructionCompletionSource : IAsyncCompletionSource
    {
        private ImmutableArray<CompletionItem> _completions;
        private bool _autocompleteInstructions;

        public InstructionCompletionSource(
            InstructionListManager instructionListManager,
            OptionsProvider optionsProvider)
        {
            _completions = ImmutableArray<CompletionItem>.Empty;

            instructionListManager.InstructionUpdated += InstructionUpdated;
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;

            InstructionUpdated(instructionListManager.InstructionList);
            DisplayOptionsUpdated(optionsProvider);
        }

        private void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteInstructions = sender.AutocompleteInstructions;

        private void InstructionUpdated(IReadOnlyList<string> instructions) =>
            _completions = instructions
                .OrderBy(i => i)
                .Select(i => new CompletionItem(i, this))
                .ToImmutableArray();

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token) =>
            Task.FromResult(_autocompleteInstructions && !_completions.IsEmpty ? new CompletionContext(_completions) : null);

        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(Parser.Tokens.TokenType.Instruction, item.DisplayText));

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            var extent = triggerLocation.GetExtent();
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }
    }
}
