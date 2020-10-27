using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Blocks;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractCodeParser : AbstractParser
    {
        abstract protected AsmType AsmType { get; }
        protected HashSet<string> Instructions { get; private set; }
        protected HashSet<string> OtherInstructions { get; private set; }

        private readonly IDocumentFactory _documentFactory;
        protected readonly DefinitionContainer _definitionContainer;

        public AbstractCodeParser(IDocumentFactory documentFactory, IInstructionListManager instructionListManager)
        {
            _documentFactory = documentFactory;
            _definitionContainer = new DefinitionContainer();
            Instructions = new HashSet<string>();
            OtherInstructions = new HashSet<string>();

            instructionListManager.InstructionsUpdated += InstructionsUpdated;
            InstructionsUpdated(instructionListManager, AsmType);
        }

        private void InstructionsUpdated(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & AsmType) == AsmType)
            {
                var instructions = sender.GetInstructions(AsmType);
                var selectedSetInstructions = sender.GetSelectedSetInstructions(AsmType);

                Instructions = selectedSetInstructions
                    .Select(i => i.Text)
                    .Distinct()
                    .ToHashSet();

                OtherInstructions = instructions
                    .Select(i => i.Text)
                    .Distinct()
                    .ToHashSet();

                OtherInstructions.ExceptWith(Instructions);
            }
        }

        protected async Task AddExternalDefinitionsAsync(string path, TrackingToken includeStr, IBlock block)
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
                        _definitionContainer.Add(block, externalDefinition);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException) { /* invalid path */ }
        }

        protected bool TryAddReference(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_definitionContainer.TryGetDefinition(tokenText, out var definitionToken))
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

                block.AddToken(new ReferenceToken(referenceType, token, version, definitionToken));
                return true;
            }

            return false;
        }
    }
}
