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

        public IReadOnlyList<IActionStep> Steps { get; }
        public StepResult[] StepResults { get; }

        public bool Successful => StepResults.All(r => r.Successful);

        private readonly Stopwatch _stopwatch;
        private long _lastRecordedTime;

        public ActionRunResult(IReadOnlyList<IActionStep> steps)
        {
            Steps = steps;
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

        public void FinishRun() =>
            TotalMillis = _stopwatch.ElapsedMilliseconds;

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

        public StepResult(bool successful, string warning, string log)
        {
            Successful = successful;
            Warning = warning;
            Log = log;
        }

        public bool Equals(StepResult result) =>
            Successful == result.Successful && Warning == result.Warning && Log == result.Log;
        public override bool Equals(object obj) => obj is StepResult result && Equals(result);
        public override int GetHashCode() => (Successful, Warning, Log).GetHashCode();
        public static bool operator ==(StepResult left, StepResult right) => left.Equals(right);
        public static bool operator !=(StepResult left, StepResult right) => !(left == right);
    }
}
