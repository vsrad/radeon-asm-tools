﻿using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class DebugMenuCommand : ICommandHandler
    {
        private readonly IProject _project;
        private readonly IProjectSourceManager _projectSourceManager;

        private string[] _openDocumentPaths;
        private string[] _openDocumentShortNames;

        [ImportingConstructor]
        public DebugMenuCommand(IProject project, IProjectSourceManager projectSourceManager)
        {
            _project = project;
            _projectSourceManager = projectSourceManager;
        }

        public Guid CommandSet => Constants.DebugMenuCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText)
        {
            if (commandId == Constants.DebugMultipleBreakpointsCommandId)
            {
                var enabled = _project.Options.DebuggerOptions.EnableMultipleBreakpoints;

                var flags = OleCommandText.GetFlags(commandText);
                if (flags == OLECMDTEXTF.OLECMDTEXTF_NAME)
                    OleCommandText.SetText(commandText, (enabled ? "Disable" : "Enable") + " Multiple Breakpoints");

                return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED | (enabled ? OLECMDF.OLECMDF_LATCHED : 0);
            }
            return OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;
        }

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.DebugMultipleBreakpointsCommandId)
            {
                _project.Options.DebuggerOptions.EnableMultipleBreakpoints = !_project.Options.DebuggerOptions.EnableMultipleBreakpoints;
            }
            if (commandId == Constants.DebugFileDropdownListId && variantOut != IntPtr.Zero)
            {
                _openDocumentPaths = _projectSourceManager.GetOpenDocuments().Where(p => !string.IsNullOrEmpty(p)).ToArray();
                _openDocumentShortNames = GetShortDocumentNames(_openDocumentPaths);
                var options = _openDocumentShortNames.Prepend("(Active tab)").ToArray();
                Marshal.GetNativeVariantForObject(options, variantOut);
            }
            if (commandId == Constants.DebugFileDropdownId && variantOut != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject("(Active tab)", variantOut);
            }
            if (commandId == Constants.DebugFileDropdownId && variantIn != IntPtr.Zero)
            {
                var selectedFile = (int)Marshal.GetObjectForNativeVariant(variantIn);
            }
        }

        private static string[] GetShortDocumentNames(string[] documentPaths)
        {
            var paths = documentPaths.Select(p => p.Split(Path.DirectorySeparatorChar)).ToArray();
            var shortNames = paths.Select(p => p[p.Length - 1]).ToArray();
            for (var haveDuplicateNames = true; haveDuplicateNames;)
            {
                haveDuplicateNames = false;
                for (var i = 0; i < shortNames.Length; ++i)
                {
                    for (var j = 0; j < shortNames.Length; ++j)
                    {
                        if (i != j && shortNames[i] == shortNames[j])
                        {
                            var li = shortNames[i].Count(c => c == Path.DirectorySeparatorChar);
                            var lj = shortNames[j].Count(c => c == Path.DirectorySeparatorChar);
                            if (li + 1 < paths[i].Length && lj + 1 < paths[j].Length)
                            {
                                haveDuplicateNames = true;
                                shortNames[i] = paths[i][paths[i].Length - 1 - (li + 1)] + Path.DirectorySeparatorChar + shortNames[i];
                                shortNames[j] = paths[j][paths[j].Length - 1 - (lj + 1)] + Path.DirectorySeparatorChar + shortNames[j];
                            }
                        }
                    }
                }
            }
            return shortNames;
        }
    }
}
