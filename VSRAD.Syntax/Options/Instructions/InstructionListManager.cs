using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsUpdateDelegate(IInstructionListManager sender, AsmType asmType);
    public interface IInstructionListManager
    {
        IEnumerable<Instruction> GetSelectedSetInstructions(AsmType asmType);
        IEnumerable<Instruction> GetInstructions(AsmType asmType);
        event InstructionsUpdateDelegate InstructionsUpdated;
    }

    public delegate void AsmTypeChange();
    public interface IInstructionSetManager
    {
        void ChangeInstructionSet(string selectedSetName);
        IInstructionSet GetInstructionSet();
        IReadOnlyList<IInstructionSet> GetInstructionSets();
        event AsmTypeChange AsmTypeChanged;
    }

    [Export(typeof(IInstructionListManager))]
    [Export(typeof(IInstructionSetManager))]
    internal sealed class InstructionListManager : IInstructionListManager, IInstructionSetManager
    {
        private readonly List<IInstructionSet> _radAsm1InstructionSets;
        private readonly List<IInstructionSet> _radAsm2InstructionSets;
        private readonly List<Instruction> _radAsm1Instructions;
        private readonly List<Instruction> _radAsm2Instructions;

        private AsmType activeDocumentAsm;
        private IInstructionSet radAsm1SelectedSet;
        private IInstructionSet radAsm2SelectedSet;

        public event InstructionsUpdateDelegate InstructionsUpdated;
        public event AsmTypeChange AsmTypeChanged;

        [ImportingConstructor]
        public InstructionListManager(IInstructionListLoader instructionListLoader, IDocumentFactory documentFactory)
        {
            instructionListLoader.InstructionsUpdated += InstructionsLoaded;
            documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            documentFactory.DocumentCreated += ActiveDocumentChanged;

            _radAsm1InstructionSets = new List<IInstructionSet>();
            _radAsm2InstructionSets = new List<IInstructionSet>();
            _radAsm1Instructions = new List<Instruction>();
            _radAsm2Instructions = new List<Instruction>();
            activeDocumentAsm = AsmType.Unknown;
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

            AsmTypeChanged?.Invoke();
            InstructionsUpdated?.Invoke(this, AsmType.RadAsmCode);
        }

        public IEnumerable<Instruction> GetSelectedSetInstructions(AsmType asmType)
        {
            switch (asmType)
            {
                case AsmType.RadAsm: return radAsm1SelectedSet ?? (IEnumerable<Instruction>)_radAsm1Instructions;
                case AsmType.RadAsm2: return radAsm2SelectedSet ?? (IEnumerable<Instruction>)_radAsm2Instructions;
                default: return Enumerable.Empty<Instruction>();
            }
        }

        public IEnumerable<Instruction> GetInstructions(AsmType asmType)
        {
            switch (asmType)
            {
                case AsmType.RadAsm: return _radAsm1Instructions;
                case AsmType.RadAsm2: return _radAsm2Instructions;
                default: return Enumerable.Empty<Instruction>();
            }
        }

        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            var newActiveDocumentAsm = activeDocument == null ? AsmType.Unknown : activeDocument.CurrentSnapshot.GetAsmType();
            if (newActiveDocumentAsm != activeDocumentAsm)
            {
                activeDocumentAsm = newActiveDocumentAsm;
                AsmTypeChanged?.Invoke();
            }
        }

        public void ChangeInstructionSet(string selected)
        {
            if (selected == null)
            {
                switch (activeDocumentAsm)
                {
                    case AsmType.RadAsm: radAsm1SelectedSet = null; break;
                    case AsmType.RadAsm2: radAsm2SelectedSet = null; break;
                }
            }
            else
            {
                switch (activeDocumentAsm)
                {
                    case AsmType.RadAsm: ChangeInstructionSet(selected, _radAsm1InstructionSets, ref radAsm1SelectedSet);  break;
                    case AsmType.RadAsm2: ChangeInstructionSet(selected, _radAsm2InstructionSets, ref radAsm2SelectedSet); break;
                }
            }

            InstructionsUpdated?.Invoke(this, activeDocumentAsm);
        }

        private void ChangeInstructionSet(string setName, List<IInstructionSet> sets, ref IInstructionSet selectedSet)
        {
            var set = sets.Find(s => s.SetName == setName);
            if (set == null)
            {
                Error.ShowErrorMessage($"Cannot find selected instruction set: {setName}", "Instruction set selector");
                selectedSet = null;
                return;
            }

            selectedSet = set;
        }

        public IReadOnlyList<IInstructionSet> GetInstructionSets() =>
            activeDocumentAsm == AsmType.RadAsm
                ? _radAsm1InstructionSets
                : activeDocumentAsm == AsmType.RadAsm2
                    ? _radAsm2InstructionSets : new List<IInstructionSet>();

        public IInstructionSet GetInstructionSet()
        {
            switch (activeDocumentAsm)
            {
                case AsmType.RadAsm: return radAsm1SelectedSet;
                case AsmType.RadAsm2: return radAsm2SelectedSet;
                default: return null;
            }
        }
    }
}
