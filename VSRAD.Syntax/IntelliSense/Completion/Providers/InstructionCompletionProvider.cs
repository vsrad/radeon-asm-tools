using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Options.Instructions;
using VSRAD.Syntax.Helpers;
using System.Threading;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class InstructionCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement Icon = RadAsmTokenType.Instruction.GetImageElement();
        private bool _autocomplete;
        private readonly List<InstructionCompletionItem> _asm1InstructionCompletions;
        private readonly List<InstructionCompletionItem> _asm2InstructionCompletions;

        public InstructionCompletionProvider(GeneralOptionProvider generalOptionProvider, IInstructionListManager instructionListManager)
            : base(generalOptionProvider)
        {
            _autocomplete = generalOptionProvider.AutocompleteInstructions;
            _asm1InstructionCompletions = new List<InstructionCompletionItem>();
            _asm2InstructionCompletions = new List<InstructionCompletionItem>();

            instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionListManager, AsmType.RadAsmCode);
        }

        public override void DisplayOptionsUpdated(GeneralOptionProvider sender) =>
            _autocomplete = sender.AutocompleteInstructions;

        public override Task<RadCompletionContext> GetContextAsync(IDocument _, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken cancellationToken)
        {
            if (!_autocomplete)
                return Task.FromResult(RadCompletionContext.Empty);

            var line = triggerLocation.GetContainingLine();
            var startSpan = new SnapshotSpan(line.Start, applicableToSpan.Start);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(startSpan.GetText())) return Task.FromResult(RadCompletionContext.Empty);

            switch (triggerLocation.Snapshot.GetAsmType())
            {
                case AsmType.RadAsm:
                    return Task.FromResult(new RadCompletionContext(_asm1InstructionCompletions));
                case AsmType.RadAsm2:
                    return Task.FromResult(new RadCompletionContext(_asm2InstructionCompletions));
                default:
                    return Task.FromResult(RadCompletionContext.Empty);
            }
        }

        private void InstructionsUpdated(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & AsmType.RadAsm) != 0)
            {
                _asm1InstructionCompletions.Clear();
                _asm1InstructionCompletions.AddRange(
                    sender.GetSelectedSetInstructions(AsmType.RadAsm)
                        .GroupBy(i => i.Text)
                        .Select(g => new InstructionCompletionItem(g, g.Key, Icon))
                    );
            }

            if ((asmType & AsmType.RadAsm2) != 0)
            {
                _asm2InstructionCompletions.Clear();
                _asm2InstructionCompletions.AddRange(
                    sender.GetSelectedSetInstructions(AsmType.RadAsm2)
                        .GroupBy(i => i.Text)
                        .Select(g => new InstructionCompletionItem(g, g.Key, Icon))
                    );
            }
        }
    }
}
