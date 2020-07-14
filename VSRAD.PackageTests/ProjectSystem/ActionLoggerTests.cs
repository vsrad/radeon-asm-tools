using Microsoft.VisualStudio.Shell;
using Moq;
using System.Collections.Generic;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.Server;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.PackageTests.ProjectSystem
{
    public class ActionLoggerTests
    {
        [Fact]
        public async Task NestedRunResultLoggingTestAsync()
        {
            var level3Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "cleanup", Arguments = "--skip" },
            };
            var level2Steps = new List<IActionStep>
            {
                new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "obtain-contract", Arguments = "-i -mm" },
                new RunActionStep(level3Steps) { Name = "Exchange Soul" }
            };
            var level1Steps = new List<IActionStep>
            {
                new RunActionStep(level2Steps) { Name = "Sign Contract" },
                new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, CheckTimestamp = true, TargetPath = "incubator", SourcePath = "soul" }
            };

            var level3Result = new ActionRunResult("Exchange Soul", level3Steps);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.TotalMillis), 20);
            level3Result.StepResults[0] = new StepResult(true, "...", "Captured stdout (exit code 2):\r\n..\r\n");
            level3Result.StepRunMillis[0] = 20;

            var level2Result = new ActionRunResult("Sign Contract", level2Steps);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.TotalMillis), 40);
            level2Result.StepResults[0] = new StepResult(true, "Some Message Indicating Contract Obtained", "Captured stdout (exit code 1):\r\ncontract obtained\r\n");
            level2Result.StepRunMillis[0] = 20;
            level2Result.StepResults[1] = new StepResult(true, "", "", level3Result);
            level2Result.StepRunMillis[1] = 20;

            var level1Result = new ActionRunResult("Transform", level1Steps);
            TestHelper.SetReadOnlyProp(level1Result, nameof(level1Result.InitTimestampFetchMillis), 10);
            TestHelper.SetReadOnlyProp(level1Result, nameof(level1Result.TotalMillis), 70);
            level1Result.StepResults[0] = new StepResult(true, "", "", level2Result);
            level1Result.StepRunMillis[0] = 40;
            level1Result.StepResults[1] = new StepResult(true, "", "");
            level1Result.StepRunMillis[1] = 20;

            string logTitle = "", logMessage = "";

            var writer = new Mock<IOutputWindowWriter>();
            writer.Setup(w => w.PrintMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string, string>((title, message) =>
            {
                logTitle = title;
                logMessage = message;
            });

            var output = new Mock<IOutputWindowManager>(MockBehavior.Strict);
            output.Setup(o => o.GetExecutionResultPane()).Returns(writer.Object);

            var logger = new ActionLogger(output.Object, new Mock<IErrorListManager>().Object);
            var warnings = await logger.LogActionWithWarningsAsync("M", level1Result);

            Assert.Equal("M action SUCCEEDED in 70ms", logTitle);
            var expectedMessage =
@"=> Fetched initial timestamps in 10ms
=> [0] Run Sign Contract SUCCEEDED in 40ms
===> Fetched initial timestamps in 0ms
===> [0] Remote Execute obtain-contract -i -mm SUCCEEDED in 20ms
Captured stdout (exit code 1):
contract obtained
===> [1] Run Exchange Soul SUCCEEDED in 20ms
=====> Fetched initial timestamps in 0ms
=====> [0] Remote Execute cleanup --skip SUCCEEDED in 20ms
Captured stdout (exit code 2):
..
=> [1] Copy to Remote soul -> incubator SUCCEEDED in 20ms
";
            Assert.Equal(expectedMessage, logMessage);
            var expectedWarnings =
@"* Some Message Indicating Contract Obtained
* ...
";
            Assert.Equal(expectedWarnings, warnings.Value.Message);
        }
    }
}
