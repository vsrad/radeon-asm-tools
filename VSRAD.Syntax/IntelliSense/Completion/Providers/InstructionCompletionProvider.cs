using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class InstructionCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Assembly);
        private bool _autocomplete;
        private bool _autocompleteAliases;
        private readonly IInstructionListManager _instructionListManager;
        private readonly List<RadCompletionItem> _asm1InstructionCompletions = new List<RadCompletionItem>();
        private readonly List<RadCompletionItem> _asm2InstructionCompletions = new List<RadCompletionItem>();

        public InstructionCompletionProvider(OptionsProvider optionsProvider, IInstructionListManager instructionListManager)
            : base(optionsProvider)
        {
            _autocomplete = optionsProvider.AutocompleteInstructions;
            _autocompleteAliases = optionsProvider.AutocompleteInstructionAliases;
            _instructionListManager = instructionListManager;
            _instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(_instructionListManager, AsmType.RadAsmCode);
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender)
        {
            _autocomplete = sender.AutocompleteInstructions;
            if (_autocompleteAliases != sender.AutocompleteInstructionAliases)
            {
                _autocompleteAliases = sender.AutocompleteInstructionAliases;
                InstructionsUpdated(_instructionListManager, AsmType.RadAsmCode);
            }
        }

        public override Task<RadCompletionContext> GetContextAsync(IDocument _, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_autocomplete)
                return Task.FromResult(RadCompletionContext.Empty);

            var line = triggerLocation.GetContainingLine();
            var startSpan = new SnapshotSpan(line.Start, applicableToSpan.Start);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(startSpan.GetText()))
            {
                var type = triggerLocation.Snapshot.GetAsmType();
                switch (type)
                {
                    case AsmType.RadAsm:
                        return Task.FromResult(new RadCompletionContext(_asm1InstructionCompletions));
                    case AsmType.RadAsm2:
                        return Task.FromResult(new RadCompletionContext(_asm2InstructionCompletions));
                    default:
                        return Task.FromResult(RadCompletionContext.Empty);
                }
            }

            return Task.FromResult(RadCompletionContext.Empty);
        }

        private void InstructionsUpdated(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & AsmType.RadAsm) != 0)
            {
                _asm1InstructionCompletions.Clear();
                FillInstructionCompletions(_asm1InstructionCompletions, sender.GetSelectedInstructionSet(AsmType.RadAsm), _autocompleteAliases);
            }

            if ((asmType & AsmType.RadAsm2) != 0)
            {
                _asm2InstructionCompletions.Clear();
                FillInstructionCompletions(_asm2InstructionCompletions, sender.GetSelectedInstructionSet(AsmType.RadAsm2), _autocompleteAliases);
            }
        }

        private static void FillInstructionCompletions(IList<RadCompletionItem> completions, IInstructionSet instructionSet, bool autocompleteAliases)
        {
            foreach (var i in instructionSet.Instructions)
            {
                var (instructionName, instruction) = (i.Key, i.Value);
                if (autocompleteAliases || instructionName == instruction.Aliases[0].GetText())
                {
                    var info = new IntelliSenseInfo(instructionSet.Type, instructionName, RadAsmTokenType.Instruction, null, instruction.Aliases, instruction.Documentation, null);
                    completions.Add(new RadCompletionItem(info, Icon));
                }
            }
        }
    }
}
