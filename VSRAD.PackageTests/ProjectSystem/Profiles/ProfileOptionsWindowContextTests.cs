using Moq;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Profiles;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class ProfileOptionsWindowContextTests
    {
        private IProject CreateTestProject()
        {
            var options = new ProjectOptions();
            options.AddProfile("kana", new ProfileOptions());
            options.Profiles["kana"].General.RemoteMachine = "money";
            options.AddProfile("midori", new ProfileOptions());
            options.Profiles["midori"].General.RemoteMachine = "setting";

            var mock = new Mock<IProject>(MockBehavior.Strict);
            mock.Setup((p) => p.Options).Returns(options);

            return mock.Object;
        }

        private static T GetPage<T>(ProfileOptionsWindowContext context)
        {
            foreach (var page in context.Pages)
            {
                if (page is T requestedPage)
                    return requestedPage;
                if (page is ProfileOptionsActionsPage actions)
                    foreach (var action in actions.Actions)
                        if (action is T requestedAction)
                            return requestedAction;
            }
            return default;
        }

        [Fact]
        public void DirtyTrackingTest()
        {
            var project = CreateTestProject();
            project.Options.ActiveProfile = "midori";
            var context = new ProfileOptionsWindowContext(project, null, null);
            GetPage<DebuggerProfileOptions>(context).Steps.Add(new ExecuteStep { Executable = "bun" });
            Assert.Empty(project.Options.Profile.Debugger.Steps);
            context.SaveChanges();
            Assert.Equal(project.Options.Profile.Debugger.Steps[0], new ExecuteStep { Executable = "bun" });

            ((ExecuteStep)GetPage<DebuggerProfileOptions>(context).Steps[0]).Arguments = "--stuffed";
            Assert.Equal("", ((ExecuteStep)project.Options.Profile.Debugger.Steps[0]).Arguments);
            context.SaveChanges();
            Assert.Equal("--stuffed", ((ExecuteStep)project.Options.Profile.Debugger.Steps[0]).Arguments);
        }

        [Fact]
        public void AddRemoveActionsTest()
        {
            var project = CreateTestProject();
            var context = new ProfileOptionsWindowContext(project, null, null);

            var actionsPage = GetPage<ProfileOptionsActionsPage>(context);
            Assert.Single(actionsPage.Actions, GetPage<DebuggerProfileOptions>(context));

            context.AddActionCommand.Execute(null);
            Assert.Collection(actionsPage.Actions,
                (page1) => Assert.True(page1 == GetPage<DebuggerProfileOptions>(context)),
                (page2) => Assert.True(page2 is ActionProfileOptions opts && opts.Name == "New Action"));

            var action = GetPage<ProfileOptionsActionsPage>(context).Actions[1];
            context.RemoveActionCommand.Execute(action);
            Assert.Single(actionsPage.Actions, GetPage<DebuggerProfileOptions>(context));
        }
    }
}
