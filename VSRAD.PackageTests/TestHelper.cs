using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.Reflection;
using VSRAD.Package;
using Xunit;

namespace VSRAD.PackageTests
{
    /// <summary><a href="https://github.com/microsoft/vssdktestfx/blob/main/doc/xunit.md">See  Microsoft.VisualStudio.Sdk.TestFramework</a></summary>
    [CollectionDefinition(Collection)]
    public class MockedVS : ICollectionFixture<GlobalServiceProvider>, ICollectionFixture<MefHostingFixture>
    {
        public const string Collection = "MockedVS";
    }

    public static class TestHelper
    {
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
