using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using VSRAD.Package;
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

            profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "Debug" });
            profiles["asa"].Actions.Add(new ActionProfileOptions { Name = "Debug" });

            var options = new ProjectOptions();
            options.SetProfiles(profiles, activeProfile: "kana");

            var projectMock = new Mock<IProject>(MockBehavior.Strict);
            projectMock.Setup((p) => p.Options).Returns(options);

            return projectMock.Object;
        }

        private static T GetPage<T>(ProfileOptionsWindowContext context) =>
            (T)context.Pages.FirstOrDefault(p => p is T);

        private static ActionProfileOptions GetAction(ProfileOptionsWindowContext context, string name) =>
            context.ActionsPage.Pages.First(a => a.Name == name);

        private static ProfileOptions GetDirtyProfile(ProfileOptionsWindowContext context, string profileName) =>
            context.DirtyProfiles.First(p => p.General.ProfileName == profileName);

        [Fact]
        public void DirtyTrackingTest()
        {
            var project = CreateTestProject();
            project.Options.ActiveProfile = "kana";
            var context = new ProfileOptionsWindowContext(project, null, null);
            GetAction(context, "Debug").Steps.Add(new ExecuteStep { Executable = "bun" });
            Assert.Empty(project.Options.Profile.Actions[0].Steps);
            context.SaveChanges();
            Assert.Equal(project.Options.Profile.Actions[0].Steps[0], new ExecuteStep { Executable = "bun" });

            ((ExecuteStep)GetAction(context, "Debug").Steps[0]).Arguments = "--stuffed";
            Assert.Equal("", ((ExecuteStep)project.Options.Profile.Actions[0].Steps[0]).Arguments);
            context.SaveChanges();
            Assert.Equal("--stuffed", ((ExecuteStep)project.Options.Profile.Actions[0].Steps[0]).Arguments);
        }

        [Fact]
        public void AddRemoveActionsTest()
        {
            var project = CreateTestProject();
            var context = new ProfileOptionsWindowContext(project, null, null);

            var actionsPage = context.ActionsPage;
            Assert.Single(actionsPage.Pages, GetAction(context, "Debug"));

            context.AddAction();
            Assert.Collection(actionsPage.Pages,
                (page1) => Assert.Equal("Debug", page1.Name),
                (page2) => Assert.Equal("New Action", page2.Name));

            var action = context.ActionsPage.Pages[1];
            context.RemoveAction(action);
            Assert.Single(actionsPage.Pages, GetAction(context, "Debug"));
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
        public void RemoveProfileTest()
        {
            var project = CreateTestProject();
            var context = new ProfileOptionsWindowContext(project, null, null);

            // a crude emulation of WPF control behavior when removing selected profile
            context.DirtyProfiles.CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems[0] == context.SelectedProfile)
                    context.SelectedProfile = null;
            };

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.RemoveSelectedProfile();

            Assert.Single(context.DirtyProfiles);
            Assert.Null(context.SelectedProfile);
            Assert.Null(context.SelectedPage);
            Assert.Empty(context.Pages);

            context.SaveChanges();

            Assert.Equal(1, project.Options.Profiles.Count);
            // Switches to the first existing profile
            Assert.Equal("asa", project.Options.ActiveProfile);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            context.RemoveSelectedProfile();

            Assert.Empty(context.DirtyProfiles);
            Assert.Null(context.SelectedProfile);
            Assert.Null(context.SelectedPage);
            Assert.Empty(context.Pages);

            context.SaveChanges();

            Assert.Null(project.Options.Profile);
            Assert.False(project.Options.HasProfiles);

            // Does not fail when there are no profiles
            context.RemoveSelectedProfile();
        }

        [Fact]
        public void SaveChangesNameConflictResolvedTest()
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

            Assert.Null(context.SaveChanges()); // no error
            nameResolver.Verify();

            Assert.Equal(2, project.Options.Profiles.Count);
            Assert.True(project.Options.Profiles.TryGetValue("kana", out var kanaSaved));
            Assert.Equal("kana-edited", kanaSaved.General.RemoteMachine);
            Assert.True(project.Options.Profiles.TryGetValue("asa-renamed", out var asaSaved));
            Assert.Equal("asa-edited", asaSaved.General.RemoteMachine);

            // Don't forget to synchronize dirty profiles!
            Assert.Equal("kana-edited", GetDirtyProfile(context, "kana").General.RemoteMachine);
            Assert.Equal("asa-edited", GetDirtyProfile(context, "asa-renamed").General.RemoteMachine);
        }

        [Fact]
        public void SaveChangesNameConflictIgnoredTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            var asa = GetDirtyProfile(context, "asa");
            asa.General.ProfileName = "kana";
            asa.General.RemoteMachine = "asa-edited";

            nameResolver.Setup(n => n("Rename", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), "kana")).Returns("kana");

            Assert.Null(context.SaveChanges()); // no error

            // Selected profile is transparently switched to the replacement
            Assert.Equal(asa, context.SelectedProfile);
            Assert.Single(context.DirtyProfiles, asa);

            Assert.Equal(1, project.Options.Profiles.Count);
            Assert.True(project.Options.Profiles.TryGetValue("kana", out var renamed));
            Assert.Equal("asa-edited", renamed.General.RemoteMachine);
        }

        [Fact]
        public void SaveChangesNameConflictResolutionCancelledTest()
        {
            var project = CreateTestProject();
            var nameResolver = new Mock<ProfileOptionsWindowContext.AskProfileNameDelegate>(MockBehavior.Strict);
            var context = new ProfileOptionsWindowContext(project, null, nameResolver.Object);

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            var asa = GetDirtyProfile(context, "asa");
            asa.General.ProfileName = "kana";

            nameResolver.Setup(n => n("Rename", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), "kana")).Returns((string)null);

            Assert.Equal("Profile options were not saved.", ((Error)context.SaveChanges()).Message);

            // Profiles are unchanged
            Assert.Equal(2, project.Options.Profiles.Count);
            Assert.True(project.Options.Profiles.ContainsKey("kana"));
            Assert.True(project.Options.Profiles.ContainsKey("asa"));
        }

        [Fact]
        public void SaveChangesPreservesSelectedPageTest()
        {
            var project = CreateTestProject();
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "kana-action" });
            var context = new ProfileOptionsWindowContext(project, null, null);
            // a crude emulation of WPF control behavior when resetting dirty profiles on save
            context.DirtyProfiles.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) context.SelectedPage = null; };

            context.SelectedPage = GetAction(context, "kana-action");
            context.SaveChanges();

            Assert.IsType<ActionProfileOptions>(context.SelectedPage);
            Assert.Equal("kana-action", ((ActionProfileOptions)context.SelectedPage).Name);
        }

        [Fact]
        public void SwitchingProfilePreservesSelectedPageTypeTest()
        {
            var project = CreateTestProject();
            project.Options.Profiles["kana"].Macros.Add(new MacroItem("kana-profile-macro", "h", userDefined: true));
            project.Options.Profiles["kana"].Actions[0].Steps.Add(new OpenInEditorStep { Path = "kana-open-path" });
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "kana-action" });
            project.Options.Profiles["asa"].Macros.Add(new MacroItem("asa-profile-macro", "h", userDefined: true));
            project.Options.Profiles["asa"].Actions[0].Steps.Add(new OpenInEditorStep { Path = "asa-open-path" });

            var context = new ProfileOptionsWindowContext(project, null, null) { SelectedPage = null };
            // a crude emulation of WPF control behavior when clearing the collection
            context.Pages.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) context.SelectedPage = null; };

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

            // Debug (shared action)

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetAction(context, "Debug");
            Assert.Equal("kana-open-path", (((ActionProfileOptions)context.SelectedPage).Steps[0] as OpenInEditorStep).Path);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.IsType<ActionProfileOptions>(context.SelectedPage);
            Assert.Equal("asa-open-path", (((ActionProfileOptions)context.SelectedPage).Steps[0] as OpenInEditorStep).Path);

            // Non-shared action

            context.SelectedProfile = GetDirtyProfile(context, "kana");
            context.SelectedPage = GetPage<ProfileOptionsActionsPage>(context).Pages.First(a => a is ActionProfileOptions ao && ao.Name == "kana-action");
            Assert.Equal("kana-action", ((ActionProfileOptions)context.SelectedPage).Name);

            context.SelectedProfile = GetDirtyProfile(context, "asa");
            Assert.Null(context.SelectedPage);
        }

        [Fact]
        public void RenamingActionsSynchronizesNameUsagesSteps()
        {
            var project = CreateTestProject();
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "kana-action" });
            project.Options.Profiles["kana"].Actions[1].Steps.Add(new RunActionStep { Name = "Debug" });
            project.Options.Profiles["kana"].MenuCommands.DebugAction = "Debug";
            project.Options.Profiles["kana"].MenuCommands.ProfileAction = "Debug";
            project.Options.Profiles["kana"].MenuCommands.DisassembleAction = "Debug";
            project.Options.Profiles["kana"].MenuCommands.DisassembleAction = "Debug";

            var context = new ProfileOptionsWindowContext(project, null, null);
            GetDirtyProfile(context, "kana").Actions[0].Name = "renamed-debug";

            Assert.Equal("renamed-debug", ((RunActionStep)GetDirtyProfile(context, "kana").Actions[1].Steps[0]).Name);
            Assert.Equal("renamed-debug", GetDirtyProfile(context, "kana").MenuCommands.DebugAction);
            Assert.Equal("renamed-debug", GetDirtyProfile(context, "kana").MenuCommands.ProfileAction);
            Assert.Equal("renamed-debug", GetDirtyProfile(context, "kana").MenuCommands.DisassembleAction);
            Assert.Equal("renamed-debug", GetDirtyProfile(context, "kana").MenuCommands.DisassembleAction);

            /* Syncs actions added to dirty profile */
            GetDirtyProfile(context, "kana").Actions.Add(new ActionProfileOptions { Name = "h" });
            ((RunActionStep)GetDirtyProfile(context, "kana").Actions[1].Steps[0]).Name = "h";
            GetDirtyProfile(context, "kana").Actions.Last().Name = "hh";
            Assert.Equal("hh", ((RunActionStep)GetDirtyProfile(context, "kana").Actions[1].Steps[0]).Name);
        }

        [Fact]
        public void NormalizesToolbarActionNames()
        {
            var project = CreateTestProject();
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "valid-action" });
            project.Options.Profiles["kana"].MenuCommands.ProfileAction = "unknown action";
            project.Options.Profiles["kana"].MenuCommands.DisassembleAction = null;
            project.Options.Profiles["kana"].MenuCommands.PreprocessAction = "valid-action";

            var context = new ProfileOptionsWindowContext(project, null, null);
            Assert.Equal("", GetDirtyProfile(context, "kana").MenuCommands.ProfileAction);
            Assert.Equal("", GetDirtyProfile(context, "kana").MenuCommands.DisassembleAction);
            Assert.Equal("valid-action", GetDirtyProfile(context, "kana").MenuCommands.PreprocessAction);

            GetDirtyProfile(context, "kana").Actions.RemoveAt(1);
            Assert.Equal("", GetDirtyProfile(context, "kana").MenuCommands.PreprocessAction);
        }

        [Fact]
        public void ContextDoesNotLeak()
        {
            // Note: this test will fail if you set regular event handlers
            // for PropertyChanged/CollectionChanged events in dirty profiles (use WeakEventManager instead)

            var project = CreateTestProject();
            project.Options.Profiles["kana"].Actions.Add(new ActionProfileOptions { Name = "kana-action" });

            var refs = new List<WeakReference>();
            new Action(() =>
            {
                var context = new ProfileOptionsWindowContext(project, null, null);

                refs.Add(new WeakReference(context, true));
                refs.Add(new WeakReference(context.Pages.First(p => p is ProfileOptionsActionsPage), true));

                GetDirtyProfile(context, "kana").Actions[0].Name = "renamed-kana-action";
                context.SaveChanges();
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            foreach (var r in refs)
                Assert.Null(r.Target);
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
            context.RemoveSelectedProfile();
            context.SelectedProfile = GetDirtyProfile(context, "asa");
            context.RemoveSelectedProfile();
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
            context.RemoveSelectedProfile();
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
