using Microsoft.VisualStudio.Shell;
using System;

namespace VSRAD.Package.Registry
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    sealed class ProvideFontAndColorsCategoryAttribute : RegistrationAttribute
    {
        private readonly string _registryName;
        private readonly Guid _guid;
        private readonly Type _defaultsService;

        public ProvideFontAndColorsCategoryAttribute(string registryName, string id, Type defaultsService)
        {
            _registryName = registryName;
            _guid = new Guid(id);
            _defaultsService = defaultsService;
        }

        public override void Register(RegistrationContext context)
        {
            var key = context.CreateKey("FontAndColors\\" + _registryName);
            key.SetValue("Category", _guid.ToString("B"));
            key.SetValue("Package", _defaultsService.GUID.ToString("B"));
        }

        public override void Unregister(RegistrationContext context) { }
    }
}
