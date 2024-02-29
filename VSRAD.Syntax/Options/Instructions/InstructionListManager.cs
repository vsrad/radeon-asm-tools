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
        IInstructionSet GetSelectedInstructionSet(AsmType asmType);
        IInstructionSet GetInstructionSetsUnion(AsmType asmType);
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
        private IInstructionSet _radAsm1InstructionsSetsUnion;
        private IInstructionSet _radAsm2InstructionsSetsUnion;

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
            _radAsm1InstructionsSetsUnion = new InstructionSet(InstructionType.RadAsm1, Enumerable.Empty<InstructionSet>());
            _radAsm2InstructionsSetsUnion = new InstructionSet(InstructionType.RadAsm2, Enumerable.Empty<InstructionSet>());
            activeDocumentAsm = AsmType.Unknown;
        }

        private void InstructionsLoaded(IReadOnlyList<IInstructionSet> instructionSets)
        {
            _radAsm1InstructionSets.Clear();
            _radAsm2InstructionSets.Clear();

            foreach (var instructionSet in instructionSets)
            {
                switch (instructionSet.Type)
                {
                    case InstructionType.RadAsm1: _radAsm1InstructionSets.Add(instructionSet); break;
                    case InstructionType.RadAsm2: _radAsm2InstructionSets.Add(instructionSet); break;
                }
            }

            _radAsm1InstructionsSetsUnion = new InstructionSet(InstructionType.RadAsm1, _radAsm1InstructionSets);
            _radAsm2InstructionsSetsUnion = new InstructionSet(InstructionType.RadAsm2, _radAsm2InstructionSets);

            CustomThreadHelper.RunOnMainThread(() =>
            {
                var options = GeneralOptions.Instance;
                radAsm1SelectedSet = SelectInstructionSet(_radAsm1InstructionSets, options.Asm1InstructionSet);
                radAsm2SelectedSet = SelectInstructionSet(_radAsm2InstructionSets, options.Asm2InstructionSet);

                AsmTypeChanged?.Invoke();
                InstructionsUpdated?.Invoke(this, AsmType.RadAsmCode);
            });
        }

        public IInstructionSet GetSelectedInstructionSet(AsmType asmType)
        {
            switch (asmType)
            {
                case AsmType.RadAsm: return radAsm1SelectedSet ?? _radAsm1InstructionsSetsUnion;
                case AsmType.RadAsm2: return radAsm2SelectedSet ?? _radAsm2InstructionsSetsUnion;
                default: return new InstructionSet(default, Enumerable.Empty<IInstructionSet>());
            }
        }

        public IInstructionSet GetInstructionSetsUnion(AsmType asmType)
        {
            switch (asmType)
            {
                case AsmType.RadAsm: return _radAsm1InstructionsSetsUnion;
                case AsmType.RadAsm2: return _radAsm2InstructionsSetsUnion;
                default: return new InstructionSet(default, Enumerable.Empty<IInstructionSet>());
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

            CustomThreadHelper.RunOnMainThread(() =>
            {
                var options = GeneralOptions.Instance;
                switch (activeDocumentAsm)
                {
                    case AsmType.RadAsm: options.Asm1InstructionSet = selected ?? string.Empty; break;
                    case AsmType.RadAsm2: options.Asm2InstructionSet = selected ?? string.Empty; break;
                }
                options.Save();

                InstructionsUpdated?.Invoke(this, activeDocumentAsm);
            });
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

        private static IInstructionSet SelectInstructionSet(IEnumerable<IInstructionSet> sets, string name) =>
            sets.FirstOrDefault(s => s.SetName.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }
}
