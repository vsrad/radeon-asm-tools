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
        public delegate void InstructionsUpdateDelegate(IReadOnlyDictionary<string, List<NavigationToken>> instructions);
        public event InstructionsUpdateDelegate InstructionUpdated;

        private readonly RadeonServiceProvider _serviceProvider;
        private readonly IContentType _contentType;
        private readonly DocumentAnalysisProvoder _documentAnalysisProvoder;

        public Dictionary<string, List<NavigationToken>> InstructionList { get; }

        [ImportingConstructor]
        public InstructionListManager(OptionsProvider optionsEventProvider, RadeonServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _contentType = _serviceProvider.ContentTypeRegistryService.GetContentType(Constants.RadeonAsmDocumentationContentType);

            _documentAnalysisProvoder = new DocumentAnalysisProvoder(this);
            InstructionList = new Dictionary<string, List<NavigationToken>>();
            optionsEventProvider.OptionsUpdated += InstructionPathsUpdated;
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

            InstructionUpdated?.Invoke(InstructionList);
            return Task.CompletedTask;
        }

        private void LoadInstructionsFromDirectory(string path)
        {
            try
            {
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.InstructionsFileExtension)
                        LoadInstructionsFromFile(filepath);
                }
            }catch (Exception e)
            {
                Error.ShowError(e, "instrunction folder paths");
            }
        }

        private void LoadInstructionsFromFile(string path)
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
                        if (InstructionList.TryGetValue(text, out var navigationTokens))
                        {
                            navigationTokens.Add(new NavigationToken(instruction, version));
                        }
                        else
                        {
                            InstructionList.Add(text, new List<NavigationToken>() { new NavigationToken(instruction, version) });
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
