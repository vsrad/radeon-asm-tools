using Microsoft.VisualStudio.ProjectSystem;
using System.ComponentModel.Composition;
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
            var title = runResult.ActionName + " action " + (runResult.Successful ? "SUCCEEDED" : "FAILED") + $" in {runResult.TotalMillis}ms";

            var log = new StringBuilder();
            var warnings = new StringBuilder();

            var actionSucceeded = LogAction(log, warnings, runResult);

            var logString = log.ToString();
            await _outputWriter.PrintMessageAsync(title, logString);
            await _errorList.AddToErrorListAsync(logString);

            if (warnings.Length == 0)
                return null;
            if (!actionSucceeded)
                return new Error(message: warnings.ToString(), critical: true, title: "RAD Action Execution Failed");
            return new Error(message: warnings.ToString(), critical: false, title: "RAD Action Execution Warnings");
        }

        private bool LogAction(StringBuilder log, StringBuilder warnings, ActionRunResult run, int depth = 0)
        {
            var logIndent = new string('=', 2 * depth);

            log.AppendFormat("{0}=> Fetched initial timestamps in {1}ms\r\n", logIndent, run.InitTimestampFetchMillis);

            var prevStepsSucceeded = true;
            for (int i = 0; i < run.Steps.Count; ++i)
            {
                var step = run.Steps[i];
                if (prevStepsSucceeded)
                {
                    var result = run.StepResults[i];
                    log.AppendFormat("{0}=> [{1}] {2} {3} in {4}ms\r\n", logIndent, i, step.Description, result.Successful ? "SUCCEEDED" : "FAILED", run.StepRunMillis[i]);
                    if (!string.IsNullOrEmpty(result.Log))
                        log.Append(result.Log);
                    if (!string.IsNullOrEmpty(result.Warning))
                        warnings.AppendFormat("* {0}\r\n", result.Warning);
                    prevStepsSucceeded = result.Successful;

                    if (run.StepResults[i].SubAction != null)
                        LogAction(log, warnings, run.StepResults[i].SubAction, depth: depth + 1);
                }
                else
                {
                    log.AppendFormat("{0}=> [{1}] {2} SKIPPED\r\n", logIndent, i, step.Description);
                }
            }

            return prevStepsSucceeded;
        }
    }
}
