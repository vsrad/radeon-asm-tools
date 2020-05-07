using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class InstructionCompletionSource : IAsyncCompletionSource
    {
        private ImmutableArray<CompletionItem> _completions;
        private readonly IParserManager _parserManager;
        private bool _autocompleteInstructions;

        public InstructionCompletionSource(
            InstructionListManager instructionListManager,
            OptionsProvider optionsProvider,
            IParserManager parserManager)
        {
            _completions = ImmutableArray<CompletionItem>.Empty;
            _parserManager = parserManager;

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

        public Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteInstructions 
                || _parserManager.ActualParser == null 
                || _parserManager.ActualParser.PointInComment(triggerLocation)
                || _completions.IsEmpty)
                return Task.FromResult<CompletionContext>(null);

            return Task.FromResult(new CompletionContext(_completions));
        }

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
