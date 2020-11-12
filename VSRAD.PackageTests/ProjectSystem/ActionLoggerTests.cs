using Microsoft.VisualStudio.Shell;
using Moq;
using System.Collections.Generic;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
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
            evaluator.Setup(e => e.EvaluateAsync(It.IsAny<string>())).Returns<string>(s => Task.FromResult<Result<string>>(s));

            Assert.True((await profile.Actions[2].EvaluateAsync(evaluator.Object, profile)).TryGetResult(out var level1action, out _));
            var level2action = (RunActionStep)level1action.Steps[0];
            var level3action = (RunActionStep)level2action.EvaluatedSteps[1];

            var level3Result = new ActionRunResult(level3action.Name, level3action.EvaluatedSteps, false);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level3Result, nameof(level3Result.TotalMillis), 20);
            level3Result.StepResults[0] = new StepResult(true, "...", "Captured stdout (exit code 2):\r\n..\r\n");
            level3Result.StepRunMillis[0] = 20;

            var level2Result = new ActionRunResult(level2action.Name, level2action.EvaluatedSteps, false);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.InitTimestampFetchMillis), 0);
            TestHelper.SetReadOnlyProp(level2Result, nameof(level2Result.TotalMillis), 40);
            level2Result.StepResults[0] = new StepResult(true, "Some Message Indicating Contract Obtained", "Captured stdout (exit code 1):\r\ncontract obtained\r\n");
            level2Result.StepRunMillis[0] = 20;
            level2Result.StepResults[1] = new StepResult(true, "", "", level3Result);
            level2Result.StepRunMillis[1] = 20;

            var level1Result = new ActionRunResult(level1action.Name, level1action.Steps, false);
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

        [Fact]
        public async Task ContinueOnErrorTestAsync()
        {
            var profile = new ProfileOptions();
            profile.Actions.Add(new ActionProfileOptions { Name = "Shibahama Yūfō Taisen!" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "draw_anime", Arguments = "--dont-miss-deadlines" });
            profile.Actions[0].Steps.Add(new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, CheckTimestamp = false, TargetPath = "ending_theme.wav", SourcePath = "some_dudes_email" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "combine_ending_animation_and_music", Arguments = "--hope-music-fits" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "rework_ending", Arguments = "--one-night" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "comet_a", Arguments = "--showcase" });
            profile.Actions[0].Steps.Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "sell_dvds", Arguments = "--lots" });

            var actionResult = new ActionRunResult(profile.Actions[0].Name, profile.Actions[0].Steps, false);

            actionResult.StepResults[0] = new StepResult(true, "", "");
            actionResult.StepResults[1] = new StepResult(true, "", "");
            actionResult.StepResults[2] = new StepResult(false, "", "");
            actionResult.StepResults[3] = new StepResult(true, "", "");
            actionResult.StepResults[4] = new StepResult(true, "", "");
            actionResult.StepResults[5] = new StepResult(true, "", "");

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
            var warnings = await logger.LogActionWithWarningsAsync(actionResult);

            Assert.Equal("Shibahama Yūfō Taisen! action FAILED in 0ms", logTitle);
            var expectedMessage =
@"=> Fetched initial timestamps in 0ms
=> [0] Execute Remote draw_anime --dont-miss-deadlines SUCCEEDED in 0ms
=> [1] Copy from Remote some_dudes_email -> ending_theme.wav SUCCEEDED in 0ms
=> [2] Execute Remote combine_ending_animation_and_music --hope-music-fits FAILED in 0ms
=> [3] Execute Remote rework_ending --one-night SKIPPED
=> [4] Execute Remote comet_a --showcase SKIPPED
=> [5] Execute Remote sell_dvds --lots SKIPPED
";
            Assert.Equal(expectedMessage, logMessage);

            profile.Actions[0].Name += " (without difficulties)";
            TestHelper.SetReadOnlyProp(actionResult, nameof(actionResult.ActionName), profile.Actions[0].Name);
            TestHelper.SetReadOnlyProp(actionResult, nameof(actionResult.ContinueOnError), true);

            warnings = await logger.LogActionWithWarningsAsync(actionResult);

            Assert.Equal("Shibahama Yūfō Taisen! (without difficulties) action FAILED in 0ms", logTitle);

            expectedMessage =
@"=> Fetched initial timestamps in 0ms
=> [0] Execute Remote draw_anime --dont-miss-deadlines SUCCEEDED in 0ms
=> [1] Copy from Remote some_dudes_email -> ending_theme.wav SUCCEEDED in 0ms
=> [2] Execute Remote combine_ending_animation_and_music --hope-music-fits FAILED in 0ms
=> [3] Execute Remote rework_ending --one-night SUCCEEDED in 0ms
=> [4] Execute Remote comet_a --showcase SUCCEEDED in 0ms
=> [5] Execute Remote sell_dvds --lots SUCCEEDED in 0ms
";
            Assert.Equal(expectedMessage, logMessage);
        }
    }
}
