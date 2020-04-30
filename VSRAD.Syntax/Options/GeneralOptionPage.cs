using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using VSRAD.Syntax.Helpers;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionPage : BaseOptionPage
    {
        private static readonly Regex fileExtensionRegular = new Regex(@"^\.\w+$");
        private readonly ContentTypeManager _contentTypeManager;
        private readonly InstructionListManager _instructionListManager;

        public GeneralOptionPage(): base()
        {
            _contentTypeManager = Package.Instance.GetMEFComponent<ContentTypeManager>();
            _instructionListManager = Package.Instance.GetMEFComponent<InstructionListManager>();
            _collectionSettings = new Dictionary<string, KeyValuePair<string, (List<string>, IReadOnlyList<string>)>>()
            {
                { "Asm1CollectionFileExtensions", new KeyValuePair<string, (List<string>, IReadOnlyList<string>)>(nameof(Asm1FileExtensions), (Asm1FileExtensions, Constants.DefaultFileExtensionAsm1)) },
                { "Asm2CollectionFileExtensions", new KeyValuePair<string, (List<string>, IReadOnlyList<string>)>(nameof(Asm2FileExtensions), (Asm2FileExtensions, Constants.DefaultFileExtensionAsm2)) },
            };
        }

        [Category("Function list")]
        [DisplayName("Function list default sort option")]
        [Description("Set default sort option for Function List")]
        [DefaultValue(SortState.ByName)]
        public SortState SortOptions { get; set; } = SortState.ByName;

        [Category("Syntax highlight")]
        [DisplayName("Indent guide lines")]
        [Description("Enable/disable indent guide lines")]
        [DefaultValue(true)]
        public bool IsEnabledIndentGuides { get; set; } = true;

        [Category("Syntax file extensions")]
        [DisplayName("Asm1 file extensions")]
        [Description("List of file extensions for the asm1 syntax")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm1FileExtensions { get; set; } = new List<string>();

        [Category("Syntax file extensions")]
        [DisplayName("Asm2 file extensions")]
        [Description("List of file extensions for the asm2 syntax")]
        [Editor(@"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        public List<string> Asm2FileExtensions { get; set; } = new List<string>();

        [Category("Syntax instruction file paths")]
        [DisplayName("Instruction file paths")]
        [Description("List of files separated by semicolon with assembly instructions")]
        [Editor(typeof(FilePathsEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string InstructionsPaths { get; set; } = "";

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

        public override async Task InitializeAsync()
        { 
            await UpdateRadeonContentTypeAsync();
            await _instructionListManager.LoadInstructionsFromFilesAsync(InstructionsPaths);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            try
            {
                base.OnApply(e);

                ValidateExtensions(Asm1FileExtensions);
                ValidateExtensions(Asm2FileExtensions);
                FunctionList.FunctionList.TryUpdateSortOptions(SortOptions);
                ThreadHelper.JoinableTaskFactory.RunAsync(() => UpdateRadeonContentTypeAsync());
                ThreadHelper.JoinableTaskFactory.RunAsync(() => _instructionListManager.LoadInstructionsFromFilesAsync(InstructionsPaths));
            }
            catch(Exception ex)
            {
                Error.ShowWarning(ex);
            }
        }
        
        private void ValidateExtensions(List<string> extensions)
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
                throw new ArgumentException(sb.ToString());
            }
        }

        private Task UpdateRadeonContentTypeAsync() =>
            _contentTypeManager.ChangeRadeonExtensionsAsync(Asm1FileExtensions, Asm2FileExtensions);
    }
}