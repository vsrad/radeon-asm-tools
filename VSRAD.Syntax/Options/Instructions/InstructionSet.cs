using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public sealed class Instruction
    {
        public AnalysisToken Documentation { get; }
        public IReadOnlyList<NavigationToken> Aliases { get; }

        public Instruction(AnalysisToken documentation, IReadOnlyList<NavigationToken> aliases)
        {
            Documentation = documentation;
            Aliases = aliases;
        }
    }

    public interface IInstructionSet
    {
        AsmType Type { get; }
        string SetName { get; }
        IReadOnlyList<string> Targets { get; }

        /// <summary>Includes all instruction names, including aliases. All aliases point to the same Instruction object.</summary>
        IReadOnlyDictionary<string, Instruction> Instructions { get; }
    }

    internal class InstructionSet : IInstructionSet
    {
        public AsmType Type { get; }
        public string SetName { get; }
        public IReadOnlyList<string> Targets { get; }
        public IReadOnlyDictionary<string, Instruction> Instructions => _instructions;

        private readonly Dictionary<string, Instruction> _instructions = new Dictionary<string, Instruction>();

        public InstructionSet(AsmType type, string setName, IReadOnlyList<string> targets)
        {
            Type = type;
            SetName = setName;
            Targets = targets;
        }

        public InstructionSet(AsmType type, IEnumerable<IInstructionSet> subsets)
        {
            Type = type;
            SetName = "";
            Targets = Array.Empty<string>();
            foreach (var subset in subsets)
            {
                foreach (var instruction in subset.Instructions)
                    AddInstruction(instruction.Value);
            }
        }

        public void AddInstruction(Instruction instruction)
        {
            Instruction existingInstruction = null;
            foreach (var alias in instruction.Aliases)
            {
                if (_instructions.TryGetValue(alias.GetText(), out existingInstruction))
                    break;
            }
            if (existingInstruction != null)
            {
                var unionAliases = existingInstruction.Aliases.Union(instruction.Aliases).ToList();
                instruction = new Instruction(existingInstruction.Documentation, unionAliases);
            }
            foreach (var alias in instruction.Aliases)
            {
                _instructions[alias.GetText()] = instruction;
            }
        }
    }
}
