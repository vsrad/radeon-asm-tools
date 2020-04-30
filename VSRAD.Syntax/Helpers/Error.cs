using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace VSRAD.Syntax.Helpers
{
    public sealed class Error
    {
        public static void LogError(Exception e, string module = "")
        {
            var source = string.IsNullOrEmpty(module) ? Constants.RadeonAsmSyntaxContentType : $"{module} - {Constants.RadeonAsmSyntaxContentType}";
#if DEBUG
            ActivityLog.LogError(source, e.ToString());
#else
            ActivityLog.LogError(source, e.Message);
#endif
        }

        public static void ShowWarning(Exception e, string module = "") =>
            CreateMessageBox(e.Message, $"Radeon syntax {module}", OLEMSGICON.OLEMSGICON_WARNING);

        public static void ShowError(Exception e, string module = "") =>
            CreateMessageBox(e.Message, $"Radeon syntax {module}", OLEMSGICON.OLEMSGICON_CRITICAL);

        private static void CreateMessageBox(string message, string title, OLEMSGICON icon)
        {
            if (ThreadHelper.CheckAccess())
            {
#pragma warning disable VSTHRD010 // CheckAccess() ensures that we're on the UI thread
                var provider = ServiceProvider.GlobalProvider;
#pragma warning restore VSTHRD010
                VsShellUtilities.ShowMessageBox(provider, message, title, icon,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else
            {
#pragma warning disable VSTHRD001 // Cannot use SwitchToMainThreadAsync in a synchronous context
                ThreadHelper.Generic.BeginInvoke(() => CreateMessageBox(message, title, icon));
#pragma warning restore VSTHRD001
            }
        }
    }
}
