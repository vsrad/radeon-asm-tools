using System.Collections.Generic;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsUpdateDelegate(IInstructionListManager sender);

    public interface IInstructionListManager
    {
        IReadOnlyList<Instruction> GetInstructions(AsmType asmType);
        event InstructionsUpdateDelegate InstructionsUpdated;
    }
}
