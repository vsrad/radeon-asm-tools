using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Helpers
{
    public static class InstructionListExtension
    {
        public static IEnumerable<INavigationToken> GetInstructionsByName(this IInstructionListManager instructionList, AsmType asmType, string instruction)
        {
            var instructions = instructionList.GetSelectedSetInstructions(asmType);
            return instructions
                .Where(i => i.Text == instruction)
                .SelectMany(i => i.Navigations);
        }
    }
}
