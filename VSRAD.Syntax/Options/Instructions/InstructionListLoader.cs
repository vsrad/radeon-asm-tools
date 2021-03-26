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
        IReadOnlyList<IInstructionSet> InstructionSets { get; }
        event InstructionsLoadDelegate InstructionsUpdated;
    }

    [Export(typeof(IInstructionListLoader))]
    internal sealed class InstructionListLoader : IInstructionListLoader
    {
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly List<IInstructionSet> _sets;
        private IReadOnlyList<string> _loadedPaths;

        public IReadOnlyList<IInstructionSet> InstructionSets => _sets;
        public event InstructionsLoadDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListLoader(GeneralOptionProvider generalOptionEventProvider,
            Lazy<IDocumentFactory> documentFactory,
            Lazy<INavigationTokenService> navigationTokenService)
        {
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
            _sets = new List<IInstructionSet>();
            _loadedPaths = new List<string>();

            generalOptionEventProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(GeneralOptionProvider provider)
        {
            var instructionPaths = provider.InstructionsPaths.ToHashSet();
            instructionPaths.SymmetricExceptWith(_loadedPaths);

            // skip if options haven't changed
            if (instructionPaths.Count == 0) return;

            Task.Run(() => LoadInstructionsFromDirectoriesAsync(provider.InstructionsPaths))
                .RunAsyncWithoutAwait();
        }

        public async Task LoadInstructionsFromDirectoriesAsync(IReadOnlyList<string> paths)
        {
            var loadFromDirectoryTasks = paths
                .Select(LoadInstructionsFromDirectoryAsync)
                .ToArray();

            try
            {
                var results = await Task.WhenAll(loadFromDirectoryTasks).ConfigureAwait(false);
                var instructionSets = results.SelectMany(t => t);

                _sets.Clear();
                _sets.AddRange(instructionSets);
                _loadedPaths = paths;

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

                var results = await Task.WhenAll(loadTasks).ConfigureAwait(false);
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
