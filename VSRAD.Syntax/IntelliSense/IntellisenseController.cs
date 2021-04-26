using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.SignatureHelp;

namespace VSRAD.Syntax.IntelliSense
{
    internal class IntellisenseController : IOleCommandTarget
    {
        private readonly ITextView _textView;
        private readonly INavigationTokenService _navigationService;
        private readonly IPeekBroker _peekBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly SignatureConfig _signatureConfig;
        private ISignatureHelpSession _currentSignatureSession;

        public IOleCommandTarget Next { get; set; }

        public IntellisenseController(IPeekBroker peekBroker, ISignatureHelpBroker signatureHelpBroker, INavigationTokenService navigationService, ITextView textView)
        {
            _peekBroker = peekBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _textView = textView;
            _navigationService = navigationService;

            var asmType = _textView.TextSnapshot.GetAsmType();
            _signatureConfig = SignatureConfig.GetSignature(asmType);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd97CmdID.GotoDefn:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;
                    }
                }
            }
            else if (pguidCmdGroup == VSConstants.VsStd12)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd12CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd12CmdID.PeekDefinition:
                            var canPeek = _peekBroker.CanTriggerPeekSession(
                                _textView,
                                PredefinedPeekRelationships.Definitions.Name,
                                filename => false
                            );
                            prgCmds[i].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED;
                            prgCmds[0].cmdf |= (uint)(canPeek == true ? OLECMDF.OLECMDF_ENABLED : OLECMDF.OLECMDF_INVISIBLE);
                            return VSConstants.S_OK;
                    }
                }
            }

            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.GotoDefn:
                        if (TryGoToDefinition()) return VSConstants.S_OK;
                        break;
                }
            }
            else if (pguidCmdGroup == VSConstants.VsStd12)
            {
                switch ((VSConstants.VSStd12CmdID)nCmdID)
                {
                    case VSConstants.VSStd12CmdID.PeekDefinition:
                        if (!_textView.Roles.Contains(PredefinedTextViewRoles.EmbeddedPeekTextView) &&
                            !_textView.Roles.Contains(PredefinedTextViewRoles.CodeDefinitionView))
                        {
                            _peekBroker.TriggerPeekSession(_textView, PredefinedPeekRelationships.Definitions.Name);
                            return VSConstants.S_OK;
                        }
                        break;
                }
            }

            var res = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

            if (pguidCmdGroup == VSConstants.VSStd2K && _signatureConfig != null && _signatureConfig.Enabled)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        var ch = GetTypeChar(pvaIn);
                        if (ch == _signatureConfig.TriggerInstructionSignatureChar 
                            || ch == _signatureConfig.TriggerFunctionSignatureChar) StartSignatureSession();
                        else if (ch == _signatureConfig.DismissSignatureChar) CancelSignatureSession();
                        else if (ch == _signatureConfig.TriggerParameterChar) ChangeParameterSignatureSession();
                        break;
                    case VSConstants.VSStd2KCmdID.BACKSPACE:
                    case VSConstants.VSStd2KCmdID.DELETE:
                    case VSConstants.VSStd2KCmdID.DELETETOEOL:
                    case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                    case VSConstants.VSStd2KCmdID.LEFT:
                    case VSConstants.VSStd2KCmdID.RIGHT:
                        ChangeParameterSignatureSession();
                        break;
                    case VSConstants.VSStd2KCmdID.DELETELINE:
                    case VSConstants.VSStd2KCmdID.DELETETOBOL:
                    case VSConstants.VSStd2KCmdID.RETURN:
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        CancelSignatureSession();
                        break;
                }
            }

            if (_signatureConfig != null && !_signatureConfig.Enabled)
                CancelSignatureSession();

            return res;
        }

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

        private static char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        private bool TryGoToDefinition()
        {
            var point = _textView.Caret.Position.BufferPosition;

            var navigationServiceResult = ThreadHelper.JoinableTaskFactory.Run(() => _navigationService.GetNavigationsAsync(point));
            if (navigationServiceResult == null || navigationServiceResult.Values.Count == 0) return false;

            _navigationService.NavigateOrOpenNavigationList(navigationServiceResult.Values);
            return true;
        }
    }
}
