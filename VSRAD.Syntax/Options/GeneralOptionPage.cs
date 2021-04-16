using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionPage : DialogPage
    {
        private readonly GeneralOptionProvider _generalOptionEventProvider;
        private readonly GeneralOptionModel _model;

        public GeneralOptionPage()
        {
            _generalOptionEventProvider = GeneralOptionProvider.Instance;
            _model = GeneralOptionModel.Instance;
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        public SortState SortOptions
        {
            get => _generalOptionEventProvider.SortOptions;
            set => _generalOptionEventProvider.SortOptions = value;
        }

        [Category("Function list")]
        [DisplayName("AutoScroll function list")]
        [Description("Scroll to current function in the function list automatically")]
        public bool AutoScroll
        {
            get => _generalOptionEventProvider.AutoScroll;
            set => _generalOptionEventProvider.AutoScroll = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        public bool IsEnabledIndentGuides
        {
            get => _generalOptionEventProvider.IsEnabledIndentGuides;
            set => _generalOptionEventProvider.IsEnabledIndentGuides = value;
        }

#if DEBUG
        [Category("Syntax highlight")]
        [DisplayName("Indent guide line thickness")]
        public double IndentGuideThickness
        {
            get => _generalOptionEventProvider.IndentGuideThickness;
            set => _generalOptionEventProvider.IndentGuideThickness = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide dash size")]
        public double IndentGuideDashSize
        {
            get => _generalOptionEventProvider.IndentGuideDashSize;
            set => _generalOptionEventProvider.IndentGuideDashSize = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide space size")]
        [Description("Space size between indent lines")]
        public double IndentGuideSpaceSize
        {
            get => _generalOptionEventProvider.IndentGuideSpaceSize;
            set => _generalOptionEventProvider.IndentGuideSpaceSize = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset X")]
        public double IndentGuideOffsetX
        {
            get => _generalOptionEventProvider.IndentGuideOffsetX;
            set => _generalOptionEventProvider.IndentGuideOffsetX = value;
        }

        [Category("Syntax highlight")]
        [DisplayName("Indent guide line offset Y")]
        public double IndentGuideOffsetY
        {
            get => _generalOptionEventProvider.IndentGuideOffsetY;
            set => _generalOptionEventProvider.IndentGuideOffsetY = value;
        }
#endif

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        public string Asm1FileExtensions
        {
            get => ConvertListTo(_generalOptionEventProvider.Asm1FileExtensions);
            set => _generalOptionEventProvider.Asm1FileExtensions = ConvertListFrom(value);
        }

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        public string Asm2FileExtensions
        {
            get => ConvertListTo(_generalOptionEventProvider.Asm2FileExtensions);
            set => _generalOptionEventProvider.Asm2FileExtensions = ConvertListFrom(value);
        }

        [Category("Syntax instruction folder paths")]
        [DisplayName("Instruction folder paths")]
        [Description("List of folder path separated by semicolon wit assembly instructions with .radasm file extension")]
        [Editor(typeof(FolderPathsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string InstructionsPaths
        {
            get => ConvertListTo(_generalOptionEventProvider.InstructionsPaths);
            set => _generalOptionEventProvider.InstructionsPaths = ConvertListFrom(value);
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete instruction")]
        [Description("Autocomplete instructions")]
        public bool AutocompleteInstructions
        {
            get => _generalOptionEventProvider.AutocompleteInstructions;
            set => _generalOptionEventProvider.AutocompleteInstructions = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete function")]
        [Description("Autocomplete function name")]
        public bool AutocompleteFunctions
        {
            get => _generalOptionEventProvider.AutocompleteFunctions;
            set => _generalOptionEventProvider.AutocompleteFunctions = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete label")]
        [Description("Autocomplete labels")]
        public bool AutocompleteLabels
        {
            get => _generalOptionEventProvider.AutocompleteLabels;
            set => _generalOptionEventProvider.AutocompleteLabels = value;
        }

        [Category("Intellisense")]
        [DisplayName("Autocomplete variable")]
        [Description("Autocomplete global variables, local variables, function arguments")]
        public bool AutocompleteVariables
        {
            get => _generalOptionEventProvider.AutocompleteVariables;
            set => _generalOptionEventProvider.AutocompleteVariables = value;
        }

        [Category("Intellisense")]
        [DisplayName("Signature help")]
        public bool SignatureHelp
        {
            get => _generalOptionEventProvider.SignatureHelp;
            set => _generalOptionEventProvider.SignatureHelp = value;
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

        // hack to avoid installation errors when reinstalling the extension
        public override void LoadSettingsFromStorage() =>
            _model.Load();

        public override void SaveSettingsToStorage() =>
            _model.Save();

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (!_model.Validate())
                e.ApplyBehavior = ApplyKind.Cancel;

            base.OnApply(e);
        }

        private static List<string> ConvertListFrom(string str) =>
            str.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        private static string ConvertListTo(IEnumerable<string> extensions) =>
            string.Join(";", extensions.ToArray());
    }
}