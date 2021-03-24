using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;

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
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly List<InstructionSet> _sets;
        private string _loadedPaths;

        public event InstructionsLoadDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListLoader(OptionsProvider optionsEventProvider,
            Lazy<IDocumentFactory> documentFactory,
            Lazy<INavigationTokenService> navigationTokenService)
        {
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
            _sets = new List<InstructionSet>();

            optionsEventProvider.OptionsUpdated += OptionsUpdated;
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
                .Select(LoadInstructionsFromDirectoryAsync)
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
                    switch (Path.GetExtension(filepath))
                    {
                        case Constants.FileExtensionAsm1Doc:
                            loadTasks.Add(LoadInstructionsFromFileAsync(filepath, InstructionType.RadAsm1));
                            break;
                        case Constants.FileExtensionAsm2Doc:
                            loadTasks.Add(LoadInstructionsFromFileAsync(filepath, InstructionType.RadAsm2));
                            break;
                    }
                }

                var results = await Task.WhenAll(loadTasks);
                instructionSets.AddRange(results);
            }
            catch (Exception e) when (
               e is DirectoryNotFoundException ||
               e is IOException ||
               e is UnauthorizedAccessException)
            {
                Error.ShowError(e, "Instruction loader");
            }

            return instructionSets;
        }

        private async Task<InstructionSet> LoadInstructionsFromFileAsync(string path, InstructionType type)
        {
            var document = _documentFactory.Value.GetOrCreateDocument(path);
            var documentAnalysis = document.DocumentAnalysis;
            var snapshot = document.CurrentSnapshot;
            var analysisResult = await documentAnalysis.GetAnalysisResultAsync(snapshot);
            var instructionSet = new InstructionSet(path, type);

            var instructions = analysisResult.Root.Tokens
                .Where(t => t.Type == RadAsmTokenType.Instruction);

            var navigationService = _navigationTokenService.Value;

            var navigationTokens = instructions
                .Select(i => navigationService.CreateToken(i))
                .GroupBy(n => n.AnalysisToken.Text);

            foreach (var instructionNameGroup in navigationTokens)
            {
                var name = instructionNameGroup.Key;
                var navigations = instructionNameGroup.ToList();
                instructionSet.AddInstruction(name, navigations);
            }

            return instructionSet;
        }
    }
}
