using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionLogger
    {
        Task<Error> LogActionRunAsync(string actionName, Result<ActionRunResult> actionRun);
    }

    [Export(typeof(IActionLogger))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ActionLogger : IActionLogger
    {
        private readonly IOutputWindowWriter _outputWriter;
        private readonly IErrorListManager _errorList;

        private DTE2 _dte;

        [ImportingConstructor]
        public ActionLogger(IProject project, SVsServiceProvider serviceProvider, IOutputWindowManager outputWindow, IErrorListManager errorList)
        {
            _outputWriter = outputWindow.GetExecutionResultPane();
            _errorList = errorList;

            project.RunWhenLoaded((options) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _dte = serviceProvider.GetService(typeof(SDTE)) as DTE2;
                Assumes.Present(_dte);
            });
        }

        public async Task<Error> LogActionRunAsync(string actionName, Result<ActionRunResult> actionRun)
        {
            await _outputWriter.ClearAsync();

            if (actionRun.TryGetResult(out var runResult, out var error) && runResult != null)
            {
                var title = runResult.ActionName + " action " + (runResult.Successful ? "SUCCEEDED" : "FAILED") + $" in {runResult.TotalMillis}ms";

                var log = new StringBuilder();
                var warnings = new StringBuilder();

                var actionSucceeded = LogAction(log, warnings, runResult);

                var logString = log.ToString();
                await _outputWriter.PrintMessageAsync(title, logString);

                var errorListOutput = runResult.GetStepOutputs();
                var errors = await _errorList.AddToErrorListAsync(errorListOutput);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (errors.ErrorCount != 0)
                    _dte.ToolWindows.ErrorList.Parent.Activate();
                else if (!actionSucceeded)
                    _dte.ToolWindows.OutputWindow.Parent.Activate();

                if (warnings.Length != 0)
                {
                    if (actionSucceeded)
                        return new Error(warnings.ToString(), critical: false, title: runResult.ActionName + " Warning");
                    else
                        return new Error(warnings.ToString(), critical: true, title: runResult.ActionName + " Error");
                }
                return default;
            }
            else
            {
                await _outputWriter.PrintMessageAsync(actionName + " action ABORTED");
                return error;
            }
        }

        private bool LogAction(StringBuilder log, StringBuilder warnings, ActionRunResult run, int depth = 0)
        {
            var logIndent = new string('=', 2 * depth);

            log.AppendFormat(CultureInfo.InvariantCulture, "{0}==> [OK {1}ms] Fetch initial timestamps\r\n", logIndent, run.InitTimestampFetchMillis);

            var prevStepsSucceeded = true;
            for (int i = 0; i < run.Steps.Count; ++i)
            {
                var step = run.Steps[i];
                if (prevStepsSucceeded || run.ContinueOnError)
                {
                    var result = run.StepResults[i];
                    log.AppendFormat(CultureInfo.InvariantCulture, "{0}==> [{1} {2}ms] #{3} {4}\r\n", logIndent, result.Successful ? "OK" : "FAIL", run.StepRunMillis[i], i + 1, step);
                    if (!string.IsNullOrEmpty(result.Log))
                        log.Append(result.Log);
                    if (!string.IsNullOrEmpty(result.Warning))
                        warnings.AppendFormat(CultureInfo.InvariantCulture, warnings.Length == 0 ? "{0}" : "\r\n\r\n{0}", result.Warning);
                    prevStepsSucceeded = result.Successful;

                    if (run.StepResults[i].SubAction != null)
                        LogAction(log, warnings, run.StepResults[i].SubAction, depth: depth + 1);
                }
                else
                {
                    log.AppendFormat(CultureInfo.InvariantCulture, "{0}==> [SKIP] #{1} {2}\r\n", logIndent, i + 1, step);
                }
            }

            return prevStepsSucceeded;
        }
    }
}
