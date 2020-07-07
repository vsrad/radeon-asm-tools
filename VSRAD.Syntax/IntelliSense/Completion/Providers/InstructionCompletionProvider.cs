using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class InstructionCompletionProvider : CompletionProvider
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Assembly);
        private readonly InstructionListManager _instructionListManager;
        private readonly List<CompletionItem> _instructions;
        private bool _autocompleteInstructions;

        public InstructionCompletionProvider(InstructionListManager instructionListManager, OptionsProvider optionsProvider)
            : base(optionsProvider)
        {
            _instructionListManager = instructionListManager;
            _autocompleteInstructions = optionsProvider.AutocompleteInstructions;
            _instructions = new List<CompletionItem>();

            _instructionListManager.InstructionUpdated += InstructionSetUpdated;
            UpdateInstructionSet();
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocompleteInstructions = sender.AutocompleteInstructions;

        public override Task<CompletionContext> GetContextAsync(DocumentAnalysis documentAnalysis, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan)
        {
            if (!_autocompleteInstructions)
                return Task.FromResult(CompletionContext.Empty);

            var line = triggerLocation.GetContainingLine();
            var startSpan = new SnapshotSpan(line.Start, applicableToSpan.Start);

            if (string.IsNullOrWhiteSpace(startSpan.GetText()))
                return Task.FromResult(new CompletionContext(_instructions));

            return Task.FromResult(CompletionContext.Empty);
        }

        private void InstructionSetUpdated(IReadOnlyList<string> instructions) =>
            UpdateInstructionSet();

        private void UpdateInstructionSet()
        {
            _instructions.Clear();
            foreach (var dictonaryRow in _instructionListManager.InstructionList)
            {
                _instructions.Add(new CompletionItem(dictonaryRow.Key, Icon, dictonaryRow.Value.Select(p => p.Key).ToList()));
            }
        }
    }
}
