using Microsoft.VisualStudio.Shell;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
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
            var profile = new ProfileOptions();
            profile.Actions.Add(new ActionProfileOptions { Name = "Exchange Soul" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "cleanup", Arguments = "--skip" });
            profile.Actions.Add(new ActionProfileOptions { Name = "Sign Contract" });
            profile.Actions[1].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "obtain-contract", Arguments = "-i -mm" });
            profile.Actions[1].Steps.Add(new RunActionStep { Name = "Exchange Soul" });
            profile.Actions.Add(new ActionProfileOptions { Name = "Transform" });
            profile.Actions[2].Steps.Add(new RunActionStep { Name = "Sign Contract" });
            profile.Actions[2].Steps.Add(new CopyFileStep { Direction = FileCopyDirection.LocalToRemote, CheckTimestamp = true, TargetPath = "incubator", SourcePath = "soul" });

            var evaluator = new Mock<IMacroEvaluator>();
            evaluator.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult(s));

            var level1action = await profile.Actions[2].EvaluateAsync(evaluator.Object, profile);
            var level2action = (RunActionStep)level1action.Steps[0];
            var level3action = (RunActionStep)level2action.EvaluatedSteps[1];

            var level3Result = new ActionRunResult(level3action.Name, level3action.EvaluatedSteps);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.TotalMillis), 20);
            level3Result.StepResults[0] = new StepResult(true, "...", "Captured stdout (exit code 2):\r\n..\r\n");
            level3Result.StepRunMillis[0] = 20;

            var level2Result = new ActionRunResult(level2action.Name, level2action.EvaluatedSteps);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.TotalMillis), 40);
            level2Result.StepResults[0] = new StepResult(true, "Some Message Indicating Contract Obtained", "Captured stdout (exit code 1):\r\ncontract obtained\r\n");
            level2Result.StepRunMillis[0] = 20;
            level2Result.StepResults[1] = new StepResult(true, "", "", level3Result);
            level2Result.StepRunMillis[1] = 20;

            var level1Result = new ActionRunResult(level1action.Name, level1action.Steps);
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
            var warnings = await logger.LogActionWithWarningsAsync(level1Result);

            Assert.Equal("Transform action SUCCEEDED in 70ms", logTitle);
            var expectedMessage =
@"=> Fetched initial timestamps in 10ms
=> [0] Run Sign Contract SUCCEEDED in 40ms
===> Fetched initial timestamps in 0ms
===> [0] Execute Remote obtain-contract -i -mm SUCCEEDED in 20ms
Captured stdout (exit code 1):
contract obtained
===> [1] Run Exchange Soul SUCCEEDED in 20ms
=====> Fetched initial timestamps in 0ms
=====> [0] Execute Remote cleanup --skip SUCCEEDED in 20ms
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
