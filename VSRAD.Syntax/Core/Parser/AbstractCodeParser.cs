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
    internal abstract class AbstractCodeParser : IParser
    {
        protected HashSet<string> OtherInstructions { get; private set; }

        private readonly IDocumentFactory _documentFactory;
        private readonly AsmType _asmType;
        private HashSet<string> _instructions;
        private IReadOnlyList<string> _includes;
        protected DocumentManager _manager;

        protected AbstractCodeParser(IDocumentFactory documentFactory, IInstructionListManager instructionListManager,
            IReadOnlyList<string> includes, DocumentManager manager, AsmType asmType)
        {
            _asmType = asmType;
            _documentFactory = documentFactory;
            _instructions = new HashSet<string>();
            _includes = includes;
            _manager = manager;
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

        protected async Task AddExternalDefinitionsAsync(IDocument document, string path, TrackingToken includeStr, IBlock block)
        {
            try
            {
                var externalFileName = includeStr.GetText(block.Snapshot).Trim('"');
                var externalFilePath = Path.Combine(Path.GetDirectoryName(path), externalFileName);
                var externalDocument = _documentFactory.GetOrCreateDocument(externalFilePath);

                if (externalDocument == null)
                {
                    foreach(var curPath in _includes)
                    {
                        externalFilePath = Path.Combine(curPath, externalFileName);
                        externalDocument = _documentFactory.GetOrCreateDocument(externalFilePath);
                        if (externalDocument != null) break;
                    }
                }

                if (externalDocument != null)
                {
                    var externalDocumentAnalysis = externalDocument.DocumentAnalysis;
                    var externalAnalysisResult = await externalDocumentAnalysis
                        .GetAnalysisResultAsync(externalDocument.CurrentSnapshot)
                        .ConfigureAwait(false);
                    _manager.AddChild(document, externalDocument);

                    //var pContainer = _manager.GetContainerForDoc(document);
                    //foreach (var externalDefinition in externalAnalysisResult.GetGlobalDefinitions())
                    //    pContainer.Add(block, externalDefinition);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is FileNotFoundException) { /* invalid path */ }
        }

        protected bool TryAddReference(IDocument doc, string tokenText, TrackingToken token, IBlock block,
            ITextSnapshot version, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var node = _manager.GetNodeForDoc(doc);
            var definitionToken = SearchForToken(node, tokenText);

            if (definitionToken != null)
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

        private DefinitionToken SearchForToken(DocumentNode node, string tokenText)
        {
            DefinitionToken token;
            if (node.DefinitionContainer.TryGetDefinition(tokenText, out token))
                return token; // look for token in current node
            foreach (var child in node.Children) // going deeper in recursion
            {
                token = SearchForToken(child, tokenText);
                if (token != null) break; // found token in child node
            }
            return token; // can still be null, in case non of children contains token
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
    }
}
