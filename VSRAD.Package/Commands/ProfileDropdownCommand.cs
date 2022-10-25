using Microsoft;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Profiles;
using VSRAD.Package.Utils;

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
            {
                var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
                if (selected == "Edit...")
                    OpenHostsEditor();
                else
                    SetNewTargetMachine(selected);
            }
        }

        private void ListTargetMachines(IntPtr variantOut)
        {
            var displayItems = _project.Options.TargetHosts.Select(h => h.Name).Prepend("Local").Append("Edit...").ToArray();
            Marshal.GetNativeVariantForObject(displayItems, variantOut);
        }

        private void GetCurrentTargetMachine(IntPtr variantOut)
        {
            string currentHost;
            if (_project.Options.Profile.General.RunActionsLocally)
            {
                currentHost = "Local";
            }
            else
            {
                currentHost = _project.Options.Connection.ToString();
                // Display current host at the top of the list
                //_project.Options.TargetHosts.Add(currentHost); TODO
            }
            Marshal.GetNativeVariantForObject(currentHost, variantOut);
        }

        private void SetNewTargetMachine(string selected)
        {
            var updatedProfile = (ProfileOptions)_project.Options.Profile.Clone();

            if (selected == "Local")
            {
                updatedProfile.General.RunActionsLocally = true;
            }
            else
            {
                var item = new HostItem(selected);
                if (item == default(HostItem))
                    return;

                _project.Options.TargetHosts.Add(item);

                _project.Options.RemoteMachine = item.Host;
                _project.Options.Port = item.Port;
                updatedProfile.General.RunActionsLocally = false;
            }

            _project.Options.UpdateActiveProfile(updatedProfile);
        }

        private void OpenHostsEditor() =>
            new TargetHostsEditor(_project) { ShowInTaskbar = false }.ShowModal();
    }
}
