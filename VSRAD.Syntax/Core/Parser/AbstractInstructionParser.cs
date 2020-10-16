using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractInstructionParser : AbstractParser
    {
        protected HashSet<string> Instructions { get; private set; }
        protected HashSet<string> OtherInstructions { get; private set; }
        private readonly AsmType _type;

        public AbstractInstructionParser(IDocumentFactory documentFactory,
            IInstructionListManager instructionManager,
            AsmType type)
            : base(documentFactory) 
        {
            Instructions = new HashSet<string>();
            _type = type;

            instructionManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionManager);
        }

        private void InstructionsUpdated(IInstructionListManager manager)
        {
            Instructions = manager
                .GetSelectedSetInstructions(_type)
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            OtherInstructions = manager
                .GetInstructions(_type)
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            OtherInstructions.ExceptWith(Instructions);
        }
    }
}
