using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VSRAD.Syntax.IntelliSense.SignatureHelper
{
    internal class SignatureHelpSource : ISignatureHelpSource
    {
        private readonly ITextBuffer _textBuffer;

        public SignatureHelpSource(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures)
        {
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(_textBuffer);


        }

        public ISignature GetBestMatch(ISignatureHelpSession session) => null;

        public void Dispose() { }
    }
}
