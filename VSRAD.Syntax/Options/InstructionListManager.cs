using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;
using VSRAD.Syntax.Core;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Options
{
    [Export(typeof(InstructionListManager))]
    internal sealed class InstructionListManager
    {
        public delegate void InstructionsUpdateDelegate(IReadOnlyList<string> instructions);
        public event InstructionsUpdateDelegate InstructionUpdated;

        private readonly OptionsProvider _optionsProvider;
        private Lazy<DocumentAnalysisProvoder> _documentAnalysisProvoder { get; set; }
        private string _loadedPaths { get; set; }

        public Dictionary<string, List<KeyValuePair<NavigationToken, AsmType>>> InstructionList { get; }

        [ImportingConstructor]
        public InstructionListManager([Import(AllowDefault = true, AllowRecomposition = true)]Lazy<DocumentAnalysisProvoder> documentAnalysisProvoder /* circular dependency approach */, 
            OptionsProvider optionsEventProvider)
        {
            _optionsProvider = optionsEventProvider;

            _documentAnalysisProvoder = documentAnalysisProvoder;
            InstructionList = new Dictionary<string, List<KeyValuePair<NavigationToken, AsmType>>>();
            _optionsProvider.OptionsUpdated += InstructionPathsUpdated;
        }

        private void InstructionPathsUpdated(OptionsProvider provider) =>
            LoadInstructionsFromDirectories(provider.InstructionsPaths);

        public void LoadInstructionsFromDirectories(string dirPathsString)
        {
            // skip if options haven't changed
            if (dirPathsString == _loadedPaths)
                return;

            _loadedPaths = dirPathsString;
            InstructionList.Clear();

            var paths = dirPathsString.Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var path in paths)
            {
                LoadInstructionsFromDirectory(path);
            }

            InstructionUpdated?.Invoke(InstructionList.Keys.ToList());
        }

        public bool TryGetInstructions(string text, AsmType asmType, out IEnumerable<NavigationToken> instructions)
        {
            if (InstructionList.TryGetValue(text, out var navigationTokens))
            {
                if (asmType == AsmType.RadAsmDoc)
                    instructions = navigationTokens.Select(p => p.Key);
                else if (asmType == AsmType.RadAsm2)
                    instructions = navigationTokens.Where(p => p.Value == AsmType.RadAsm2).Select(p => p.Key);
                else
                    instructions = navigationTokens.Where(p => p.Value == AsmType.RadAsm).Select(p => p.Key);

                return true;
            }

            instructions = Enumerable.Empty<NavigationToken>();
            return false;
        }

        private void LoadInstructionsFromDirectory(string path)
        {
            try
            {
                foreach (var filepath in Directory.EnumerateFiles(path))
                {
                    if (Path.GetExtension(filepath) == Constants.FileExtensionAsm1Doc)
                    {
                        LoadInstructionsFromFile(filepath, AsmType.RadAsm);
                    }
                    else if (Path.GetExtension(filepath) == Constants.FileExtensionAsm2Doc)
                    {
                        LoadInstructionsFromFile(filepath, AsmType.RadAsm2);
                    }
                }
            }
            catch (Exception e)
            {
                Error.ShowError(e, "instrunction folder paths");
            }
        }

        private void LoadInstructionsFromFile(string path, AsmType asmType)
        {
            try
            {
                var documentAnalysis = _documentAnalysisProvoder.Value.GetOrCreateDocumentAnalysis(path);
                if (documentAnalysis.LastParserResult.Count > 0)
                {
                    var instructions = documentAnalysis
                        .LastParserResult[0]
                        .Tokens
                        .Where(t => t.Type == Core.Tokens.RadAsmTokenType.Instruction);

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
    }
}
