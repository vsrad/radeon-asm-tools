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
                    Marshal.GetNativeVariantForObject(Utils.BreakModeConverter.BreakModeOptions, variantOut);
                }
            }
            if (commandId == Constants.BreakModeDropdownId)
            {
                if (variantOut != IntPtr.Zero) /* get current item */
                {
                    var currentMode = Utils.BreakModeConverter.BreakModeToString(_project.Options.DebuggerOptions.BreakMode);
                    Marshal.GetNativeVariantForObject(currentMode, variantOut);
                }
                else if (variantIn != IntPtr.Zero) /* set new item */
                {
                    var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
                    _project.Options.DebuggerOptions.BreakMode = Utils.BreakModeConverter.FromString(selected);
                }
            }
        }
    }
}
