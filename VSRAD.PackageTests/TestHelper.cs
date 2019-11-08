using Microsoft.VisualStudio.Threading;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSRAD.Package;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;

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
            _packageFactoryOverridden = true;

            var jtc = new JoinableTaskContext();
            VSPackage.TaskFactory = jtc.Factory;
        }

        public static IProject MakeProjectWithProfile(Dictionary<string, string> macros)
        {
            var mock = new Mock<IProject>(MockBehavior.Strict);
            var options = new Package.Options.ProjectOptions();
            options.AddProfile("Default", new Package.Options.ProfileOptions());
            mock.Setup((p) => p.Options).Returns(options);

            var evaluator = new Mock<IMacroEvaluator>();
            foreach (var macro in macros)
                evaluator.Setup((e) => e.GetMacroValueAsync(macro.Key)).Returns(Task.FromResult(macro.Value));

            mock.Setup((p) => p.GetMacroEvaluatorAsync(It.IsAny<uint>(), It.IsAny<string[]>())).Returns(Task.FromResult(evaluator.Object));
            return mock.Object;
        }
    }
}
