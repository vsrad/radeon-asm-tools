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
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractCodeParser : IAsmParser
    {
        protected AsmType AsmType { get; set; }
        protected HashSet<string> OtherInstructions { get; private set; }

        private readonly IDocumentFactory _documentFactory;
        private HashSet<string> _instructions;
        protected readonly DefinitionContainer _definitionContainer;
        protected readonly LinkedList<(string text, TrackingToken trackingToken, IBlock block)> _referenceCandidates;

        protected AbstractCodeParser(IDocumentFactory documentFactory, IInstructionListManager instructionListManager)
        {
            _documentFactory = documentFactory;
            _definitionContainer = new DefinitionContainer();
            _referenceCandidates = new LinkedList<(string text, TrackingToken trackingToken, IBlock block)>();
            _instructions = new HashSet<string>();
            OtherInstructions = new HashSet<string>();
            UpdateInstructions(instructionListManager, AsmType);
        }

        public abstract Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);

        public void UpdateInstructions(IInstructionListManager sender, AsmType asmType)
        {
            if ((asmType & AsmType) != AsmType) return;

            var instructions = sender.GetInstructions(AsmType);
            var selectedSetInstructions = sender.GetSelectedSetInstructions(AsmType);

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
            if (!_definitionContainer.TryGetDefinition(tokenText, out var definitionToken)) 
                return false;

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

        protected bool TryAddInstruction(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version)
        {
            if (!_instructions.Contains(tokenText)) return false;

            block.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
            return true;
        }
    }
}
