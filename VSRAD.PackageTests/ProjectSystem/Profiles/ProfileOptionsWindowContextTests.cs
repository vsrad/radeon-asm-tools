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

        private static T GetPage<T>(ProfileOptionsWindowContext context) => (T)context.Pages.Find(p => p is T);

        [Fact]
        public void DirtyTrackingTest()
        {
            var options = CreateTestOptions();
            options.ActiveProfile = "midori";
            var context = new ProfileOptionsWindowContext(options, null);
            GetPage<DebuggerProfileOptions>(context).Actions.Add(new ExecuteAction { Executable = "bun" });
            Assert.Empty(options.Profile.Debugger.Actions);
            context.SaveChanges();
            Assert.Equal(options.Profile.Debugger.Actions[0], new ExecuteAction { Executable = "bun" });

            ((ExecuteAction)GetPage<DebuggerProfileOptions>(context).Actions[0]).Arguments = "--stuffed";
            Assert.Equal("", ((ExecuteAction)options.Profile.Debugger.Actions[0]).Arguments);
            context.SaveChanges();
            Assert.Equal("--stuffed", ((ExecuteAction)options.Profile.Debugger.Actions[0]).Arguments);
        }
    }
}
