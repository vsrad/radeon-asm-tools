using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
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
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.IntelliSense.Completion
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(InstructionCompletionSourceProvider))]
    internal class InstructionCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly ITextStructureNavigatorSelectorService _textStructureNavigatorSelector;
        private readonly InstructionListManager _instructionListManager;
        private readonly OptionsEventProvider _optionsEventProvider;

        [ImportingConstructor]
        public InstructionCompletionSourceProvider(
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
            return new InstructionCompletionSource(textStructureNavigator, _instructionListManager, _optionsEventProvider);
        }
    }

    internal sealed class InstructionCompletionSource : IAsyncCompletionSource
    {
        private readonly ITextStructureNavigator _textStructureNavigator;
        private ImmutableArray<CompletionItem> _completions;
        private bool _autocompleteInstructions;

        public InstructionCompletionSource(
            ITextStructureNavigator textStructureNavigator,
            InstructionListManager instructionListManager,
            OptionsEventProvider optionsProvider)
        {
            _textStructureNavigator = textStructureNavigator;
            _completions = ImmutableArray<CompletionItem>.Empty;

            instructionListManager.InstructionUpdated += InstructionUpdated;
            optionsProvider.OptionsUpdated += DisplayOptionsUpdated;

            InstructionUpdated(instructionListManager.InstructionList);
            DisplayOptionsUpdated(optionsProvider);
        }

        private void DisplayOptionsUpdated(OptionsEventProvider sender) =>
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
            var extent = _textStructureNavigator.GetExtentOfWord(triggerLocation - 1);
            if (extent.IsSignificant && extent.Span.Length > 2)
                return new CompletionStartData(CompletionParticipation.ProvidesItems, extent.Span);

            return CompletionStartData.DoesNotParticipateInCompletion;
        }
    }
}
