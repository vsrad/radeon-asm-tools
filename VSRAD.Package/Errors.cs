using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package
{
    public readonly struct Error : IEquatable<Error>
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

        public bool Equals(Error e) => Critical == e.Critical && Message == e.Message && Title == e.Title;
        public override bool Equals(object o) => o is Error e && Equals(e);
        public override int GetHashCode() => (Critical, Message, Title).GetHashCode();
        public static bool operator ==(Error left, Error right) => left.Equals(right);
        public static bool operator !=(Error left, Error right) => !(left == right);
    }

    public static class Errors
    {
        public delegate void CreateMessageBoxImpl(string message, string title, OLEMSGICON icon);

        private static CreateMessageBoxImpl _createMessageBox;
        public static CreateMessageBoxImpl CreateMessageBox
        {
            get => _createMessageBox ?? VsCreateMessageBox;
            set => _createMessageBox = value;
        }

        public static void Show(Error error) =>
            CreateMessageBox(error.Message, error.Title, error.Critical ? OLEMSGICON.OLEMSGICON_CRITICAL : OLEMSGICON.OLEMSGICON_WARNING);

        public static void ShowCritical(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_CRITICAL);

        public static void ShowWarning(string message, string title = "RAD Debugger") =>
            CreateMessageBox(message, title, OLEMSGICON.OLEMSGICON_WARNING);

        public static void ShowProfileUninitializedError() =>
            CreateMessageBox("RAD Debug has not been configured for this project yet.\nOpen Tools->RAD Debug->Options and create a profile.", "RAD Debugger", OLEMSGICON.OLEMSGICON_CRITICAL);

        public static void ShowException(Exception e)
        {
            // Cancelled operations are usually triggered by the user or are accompanied by a more descriptive message.
            if (e is OperationCanceledException) return;

#if DEBUG
            ShowCritical(e.Message + "\r\n\r\n" + e.StackTrace);
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
                    try
                    {
                        exceptionCallbackOnMainThread?.Invoke();
                        ShowException(e);
                    }
                    catch (Exception callbackExc)
                    {
                        ShowCritical(e.Message + "\r\n\r\nAn exception has occurred during the handling of this error. Exception message and stack trace are provided below:\r\n\r\n" + callbackExc.Message + "\r\n\r\n" + callbackExc.StackTrace);
                    }
                }
            });

        private static void VsCreateMessageBox(string message, string title, OLEMSGICON icon)
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
