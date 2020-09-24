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
        public string Text { get; }
        public IReadOnlyList<NavigationToken> Navigations { get; }
        public InstructionType Type { get; }

        public Instruction(string text, IReadOnlyList<NavigationToken> navigations, InstructionType instructionType)
        {
            Text = text;
            Navigations = navigations;
            Type = instructionType;
        }
    }
}
