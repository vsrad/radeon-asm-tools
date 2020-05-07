using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Peek.DefinitionService;
using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using VSRAD.Syntax.IntelliSense.Completion;

namespace VSRAD.Syntax.Editor
{
    internal sealed class EditorFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _wpfTextView;
        private readonly DefinitionService _definitionService;

        public IOleCommandTarget Next { get; set; }

        public EditorFilter(DefinitionService definitionService, IWpfTextView wpfTextView)
        {
            this._wpfTextView = wpfTextView;
            this._definitionService = definitionService;
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
            else if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                for (int i = 0; i < cCmds; i++)
                {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                        case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                        case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                        case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
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
                            var canPeek = _definitionService.PeekBroker?.CanTriggerPeekSession(
                                _wpfTextView,
                                PredefinedPeekRelationships.Definitions.Name,
                                isStandaloneFilePredicate: (string filename) => false
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

            try
            {
                return ExecuteCommand(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            catch (Exception)
            {
                return VSConstants.E_FAIL;
            }
        }

        private int ExecuteCommand(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                switch ((VSConstants.VSStd97CmdID)nCmdID)
                {
                    case VSConstants.VSStd97CmdID.GotoDefn:
                        _definitionService.GoToDefinition(_wpfTextView);
                        return VSConstants.S_OK;
                }
            }
            else if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                        if (_wpfTextView.CommentUncommentBlock(comment: true))
                            return VSConstants.S_OK;

                        break;
                    case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
                        if (_wpfTextView.CommentUncommentBlock(comment: false))
                            return VSConstants.S_OK;

                        break;

                }
            }
            else if (pguidCmdGroup == VSConstants.VsStd12)
            {
                switch ((VSConstants.VSStd12CmdID)nCmdID)
                {
                    case VSConstants.VSStd12CmdID.PeekDefinition:
                        if (_definitionService.PeekBroker != null &&
                            !_wpfTextView.Roles.Contains(PredefinedTextViewRoles.EmbeddedPeekTextView) &&
                            !_wpfTextView.Roles.Contains(PredefinedTextViewRoles.CodeDefinitionView))
                        {
                            _definitionService.PeekBroker.TriggerPeekSession(_wpfTextView, PredefinedPeekRelationships.Definitions.Name);
                            return VSConstants.S_OK;
                        }

                        break;
                }
            }

            return Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }
    }
}
