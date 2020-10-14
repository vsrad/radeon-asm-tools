using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsUpdateDelegate(IInstructionListManager sender);
    public interface IInstructionListManager
    {
        IEnumerable<Instruction> GetInstructions(AsmType asmType);
        event InstructionsUpdateDelegate InstructionsUpdated;
    }

    [Export(typeof(IInstructionListManager))]
    internal sealed class InstructionListManager : IInstructionListManager
    {
        private static readonly ReaderWriterLock readerWriterLock = new ReaderWriterLock();
        private readonly List<IInstructionSet> _radAsm1InstructionSets;
        private readonly List<IInstructionSet> _radAsm2InstructionSets;
        private readonly List<Instruction> _radAsm1Instructions;
        private readonly List<Instruction> _radAsm2Instructions;

        private IInstructionSet radAsm1SelectedSet;
        private IInstructionSet radAsm2SelectedSet;

        public event InstructionsUpdateDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListManager(IInstructionListLoader instructionListLoader)
        {
            instructionListLoader.InstructionsUpdated += InstructionsLoaded;
            _radAsm1InstructionSets = new List<IInstructionSet>();
            _radAsm2InstructionSets = new List<IInstructionSet>();
            _radAsm1Instructions = new List<Instruction>();
            _radAsm2Instructions = new List<Instruction>();
        }

        private void InstructionsLoaded(IReadOnlyList<IInstructionSet> instructions)
        {
            _radAsm1InstructionSets.Clear();
            _radAsm2InstructionSets.Clear();
            _radAsm1Instructions.Clear();
            _radAsm2Instructions.Clear();

            foreach (var typeGroup in instructions.GroupBy(s => s.Type))
            {
                switch (typeGroup.Key)
                {
                    case InstructionType.RadAsm1: _radAsm1InstructionSets.AddRange(typeGroup.AsEnumerable()); break;
                    case InstructionType.RadAsm2: _radAsm2InstructionSets.AddRange(typeGroup.AsEnumerable()); break;
                }
            }

            _radAsm1Instructions.AddRange(_radAsm1InstructionSets.SelectMany(s => s.Select(i => i)));
            _radAsm2Instructions.AddRange(_radAsm2InstructionSets.SelectMany(s => s.Select(i => i)));
            radAsm1SelectedSet = null;
            radAsm2SelectedSet = null;

            InstructionsUpdated?.Invoke(this);
        }

        public IEnumerable<Instruction> GetInstructions(AsmType asmType)
        {
            if (asmType == AsmType.RadAsm)
            {
                return radAsm1SelectedSet ?? (IEnumerable<Instruction>)_radAsm1Instructions;
            }
            else if (asmType == AsmType.RadAsm2)
            {
                return radAsm2SelectedSet ?? (IEnumerable<Instruction>)_radAsm2Instructions;
            }
            else
            {
                return Enumerable.Empty<Instruction>();
            }
        }
    }
}
