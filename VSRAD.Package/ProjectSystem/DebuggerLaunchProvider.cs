using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Debug;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.VS.Debug;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace VSRAD.Package.ProjectSystem
{
    [ExportDebugger(RADDebugger.SchemaName)]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    internal sealed class DebuggerLaunchProvider : DebugLaunchProviderBase
    {
        private readonly DebuggerIntegration _debugger;

        [ImportingConstructor]
        public DebuggerLaunchProvider(ConfiguredProject project, DebuggerIntegration debugger) : base(project)
        {
            _debugger = debugger;
        }

        /* "RadeonAsmDebugger" must match AssemblyName, PublicKeyToken must match the output of `sn.exe -T` */
        [ExportPropertyXamlRuleDefinition("RadeonAsmDebugger, Version=1.0.0.0, Culture=neutral, PublicKeyToken=ec2d07958d3464d7", "XamlRuleToCode:RADDebugger.xaml", "Project")]
        [AppliesTo(Constants.RadProjectCapability)]
        private object DebuggerXaml { get { throw new NotImplementedException(); } }

        public override Task<bool> CanLaunchAsync(DebugLaunchOptions launchOptions) => Task.FromResult(true);

        public override Task<IReadOnlyList<IDebugLaunchSettings>> QueryDebugTargetsAsync(DebugLaunchOptions launchOptions)
        {
            var settings = new DebugLaunchSettings(launchOptions)
            {
                PortSupplierGuid = Deborgar.Constants.RemotePortSupplierGuid,
                PortName = Deborgar.Constants.RemotePortName, // can be any _nonempty_ string, really, since RemotePortSupplier does not check it
                Executable = Deborgar.Constants.RemotePortName, // same, except that this value is passed to DebugEngine
                LaunchOperation = DebugLaunchOperation.AlreadyRunning,
                LaunchDebugEngineGuid = Deborgar.Constants.DebugEngineGuid
            };
            return Task.FromResult<IReadOnlyList<IDebugLaunchSettings>>(new[] { settings });
        }

        public override Task LaunchAsync(DebugLaunchOptions launchOptions)
        {
            if (_debugger.TryCreateDebugSession())
                return base.LaunchAsync(launchOptions);
            else
                return Task.CompletedTask;
        }
    }
}
