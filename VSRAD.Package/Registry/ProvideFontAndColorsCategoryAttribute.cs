using Microsoft.VisualStudio.Shell;
using System;

namespace VSRAD.Package.Registry
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    sealed class ProvideFontAndColorsCategoryAttribute : RegistrationAttribute
    {
        private readonly string _registryName;
        private readonly string _displayNameResourceId;
        private readonly Guid _guid;

        // displayNameResourceId should refer to a string defined in VSPackage.resx
        public ProvideFontAndColorsCategoryAttribute(string registryName, string displayNameResourceId, string id)
        {
            _registryName = registryName;
            _displayNameResourceId = displayNameResourceId;
            _guid = new Guid(id);
        }

        public override void Register(RegistrationContext context)
        {
            var key = context.CreateKey("FontAndColors\\" + _registryName);
            key.SetValue("Category", _guid.ToString("B"));
            key.SetValue("NameID", _displayNameResourceId);
            key.SetValue("ToolWindowPackage", Constants.PackageGuid.ToString("B"));
            // https://docs.microsoft.com/en-us/visualstudio/extensibility/how-to-access-the-built-in-fonts-and-color-scheme
            key.SetValue("Package", "{F5E7E71D-1401-11D1-883B-0000F87579D2}");
        }

        public override void Unregister(RegistrationContext context) { }
    }
}
