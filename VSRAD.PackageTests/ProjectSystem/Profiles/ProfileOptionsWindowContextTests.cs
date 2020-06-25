using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Profiles;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class ProfileOptionsWindowContextTests
    {
        private ProjectOptions CreateTestOptions()
        {
            var options = new ProjectOptions();
            options.AddProfile("kana", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "money")));
            options.AddProfile("midori", new ProfileOptions(general: new GeneralProfileOptions(remoteMachine: "setting")));
            return options;
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
            var options = CreateTestOptions();
            options.ActiveProfile = "midori";
            var context = new ProfileOptionsWindowContext(options, null);
            GetPage<DebuggerProfileOptions>(context).Steps.Add(new ExecuteStep { Executable = "bun" });
            Assert.Empty(options.Profile.Debugger.Steps);
            context.SaveChanges();
            Assert.Equal(options.Profile.Debugger.Steps[0], new ExecuteStep { Executable = "bun" });

            ((ExecuteStep)GetPage<DebuggerProfileOptions>(context).Steps[0]).Arguments = "--stuffed";
            Assert.Equal("", ((ExecuteStep)options.Profile.Debugger.Steps[0]).Arguments);
            context.SaveChanges();
            Assert.Equal("--stuffed", ((ExecuteStep)options.Profile.Debugger.Steps[0]).Arguments);
        }
    }
}
