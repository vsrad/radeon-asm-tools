using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(OptionsProvider))]
    public class OptionsProvider
    {
        public OptionsProvider()
        {
            SortOptions = GeneralOptionPage.SortState.ByName;
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
            AutocompleteInstructions = false;
            AutocompleteFunctions = false;
            AutocompleteLabels = false;
            AutocompleteVariables = false;
    }

        public GeneralOptionPage.SortState SortOptions;
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
    }

    public class GeneralOptionPage : DialogPage
    {
        private const string InstructionCollectionPath = "VSRADInstructionCollectionPath";
        private static readonly Regex fileExtensionRegular = new Regex(@"^\.\w+$");
        private readonly OptionsProvider _optionsEventProvider;

        public GeneralOptionPage(): base()
        {
            _optionsEventProvider = Package.Instance.GetMEFComponent<OptionsProvider>();
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        public SortState SortOptions
        {
            get { return _optionsEventProvider.SortOptions; }
            set { _optionsEventProvider.SortOptions = value; }
        }

        [Category("Function list")]
        [DisplayName("Autoscroll function list")]
        [Description("Scroll to current function in the function list automatically")]
        public bool Autoscroll
        {
            get { return _optionsEventProvider.Autoscroll; }
            set { _optionsEventProvider.Autoscroll = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        public bool IsEnabledIndentGuides
        {
            get { return _optionsEventProvider.IsEnabledIndentGuides; }
            set { _optionsEventProvider.IsEnabledIndentGuides = value; }
        }

#if DEBUG
        [Category("Syntax highlight")]
        [DisplayName("Indent guide line thikness")]
        public double IndentGuideThikness
        {
            get { return _optionsEventProvider.IndentGuideThickness; }
            set { _optionsEventProvider.IndentGuideThickness = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide dash size")]
        public double IndentGuideDashSize
        {
            get { return _optionsEventProvider.IndentGuideDashSize; }
            set { _optionsEventProvider.IndentGuideDashSize = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide space size")]
        [Description("Space size between indent lines")]
        public double IndentGuideSpaceSize
        {
            get { return _optionsEventProvider.IndentGuideSpaceSize; }
            set { _optionsEventProvider.IndentGuideSpaceSize = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset X")]
        public double IndentGuideOffsetX
        {
            get { return _optionsEventProvider.IndentGuideOffsetX; }
            set { _optionsEventProvider.IndentGuideOffsetX = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset Y")]
        public double IndentGuideOffsetY
        {
            get { return _optionsEventProvider.IndentGuideOffsetY; }
            set { _optionsEventProvider.IndentGuideOffsetY = value; }
        }
#endif

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        public string Asm1FileExtensions
        {
            get { return ConvertExtensionsTo(_optionsEventProvider.Asm1FileExtensions); }
            set { var extensions = ConvertExtensionsFrom(value); if (ValidateExtensions(extensions)) _optionsEventProvider.Asm1FileExtensions = extensions; }
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        public string Asm2FileExtensions
        {
            get { return ConvertExtensionsTo(_optionsEventProvider.Asm2FileExtensions); }
            set { var extensions = ConvertExtensionsFrom(value); if (ValidateExtensions(extensions)) _optionsEventProvider.Asm2FileExtensions = extensions; }
        }

        [Category("Syntax instruction folder paths")]
        [DisplayName("Instruction folder paths")]
        [Description("List of folder path separated by semicolon wit assembly instructions with .radasm file extension")]
        [Editor(typeof(FolderPathsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string InstructionsPaths
        {
            get { return _optionsEventProvider.InstructionsPaths; }
            set { _optionsEventProvider.InstructionsPaths = value; }
        }

        [Category("Autocompletion")]
        [DisplayName("Instruction autocompletion")]
        [Description("Autocomplete instructions")]
        public bool AutocompleteInstructions
        {
            get { return _optionsEventProvider.AutocompleteInstructions; }
            set { _optionsEventProvider.AutocompleteInstructions = value; }
        }

        [Category("Autocompletion")]
        [DisplayName("Function autocompletion")]
        [Description("Autocomplete function name")]
        public bool AutocompleteFunctions
        {
            get { return _optionsEventProvider.AutocompleteFunctions; }
            set { _optionsEventProvider.AutocompleteFunctions = value; }
        }

        [Category("Autocompletion")]
        [DisplayName("Label autocompletion")]
        [Description("Autocomplete labels")]
        public bool AutocompleteLabels
        {
            get { return _optionsEventProvider.AutocompleteLabels; }
            set { _optionsEventProvider.AutocompleteLabels = value; }
        }

        [Category("Autocompletion")]
        [DisplayName("Variable autocompletion")]
        [Description("Autocomplete global variables, local variables, function arguments")]
        public bool AutocompleteVariables
        {
            get { return _optionsEventProvider.AutocompleteVariables; }
            set { _optionsEventProvider.AutocompleteVariables = value; }
        }

        public enum SortState
        {
            [Description("by line number")]
            ByLine = 1,
            [Description("by line number descending")]
            ByLineDescending = 2,
            [Description("by name")]
            ByName = 3,
            [Description("by name descending")]
            ByNameDescending = 4,
        }

        public Task InitializeAsync()
        {
            // make sure this managers initialized before initial option event
            _ = Package.Instance.GetMEFComponent<ContentTypeManager>();
            _ = Package.Instance.GetMEFComponent<IInstructionListManager>();

            _optionsEventProvider.OptionsUpdatedInvoke();
            return Task.CompletedTask;
        }

        // hack to avoid installation errors when reinstalling the extension
        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            InstructionsPaths = userSettingsStore.CollectionExists(InstructionCollectionPath)
                ? userSettingsStore.GetString(InstructionCollectionPath, nameof(InstructionsPaths))
                : OptionsProvider.GetDefaultInstructionDirectoryPath();
        }

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (InstructionsPaths != OptionsProvider.GetDefaultInstructionDirectoryPath())
            {
                if (!userSettingsStore.CollectionExists(InstructionCollectionPath))
                    userSettingsStore.CreateCollection(InstructionCollectionPath);

                userSettingsStore.SetString(InstructionCollectionPath, nameof(InstructionsPaths), InstructionsPaths);
            }
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            try
            {
                base.OnApply(e);
                _optionsEventProvider.OptionsUpdatedInvoke();
            }
            catch(Exception ex)
            {
                Error.ShowWarning(ex);
            }
        }

        private static List<string> ConvertExtensionsFrom(string str) =>
            str.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        private static string ConvertExtensionsTo(IReadOnlyList<string> extensions) =>
            string.Join(";", extensions.ToArray());
        
        private static bool ValidateExtensions(List<string> extensions)
        {
            var sb = new StringBuilder();
            foreach (var ext in extensions)
            {
                if (!fileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }
            if (sb.Length != 0)
            {
                sb.AppendLine();
                sb.AppendLine("Format example: .asm");
                return false;
            }
            return true;
        }
    }
}