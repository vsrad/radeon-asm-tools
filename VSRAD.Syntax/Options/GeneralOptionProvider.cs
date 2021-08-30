using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using VSRAD.Syntax.Options.Instructions;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public interface IOptions
    {
        Task LoadAsync();
    }

    [Export(typeof(OptionsProvider))]
    [Export(typeof(IOptions))]
    public class OptionsProvider : IOptions
    {
        public OptionsProvider()
        {
            SortOptions = SortState.ByName;
            Autoscroll = true;
            IsEnabledIndentGuides = false;
            IndentGuideThickness = 0.9;
            IndentGuideDashSize = 3.0;
            IndentGuideSpaceSize = 2.9;
            IndentGuideOffsetX = 3.2;
            IndentGuideOffsetY = 2.0;
            Asm1FileExtensions = Constants.DefaultFileExtensionAsm1;
            Asm2FileExtensions = Constants.DefaultFileExtensionAsm2;
            InstructionsPaths = GetDefaultInstructionDirectoryPath();
            Asm1InstructionSet = string.Empty;
            Asm2InstructionSet = string.Empty;
            AutocompleteInstructions = false;
            AutocompleteFunctions = false;
            AutocompleteLabels = false;
            AutocompleteVariables = false;
        }

        public SortState SortOptions;
        public bool Autoscroll;
        public bool IsEnabledIndentGuides;
        public double IndentGuideThickness;
        public double IndentGuideDashSize;
        public double IndentGuideSpaceSize;
        public double IndentGuideOffsetY;
        public double IndentGuideOffsetX;
        public IReadOnlyList<string> Asm1FileExtensions;
        public IReadOnlyList<string> Asm2FileExtensions;
        public string InstructionsPaths;
        public string Asm1InstructionSet;
        public string Asm2InstructionSet;
        public bool AutocompleteInstructions;
        public bool AutocompleteFunctions;
        public bool AutocompleteLabels;
        public bool AutocompleteVariables;

        public delegate void OptionsUpdate(OptionsProvider sender);
        public event OptionsUpdate OptionsUpdated;

        public void OptionsUpdatedInvoke() =>
            OptionsUpdated?.Invoke(this);

        public static string GetDefaultInstructionDirectoryPath()
        {
            var assemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(assemblyFolder);
        }

        public async Task LoadAsync()
        {
            var model = await GeneralOptions.GetInstanceAsync();

            // make sure this managers initialized before initial option event
            _ = Package.Instance.GetMEFComponent<ContentTypeManager>();
            _ = Package.Instance.GetMEFComponent<IInstructionListManager>();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            OptionsUpdatedInvoke();
        }
    }
}
