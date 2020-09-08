using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.Options.Instructions
{
    [Export(typeof(IInstructionListManager))]
    internal sealed class InstructionListManager : IInstructionListManager
    {
        private readonly OptionsProvider _optionsProvider;
        private readonly Lazy<IDocumentFactory> _documentFactory;
        private readonly Lazy<INavigationTokenService> _navigationTokenService;
        private readonly List<Instruction> _instructions;
        private string _loadedPaths;

        public event InstructionsUpdateDelegate InstructionsUpdated;

        [ImportingConstructor]
        public InstructionListManager(OptionsProvider optionsEventProvider, 
            Lazy<IDocumentFactory> documentFactory,
            Lazy<INavigationTokenService> navigationTokenService)
        {
            _optionsProvider = optionsEventProvider;
            _documentFactory = documentFactory;
            _navigationTokenService = navigationTokenService;
            _instructions = new List<Instruction>();

            _optionsProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(OptionsProvider provider)
        {
            var instructionPaths = provider.InstructionsPaths;

            // skip if options haven't changed
            if (instructionPaths == _loadedPaths) return;

            Task.Run(() => LoadInstructionsFromDirectories(instructionPaths))
                .RunAsyncWithoutAwait();
        }

        public void LoadInstructionsFromDirectories(string dirPathsString)
        {
            _instructions.Clear();

            var paths = dirPathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            var loadFromDirectoryTasks = paths
                .Select(p => LoadInstructionsFromDirectoryAsync(p))
                .ToArray();

            try
            {
                Task.WaitAll(loadFromDirectoryTasks);

                _loadedPaths = dirPathsString;
                InstructionsUpdated?.Invoke(this);
            }catch(AggregateException e)
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

        private Task LoadInstructionsFromDirectoryAsync(string path)
        {
            try
            {
                List<Task> loadTasks = new List<Task>();
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.FileExtensionAsm1Doc)
                        loadTasks.Add(LoadInstructionsFromFileAsync(filepath, InstructionType.RadAsm1));

                    else if (Path.GetExtension(filepath) == Constants.FileExtensionAsm2Doc)
                        loadTasks.Add(LoadInstructionsFromFileAsync(filepath, InstructionType.RadAsm2));
                }

                return Task.WhenAll(loadTasks);
            }
            catch (Exception e) when (
               e is DirectoryNotFoundException ||
               e is IOException ||
               e is PathTooLongException ||
               e is SecurityException ||
               e is UnauthorizedAccessException)
            {
                return Task.FromException(e);
            }
        }

        private async Task LoadInstructionsFromFileAsync(string path, InstructionType type)
        {
            var document = _documentFactory.Value.GetOrCreateDocument(path);
            var documentAnalysis = document.DocumentAnalysis;
            var snapshot = document.CurrentSnapshot;
            var analysisResult = await documentAnalysis.GetAnalysisResultAsync(snapshot);

            var instructions = analysisResult.Root.Tokens
                .Where(t => t.Type == RadAsmTokenType.Instruction);

            foreach (var instructionToken in instructions)
            {
                var text = instructionToken.TrackingToken.GetText(snapshot);
                var navigation = _navigationTokenService.Value.CreateToken(instructionToken, path);
                var instruction = new Instruction(text, navigation, type);

                _instructions.Add(instruction);
            }
        }
        public IReadOnlyList<Instruction> GetInstructions(AsmType asmType)
        {
            IEnumerable<Instruction> instructions;
            switch (asmType)
            {
                case AsmType.RadAsm:
                    instructions = _instructions.Where(i => i.Type == InstructionType.RadAsm1); break;
                case AsmType.RadAsm2:
                    instructions = _instructions.Where(i => i.Type == InstructionType.RadAsm2); break;
                default:
                    instructions = _instructions; break;
            }
            return instructions.ToList();
        }
    }
}
