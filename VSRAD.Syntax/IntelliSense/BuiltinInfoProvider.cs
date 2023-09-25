﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense
{
    public sealed class BuiltinInfo
    {
        public string Name { get; }
        public IReadOnlyList<string> Parameters { get; }
        public string Description { get; }

        public BuiltinInfo(string name, IReadOnlyList<string> parameters, string description)
        {
            Name = name;
            Parameters = parameters;
            Description = description;
        }
    }

    public interface IBuiltinInfoProvider
    {
        bool TryGetBuilinInfo(AsmType asmType, string builtinName, out BuiltinInfo builtinInfo);
    }

    [Export(typeof(IBuiltinInfoProvider))]
    public sealed class BuiltinInfoProvider : IBuiltinInfoProvider
    {
        private static readonly Dictionary<string, BuiltinInfo> BuiltinInfo = LoadBuiltinInfo();

        public bool TryGetBuilinInfo(AsmType asmType, string builtinName, out BuiltinInfo builtinInfo)
        {
            if (asmType == AsmType.RadAsm2)
                return BuiltinInfo.TryGetValue(builtinName, out builtinInfo);

            builtinInfo = null;
            return false;
        }

        private static Dictionary<string, BuiltinInfo> LoadBuiltinInfo()
        {
            var builtinsXmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VSRAD.Syntax.DefaultConfiguration.Asm2Builtins.xml");
            var builtinsXml = XElement.Load(builtinsXmlStream);
            var builtinList = builtinsXml.Elements().Select(b =>
            {
                var name = b.Attribute("Name").Value;
                var parameters = b.Attribute("Parameters").Value.Split(',');
                var description = b.Value.Trim();
                return new KeyValuePair<string, BuiltinInfo>(name, new BuiltinInfo(name, parameters, description));
            });
            return builtinList.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}
