using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionProvider
    {
        private static readonly Lazy<GeneralOptionProvider> LazyInstance =
            new Lazy<GeneralOptionProvider>(() => new GeneralOptionProvider());
        public static GeneralOptionProvider Instance => LazyInstance.Value;

        public GeneralOptionProvider()
        {
            SortOptions = GeneralOptionPage.SortState.ByName;
            AutoScroll = true;
            IsEnabledIndentGuides = false;
            IndentGuideThickness = 0.9;
            IndentGuideDashSize = 3.0;
            IndentGuideSpaceSize = 2.9;
            IndentGuideOffsetX = 3.2;
            IndentGuideOffsetY = 2.0;
            Asm1FileExtensions = Constants.DefaultFileExtensionAsm1;
            Asm2FileExtensions = Constants.DefaultFileExtensionAsm2;
            InstructionsPaths = GetDefaultInstructionDirectoryPath();
            AutocompleteInstructions = false;
            AutocompleteFunctions = false;
            AutocompleteLabels = false;
            AutocompleteVariables = false;
        }

        public GeneralOptionPage.SortState SortOptions;
        public bool AutoScroll;
        public bool IsEnabledIndentGuides;
        public double IndentGuideThickness;
        public double IndentGuideDashSize;
        public double IndentGuideSpaceSize;
        public double IndentGuideOffsetY;
        public double IndentGuideOffsetX;
        public IReadOnlyList<string> Asm1FileExtensions;
        public IReadOnlyList<string> Asm2FileExtensions;
        public IReadOnlyList<string> InstructionsPaths;
        public bool AutocompleteInstructions;
        public bool AutocompleteFunctions;
        public bool AutocompleteLabels;
        public bool AutocompleteVariables;

        public delegate void OptionsUpdate(GeneralOptionProvider sender);
        public event OptionsUpdate OptionsUpdated;

        public void OptionsUpdatedInvoke() =>
            OptionsUpdated?.Invoke(this);

        public static IReadOnlyList<string> GetDefaultInstructionDirectoryPath()
        {
            var assemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return new List<string>() { Path.GetDirectoryName(assemblyFolder) };
        }
    }
}