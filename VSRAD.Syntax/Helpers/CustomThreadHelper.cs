using System;
using System.Threading;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Syntax.Helpers
{
    internal static class CustomThreadHelper
    {
        public static T RunOnMainThread<T>(Func<T> func) =>
            RunOnMainThread(func, CancellationToken.None);

        public static T RunOnMainThread<T>(Func<T> func, CancellationToken ct) =>
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                ThreadHelper.ThrowIfNotOnUIThread();
                return func();
            });
    }
}
