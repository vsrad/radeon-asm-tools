using Microsoft.VisualStudio.ProjectSystem;
using System.ComponentModel.Composition;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Server;

namespace VSRAD.Package.ProjectSystem
{
    public interface IActionLogger
    {
        Task<Error?> LogActionWithWarningsAsync(string tag, ActionRunResult runResult);
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

        public async Task<Error?> LogActionWithWarningsAsync(string tag, ActionRunResult runResult)
        {
            var title = tag + " action " + (runResult.Successful ? "SUCCEEDED" : "FAILED") + $" in {runResult.TotalMillis}ms";

            var log = new StringBuilder();
            log.AppendFormat("* Initial timestamp fetch took {0}ms\r\n", runResult.InitTimestampFetchMillis);

            var warnings = new StringBuilder();

            var prevStepsSucceeded = true;
            for (int i = 0; i < runResult.Steps.Count; ++i)
            {
                var step = runResult.Steps[i];
                if (prevStepsSucceeded)
                {
                    var result = runResult.StepResults[i];
                    log.AppendFormat("* [{0}] {1} step {2} in {3}ms\r\n", i, step, result.Successful ? "SUCCEEDED" : "FAILED", runResult.StepRunMillis[i]);
                    if (!string.IsNullOrEmpty(result.Log))
                        log.Append(result.Log);
                    if (!string.IsNullOrEmpty(result.Warning))
                        warnings.AppendFormat("* {0}\r\n", result.Warning);
                    prevStepsSucceeded = result.Successful;
                }
                else
                {
                    log.AppendFormat("* [{0}] {1} step SKIPPED\r\n", i, step);
                }
            }

            var logString = log.ToString();
            await _outputWriter.PrintMessageAsync(title, logString);
            await _errorList.AddToErrorListAsync(logString);

            if (warnings.Length == 0)
                return null;
            if (!prevStepsSucceeded)
                return new Error(message: warnings.ToString(), critical: true, title: "RAD Action Execution Failed");
            return new Error(message: warnings.ToString(), critical: false, title: "RAD Action Execution Warnings");
        }
    }
}
