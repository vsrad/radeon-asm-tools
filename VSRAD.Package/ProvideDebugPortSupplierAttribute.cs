using System;
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace VSRAD.Package
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class ProvideDebugPortSupplierAttribute : RegistrationAttribute
    {
        private readonly string _name;
        private readonly Guid _guid;
        private readonly Type _portSupplier;

        public ProvideDebugPortSupplierAttribute(string name, string id, Type portSupplier)
        {
            _name = name;
            _guid = new Guid(id);
            _portSupplier = portSupplier;
        }

        public override void Register(RegistrationContext context)
        {
            var engineKey = context.CreateKey("AD7Metrics\\PortSupplier\\" + _guid.ToString("B"));
            engineKey.SetValue("Name", _name);
            engineKey.SetValue("CLSID", _portSupplier.GUID.ToString("B"));

            var clsidKey = context.CreateKey("CLSID");
            var clsidGuidKey = clsidKey.CreateSubkey(_portSupplier.GUID.ToString("B"));
            clsidGuidKey.SetValue("Assembly", _portSupplier.Assembly.FullName);
            clsidGuidKey.SetValue("Class", _portSupplier.FullName);
            clsidGuidKey.SetValue("InprocServer32", context.InprocServerPath);
            clsidGuidKey.SetValue("CodeBase", Path.Combine(context.ComponentPath, _portSupplier.Module.Name));
            clsidGuidKey.SetValue("ThreadingModel", "Free");
        }

        public override void Unregister(RegistrationContext context)
        {
        }
    }
}
