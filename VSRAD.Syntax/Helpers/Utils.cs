using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;

namespace VSRAD.Syntax.Helpers
{
    public static class Utils
    {
        public static IVsTextBuffer GetWindowVisualBuffer(Window window, IServiceProvider serviceProvider)
        {
            if (window == null || window.Document == null || !window.Kind.Equals("Document", StringComparison.OrdinalIgnoreCase)) return null;

            var fullPath = Path.Combine(window.Document.Path, window.Document.Name);
            if (VsShellUtilities.IsDocumentOpen(serviceProvider, fullPath, Guid.Empty, out _, out _, out var windowFrame))
            {
                var textView = VsShellUtilities.GetTextView(windowFrame);
                if (textView.GetBuffer(out var vsTextBuffer) == VSConstants.S_OK) return vsTextBuffer;
            }

            return null;
        }
    }
}
