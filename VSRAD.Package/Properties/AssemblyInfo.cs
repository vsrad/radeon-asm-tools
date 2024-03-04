using Microsoft.VisualStudio.Shell;
using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\RadeonAsmDebugger.dll")]
[assembly: ProvideCodeBase(CodeBase = @"$PackageFolder$\VSRAD.Deborgar.dll")]

[assembly: InternalsVisibleTo("VSRAD.PackageTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010021ad8acc5f48512f58127fffddbf12ce9f44e9e27f2e00cdea2ccfb5591acdbf0ba5da109d0800e4380d7101d9bbb701f9d0d0f8254f602c34ae964e107873731548eaa7e768498111bac8b8df43b0677d5edc58f95c4ca54d7c2548c1b947141e750c9b21f1d0203bd0937b25106dde6006c5af2baa3719a01e70c44a9bcfcf")]

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("VSRAD.Package")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("VSRAD.Package")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]   
[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers 
// by using the '*' as shown below:

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]



