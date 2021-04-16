using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class SignatureHelpSource : ISignatureHelpSource
    {
        private readonly IDocumentAnalysis _documentAnalysis;
        private readonly ITextBuffer _textBuffer;
        private readonly SignatureConfig _signatureConfig;

        public SignatureHelpSource(IDocumentAnalysis documentAnalysis, ITextBuffer textBuffer, SignatureConfig signatureConfig)
        {
            _documentAnalysis = documentAnalysis;
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
            var triggerToken = (ReferenceToken)triggerBlock.Tokens
                .Where(t => ContainsInclusive(searchSpan, t.Span))
                .FirstOrDefault(t => t.Type == RadAsmTokenType.FunctionReference);

            if (triggerToken == null) return;

            var applicableSpan = new SnapshotSpan(triggerToken.Span.End, triggerLine.End);
            var trackingSpan = snapshot.CreateTrackingSpan(applicableSpan, SpanTrackingMode.EdgeInclusive);
            var functionToken = (IFunctionToken) triggerToken.Definition;

            var parameterIdx = applicableSpan.GetCurrentParameter(_signatureConfig.TriggerParameterChar);
            var functionSign = new FunctionSignature(trackingSpan, functionToken.FunctionBlock, _signatureConfig, parameterIdx);

            signatures.Add(functionSign);
        }

        public ISignature GetBestMatch(ISignatureHelpSession session) => null;

        public void Dispose() { }

        private static bool ContainsInclusive(Span first, Span second) =>
            first.Start <= second.Start && first.End >= second.End;
    }
}
