using System.Collections.Generic;
using System.IO;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public enum InstructionType
    {
        RadAsm1 = 1,
        RadAsm2 = 2,
    }

#pragma warning disable CA1710 // Identifiers should have correct suffix
    public interface IInstructionSet : ISet<Instruction>, IReadOnlyCollection<Instruction>
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        InstructionType Type { get; }
        string SetName { get; }
    }

    internal class InstructionSet : HashSet<Instruction>, IInstructionSet
    {
        public InstructionType Type { get; }
        public string SetName { get; }

        public InstructionSet(string path, InstructionType instructionType)
        {
            Type = instructionType;
            SetName = Path.GetFileNameWithoutExtension(path);
        }

        public void AddInstruction(string text, IReadOnlyList<INavigationToken> navigations)
        {
            var instruction = new Instruction(text, navigations);
            Add(instruction);
        }
    }
}
