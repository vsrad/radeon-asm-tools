using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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

        public static List<(string Message, string Title, OLEMSGICON Icon)> CapturePackageMessageBoxErrors()
        {
            var errorList = new List<(string Message, string Title, OLEMSGICON Icon)>();
            Errors.CreateMessageBox = (message, title, icon) => errorList.Add((message, title, icon));
            return errorList;
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
