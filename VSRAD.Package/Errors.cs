using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package
{
    public delegate void MessageBoxFactory(string message, string title, OLEMSGICON icon);

    public static class Errors
    {
        public static MessageBoxFactory CreateMessageBox = DefaultMessageBoxFactory;

        public static void ShowCritical(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_CRITICAL);

        public static void ShowWarning(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_WARNING);

        private static void DefaultMessageBoxFactory(string message, string title, OLEMSGICON icon)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, message, title, icon,
                OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static void RunAsyncWithErrorHandling(this JoinableTaskFactory taskFactory, Func<Task> method, Action exceptionCallbackOnMainThread = null) =>
            taskFactory.RunAsync(async () =>
            {
                try
                {
                    await method();
                }
                catch (Exception e)
                {
                    await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                    exceptionCallbackOnMainThread?.Invoke();

                    // Cancelled operations are usually triggered by the user or are accompanied by a more descriptive message.
                    if (e is OperationCanceledException) return;

#if DEBUG
                    ShowCritical($"Message: {e.Message}\n Additional info: {e.StackTrace}");
#else
                    ShowCritical(e.Message);
#endif
                }
            });
    }
}
