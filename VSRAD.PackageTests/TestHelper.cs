using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using System.IO;
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
    public class MefHostingFixture : MefHosting
    {
        public MefHostingFixture() { }
    }

    public static class TestHelper
    {
        public static string GetFixturePath(string fixtureName)
        {
            var binDebug = Directory.GetCurrentDirectory();
            return Path.Combine(Directory.GetParent(binDebug).Parent.FullName, "Fixtures", fixtureName);
        }

        public static int GetFixtureSize(string fixtureName) =>
            (int)new FileInfo(GetFixturePath(fixtureName)).Length;

        public static string ReadFixture(string fixtureName) =>
            File.ReadAllText(GetFixturePath(fixtureName));

        public static byte[] ReadFixtureBytes(string fixtureName) =>
            File.ReadAllBytes(GetFixturePath(fixtureName));

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
