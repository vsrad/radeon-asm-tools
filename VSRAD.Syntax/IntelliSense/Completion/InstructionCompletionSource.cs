using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
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
            DocumentAnalysis documentAnalysis) : base(optionsProvider, documentAnalysis)
        {
            _completions = ImmutableArray<CompletionItem>.Empty;
            instructionListManager.InstructionUpdated += InstructionUpdated;

            InstructionUpdated(instructionListManager.InstructionList);
            DisplayOptionsUpdated(optionsProvider);
        }

        public override Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (!_autocompleteInstructions || !_completions.Any())
                return Task.FromResult<CompletionContext>(null);

            var spanText = triggerLocation
                .GetExtent()
                .Span.GetText();

            var completions = _completions
                .Where(c => c.DisplayText.Contains(spanText))
                .ToImmutableArray();

            return completions.Any()
                ? Task.FromResult(new CompletionContext(completions))
                : Task.FromResult<CompletionContext>(null);
        }

        public override Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            Task.FromResult(IntellisenseTokenDescription.GetColorizedDescription(Parser.Tokens.RadAsmTokenType.Instruction, item.DisplayText));

        protected override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteInstructions = sender.AutocompleteInstructions;

        private void InstructionUpdated(IReadOnlyDictionary<string, List<NavigationToken>> instructions) =>
            _completions = instructions
                .Select(i => i.Key)
                .OrderBy(i => i)
                .Select(i => new CompletionItem(i, this, Icon));
    }
}
