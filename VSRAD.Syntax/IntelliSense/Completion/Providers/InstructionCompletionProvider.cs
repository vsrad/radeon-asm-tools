using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
    internal class InstructionCompletionProvider : RadCompletionProvider
    {
        private static readonly ImageElement Icon = GetImageElement(KnownImageIds.Assembly);
        private bool _autocomplete;
        private readonly List<RadCompletionItem> _asm1InstructionCompletions;
        private readonly List<RadCompletionItem> _asm2InstructionCompletions;

        public InstructionCompletionProvider(OptionsProvider optionsProvider, IInstructionListManager instructionListManager)
            : base(optionsProvider)
        {
            _autocomplete = optionsProvider.AutocompleteInstructions;
            _asm1InstructionCompletions = new List<RadCompletionItem>();
            _asm2InstructionCompletions = new List<RadCompletionItem>();

            instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionListManager, AsmType.RadAsmCode);
        }

        public override void DisplayOptionsUpdated(OptionsProvider sender) =>
            _autocomplete = sender.AutocompleteInstructions;

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
                _asm1InstructionCompletions.AddRange(
                    GetInstructionCompletions(sender, AsmType.RadAsm));
            }

            if ((asmType & AsmType.RadAsm2) != 0)
            {
                _asm2InstructionCompletions.Clear();
                _asm2InstructionCompletions.AddRange(
                    GetInstructionCompletions(sender, AsmType.RadAsm2));
            }
        }

        private static IEnumerable<RadCompletionItem> GetInstructionCompletions(IInstructionListManager manager, AsmType asmType) =>
            manager.GetSelectedInstructionSet(asmType).Instructions
              .Select(i =>
              {
                  var info = new IntelliSenseInfo(asmType, i.Key, Core.Tokens.RadAsmTokenType.Instruction, null, i.Value.Aliases, i.Value.Documentation, null);
                  return new RadCompletionItem(info, Icon);
              });
    }
}
