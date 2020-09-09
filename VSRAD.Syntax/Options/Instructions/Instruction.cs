using System.Collections.Generic;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public enum InstructionType
    {
        RadAsm1 = 1,
        RadAsm2 = 2,
    }

    public sealed class Instruction
    {
        public string Text { get; private set; }
        public IReadOnlyList<NavigationToken> Navigations { get; private set; }
        public InstructionType Type { get; private set; }

        public Instruction(string text, IReadOnlyList<NavigationToken> navigations, InstructionType instructionType)
        {
            Text = text;
            Navigations = navigations;
            Type = instructionType;
        }
    }
}
