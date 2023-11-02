using Microsoft.VisualStudio.ProjectSystem;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionLogger
    {
        Task<Error?> LogActionWithWarningsAsync(ActionRunResult runResult);
    }

    [Export(typeof(IActionLogger))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ActionLogger : IActionLogger
    {
        private readonly IOutputWindowWriter _outputWriter;
        private readonly IErrorListManager _errorList;

        [ImportingConstructor]
        public ActionLogger(IOutputWindowManager outputWindow, IErrorListManager errorList)
        {
            _outputWriter = outputWindow.GetExecutionResultPane();
            _errorList = errorList;
        }

        public async Task<Error?> LogActionWithWarningsAsync(ActionRunResult runResult)
        {
            await _outputWriter.ClearAsync();

            var title = runResult.ActionName + " action " + (runResult.Successful ? "SUCCEEDED" : "FAILED") + $" in {runResult.TotalMillis}ms";

            var log = new StringBuilder();
            var warnings = new StringBuilder();

            var actionSucceeded = LogAction(log, warnings, runResult);

            var logString = log.ToString();
            await _outputWriter.PrintMessageAsync(title, logString);

            var errorListOutput = runResult.GetStepOutputs();
            await _errorList.AddToErrorListAsync(errorListOutput);

            if (warnings.Length == 0)
                return null;
            if (!actionSucceeded)
                return new Error(message: warnings.ToString(), critical: true, title: "RAD Action Execution Failed");
            return new Error(message: warnings.ToString(), critical: false, title: "RAD Action Execution Warnings");
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
                        warnings.AppendFormat(CultureInfo.InvariantCulture, "* {0}\r\n", result.Warning);
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
