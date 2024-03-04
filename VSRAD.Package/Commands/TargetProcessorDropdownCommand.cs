using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class TargetProcessorDropdownCommand : ICommandHandler
    {
        private readonly IProject _project;

        [ImportingConstructor]
        public TargetProcessorDropdownCommand(IProject project, SVsServiceProvider serviceProvider)
        {
            _project = project;
        }

        public Guid CommandSet => Constants.TargetProcessorDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.TargetProcessorDropdownListId && variantOut != IntPtr.Zero)
            {
                var targetProcessorList = _project.Options.TargetProcessors.Prepend(_project.Options.DefaultTargetProcessor).Distinct().Append("Edit...").ToArray();
                Marshal.GetNativeVariantForObject(targetProcessorList, variantOut);
            }
            if (commandId == Constants.TargetProcessorDropdownId && variantOut != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(_project.Options.TargetProcessor, variantOut);
            }
            if (commandId == Constants.TargetProcessorDropdownId && variantIn != IntPtr.Zero)
            {
                var selectedProcessor = (string)Marshal.GetObjectForNativeVariant(variantIn);
                if (selectedProcessor == "Edit...")
                    OpenProcessorListEditor();
                else
                    _project.Options.TargetProcessor = selectedProcessor;
            }
        }

        public sealed class ProcessorItem : DefaultNotifyPropertyChanged
        {
            private string _value = "";
            public string Value { get => _value; set => SetField(ref _value, value); }
        }

        private void OpenProcessorListEditor()
        {
            var initProcessorList = _project.Options.TargetProcessors.Count > 0
                ? _project.Options.TargetProcessors.Select(a => new ProcessorItem { Value = a })
                : new[] { new ProcessorItem { Value = _project.Options.TargetProcessor } };
            var editor = new WpfMruEditor("Target Processor", initProcessorList)
            {
                CreateItem = () => new ProcessorItem { Value = "" },
                ValidateEditedItem = (_) => true,
                CheckHaveUnsavedChanges = (items) =>
                {
                    if (items.Count != _project.Options.TargetProcessors.Count)
                        return true;
                    for (int i = 0; i < items.Count; ++i)
                        if (((ProcessorItem)items[i]).Value != _project.Options.TargetProcessors[i])
                            return true;
                    return false;
                },
                SaveChanges = (items) =>
                {
                    _project.Options.TargetProcessors.Clear();
                    _project.Options.TargetProcessors.AddRange(items.Select(a => ((ProcessorItem)a).Value).Distinct());
                    _project.SaveOptions();
                }
            };
            editor.ShowModal();
        }
    }
}
