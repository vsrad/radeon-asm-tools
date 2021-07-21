using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Settings;
using System;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public enum SortState
    {
        [Description("by line number")]
        ByLine,
        [Description("by line number descending")]
        ByLineDescending,
        [Description("by name")]
        ByName,
        [Description("by name descending")]
        ByNameDescending,
    }

    public class GeneralOptions : BaseOptionModel<GeneralOptions>
    {
        private const string InstructionCollectionName = "RadeonAsmInstructionCollection";
        private static readonly Regex _fileExtensionRegular = new Regex(@"^\.\w+$");
        private readonly OptionsProvider _optionsProvider;

        public GeneralOptions()
        {
            _optionsProvider = Package.Instance.GetMEFComponent<OptionsProvider>();
        }

        #region Options
        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        public SortState SortOptions
        {
            get => _optionsProvider.SortOptions;
            set => _optionsProvider.SortOptions = value;
        }

        [Category("Function list")]
        [DisplayName("Autoscroll function list")]
        [Description("Scroll to current function in the function list automatically")]
        public bool Autoscroll
        {
            get => _optionsProvider.Autoscroll;
            set => _optionsProvider.Autoscroll = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        public bool IsEnabledIndentGuides
        {
            get => _optionsProvider.IsEnabledIndentGuides;
            set => _optionsProvider.IsEnabledIndentGuides = value;
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        public string Asm1FileExtensions
        {
            get => ConvertExtensionsTo(_optionsProvider.Asm1FileExtensions);
            set { var extensions = ConvertExtensionsFrom(value); if (ValidateExtensions(extensions)) _optionsProvider.Asm1FileExtensions = extensions; }
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        public string Asm2FileExtensions
        {
            get => ConvertExtensionsTo(_optionsProvider.Asm2FileExtensions);
            set { var extensions = ConvertExtensionsFrom(value); if (ValidateExtensions(extensions)) _optionsProvider.Asm2FileExtensions = extensions; }
        }

        [Category("Syntax instruction folder paths")]
        [DisplayName("Instruction folder paths")]
        [Description("List of folder path separated by semicolon wit assembly instructions with .radasm file extension")]
        [Editor(typeof(FolderPathsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string InstructionsPaths
        {
            get => _optionsProvider.InstructionsPaths;
            set => _optionsProvider.InstructionsPaths = value;
        }

        [Category("Autocompletion")]
        [DisplayName("Instruction auto-completion")]
        [Description("Autocomplete instructions")]
        public bool AutocompleteInstructions
        {
            get => _optionsProvider.AutocompleteInstructions;
            set => _optionsProvider.AutocompleteInstructions = value;
        }

        [Category("Autocompletion")]
        [DisplayName("Function auto-completion")]
        [Description("Autocomplete function name")]
        public bool AutocompleteFunctions
        {
            get => _optionsProvider.AutocompleteFunctions;
            set => _optionsProvider.AutocompleteFunctions = value;
        }

        [Category("Autocompletion")]
        [DisplayName("Label auto-completion")]
        [Description("Autocomplete labels")]
        public bool AutocompleteLabels
        {
            get => _optionsProvider.AutocompleteLabels;
            set => _optionsProvider.AutocompleteLabels = value;
        }

        [Category("Autocompletion")]
        [DisplayName("Variable auto-completion")]
        [Description("Autocomplete global variables, local variables, function arguments")]
        public bool AutocompleteVariables
        {
            get => _optionsProvider.AutocompleteVariables;
            set => _optionsProvider.AutocompleteVariables = value;
        }
        #endregion

        public override async Task LoadAsync()
        {
            await base.LoadAsync();

            var settingsManager = await SettingsManager.GetValueAsync();
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!userSettingsStore.CollectionExists(InstructionCollectionName))
                return;

            InstructionsPaths = userSettingsStore.PropertyExists(InstructionCollectionName, nameof(InstructionsPaths))
                ? userSettingsStore.GetString(InstructionCollectionName, nameof(InstructionsPaths))
                : OptionsProvider.GetDefaultInstructionDirectoryPath();
        }

        public override async Task SaveAsync()
        {
            await base.SaveAsync();

            var settingsManager = await SettingsManager.GetValueAsync();
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!userSettingsStore.CollectionExists(InstructionCollectionName))
                userSettingsStore.CreateCollection(InstructionCollectionName);

            if (!InstructionsPaths.Equals(
                OptionsProvider.GetDefaultInstructionDirectoryPath(),
                StringComparison.OrdinalIgnoreCase))
            {
                userSettingsStore.SetString(InstructionCollectionName, nameof(InstructionsPaths), InstructionsPaths);
            }
        }

        private static IReadOnlyList<string> ConvertExtensionsFrom(string str) =>
            str.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        private static string ConvertExtensionsTo(IEnumerable<string> extensions) =>
            string.Join(";", extensions.ToArray());

        private static bool ValidateExtensions(IEnumerable<string> extensions)
        {
            var sb = new StringBuilder();
            foreach (var ext in extensions)
            {
                if (!_fileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }

            if (sb.Length == 0)
                return true;

            sb.AppendLine();
            sb.AppendLine("Format example: .asm");
            return false;
        }
    }
}
