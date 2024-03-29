﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace VSRAD.Syntax.Helpers
{
    public sealed class Error
    {
        public static void LogError(Exception e, string module = null) =>
            LogError(e.Message, module);

        public static void LogError(string message, string module = null)
        {
            var source = string.IsNullOrWhiteSpace(module) ? Constants.RadeonAsmSyntaxContentType : $"{module} - {Constants.RadeonAsmSyntaxContentType}";
#if DEBUG
            //ShowErrorMessage(message, source);
#else
            ActivityLog.LogError(source, message);
#endif
        }

        public static void ShowWarning(Exception e, string module = "") =>
            ShowWarningMessage(e.Message, module);

        public static void ShowError(Exception e, string module = "") =>
            ShowErrorMessage(e.Message, module);

        public static void ShowErrorMessage(string msg, string module = "") =>
            ShowMessage(OLEMSGICON.OLEMSGICON_CRITICAL, msg, module);

        public static void ShowWarningMessage(string msg, string module = "") =>
            ShowMessage(OLEMSGICON.OLEMSGICON_WARNING, msg, module);

        public static void ShowMessage(OLEMSGICON type, string msg, string module) =>
            CreateMessageBox(msg, $"Radeon syntax {module}", type);

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
