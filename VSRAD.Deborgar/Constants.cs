using System;

namespace VSRAD.Deborgar
{
    public static class Constants
    {
        public const string DebugEngineName = "RAD";
        public const string DebugEngineId = "8355452D-6D2F-41b0-89B8-BB2AA2529E94";
        public const string DebugEngineVendorId = "BE3E4337-D15A-4683-A5E4-0574F6C3BA78";
        public static readonly Guid DebugEngineGuid = new Guid(DebugEngineId);

        public const string RemotePortSupplierName = "RAD Debugger (Remote)";
        public const string RemotePortSupplierId = "6F09D2CD-815C-4EE2-A39F-322BE7CB1074";
        public static readonly Guid RemotePortSupplierGuid = new Guid(RemotePortSupplierId);

        public const string RemotePortName = "RAD Remote";

        public const string VisualStudioLocalPortSupplierId = "708C1ECA-FF48-11D2-904F-00C04FA302A1";

        public const string ThreadName = "RAD Program Thread";
        public const string ProgramName = "RAD Program";

        public const string LanguageName = "RadeonAsm";
        public const string LanguageId = "02667B19-0E19-4690-BF72-2AED16735AC0";
        public static readonly Guid LanguageGuid = new Guid(LanguageId);
    }
}
