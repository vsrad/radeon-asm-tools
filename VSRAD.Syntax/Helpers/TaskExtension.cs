using System;
using System.Threading.Tasks;

namespace VSRAD.Syntax.Helpers
{
    internal static class TaskExtension
    {
        public static void RunAsyncWithoutAwait(this Task task, Action<Exception> exceptionHandler)
        {
            task.ContinueWith(t => exceptionHandler(t.Exception), 
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void RunAsyncWithoutAwait(this Task task)
            => RunAsyncWithoutAwait(task, e => Error.LogError(e));
    }
}
