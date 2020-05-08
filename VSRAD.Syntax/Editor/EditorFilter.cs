using VSRAD.Syntax.Helpers;
using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.Editor
{
    internal sealed class EditorFilter : IOleCommandTarget
    {
        private readonly ITextView _textView;

        public IOleCommandTarget Next { get; set; }

        public EditorFilter(ITextView textView)
        {
            _textView = textView;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
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
            if (pguidCmdGroup == typeof(VSConstants.VSStd2KCmdID).GUID)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.COMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.COMMENTBLOCK:
                        if (_textView.CommentUncommentBlock(comment: true))
                            return VSConstants.S_OK;

                        break;
                    case VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK:
                    case VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK:
                        if (_textView.CommentUncommentBlock(comment: false))
                            return VSConstants.S_OK;

                        break;

                }
            }

            return Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }
    }
}
