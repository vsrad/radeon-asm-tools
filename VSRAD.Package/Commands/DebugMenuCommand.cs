using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using System;
using System.Collections.Generic;
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

            var debugStartupActiveTab = "(Active tab)";
            if (commandId == Constants.DebugFileDropdownListId && variantOut != IntPtr.Zero)
            {
                _openDocumentPaths = GetStartupFileCandidates().ToArray();
                _openDocumentShortNames = GetShortDocumentNames(_openDocumentPaths);

                string activeDocumentPath = "";
                try { activeDocumentPath = _projectSourceManager.GetActiveEditorView().GetFilePath(); } catch { }
                string activeDocumentName = _openDocumentPaths.Zip(_openDocumentShortNames, (path, name) => (path, name))
                    .FirstOrDefault(e => string.Equals(e.path, activeDocumentPath, StringComparison.OrdinalIgnoreCase)).name;
                string startupName = _openDocumentPaths.Zip(_openDocumentShortNames, (path, name) => (path, name))
                    .FirstOrDefault(e => string.Equals(e.path, _projectSourceManager.DebugStartupPath, StringComparison.OrdinalIgnoreCase)).name;

                var options = _openDocumentShortNames.Prepend((startupName == null ? "-> " : "") + debugStartupActiveTab)
                    .Select(n => (n == startupName ? "-> " : "") + (n == activeDocumentName ? "* " : "") + n).ToArray();

                Marshal.GetNativeVariantForObject(options, variantOut);
            }
            if (commandId == Constants.DebugFileDropdownId && variantOut != IntPtr.Zero)
            {
                if (_projectSourceManager.DebugStartupPath is string startupPath)
                {
                    var shortNames = GetShortDocumentNames(GetStartupFileCandidates().Prepend(startupPath).Distinct());
                    Marshal.GetNativeVariantForObject(shortNames[0], variantOut);
                }
                else
                {
                    Marshal.GetNativeVariantForObject(debugStartupActiveTab, variantOut);
                }
            }
            if (commandId == Constants.DebugFileDropdownId && variantIn != IntPtr.Zero)
            {
                var optionIdx = (int)Marshal.GetObjectForNativeVariant(variantIn);
                if (optionIdx == 0) // "(Active tab)"
                    _projectSourceManager.DebugStartupPath = null;
                else if (optionIdx - 1 < _openDocumentPaths.Length)
                    _projectSourceManager.DebugStartupPath = _openDocumentPaths[optionIdx - 1];
            }
        }

        private IEnumerable<string> GetStartupFileCandidates()
        {
            var openDocuments = _projectSourceManager.GetOpenDocuments();
            try
            {
                const string syntaxOptsProvider = "VSRAD.Syntax.Options.OptionsProvider";
                var syntaxOptsImport = new ImportDefinition(syntaxOptsProvider, ImportCardinality.ExactlyOne, new Dictionary<string, object>(), new List<IImportSatisfiabilityConstraint>());
                var syntaxOptsImported = _project.UnconfiguredProject.Services.ExportProvider.GetExports(syntaxOptsImport);
                var syntaxOpts = syntaxOptsImported.First().Value;
                var asm1FileExtensions = (IEnumerable<string>)syntaxOpts.GetType().GetField("Asm1FileExtensions").GetValue(syntaxOpts);
                var asm2FileExtensions = (IEnumerable<string>)syntaxOpts.GetType().GetField("Asm2FileExtensions").GetValue(syntaxOpts);
                var exts = asm1FileExtensions.Concat(asm2FileExtensions).ToList();
                return openDocuments.Where(p => exts.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return openDocuments;
            }
        }

        private static string[] GetShortDocumentNames(IEnumerable<string> documentPaths)
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
