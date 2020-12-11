using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public ProfileDropdownCommand(IProject project, SVsServiceProvider serviceProvider)
        {
            _project = project;
            _serviceProvider = serviceProvider;
            _project.Options.PropertyChanged += ProjectOptionsChanged;
        }

        private void ProjectOptionsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (e.PropertyName == nameof(ProjectOptions.ActiveProfile))
            {
                var shell = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));
                Assumes.Present(shell);
                shell.UpdateCommandUI(0); // Force VS to refresh dropdown items
            }
        }

        public Guid CommandSet => Constants.ProfileDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (_project.Options.Profile == null)
                return;
            if (commandId == Constants.ProfileTargetMachineDropdownListId && variantOut != IntPtr.Zero)
                ListTargetMachines(variantOut);
            if (commandId == Constants.ProfileTargetMachineDropdownId && variantOut != IntPtr.Zero)
                GetCurrentTargetMachine(variantOut);
            if (commandId == Constants.ProfileTargetMachineDropdownId && variantIn != IntPtr.Zero)
                SetNewTargetMachine(variantIn);
        }

        private void ListTargetMachines(IntPtr variantOut)
        {
            if (_project.Options.RecentlyUsedHosts.Count == 0)
            {
                foreach (var profile in _project.Options.Profiles)
                    _project.Options.RecentlyUsedHosts.Add(profile.Value.General.Connection.ToString());
            }

            // Add the current host to the list in case the user switches to a different profile
            // (if the profile is not changed this is a no-op because the current host is already at the top of the list)
            _project.Options.RecentlyUsedHosts.Add(_project.Options.Profile.General.Connection.ToString());

            var displayItems = _project.Options.RecentlyUsedHosts.Prepend("Local").ToArray();
            Marshal.GetNativeVariantForObject(displayItems, variantOut);
        }

        private void GetCurrentTargetMachine(IntPtr variantOut)
        {
            var currentHost = _project.Options.Profile.General.RunActionsLocally ? "Local" : _project.Options.Profile.General.Connection.ToString();
            Marshal.GetNativeVariantForObject(currentHost, variantOut);
        }

        private void SetNewTargetMachine(IntPtr variantIn)
        {
            var updatedProfile = (ProfileOptions)_project.Options.Profile.Clone();

            var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
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

                _project.Options.RecentlyUsedHosts.Add($"{host}:{port}");

                updatedProfile.General.RemoteMachine = host;
                updatedProfile.General.Port = port;
                updatedProfile.General.RunActionsLocally = false;
            }

            _project.Options.UpdateActiveProfile(updatedProfile);
        }
    }
}
