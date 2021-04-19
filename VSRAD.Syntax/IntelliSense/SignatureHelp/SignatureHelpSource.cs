using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class SignatureHelpSource : ISignatureHelpSource
    {
        private readonly IDocumentAnalysis _documentAnalysis;
        private readonly IInstructionListManager _instructionListManager;
        private readonly ITextBuffer _textBuffer;
        private readonly SignatureConfig _signatureConfig;

        public SignatureHelpSource(IDocumentAnalysis documentAnalysis, IInstructionListManager instructionListManager, 
            ITextBuffer textBuffer, SignatureConfig signatureConfig)
        {
            _documentAnalysis = documentAnalysis;
            _instructionListManager = instructionListManager;
            _textBuffer = textBuffer;
            _signatureConfig = signatureConfig;
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures)
        {
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);
            if (!triggerPoint.HasValue) return;

            var triggerLine = triggerPoint.Value.GetContainingLine();
            var searchSpan = new SnapshotSpan(triggerLine.Start, triggerPoint.Value);

            var analysisResult = ThreadHelper.JoinableTaskFactory.Run(
                () => _documentAnalysis.GetAnalysisResultAsync(snapshot));

            var triggerBlock = analysisResult.GetBlock(searchSpan.Start);
            var triggerToken = triggerBlock.Tokens
                .Where(t => ContainsInclusive(searchSpan, t.Span))
                .FirstOrDefault(t => t.Type == RadAsmTokenType.FunctionReference || t.Type == RadAsmTokenType.Instruction);

            if (triggerToken == null) return;

            var applicableSpan = new SnapshotSpan(triggerToken.Span.End, triggerLine.End);
            var trackingSpan = snapshot.CreateTrackingSpan(applicableSpan, SpanTrackingMode.EdgeInclusive);
            var parameterIdx = applicableSpan.GetCurrentParameter(triggerPoint.Value, _signatureConfig.TriggerParameterChar);

            switch (triggerToken.Type)
            {
                case RadAsmTokenType.FunctionReference:
                    AddFunctionSignature(trackingSpan, triggerToken, parameterIdx, signatures); 
                    break;
                case RadAsmTokenType.Instruction:
                    AddInstructionSignature(snapshot, trackingSpan, triggerToken, parameterIdx, signatures);
                    break;
            }
        }

        private void AddFunctionSignature(ITrackingSpan trackingSpan, IAnalysisToken token, int parameterIdx, ICollection<ISignature> signatures)
        {
            var functionToken = (IFunctionToken)((ReferenceToken)token).Definition;
            var functionSign = new FunctionSignature(trackingSpan, functionToken.FunctionBlock, _signatureConfig, parameterIdx);
            signatures.Add(functionSign);
        }

        private void AddInstructionSignature(ITextSnapshot snapshot, ITrackingSpan trackingSpan, IAnalysisToken token, int parameterIdx, ICollection<ISignature> signatures)
        {
            var asmType = snapshot.GetAsmType();
            var tokenText = token.GetText();

            foreach (var instruction in _instructionListManager.GetInstructionsByName(asmType, tokenText))
            {
                if (instruction.Definition is IInstructionToken instructionToken)
                {
                    var instructionSign = new InstructionSignature(trackingSpan, instructionToken, parameterIdx);
                    signatures.Add(instructionSign);
                }
            }
        }

        public ISignature GetBestMatch(ISignatureHelpSession session) => null;

        public void Dispose() { }

        private static bool ContainsInclusive(Span first, Span second) =>
            first.Start <= second.Start && first.End >= second.End;
    }
}
