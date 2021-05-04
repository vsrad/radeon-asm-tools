using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.IntelliSense.SignatureHelp;

namespace VSRAD.Syntax.IntelliSense
{
    internal partial class IntellisenseController
    {
        private void StartSignatureSession()
        {
            if (_currentSignatureSession != null)
                return;

            if (_signatureHelpBroker.IsSignatureHelpActive(_textView))
            {
                _currentSignatureSession = _signatureHelpBroker.GetSessions(_textView)[0];
            }
            else
            {
                var point = _textView.Caret.Position.BufferPosition;
                var snapshot = point.Snapshot;
                var trackingPoint = snapshot.CreateTrackingPoint(point, PointTrackingMode.Positive);

                _currentSignatureSession = _signatureHelpBroker.CreateSignatureHelpSession(_textView, trackingPoint, true);
                _textView.TextBuffer.Properties.AddProperty(typeof(ISignatureHelpSession), _currentSignatureSession);
            }

            _currentSignatureSession.Dismissed += SignatureSessionDismissed;
            _currentSignatureSession.Start();
        }

        private void ChangeParameterSignatureSession()
        {
            if (_currentSignatureSession == null) return;
            if (_currentSignatureSession.Signatures.Count == 0) return;

            // all signatures have the same applicable span
            var trackingSpan = _currentSignatureSession.Signatures[0].ApplicableToSpan;
            var currentPosition = _textView.Caret.Position.BufferPosition;
            var trackingStart = trackingSpan.GetStartPoint(currentPosition.Snapshot);

            // check left border of applicable span, it might be invalid token
            if (trackingStart == currentPosition)
            {
                _currentSignatureSession.Recalculate();
                // if there is no applicable token, then current signatureSession will be null
                if (_currentSignatureSession == null) return;
            }

            var searchParam = new SnapshotSpan(trackingStart, currentPosition);
            var currentParam = searchParam.GetCurrentParameter(_signatureConfig.TriggerParameterChar);

            foreach (var signature in _currentSignatureSession.Signatures)
            {
                if (signature is ISyntaxSignature syntaxSignature)
                    syntaxSignature.SetCurrentParameter(currentParam);
            }
        }

        private void SignatureSessionDismissed(object sender, EventArgs e)
        {
            _textView.TextBuffer.Properties.RemoveProperty(typeof(ISignatureHelpSession));
            _currentSignatureSession = null;
        }

        private void CancelSignatureSession() =>
            _currentSignatureSession?.Dismiss();
    }
}
