using Microsoft.VisualStudio.Threading;
using Moq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext

namespace VSRAD.PackageTests
{
    public static class TestHelper
    {
        private static bool _packageFactoryOverridden;

        /* https://github.com/Microsoft/vs-threading/blob/master/doc/testing_vs.md */
        public static void InitializePackageTaskFactory()
        {
            if (_packageFactoryOverridden) return;

            var sta = new Thread(() =>
            {
                var jtc = new JoinableTaskContext();
                VSPackage.TaskFactory = jtc.Factory;

                _packageFactoryOverridden = true;
            });
            sta.SetApartmentState(ApartmentState.STA); // JTC needs to be created on the main thread
            sta.Start();
            sta.Join();
        }

        public static Mock<IProject> MakeProjectWithProfile(Dictionary<string, string> macros = null, string projectRoot = "", Package.Options.ProfileOptions profile = null, string remoteWorkDir = "")
        {
            var mock = new Mock<IProject>(MockBehavior.Strict);
            var options = new ProjectOptions();
            options.SetProfiles(new Dictionary<string, ProfileOptions> { { "Default", profile ?? new ProfileOptions() } }, activeProfile: "Default");
            mock.Setup((p) => p.Options).Returns(options);
            mock.Setup((m) => m.RootPath).Returns(projectRoot);

            var evaluator = new Mock<IMacroEvaluator>();
            if (macros != null)
                foreach (var macro in macros)
                    evaluator.Setup((e) => e.GetMacroValueAsync(macro.Key)).Returns(Task.FromResult<Result<string>>(macro.Value));
            evaluator.Setup((e) => e.EvaluateAsync(It.IsAny<string>())).Returns<string>((val) => Task.FromResult<Result<string>>(val));
            evaluator.Setup((e) => e.EvaluateAsync("$(" + CleanProfileMacros.RemoteWorkDir + ")")).Returns(Task.FromResult<Result<string>>(remoteWorkDir));

            mock.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint[]>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));
            return mock;
        }

        public static T MakeWithReadOnlyProps<T>(params (string prop, object value)[] properties) where T : new()
        {
            var obj = new T();
            foreach (var (prop, value) in properties)
                SetReadOnlyProp(obj, prop, value);
            return obj;
        }

        public static void SetReadOnlyProp<T>(T obj, string prop, object value) =>
            typeof(T).GetField($"<{prop}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    }
}
