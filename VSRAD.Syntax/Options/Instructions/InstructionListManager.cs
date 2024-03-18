﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsUpdateDelegate(IInstructionListManager sender, AsmType asmType);
    public interface IInstructionListManager
    {
        IInstructionSet GetSelectedInstructionSet(AsmType asmType);
        IInstructionSet GetInstructionSetsUnion(AsmType asmType);
        event InstructionsUpdateDelegate InstructionsUpdated;
    }

    [Export(typeof(IInstructionListManager))]
    internal sealed class InstructionListManager : IInstructionListManager
    {
        private readonly SyntaxPackageBridge.ISyntaxPackageBridge _syntaxPackageBridge;
        private readonly List<IInstructionSet> _radAsm1InstructionSets;
        private readonly List<IInstructionSet> _radAsm2InstructionSets;
        private IInstructionSet _radAsm1InstructionsSetsUnion;
        private IInstructionSet _radAsm2InstructionsSetsUnion;

        private AsmType activeDocumentAsm;
        private IInstructionSet radAsm1SelectedSet;
        private IInstructionSet radAsm2SelectedSet;

        public event InstructionsUpdateDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListManager(
            IInstructionListLoader instructionListLoader,
            IDocumentFactory documentFactory,
            [Import(AllowDefault = true)] SyntaxPackageBridge.ISyntaxPackageBridge syntaxPackageBridge)
        {
            instructionListLoader.InstructionsUpdated += InstructionsLoaded;
            documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            documentFactory.DocumentCreated += ActiveDocumentChanged;

            _syntaxPackageBridge = syntaxPackageBridge;
            if (_syntaxPackageBridge != null)
            {
                _syntaxPackageBridge.PackageRequestedTargetProcessorList += GetInstructionSetList;
                _syntaxPackageBridge.PackageUpdatedSelectedTargetProcessor += SelectedInstructionSetUpdated;
            }

            _radAsm1InstructionSets = new List<IInstructionSet>();
            _radAsm2InstructionSets = new List<IInstructionSet>();
            _radAsm1InstructionsSetsUnion = new InstructionSet(AsmType.RadAsm, Enumerable.Empty<InstructionSet>());
            _radAsm2InstructionsSetsUnion = new InstructionSet(AsmType.RadAsm2, Enumerable.Empty<InstructionSet>());
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
                    case AsmType.RadAsm: _radAsm1InstructionSets.Add(instructionSet); break;
                    case AsmType.RadAsm2: _radAsm2InstructionSets.Add(instructionSet); break;
                }
            }
            _radAsm1InstructionsSetsUnion = new InstructionSet(AsmType.RadAsm, _radAsm1InstructionSets);
            _radAsm2InstructionsSetsUnion = new InstructionSet(AsmType.RadAsm2, _radAsm2InstructionSets);
            CustomThreadHelper.RunOnMainThread(() => InstructionsUpdated?.Invoke(this, AsmType.RadAsmCode));
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
                SelectedInstructionSetUpdated(this, new EventArgs());
            }
        }

        private void SelectedInstructionSetUpdated(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var selected = await (_syntaxPackageBridge?.GetSelectedTargetProcessor() ?? Task.FromResult<(string, string)>(default));
                switch (activeDocumentAsm)
                {
                    case AsmType.RadAsm: radAsm1SelectedSet = _radAsm1InstructionSets.Find(s => string.Equals(s.SetName, selected.InstructionSet, StringComparison.OrdinalIgnoreCase)); break;
                    case AsmType.RadAsm2: radAsm2SelectedSet = _radAsm2InstructionSets.Find(s => string.Equals(s.SetName, selected.InstructionSet, StringComparison.OrdinalIgnoreCase)); break;
                }
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                InstructionsUpdated?.Invoke(this, activeDocumentAsm);
            });
        }

        private void GetInstructionSetList(object sender, SyntaxPackageBridge.TargetProcessorListEventArgs e)
        {
            switch (activeDocumentAsm)
            {
                case AsmType.RadAsm: e.List = _radAsm1InstructionSets.Select(s => (TargetProcessor: s.SetName, SyntaxName: s.SetName)).ToList(); break;
                case AsmType.RadAsm2: e.List = _radAsm2InstructionSets.Select(s => (TargetProcessor: s.SetName, SyntaxName: s.SetName)).ToList(); break;
            }
        }
    }
}
