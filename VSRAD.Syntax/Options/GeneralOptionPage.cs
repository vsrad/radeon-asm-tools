using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionPage : DialogPage
    {
        private readonly GeneralOptionProvider _optionProvider;

        public GeneralOptionPage()
        {
            _optionProvider = GeneralOptionProvider.Instance;
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        public SortState SortOptions
        {
            get => _optionProvider.SortOptions;
            set => _optionProvider.SortOptions = value;
        }

        [Category("Function list")]
        [DisplayName("AutoScroll function list")]
        [Description("Scroll to current function in the function list automatically")]
        public bool AutoScroll
        {
            get => _optionProvider.AutoScroll;
            set => _optionProvider.AutoScroll = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        public bool IsEnabledIndentGuides
        {
            get => _optionProvider.IsEnabledIndentGuides;
            set => _optionProvider.IsEnabledIndentGuides = value;
        }

#if DEBUG
        [Category("Syntax highlight")]
        [DisplayName("Indent guide line thickness")]
        public double IndentGuideThickness
        {
            get => _optionProvider.IndentGuideThickness;
            set => _optionProvider.IndentGuideThickness = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide dash size")]
        public double IndentGuideDashSize
        {
            get => _optionProvider.IndentGuideDashSize;
            set => _optionProvider.IndentGuideDashSize = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide space size")]
        [Description("Space size between indent lines")]
        public double IndentGuideSpaceSize
        {
            get => _optionProvider.IndentGuideSpaceSize;
            set => _optionProvider.IndentGuideSpaceSize = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset X")]
        public double IndentGuideOffsetX
        {
            get => _optionProvider.IndentGuideOffsetX;
            set => _optionProvider.IndentGuideOffsetX = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset Y")]
        public double IndentGuideOffsetY
        {
            get => _optionProvider.IndentGuideOffsetY;
            set => _optionProvider.IndentGuideOffsetY = value;
        }
#endif

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        public string Asm1FileExtensions
        {
            get => ConvertListTo(_optionProvider.Asm1FileExtensions);
            set => _optionProvider.Asm1FileExtensions = ConvertListFrom(value);
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        public string Asm2FileExtensions
        {
            get => ConvertListTo(_optionProvider.Asm2FileExtensions);
            set => _optionProvider.Asm2FileExtensions = ConvertListFrom(value);
        }

        [Category("Syntax instruction folder paths")]
        [DisplayName("Instruction folder paths")]
        [Description("List of folder path separated by semicolon wit assembly instructions with .radasm file extension")]
        [Editor(typeof(FolderPathsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string InstructionsPaths
        {
            get => ConvertListTo(_optionProvider.InstructionsPaths);
            set => _optionProvider.InstructionsPaths = ConvertListFrom(value);
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete instruction")]
        [Description("Autocomplete instructions")]
        public bool AutocompleteInstructions
        {
            get => _optionProvider.AutocompleteInstructions;
            set => _optionProvider.AutocompleteInstructions = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete function")]
        [Description("Autocomplete function name")]
        public bool AutocompleteFunctions
        {
            get => _optionProvider.AutocompleteFunctions;
            set => _optionProvider.AutocompleteFunctions = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete label")]
        [Description("Autocomplete labels")]
        public bool AutocompleteLabels
        {
            get => _optionProvider.AutocompleteLabels;
            set => _optionProvider.AutocompleteLabels = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete variable")]
        [Description("Autocomplete global variables, local variables, function arguments")]
        public bool AutocompleteVariables
        {
            get => _optionProvider.AutocompleteVariables;
            set => _optionProvider.AutocompleteVariables = value;
        }

        [Category("Intellisense")]
        [DisplayName("Signature help")]
        public bool SignatureHelp
        {
            get => _optionProvider.SignatureHelp;
            set => _optionProvider.SignatureHelp = value;
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

        public override void LoadSettingsFromStorage() =>
            _optionProvider.Load();

        public override void SaveSettingsToStorage() =>
            _optionProvider.Save();

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (!_optionProvider.Validate())
                e.ApplyBehavior = ApplyKind.Cancel;

            base.OnApply(e);
        }

        private static List<string> ConvertListFrom(string str) =>
            str.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        private static string ConvertListTo(IEnumerable<string> extensions) =>
            string.Join(";", extensions.ToArray());
    }
}