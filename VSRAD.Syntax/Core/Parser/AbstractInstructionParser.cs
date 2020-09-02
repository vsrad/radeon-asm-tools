using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractInstructionParser : AbstractParser
    {
        protected readonly HashSet<string> Instructions;
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
            var instructions = manager
                .GetInstructions(_type)
                .Select(i => i.Text)
                .Distinct();

            Instructions.Clear();
            foreach (var instruction in instructions)
                Instructions.Add(instruction);
        }
    }
}
