using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Moq;
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

        public static (Queue<string> errors, Queue<string> warnings) SetupGlobalErrorMessageSink()
        {
            var errorMessages = new Queue<string>();
            var warningMessages = new Queue<string>();
            var mock = new Mock<MessageBoxFactory>();
            mock.Setup((m) => m(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OLEMSGICON>()))
                .Callback((string message, string title, OLEMSGICON icon) =>
                {
                    if (icon == OLEMSGICON.OLEMSGICON_CRITICAL) errorMessages.Enqueue(message);
                    else if (icon == OLEMSGICON.OLEMSGICON_WARNING) warningMessages.Enqueue(message);
                });
            Errors.CreateMessageBox = mock.Object;
            return (errorMessages, warningMessages);
        }
    }
}
