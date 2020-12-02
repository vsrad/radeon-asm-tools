using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ProfileDropdownCommand : ICommandHandler
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public ProfileDropdownCommand(IProject project)
        {
            _project = project;
        }

        public Guid CommandSet => Constants.ProfileDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (_project.Options.Profile == null)
                return;

            if (commandId == Constants.ProfileTargetMachineDropdownListId)
            {
                if (variantOut != IntPtr.Zero) /* list available items */
                {
                    var items = _project.Options.Profiles.Select(p => p.Value.General.Connection.ToString()).Distinct().Prepend("Local").ToArray();
                    Marshal.GetNativeVariantForObject(items, variantOut);
                }
            }
            if (commandId == Constants.ProfileTargetMachineDropdownId)
            {
                if (variantOut != IntPtr.Zero) /* get current item */
                {
                    var currentHost = _project.Options.Profile.General.RunActionsLocally ? "Local" : _project.Options.Profile.General.Connection.ToString();
                    Marshal.GetNativeVariantForObject(currentHost, variantOut);
                }
                else if (variantIn != IntPtr.Zero) /* set new item */
                {
                    var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);

                    var updatedProfile = (ProfileOptions)_project.Options.Profile.Clone();
                    if (selected == "Local")
                    {
                        updatedProfile.General.RunActionsLocally = true;
                    }
                    else
                    {
                        var hostPort = selected.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (hostPort.Length == 0)
                            return;

                        string host = hostPort[0];
                        if (hostPort.Length < 2 || !int.TryParse(hostPort[1], out var port))
                            port = 9339;

                        updatedProfile.General.RemoteMachine = host;
                        updatedProfile.General.Port = port;
                        updatedProfile.General.RunActionsLocally = false;
                    }
                    _project.Options.UpdateActiveProfile(updatedProfile);
                }
            }
        }
    }
}
