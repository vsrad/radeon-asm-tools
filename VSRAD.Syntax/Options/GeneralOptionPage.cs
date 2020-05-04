using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VSRAD.Syntax.Helpers;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(OptionsEventProvider))]
    public class OptionsEventProvider
    {
        public OptionsEventProvider()
        {
            SortOptions = GeneralOptionPage.SortState.ByName;
            IsEnabledIndentGuides = true;
            Asm1FileExtensions = Constants.DefaultFileExtensionAsm1;
            Asm2FileExtensions = Constants.DefaultFileExtensionAsm2;
            InstructionsPaths = GetDefaultInstructionDirectoryPath();
            AutocompleteInstructions = false;
            AutocompleteFunctions = false;
            AutocompleteLabels = false;
            AutocompleteVariables = false;
    }

        public GeneralOptionPage.SortState SortOptions;
        public bool IsEnabledIndentGuides;
        public List<string> Asm1FileExtensions;
        public List<string> Asm2FileExtensions;
        public string InstructionsPaths;
        public bool AutocompleteInstructions;
        public bool AutocompleteFunctions;
        public bool AutocompleteLabels;
        public bool AutocompleteVariables;

        public delegate void OptionsUpdate(OptionsEventProvider sender);
        public event OptionsUpdate OptionsUpdated;

        public void OptionsUpdatedInvoke() =>
            OptionsUpdated?.Invoke(this);

        private static string GetDefaultInstructionDirectoryPath()
        {
            var assemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(assemblyFolder);
        }
    }

    public class GeneralOptionPage : BaseOptionPage
    {
        private static readonly Regex fileExtensionRegular = new Regex(@"^\.\w+$");
        private readonly OptionsEventProvider _optionsEventProvider;

        public GeneralOptionPage(): base()
        {
            _optionsEventProvider = Package.Instance.GetMEFComponent<OptionsEventProvider>();
            _collectionSettings = new Dictionary<string, KeyValuePair<string, (List<string>, IReadOnlyList<string>)>>()
            {
                { "Asm1CollectionFileExtensions", new KeyValuePair<string, (List<string>, IReadOnlyList<string>)>(nameof(Asm1FileExtensions), (Asm1FileExtensions, Constants.DefaultFileExtensionAsm1)) },
                { "Asm2CollectionFileExtensions", new KeyValuePair<string, (List<string>, IReadOnlyList<string>)>(nameof(Asm2FileExtensions), (Asm2FileExtensions, Constants.DefaultFileExtensionAsm2)) },
            };
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        public SortState SortOptions
        {
            get { return _optionsEventProvider.SortOptions; }
            set { _optionsEventProvider.SortOptions = value; }
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        public bool IsEnabledIndentGuides
        {
            get { return _optionsEventProvider.IsEnabledIndentGuides; }
            set { _optionsEventProvider.IsEnabledIndentGuides = value; }
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm1FileExtensions
        {
            get { return _optionsEventProvider.Asm1FileExtensions; }
            set { if (ValidateExtensions(value)) _optionsEventProvider.Asm1FileExtensions = value; }
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm2FileExtensions
        {
            get { return _optionsEventProvider.Asm2FileExtensions; }
            set { if (ValidateExtensions(value)) _optionsEventProvider.Asm2FileExtensions = value; }
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

        public override Task InitializeAsync()
        {
            // make sure this managers initialized before initial option event
            _ = Package.Instance.GetMEFComponent<ContentTypeManager>();
            _ = Package.Instance.GetMEFComponent<InstructionListManager>();

            _optionsEventProvider.OptionsUpdatedInvoke();
            return Task.CompletedTask;
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