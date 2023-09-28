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
        protected HashSet<string> OtherInstructions { get; private set; }

        private readonly IDocumentFactory _documentFactory;
        private readonly IBuiltinInfoProvider _builtinInfoProvider;
        private readonly AsmType _asmType;
        private HashSet<string> _instructions;

        protected AbstractCodeParser(IDocumentFactory documentFactory, IBuiltinInfoProvider builtinInfoProvider, IInstructionListManager instructionListManager, AsmType asmType)
        {
            _asmType = asmType;
            _documentFactory = documentFactory;
            _builtinInfoProvider = builtinInfoProvider;
            _instructions = new HashSet<string>();
            OtherInstructions = new HashSet<string>();

            instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionListManager, _asmType);
        }

        public abstract Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);

        private void InstructionsUpdated(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & _asmType) == _asmType)
            {
                var instructions = sender.GetInstructions(_asmType);
                var selectedSetInstructions = sender.GetSelectedSetInstructions(_asmType);

                _instructions = selectedSetInstructions
                    .Select(i => i.Text)
                    .Distinct()
                    .ToHashSet();

                OtherInstructions = instructions
                    .Select(i => i.Text)
                    .Distinct()
                    .ToHashSet();

                OtherInstructions.ExceptWith(_instructions);
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
            if (_instructions.Contains(tokenText))
            {
                block.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
                return true;
            }

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
