using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using VSRAD.Package;

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
    }
}
