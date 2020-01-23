using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package
{
    public readonly struct Error
    {
        public bool Critical { get; }
        public string Message { get; }
        public string Title { get; }

        public Error(string message, bool critical = false, string title = "RAD Debugger")
        {
            Critical = critical;
            Message = message;
            Title = title;
        }
    }

    public static class Errors
    {
        public static void Show(Error error) =>
            CreateMessageBox(error.Message, error.Title, error.Critical ? OLEMSGICON.OLEMSGICON_CRITICAL : OLEMSGICON.OLEMSGICON_WARNING);

        public static void ShowCritical(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_CRITICAL);

        public static void ShowWarning(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_WARNING);

        public static void ShowException(Exception e)
        {
            // Cancelled operations are usually triggered by the user or are accompanied by a more descriptive message.
            if (e is OperationCanceledException) return;

#if DEBUG
            ShowCritical($"Message: {e.Message}\n Additional info: {e.StackTrace}");
#else
            ShowCritical(e.Message);
#endif
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
                    ShowException(e);
                }
            });

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
