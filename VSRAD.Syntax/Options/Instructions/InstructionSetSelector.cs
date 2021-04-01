using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;

namespace VSRAD.Syntax.Options.Instructions
{
    internal sealed class InstructionSetSelector
    {
        public static InstructionSetSelector Instance;
        private static readonly Guid CommandSet = new Guid(Constants.InstructionSetSelectorCommandSetGuid);
        private const string AllSets = "all";

        private string[] _instructionSets;
        private string _currentSet;
        private IInstructionSetManager _instructionSetManager;
        private bool _initialized;

        public InstructionSetSelector(IMenuCommandService commandService)
        {
            _initialized = false;
            _currentSet = AllSets;

            var instructionSetDropDownComboCommandId = new CommandID(CommandSet, Constants.InstructionSetDropDownComboCommandId);
            var instructionSetDropDownCombo = new OleMenuCommand(OnMenuCombo, instructionSetDropDownComboCommandId);
            commandService.AddCommand(instructionSetDropDownCombo);

            var instructionSetDropDownComboGetListCommandId = new CommandID(CommandSet, Constants.InstructionSetDropDownComboGetListCommandId);
            var instructionSetDropDownComboGetList = new OleMenuCommand(OnMenuComboGetList, instructionSetDropDownComboGetListCommandId);
            commandService.AddCommand(instructionSetDropDownComboGetList);
        }

        private void AsmTypeChanged() => InitializeSets();

        private void InitializeSets()
        {
            var sets = new List<string>() { AllSets };
            sets.AddRange(_instructionSetManager.GetInstructionSets().Select(s => s.SetName));

            _instructionSets = sets.ToArray();
            var currentSet = _instructionSetManager.GetInstructionSet();
            _currentSet = currentSet == null ? AllSets : currentSet.SetName;
        }

        private void OnMenuCombo(object sender, EventArgs e)
        {
            if (e == null || e == EventArgs.Empty) throw new ArgumentException("Event args are required");
            if (!(e is OleMenuCmdEventArgs eventArgs)) throw new ArgumentException("Event args should be OleMenuCmdEventArgs");
            if (InstructionListManager.Instance == null) return;
            if (!_initialized)
            {
                _instructionSetManager = InstructionListManager.Instance;
                _instructionSetManager.AsmTypeChanged += AsmTypeChanged;
                _initialized = true;
            }

            var vOut = eventArgs.OutValue;
            var input = eventArgs.InValue;
            if (_instructionSets == null) InitializeSets();

            if (vOut != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(_currentSet, vOut);
            }
            else if (input != null)
            {
                _currentSet = input.ToString();
                _instructionSetManager.ChangeInstructionSet(_currentSet != AllSets ? _currentSet : null);
            }
        }

        private void OnMenuComboGetList(object sender, EventArgs e)
        {
            if (!(e is OleMenuCmdEventArgs eventArgs)) throw new ArgumentException("Event args should be OleMenuCmdEventArgs");

            Marshal.GetNativeVariantForObject(_instructionSets, eventArgs.OutValue);
        }

        public static void Initialize(IMenuCommandService commandService) =>
            Instance = new InstructionSetSelector(commandService);
    }
}
