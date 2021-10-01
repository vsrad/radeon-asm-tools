using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebugProfileDropdownCommand : ICommandHandler
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public DebugProfileDropdownCommand(IProject project)
        {
            _project = project;
        }

        public Guid CommandSet => Constants.ProfileSelectorCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.ProfileDropdownListId)
            {
                if (variantOut != IntPtr.Zero) /* list available items */
                {
                    Marshal.GetNativeVariantForObject(_project.Options.Profiles.Keys.Cast<string>().ToArray(), variantOut);
                }
            }
            if (commandId == Constants.ProfileDropdownId)
            {
                if (variantOut != IntPtr.Zero) /* get current item */
                {
                    var currentProfile = _project.Options.ActiveProfile;
                    Marshal.GetNativeVariantForObject(currentProfile, variantOut);
                }
                else if (variantIn != IntPtr.Zero) /* set new item */
                {
                    var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
                    _project.Options.ActiveProfile = selected;
                }
            }
        }
    }
}
