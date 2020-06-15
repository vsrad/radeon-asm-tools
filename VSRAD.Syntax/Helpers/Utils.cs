using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace VSRAD.Syntax.Helpers
{
    public static class Utils
    {
        public static bool TryOpenDocument(IServiceProvider serviceProvider, string path, out IVsTextBuffer buffer)
        {
            VsShellUtilities.OpenDocument(serviceProvider, path, Guid.Empty, out _, out _, out var windowFrame);
            var view = VsShellUtilities.GetTextView(windowFrame);

            if (view.GetBuffer(out var lines) == 0)
            {
                if (lines is IVsTextBuffer newBuffer)
                {
                    buffer = newBuffer;
                    return true;
                }
            }

            buffer = null;
            return false;
        }

        public static bool IsDocumentOpen(IServiceProvider serviceProvider, string path, out IVsTextBuffer buffer)
        {
            var rc = VsShellUtilities.IsDocumentOpen(serviceProvider, path, Guid.Empty, out _, out _, out var windowFrame);
            if (rc)
            {
                var view = VsShellUtilities.GetTextView(windowFrame);
                if (view.GetBuffer(out var lines) == 0)
                {
                    if (lines is IVsTextBuffer newBuffer)
                    {
                        buffer = newBuffer;
                        return true;
                    }
                }
            }

            buffer = null;
            return false;
        }
    }
}
