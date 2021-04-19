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
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Core.Parser
{
    internal abstract class AbstractCodeParser : IParser
    {
        private readonly IDocumentFactory _documentFactory;
        protected readonly DefinitionContainer _definitionContainer;
        protected readonly LinkedList<(string text, TrackingToken trackingToken, IBlock block)> _referenceCandidates;

        protected AbstractCodeParser(IDocumentFactory documentFactory)
        {
            _documentFactory = documentFactory;
            _definitionContainer = new DefinitionContainer();
            _referenceCandidates = new LinkedList<(string text, TrackingToken trackingToken, IBlock block)>();
        }

        public abstract Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);

        protected async Task AddExternalDefinitionsAsync(string path, TrackingToken includeStr, ITextSnapshot snapshot, IBlock block)
        {
            try
            {
                var externalFileName = includeStr.GetText(snapshot).Trim('"');
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

        protected bool TryAddInstruction(string tokenText, TrackingToken token, IBlock block, ITextSnapshot version, HashSet<string> instructions)
        {
            if (!instructions.Contains(tokenText)) return false;

            block.AddToken(new AnalysisToken(RadAsmTokenType.Instruction, token, version));
            return true;
        }

        protected static void UpdateInstructions(IInstructionListManager sender, AsmType asmType, ref HashSet<string> instructionSet, ref HashSet<string> other)
        {
            var instructions = sender.GetInstructions(asmType);
            var selectedSetInstructions = sender.GetSelectedSetInstructions(asmType);

            instructionSet = selectedSetInstructions
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            other = instructions
                .Select(i => i.Text)
                .Distinct()
                .ToHashSet();

            other.ExceptWith(instructionSet);
        }
    }
}
