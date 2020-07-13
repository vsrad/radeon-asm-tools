using Moq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.ProjectSystem.Profiles;
using Xunit;

namespace VSRAD.PackageTests.ProjectSystem.Profiles
{
    public class ProfileOptionsWindowContextTests
    {
        private IProject CreateTestProject()
        {
            var profiles = new Dictionary<string, ProfileOptions>
            {
                { "kana", new ProfileOptions() },
                { "asa", new ProfileOptions() }
            };
            profiles["kana"].General.RemoteMachine = "money";
            profiles["asa"].General.RemoteMachine = "setting";

            var options = new ProjectOptions();
            options.SetProfiles(profiles, activeProfile: "kana");

            var projectMock = new Mock<IProject>(MockBehavior.Strict);
            projectMock.Setup((p) => p.Options).Returns(options);

            return projectMock.Object;
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

        private static ProfileOptions GetDirtyProfile(ProfileOptionsWindowContext context, string profileName) =>
            context.DirtyProfiles.First(p => p.General.ProfileName == profileName);

        [Fact]
        public void DirtyTrackingTest()
        {
            var project = CreateTestProject();
            project.Options.ActiveProfile = "kana";
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

        [Fact]
        public void AddProfileInsertsMacrosTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            nameResolver.Setup(n => n("Creating a new profile", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), "")).Returns("dou");

            context.CreateNewProfile();
            var newProfile = GetDirtyProfile(context, "dou");
            var expectedMacros = CleanProfileMacros.Macros.Select(m => new MacroItem(m.Item1, m.Item2, userDefined: true));
            Assert.Equal(expectedMacros.ToArray(), newProfile.Macros.ToArray());
        }

        [Fact]
        public void AddProfileNameConflictTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            nameResolver.Setup(n => n("Creating a new profile", "Enter the name for the new profile:", It.IsAny<IEnumerable<string>>(), ""))
                .Returns("kana").Verifiable();

            Assert.Equal(2, context.DirtyProfiles.Count);
            var oldProfileMachine = GetDirtyProfile(context, "kana").General.RemoteMachine;

            context.CreateNewProfile();
            nameResolver.Verify();

            Assert.Equal(2, context.DirtyProfiles.Count);
            var newProfileMachine = GetDirtyProfile(context, "kana").General.RemoteMachine;
            Assert.NotEqual(oldProfileMachine, newProfileMachine);
        }

        [Fact]
        public void SaveChangesNameConflictTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            var kana = GetDirtyProfile(context, "kana");
            kana.General.RemoteMachine = "kana-edited";
            var asa = GetDirtyProfile(context, "asa");
            asa.General.ProfileName = "kana";
            asa.General.RemoteMachine = "asa-edited";

            nameResolver.Setup(n => n("Rename", "Profile kana already exists. Enter a new name or leave it as is to overwrite the profile:", It.IsAny<IEnumerable<string>>(), "kana"))
                .Returns("asa-renamed").Verifiable();

            context.SaveChanges();
            nameResolver.Verify();

            Assert.Equal(2, project.Options.Profiles.Count);
            Assert.True(project.Options.Profiles.TryGetValue("kana", out var kanaSaved));
            Assert.Equal("kana-edited", kanaSaved.General.RemoteMachine);
            Assert.True(project.Options.Profiles.TryGetValue("asa-renamed", out var asaSaved));
            Assert.Equal("asa-edited", asaSaved.General.RemoteMachine);
        }

        [Fact]
        public void SwitchingProfilePreservesSelectedPageTypeTests()
        {
            var project = CreateTestProject();
            project.Options.Profiles["kana"].Macros.Add(new MacroItem("kana-profile-macro", "h", userDefined: true));
            project.Options.Profiles["kana"].Debugger.OutputFile.Path = "kana-output-path";
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "kana-action" });
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "shared-action" });
            project.Options.Profiles["asa"].Macros.Add(new MacroItem("asa-profile-macro", "h", userDefined: true));
            project.Options.Profiles["asa"].Debugger.OutputFile.Path = "asa-output-path";
            project.Options.Profiles["asa"].Actions.Add(new ActionProfileOptions { Name = "shared-action" });

            var context = new ProfileOptionsWindowContext(project, null, null) { SelectedPage = null };
            // crude emulation of WPF control behavior when clearing the collection
            context.Pages.CollectionChanged += (s, e) => context.SelectedPage = null;

            // No page selected

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.Null(context.SelectedPage); // does not throw a null pointer exception

            // General, Macros

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetPage<ProfileOptionsMacrosPage>(context);
            var lastMacro = ((ProfileOptionsMacrosPage)context.SelectedPage).Macros.Last();
            Assert.Equal("kana-profile-macro", lastMacro.Name);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.IsType<ProfileOptionsMacrosPage>(context.SelectedPage);
            lastMacro = ((ProfileOptionsMacrosPage)context.SelectedPage).Macros.Last();
            Assert.Equal("asa-profile-macro", lastMacro.Name);

            // Debug

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetPage<DebuggerProfileOptions>(context);
            Assert.Equal("kana-output-path", ((DebuggerProfileOptions)context.SelectedPage).OutputFile.Path);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.IsType<DebuggerProfileOptions>(context.SelectedPage);
            Assert.Equal("asa-output-path", ((DebuggerProfileOptions)context.SelectedPage).OutputFile.Path);

            // Shared action

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetPage<ProfileOptionsActionsPage>(context).Actions.First(a => a is ActionProfileOptions ao && ao.Name == "shared-action");
            Assert.Equal("shared-action", ((ActionProfileOptions)context.SelectedPage).Name);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.IsType<ActionProfileOptions>(context.SelectedPage);
            Assert.Equal("shared-action", ((ActionProfileOptions)context.SelectedPage).Name);

            // Non-shared action

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetPage<ProfileOptionsActionsPage>(context).Actions.First(a => a is ActionProfileOptions ao && ao.Name == "kana-action");
            Assert.Equal("kana-action", ((ActionProfileOptions)context.SelectedPage).Name);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.Null(context.SelectedPage);
        }

        #region Profile Transfer
        [Fact]
        public void ImportExportTest()
        {
            var project = CreateTestProject();
            var context = new ProfileOptionsWindowContext(project, null, null);

            var tmpFile = Path.GetTempFileName();
            context.ExportProfiles(tmpFile);

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.RemoveProfileCommand.Execute(null);
            context.SelectedProfile = GetDirtyProfile(context, "asa");
            context.RemoveProfileCommand.Execute(null);
            Assert.Empty(context.ProfileNames);

            context.ImportProfiles(tmpFile);
            File.Delete(tmpFile);

            Assert.Collection(context.ProfileNames, fst => Assert.Equal("kana", fst), snd => Assert.Equal("asa", snd));
        }

        [Fact]
        public void ImportNameConflictTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            var tmpFile = Path.GetTempFileName();
            context.ExportProfiles(tmpFile);

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.RemoveProfileCommand.Execute(null);
            Assert.Single(context.ProfileNames, "asa");

            nameResolver.Setup(n => n("Import", "Profile asa already exists. Enter a new name or leave it as is to overwrite the profile:", It.IsAny<IEnumerable<string>>(), "asa"))
                .Returns("mizu").Verifiable();
            context.ImportProfiles(tmpFile);
            File.Delete(tmpFile);

            nameResolver.Verify();
            Assert.Collection(context.ProfileNames, fst => Assert.Equal("asa", fst), snd => Assert.Equal("kana", snd), trd => Assert.Equal("mizu", trd));
        }
        #endregion
    }
}
