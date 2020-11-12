using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.Server
{
    public sealed class ActionRunResult
    {
        public long[] StepRunMillis { get; }
        public long InitTimestampFetchMillis { get; private set; }
        public long TotalMillis { get; private set; }

        public string ActionName { get; }
        public bool ContinueOnError { get; }
        public IReadOnlyList<IActionStep> Steps { get; }
        public StepResult[] StepResults { get; }

        /// <summary>Non-null if the action includes a <c>ReadDebugData</c> step and it was executed successfully.</summary>
        public BreakState BreakState { get; private set; }

        public bool Successful => StepResults.All(r => r.Successful);

        private readonly Stopwatch _stopwatch;
        private long _lastRecordedTime;

        public ActionRunResult(string actionName, IReadOnlyList<IActionStep> steps, bool continueOnError)
        {
            ActionName = actionName;
            Steps = steps;
            ContinueOnError = continueOnError;
            StepRunMillis = new long[steps.Count];
            StepResults = new StepResult[steps.Count];
            _stopwatch = Stopwatch.StartNew();
        }

        public void RecordInitTimestampFetch() =>
            InitTimestampFetchMillis = MeasureInterval();

        public void RecordStep(int stepIndex, StepResult result)
        {
            StepRunMillis[stepIndex] = MeasureInterval();
            StepResults[stepIndex] = result;
        }

        public void RecordDebugDataStep(int stepIndex, StepResult result, BreakState breakState)
        {
            RecordStep(stepIndex, result);
            BreakState = breakState;
        }

        public void FinishRun() =>
            TotalMillis = _stopwatch.ElapsedMilliseconds;

        public IEnumerable<string> GetStepOutputs()
        {
            // recursive algorithm to get action outputs
            // if there are loops in the actions, it will be detected before the action starts
            foreach (var result in StepResults)
            {
                if (result.ErrorListOutput != null)
                    foreach (var output in result.ErrorListOutput)
                        yield return output;

                if (result.SubAction != null)
                    foreach (var output in result.SubAction.GetStepOutputs())
                        yield return output;
            }
        }

        private long MeasureInterval()
        {
            var currentTime = _stopwatch.ElapsedMilliseconds;
            var elapsed = currentTime - _lastRecordedTime;
            _lastRecordedTime = currentTime;
            return elapsed;
        }
    }

    public readonly struct StepResult : IEquatable<StepResult>
    {
        public bool Successful { get; }
        public string Warning { get; }
        public string Log { get; }
        public string[] ErrorListOutput { get; }
        public ActionRunResult SubAction { get; }

        public StepResult(bool successful, string warning, string log, ActionRunResult subAction = null, string[] errorListOutput = null)
        {
            Successful = successful;
            Warning = warning;
            Log = log;
            SubAction = subAction;
            ErrorListOutput = errorListOutput;
        }

        public bool Equals(StepResult result) =>
            Successful == result.Successful && Warning == result.Warning && Log == result.Log && ErrorListOutput == result.ErrorListOutput && SubAction == result.SubAction;
        public override bool Equals(object obj) => obj is StepResult result && Equals(result);
        public override int GetHashCode() => (Successful, Warning, Log, ErrorListOutput, SubAction).GetHashCode();
        public static bool operator ==(StepResult left, StepResult right) => left.Equals(right);
        public static bool operator !=(StepResult left, StepResult right) => !(left == right);
    }
}
