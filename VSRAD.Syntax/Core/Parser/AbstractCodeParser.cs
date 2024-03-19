using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractCodeParser : IParser
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly IBuiltinInfoProvider _builtinInfoProvider;
        private readonly AsmType _asmType;
        private IInstructionSet _selectedInstructionSet;
        private IInstructionSet _unionInstructionSet;
        private IReadOnlyList<IInstructionSet> _allInstructionSets;

        protected AbstractCodeParser(IDocumentFactory documentFactory, IBuiltinInfoProvider builtinInfoProvider, IInstructionListManager instructionListManager, AsmType asmType)
        {
            _asmType = asmType;
            _documentFactory = documentFactory;
            _builtinInfoProvider = builtinInfoProvider;

            instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionListManager, _asmType);
        }

        public abstract Task<ParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);

        private void InstructionsUpdated(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & _asmType) == _asmType)
            {
                _selectedInstructionSet = sender.GetSelectedInstructionSet(_asmType);
                _unionInstructionSet = sender.GetInstructionSetsUnion(_asmType);
                _allInstructionSets = sender.GetAllInstructionSets(_asmType);
            }
        }

        protected async Task AddExternalDefinitionsAsync(string path, TrackingToken includeStr, IBlock block, DefinitionContainer definitionContainer)
        {
            try
            {
                var externalFileName = includeStr.GetText(block.Snapshot).Trim('"');
                var externalFilePath = Path.Combine(Path.GetDirectoryName(path), externalFileName);
                var externalDocument = _documentFactory.GetOrCreateDocument(externalFilePath);

                if (externalDocument != null)
                {
                    var externalDocumentAnalysis = externalDocument.DocumentAnalysis;
                    var externalAnalysisResult = await externalDocumentAnalysis
                        .GetAnalysisResultAsync(externalDocument.CurrentSnapshot)
                        .ConfigureAwait(false);

                    foreach (var externalDefinition in externalAnalysisResult.GetGlobalDefinitions())
                        definitionContainer.Add(block, externalDefinition);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException) { /* invalid path */ }
        }

        protected bool TryAddReference(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version, DefinitionContainer definitionContainer, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (definitionContainer.TryGetDefinition(tokenText, out var definitionToken))
            {
                RadAsmTokenType referenceType;
                switch (definitionToken.Type)
                {
                    case RadAsmTokenType.PreprocessorMacro:
                        referenceType = RadAsmTokenType.PreprocessorMacroReference;
                        break;
                    case RadAsmTokenType.FunctionName:
                        referenceType = RadAsmTokenType.FunctionReference;
                        break;
                    case RadAsmTokenType.FunctionParameter:
                        referenceType = RadAsmTokenType.FunctionParameterReference;
                        break;
                    case RadAsmTokenType.Label:
                        referenceType = RadAsmTokenType.LabelReference;
                        break;
                    case RadAsmTokenType.GlobalVariable:
                        referenceType = RadAsmTokenType.GlobalVariableReference;
                        break;
                    case RadAsmTokenType.LocalVariable:
                        referenceType = RadAsmTokenType.LocalVariableReference;
                        break;
                    default: return true;
                }

                var referenceToken = new ReferenceToken(referenceType, token, version, definitionToken);

                // definition contains only references from the same document
                if (referenceToken.Snapshot == definitionToken.Snapshot)
                    definitionToken.AddReference(referenceToken);

                block.AddToken(referenceToken);
                return true;
            }

            return false;
        }

        protected bool TryAddInstruction(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version)
        {
            if (_selectedInstructionSet.Instructions.ContainsKey(tokenText))
            {
                block.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
                return true;
            }

            return false;
        }

        protected bool CheckInstructionDefinedInOtherSetsError(string instruction, out string error)
        {
            if (!_selectedInstructionSet.Instructions.ContainsKey(instruction) && _unionInstructionSet.Instructions.ContainsKey(instruction))
            {
                var definedIn = _allInstructionSets.Where(s => s.Instructions.ContainsKey(instruction)).Select(s => $"'{s.SetName}'");
                error = $"Instruction '{instruction}' is not defined in the current instruction set. It is defined in: {string.Join(", ", definedIn)}.";
                return true;
            }
            error = "";
            return false;
        }

        protected bool TryAddBuiltinReference(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version)
        {
            if (_builtinInfoProvider.TryGetBuilinInfo(_asmType, tokenText, out _))
            {
                block.AddToken(new AnalysisToken(RadAsmTokenType.BuiltinFunction, token, version));
                return true;
            }

            return false;
        }
    }
}
