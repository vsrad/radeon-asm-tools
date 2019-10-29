using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace VSRAD.Package
{
    // https://github.com/Microsoft/PTVS/blob/1d04f01b7b902a9e1051b4080770b4a27e6e97e7/Common/Product/SharedProject/ProvideDebugEngineAttribute.cs
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideDebugEngineAttribute : RegistrationAttribute
    {
        private readonly string _name;
        private readonly Type _engine;

        public string[] PortSupplierGuids { get; set; }

        public ProvideDebugEngineAttribute(string name, Type engine)
        {
            _name = name;
            _engine = engine;
        }

        public override void Register(RegistrationContext context)
        {
            var engineKey = context.CreateKey("AD7Metrics\\Engine\\" + _engine.GUID.ToString("B"));
            engineKey.SetValue("Name", _name);
            engineKey.SetValue("CLSID", _engine.GUID.ToString("B"));

            var portSupplierKey = engineKey.CreateSubkey("PortSupplier");
            for (int i = 0; i < PortSupplierGuids.Length; i++)
            {
                portSupplierKey.SetValue(i.ToString(), new Guid(PortSupplierGuids[i]).ToString("B"));
            }

            engineKey.SetValue("Attach", 1);
            engineKey.SetValue("AddressBP", 0);
            engineKey.SetValue("AutoSelectPriority", 6);
            engineKey.SetValue("CallstackBP", 0);
            engineKey.SetValue("ConditionalBP", 0);
            engineKey.SetValue("DataBP", 0);
            engineKey.SetValue("Exceptions", 0);
            engineKey.SetValue("SetNextStatement", 0);
            engineKey.SetValue("RemoteDebugging", 1);
            engineKey.SetValue("HitCountBP", 0);
            engineKey.SetValue("JustMyCodeStepping", 0);

            engineKey.SetValue("EngineClass", _engine.FullName);
            engineKey.SetValue("EngineAssembly", _engine.Assembly.FullName);

            engineKey.SetValue("LoadProgramProviderUnderWOW64", 1);
            engineKey.SetValue("AlwaysLoadProgramProviderLocal", 1);
            engineKey.SetValue("AlwaysLoadLocal", 1);
            engineKey.SetValue("LoadUnderWOW64", 1);

            var clsidKey = context.CreateKey("CLSID");
            var clsidGuidKey = clsidKey.CreateSubkey(_engine.GUID.ToString("B"));
            clsidGuidKey.SetValue("Assembly", _engine.Assembly.FullName);
            clsidGuidKey.SetValue("Class", _engine.FullName);
            clsidGuidKey.SetValue("InprocServer32", context.InprocServerPath);
            clsidGuidKey.SetValue("CodeBase", Path.Combine(context.ComponentPath, _engine.Module.Name));
            clsidGuidKey.SetValue("ThreadingModel", "Free");
        }

        public override void Unregister(RegistrationContext context)
        {
        }
    }
}
