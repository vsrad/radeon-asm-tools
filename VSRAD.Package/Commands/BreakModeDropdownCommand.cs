using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ToolWindows;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class BreakModeDropdownCommand : ICommandHandler
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public BreakModeDropdownCommand(IProject project)
        {
            _project = project;
        }

        public Guid CommandSet => Constants.BreakModeDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.BreakModeDropdownListId)
            {
                if (variantOut != IntPtr.Zero) /* list available items */
                {
                    var items = new[] { "Single active breakpoint, round-robin",
                                        "Single active breakpoint, rerun same line",
                                        "Multiple active breakpoints" };
                    Marshal.GetNativeVariantForObject(items, variantOut);
                }
            }
            if (commandId == Constants.BreakModeDropdownId)
            {
                if (variantOut != IntPtr.Zero) /* get current item */
                {
                    string currentMode;
                    switch (_project.Options.DebuggerOptions.BreakMode)
                    {
                        case Options.BreakMode.SingleRoundRobin:
                            currentMode = "Single active breakpoint, round-robin";
                            break;
                        case Options.BreakMode.SingleRerun:
                            currentMode = "Single active breakpoint, rerun same line";
                            break;
                        case Options.BreakMode.Multiple:
                            currentMode = "Multiple active breakpoints";
                            break;
                        default:
                            throw new ArgumentException($"Unknown break mode: {_project.Options.DebuggerOptions.BreakMode}");
                    }
                                
                    Marshal.GetNativeVariantForObject(currentMode, variantOut);
                }
                else if (variantIn != IntPtr.Zero) /* set new item */
                {
                    var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);

                    switch (selected)
                    {
                        case "Single active breakpoint, round-robin":
                            _project.Options.DebuggerOptions.BreakMode = Options.BreakMode.SingleRoundRobin;
                            break;
                        case "Single active breakpoint, rerun same line":
                            _project.Options.DebuggerOptions.BreakMode = Options.BreakMode.SingleRerun;
                            break;
                        case "Multiple active breakpoints":
                            _project.Options.DebuggerOptions.BreakMode = Options.BreakMode.Multiple;
                            break;
                    }
                }
            }
        }
    }
}
