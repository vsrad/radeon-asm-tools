using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    public class GeneralOptionProvider
    {
        private static readonly Regex FileExtensionRegular = new Regex(@"^\.\w+$", RegexOptions.Compiled);
        private static readonly Lazy<GeneralOptionProvider> LazyInstance =
            new Lazy<GeneralOptionProvider>(() => new GeneralOptionProvider());
        private readonly Task<GeneralOptionModel> _optionModelInitTask;
        private GeneralOptionModel OptionModel => _optionModelInitTask.Result;

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
            Asm1SelectedSet = string.Empty;
            Asm2SelectedSet = string.Empty;
            InstructionsPaths = DefaultInstructionPaths.Value;
            AutocompleteInstructions = false;
            AutocompleteFunctions = false;
            AutocompleteLabels = false;
            AutocompleteVariables = false;
            SignatureHelp = false;

            _optionModelInitTask = Task.Run(async () =>
            {
                var optionModel = await GeneralOptionModel
                    .GetInstanceAsync()
                    .ConfigureAwait(false);

                // required initialization before OptionsSaved event
                var serviceProvider = AsyncServiceProvider.GlobalProvider;
                _ = await serviceProvider.GetMefServiceAsync<ContentTypeManager>();
                _ = await serviceProvider.GetMefServiceAsync<IInstructionListLoader>();

                optionModel.OptionsSaved += OptionsUpdatedInvoke;
                OptionsUpdatedInvoke(optionModel);

                return optionModel;
            });
        }

        public static GeneralOptionProvider Instance => LazyInstance.Value;

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
        public string Asm1SelectedSet;
        public string Asm2SelectedSet;
        public IReadOnlyList<string> InstructionsPaths;
        public bool AutocompleteInstructions;
        public bool AutocompleteFunctions;
        public bool AutocompleteLabels;
        public bool AutocompleteVariables;
        public bool SignatureHelp;

        public delegate void OptionsUpdate(GeneralOptionProvider sender);
        public event OptionsUpdate OptionsUpdated;

        public void Load() =>
            OptionModel.Load();

        public void Save() =>
            OptionModel.Save();

        public bool Validate()
        {
            var sb = new StringBuilder();
            foreach (var ext in Asm1FileExtensions)
            {
                if (!FileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }

            foreach (var ext in Asm2FileExtensions)
            {
                if (!FileExtensionRegular.IsMatch(ext))
                    sb.AppendLine($"Invalid file extension format \"{ext}\"");
            }

            var asm1Set = Asm1FileExtensions.ToHashSet();
            var asm2Set = Asm2FileExtensions.ToHashSet();
            asm1Set.IntersectWith(asm2Set);
            foreach (var ext in asm1Set)
                sb.AppendLine($"\"{ext}\" must be only in one syntax (asm1 or asm2)");

            foreach (var path in InstructionsPaths)
                if (!Directory.Exists(path))
                    sb.AppendLine($"\"{path}\" is not exists");

            if (sb.Length == 0) return true;

            Error.ShowErrorMessage(sb.ToString());
            return false;
        }

        private void OptionsUpdatedInvoke(GeneralOptionModel sender) =>
            OptionsUpdated?.Invoke(this);

        public static readonly Lazy<IReadOnlyList<string>>
            DefaultInstructionPaths = new Lazy<IReadOnlyList<string>>(() =>
                {
                    var assemblyFolder = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                    return new List<string>() { Path.GetDirectoryName(assemblyFolder) };
                });

        public static bool IsDefaultInstructionPaths(IEnumerable<string> paths)
        {
            var set = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            set.SymmetricExceptWith(DefaultInstructionPaths.Value);
            return set.Count == 0;
        }
    }
}