using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Parser;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Utilities;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(InstructionListManager))]
    internal sealed class InstructionListManager
    {
        public delegate void InstructionsUpdateDelegate(IReadOnlyList<string> instructions);
        public event InstructionsUpdateDelegate InstructionUpdated;

        private readonly RadeonServiceProvider _serviceProvider;
        private readonly OptionsProvider _optionsProvider;
        private readonly IContentType _contentType;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        public Dictionary<string, List<KeyValuePair<NavigationToken, AsmType>>> InstructionList { get; }

        [ImportingConstructor]
        public InstructionListManager(OptionsProvider optionsEventProvider, RadeonServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _optionsProvider = optionsEventProvider;
            _contentType = _serviceProvider.ContentTypeRegistryService.GetContentType(Constants.RadeonAsmDocumentationContentType);

            _documentAnalysisProvoder = new DocumentAnalysisProvoder(this);
            InstructionList = new Dictionary<string, List<KeyValuePair<NavigationToken, AsmType>>>();
            _optionsProvider.OptionsUpdated += InstructionPathsUpdated;
        }

        private void InstructionPathsUpdated(OptionsProvider provider) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => LoadInstructionsFromDirectoriesAsync(provider.InstructionsPaths));

        public Task LoadInstructionsFromDirectoriesAsync(string dirPathsString)
        {
            InstructionList.Clear();
            if (string.IsNullOrWhiteSpace(dirPathsString))
                return Task.CompletedTask;

            var paths = dirPathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var path in paths)
            {
                LoadInstructionsFromDirectory(path);
            }

            InstructionUpdated?.Invoke(InstructionList.Keys.ToList());
            return Task.CompletedTask;
        }

        private void LoadInstructionsFromDirectory(string path)
        {
            try
            {
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.FileExtensionAsmDoc)
                    {
                        var extension = Path.GetExtension(filepath.TrimSuffix(Constants.FileExtensionAsmDoc, StringComparison.OrdinalIgnoreCase));
                        if (_optionsProvider.Asm1FileExtensions.Contains(extension))
                            LoadInstructionsFromFile(filepath, AsmType.RadAsm);
                        else if (_optionsProvider.Asm2FileExtensions.Contains(extension))
                            LoadInstructionsFromFile(filepath, AsmType.RadAsm2);
                    }
                }
            }catch (Exception e)
            {
                Error.ShowError(e, "instrunction folder paths");
            }
        }

        private void LoadInstructionsFromFile(string path, AsmType asmType)
        {
            try
            {
                var buffer = CreateTextDocument(path);
                if (buffer == null)
                    throw new InvalidDataException($"Cannot create ITextBuffer for the {path}");

                var documentAnalysis = _documentAnalysisProvoder.CreateDocumentAnalysis(buffer);
                if (documentAnalysis.LastParserResult.Count > 0)
                {
                    var instructions = documentAnalysis
                        .LastParserResult[0]
                        .Tokens
                        .Where(t => t.Type == Parser.Tokens.RadAsmTokenType.Instruction);

                    var version = documentAnalysis.CurrentSnapshot;
                    foreach (var instruction in instructions)
                    {
                        var text = instruction.TrackingToken.GetText(version);
                        var pair = new KeyValuePair<NavigationToken, AsmType>(new NavigationToken(instruction, version), asmType);
                        if (InstructionList.TryGetValue(text, out var navigationTokens))
                        {
                            navigationTokens.Add(pair);
                        }
                        else
                        {
                            InstructionList.Add(text, new List<KeyValuePair<NavigationToken, AsmType>>() { pair });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error.ShowError(e, "instrunction file paths");
            }
        }

        private ITextBuffer CreateTextDocument(string name)
        {
            var document = _serviceProvider.TextDocumentFactoryService.CreateAndLoadTextDocument(name, _contentType);
            return document.TextBuffer;
        }
    }
}
