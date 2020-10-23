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

        private readonly IInstructionSetManager _instructionSetManager;
        private string[] instructionSets;
        private string currentSet;

        public InstructionSetSelector(Package package, OleMenuCommandService commandService)
        {
            _instructionSetManager = package.GetMEFComponent<IInstructionSetManager>();
            _instructionSetManager.AsmTypeChanged += AsmTypeChanged;
            currentSet = AllSets;

            var instructionSetDropDownComboCommandId = new CommandID(CommandSet, Constants.InstructionSetDropDownComboCommandId);
            var instructionSetDropDownCombo = new OleMenuCommand(new EventHandler(OnMenuCombo), instructionSetDropDownComboCommandId);
            commandService.AddCommand(instructionSetDropDownCombo);

            var instructionSetDropDownComboGetListCommandId = new CommandID(CommandSet, Constants.InstructionSetDropDownComboGetListCommandId);
            var instructionSetDropDownComboGetList = new OleMenuCommand(new EventHandler(OnMenuComboGetList), instructionSetDropDownComboGetListCommandId);
            commandService.AddCommand(instructionSetDropDownComboGetList);
        }

        private void AsmTypeChanged() => InitializeSets();

        private void InitializeSets()
        {
            var sets = new List<string>() { AllSets };
            sets.AddRange(_instructionSetManager.GetInstructionSets().Select(s => s.SetName));

            instructionSets = sets.ToArray();
            var currentSet = _instructionSetManager.GetInstructionSet();
            this.currentSet = currentSet == null ? AllSets : currentSet.SetName;
        }

        private void OnMenuCombo(object sender, EventArgs e)
        {
            if (e == null || e == EventArgs.Empty) throw new ArgumentException("Event args are required");
            if (!(e is OleMenuCmdEventArgs eventArgs)) throw new ArgumentException("Event args should be OleMenuCmdEventArgs");

            var vOut = eventArgs.OutValue;
            var input = eventArgs.InValue;
            if (instructionSets == null) InitializeSets();

            if (vOut != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(currentSet, vOut);
            }
            else if (input != null)
            {
                currentSet = input.ToString();
                _instructionSetManager.ChangeInstructionSet(currentSet != AllSets ? currentSet : null);
            }
        }

        private void OnMenuComboGetList(object sender, EventArgs e)
        {
            if (!(e is OleMenuCmdEventArgs eventArgs)) throw new ArgumentException("Event args should be OleMenuCmdEventArgs");

            Marshal.GetNativeVariantForObject(instructionSets, eventArgs.OutValue);
        }

        public static void Initialize(Package package, OleMenuCommandService commandService) =>
            Instance = new InstructionSetSelector(package, commandService);
    }
}
