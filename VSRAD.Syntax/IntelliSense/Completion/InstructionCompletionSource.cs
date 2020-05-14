using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    internal sealed class InstructionCompletionSource : BasicCompletionSource
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Assembly);
        private IEnumerable<CompletionItem> _completions;
        private bool _autocompleteInstructions;

        public InstructionCompletionSource(
            InstructionListManager instructionListManager,
            OptionsProvider optionsProvider,
            IParserManager parserManager) : base(optionsProvider, parserManager)
        {
            _completions = ImmutableArray<CompletionItem>.Empty;
            instructionListManager.InstructionUpdated += InstructionUpdated;

            InstructionUpdated(instructionListManager.InstructionList);
            DisplayOptionsUpdated(optionsProvider);
        }

        public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteInstructions || !_completions.Any())
                return Task.FromResult(CompletionContext.Empty);

            return Task.FromResult(new CompletionContext(GetCompetionItems(triggerLocation)));
        }

        public override Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(Parser.Tokens.TokenType.Instruction, item.DisplayText));

        protected override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteInstructions = sender.AutocompleteInstructions;

        private void InstructionUpdated(IReadOnlyList<string> instructions) =>
            _completions = instructions
                .OrderBy(i => i)
                .Select(i => new CompletionItem(i, this, Icon));

        private ImmutableArray<CompletionItem> GetCompetionItems(SnapshotPoint triggerPoint)
        {
            var spanText = triggerPoint
                .GetExtent()
                .Span.GetText();

            return _completions
                .Where(c => c.DisplayText.Contains(spanText))
                .ToImmutableArray();
        }
    }
}
