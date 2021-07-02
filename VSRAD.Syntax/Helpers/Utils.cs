using System.IO;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft;
using Microsoft.VisualStudio.Shell.Interop;

namespace VSRAD.Syntax.Helpers
{
    public static class Utils
    {
        public static IVsTextBuffer GetBufferAdapter(string path)
        {
            if (!IsDocumentOpen(path, out var frame))
                return null;

            var vsTextView = VsShellUtilities.GetTextView(frame);
            if (vsTextView == null)
                return null;

            if (vsTextView.GetBuffer(out var vsTextBuffer) != VSConstants.S_OK)
                return null;

            return vsTextBuffer;
        }

        public static void OpenHiddenView(string path)
        {
            Assumes.NotNullOrEmpty(path);

            var service = ServiceProvider.GlobalProvider;
            var logview = VSConstants.LOGVIEWID_Primary;

            // document is already open in the window
            if (IsDocumentOpen(path, out var frame))
                return;

            VsShellUtilities.OpenDocument(service, path, logview, out _, out _, out frame);
            frame.Hide();
        }

        public static string GetDteDocumentPath(Document document)
        {
            Assumes.NotNull(document);
            return Path.Combine(document.Path, document.Name);
        }

        private static bool IsDocumentOpen(string path, out IVsWindowFrame frame)
        {
            var service = ServiceProvider.GlobalProvider;
            var logview = VSConstants.LOGVIEWID_Primary;

            return VsShellUtilities.IsDocumentOpen(service, path, logview, out _, out _, out frame);
        }
    }
}
