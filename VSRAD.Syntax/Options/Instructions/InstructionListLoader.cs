using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.Options.Instructions
{
    public delegate void InstructionsLoadDelegate(IEnumerable<IInstructionSet> instructions);

    public interface IInstructionListLoader
    {
        IEnumerable<IInstructionSet> InstructionSets { get; }
        event InstructionsLoadDelegate InstructionsUpdated;
    }

    [Export(typeof(IInstructionListLoader))]
    internal sealed class InstructionListLoader : IInstructionListLoader
    {
        private readonly object _lock = new object();
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly Dictionary<string, IInstructionSet> _sets;
        private readonly Dictionary<string, ITextDocument> _instructionSetPaths;

        public IEnumerable<IInstructionSet> InstructionSets => _sets.Values;
        public event InstructionsLoadDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListLoader(GeneralOptionProvider generalOptionEventProvider,
            Lazy<IDocumentFactory> documentFactory,
            Lazy<INavigationTokenService> navigationTokenService)
        {
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
            _sets = new Dictionary<string, IInstructionSet>(StringComparer.OrdinalIgnoreCase);
            _instructionSetPaths = new Dictionary<string, ITextDocument>(StringComparer.OrdinalIgnoreCase);

            generalOptionEventProvider.OptionsUpdated += OptionsUpdated;
            OptionsUpdated(generalOptionEventProvider);
        }

        private void OptionsUpdated(GeneralOptionProvider provider)
        {
            var sb = new StringBuilder();
            var newInstructionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in provider.InstructionsPaths)
            {
                try
                {
                    foreach (var filepath in Directory.EnumerateFiles(directory))
                    {
                        newInstructionPaths.Add(filepath);
                    }
                }
                catch (Exception e) when (
                    e is DirectoryNotFoundException ||
                    e is PathTooLongException ||
                    e is SecurityException ||
                    e is IOException ||
                    e is UnauthorizedAccessException)
                {
                    sb.AppendLine(e.Message);
                }
            }

            if (sb.Length != 0)
                Error.ShowErrorMessage(sb.ToString(), "Instruction loader");

            lock (_lock)
            {
                var invalidatedPaths = newInstructionPaths.ToHashSet();
                invalidatedPaths.SymmetricExceptWith(_instructionSetPaths.Keys);

                // skip if options haven't changed
                if (invalidatedPaths.Count == 0) return;

                invalidatedPaths.ExceptWith(newInstructionPaths);
                CleanInstructionSets(invalidatedPaths);

                // now _instructionSetPaths contains only elements which is both in new sets and old sets
                newInstructionPaths.ExceptWith(_instructionSetPaths.Keys);
            }

            Task.Run(() => LoadInstructionsFromDirectoriesAsync(newInstructionPaths))
                .RunAsyncWithoutAwait();
        }

        public async Task LoadInstructionsFromDirectoriesAsync(IEnumerable<string> paths)
        {
            var loadFromDirectoryTasks = paths
                .Select(LoadInstructionsFromDirectoryAsync)
                .ToArray();

            try
            {
                var results = await Task.WhenAll(loadFromDirectoryTasks).ConfigureAwait(false);
                var instructionSets = results.Where(s => s != null);

                lock (_lock)
                {
                    foreach (var tuple in instructionSets)
                    {
                        var (path, document, set) = tuple;
                        if (document.CurrentSnapshot.TextBuffer.GetTextDocument(out var textDocument))
                        {

                            _sets.Add(path, set);
                            _instructionSetPaths.Add(path, textDocument);
                        }
                    }

                    InstructionsUpdated?.Invoke(_sets.Values);
                }
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
        private async Task<Tuple<string, IDocument, InstructionSet>> LoadInstructionsFromDirectoryAsync(string path)
        {

            Task<Tuple<IDocument, InstructionSet>> loadTask;
            switch (Path.GetExtension(path))
            {
                case Constants.FileExtensionAsm1Doc:
                    loadTask = LoadInstructionsFromFileAsync(path, InstructionType.RadAsm1);
                    break;
                case Constants.FileExtensionAsm2Doc:
                    loadTask = LoadInstructionsFromFileAsync(path, InstructionType.RadAsm2);
                    break;
                default:
                    return null;
            }

            var (document, set) = await loadTask.ConfigureAwait(false);
            return new Tuple<string, IDocument, InstructionSet>(path, document, set);
        }

        private async Task<Tuple<IDocument, InstructionSet>> LoadInstructionsFromFileAsync(string path, InstructionType type)
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
                .Select(i => navigationService.CreateToken(i, document))
                .GroupBy(n => n.AnalysisToken.Text);

            foreach (var instructionNameGroup in navigationTokens)
            {
                var name = instructionNameGroup.Key;
                var navigations = instructionNameGroup.ToList();
                instructionSet.AddInstruction(name, navigations);
            }

            return new Tuple<IDocument, InstructionSet>(document, instructionSet);
        }

        private void CleanInstructionSets(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (_instructionSetPaths.TryGetValue(path, out var textDocument))
                {
                    textDocument.Dispose();
                    _sets.Remove(path);
                    _instructionSetPaths.Remove(path);
                }
            }
        }
    }
}
