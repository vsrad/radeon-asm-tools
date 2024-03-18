using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsLoadDelegate(IReadOnlyList<IInstructionSet> instructions);

    public interface IInstructionListLoader
    {
        event InstructionsLoadDelegate InstructionsUpdated;
    }

    [Export(typeof(IInstructionListLoader))]
    internal sealed class InstructionListLoader : IInstructionListLoader
    {
        private readonly OptionsProvider _optionsProvider;
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly List<InstructionSet> _sets;
        private string _loadedPaths;

        public event InstructionsLoadDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListLoader(OptionsProvider optionsEventProvider, Lazy<IDocumentFactory> documentFactory)
        {
            _optionsProvider = optionsEventProvider;
            _documentFactory = documentFactory;
            _sets = new List<InstructionSet>();

            _optionsProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(OptionsProvider provider)
        {
            var instructionPaths = provider.InstructionsPaths;

            // skip if options haven't changed
            if (instructionPaths == _loadedPaths) return;

            Task.Run(() => LoadInstructionsFromDirectoriesAsync(instructionPaths))
                .RunAsyncWithoutAwait();
        }

        public async Task LoadInstructionsFromDirectoriesAsync(string dirPathsString)
        {
            var paths = dirPathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            var loadFromDirectoryTasks = paths
                .Select(p => LoadInstructionsFromDirectoryAsync(p))
                .ToArray();

            try
            {
                var results = await Task.WhenAll(loadFromDirectoryTasks);
                var instructionSets = results.SelectMany(t => t);

                _sets.Clear();
                _sets.AddRange(instructionSets);
                _loadedPaths = dirPathsString;

                InstructionsUpdated?.Invoke(_sets);
            }
            catch (AggregateException e)
            {
                var sb = new StringBuilder();
                sb.AppendLine(e.Message);
                sb.AppendLine();

                foreach (var innerEx in e.InnerExceptions)
                    sb.AppendLine(innerEx.Message);

                sb.AppendLine();
                sb.AppendLine("Change the path to instructions");
                Error.ShowErrorMessage(sb.ToString(), "Instruction loader");
            }
        }

        // TODO: implement with IAsyncEnumerable
        private async Task<IEnumerable<InstructionSet>> LoadInstructionsFromDirectoryAsync(string path)
        {
            var instructionSets = new List<InstructionSet>();
            try
            {
                var loadTasks = new List<Task<InstructionSet>>();
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.FileExtensionAsm1Doc)
                        loadTasks.Add(LoadInstructionsFromFileAsync(filepath, AsmType.RadAsm));

                    else if (Path.GetExtension(filepath) == Constants.FileExtensionAsm2Doc)
                        loadTasks.Add(LoadInstructionsFromFileAsync(filepath, AsmType.RadAsm2));
                }

                var results = await Task.WhenAll(loadTasks);
                instructionSets.AddRange(results);
            }
            catch (Exception e) when (
               e is DirectoryNotFoundException ||
               e is IOException ||
               e is PathTooLongException ||
               e is UnauthorizedAccessException)
            {
                Error.ShowError(e, "Instruction loader");
            }

            return instructionSets;
        }

        private async Task<InstructionSet> LoadInstructionsFromFileAsync(string path, AsmType type)
        {
            var document = _documentFactory.Value.GetOrCreateDocument(path);
            var documentAnalysis = document.DocumentAnalysis;
            var snapshot = document.CurrentSnapshot;
            var analysisResult = await documentAnalysis.GetAnalysisResultAsync(snapshot);

            var setName = Path.GetFileNameWithoutExtension(path);
            var targets = new List<string>();
            if (analysisResult.Root.Tokens.Find(t => t is DocTargetListToken) is DocTargetListToken targetListToken)
                targets.AddRange(targetListToken.TargetList);
            else
                targets.Add(setName);

            var instructionSet = new InstructionSet(type, setName, targets);
            foreach (var block in analysisResult.Root.Children)
            {
                if (block is InstructionDocBlock docBlock)
                {
                    var aliases = docBlock.InstructionNames.Distinct(new AnalysisTokenTextComparer()).Select(n => new NavigationToken(document, n)).ToList();
                    instructionSet.AddInstruction(new Instruction(docBlock.DocComment, aliases));
                }
            }
            return instructionSet;
        }
    }
}
