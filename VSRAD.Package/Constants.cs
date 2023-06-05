using System;

namespace VSRAD.Package
{
    public static class Constants
    {
        public const string PackageId = "033a507d-aaaa-4a75-b906-895d8cc0616e";
        public static readonly Guid PackageGuid = new Guid(PackageId);

        public const string ProjectTypeId = "{BFE1F494-F7E3-4EA1-AD50-435F95335B74}"; // braces required
        public const string ProjectFileExtension = "radproj";

        public const string DebuggerSchemaName = "RADDebugger";

        /// <summary>
        /// A unique capability that may be used together with the [AppliesTo] attribute to load our MEF components for RAD projects only.
        /// </summary>
        /// <remarks>
        /// This value should be kept in sync with the capability in BuildSystem/DeployedBuildSystem/RADProject.targets.
        /// </remarks>
        public const string RadProjectCapability = "RADProject";
        public const string RadOrVisualCProjectCapability = "RADProject | VisualC";

        public const int MenuCommandId = 0x0100;
        public const int ToolWindowVisualizerCommandId = 0x0100;
        public const int ToolWindowOptionsCommandId = 0x0101;
        public const int ToolWindowSliceVisualizerCommandId = 0x0102;
        public const int ActionsMenuCommandId = 0x100;
        public const int ProfileCommandId = 0x10;
        public const int DisassembleCommandId = 0x11;
        public const int PreprocessCommandId = 0x12;
        public const int DebugActionCommandId = 0x13;
        public const int EvaluateSelectedCommandId = 0x0100;
        public const int AddToWatchesCommandId = 0x0100;
        public const int AddToWatchesArrayCustomCommandId = 0x1900;
        public const int AddArrayToWatchesIndexCount = 16;
        public const int AddArrayToWatchesFromHeaderId = 0x1031;
        public const int AddArrayToWatchesToIdOffset = 0x1400;
        public const int AddArrayToWatchesToFromOffset = 0x100;
        public const int AddArrayToWatchesToHeaderOffset = 0x1200;
        public const int ProfileTargetMachineDropdownId = 0x10;
        public const int ProfileTargetMachineDropdownListId = 0x100;
        public const int BreakModeDropdownId = 0x11;
        public const int BreakModeDropdownListId = 0x101;
        public const int BreakpointMenuToggleResumable = 0x1020;
        public const int BreakpointMenuAllToResumable = 0x1021;
        public const int BreakpointMenuAllToUnresumable = 0x1022;
        public static readonly Guid ToolWindowCommandSet = new Guid("03c8f3ba-2e44-4159-ac37-b08fc295a0cc");
        public static readonly Guid ForceRunToCursorCommandSet = new Guid("cefc8250-7cd1-46c1-b4f6-46a0a22a1c81");
        public static readonly Guid AddToWatchesCommandSet = new Guid("8560BD12-1D31-40BA-B300-1A31FC901E93");
        public static readonly Guid EvaluateSelectedCommandSet = new Guid("6624A31D-4C20-4675-84D7-67D140842579");
        public static readonly Guid ToolbarCommandSet = new Guid("E1436EB5-1D47-4714-85CB-6177E62AB2AD");
        public static readonly Guid ActionsMenuCommandSet = new Guid("7CF54FFE-BCAC-4751-BEEC-D103FD953C8B");
        public static readonly Guid ProfileDropdownCommandSet = new Guid("912C011A-EDAA-4922-85F2-74436F2265CA");
        public static readonly Guid BreakModeDropdownCommandSet = new Guid("EADE0887-3138-4DBF-8A14-FCEB0017E5E8");
        public static readonly Guid BreakpointMenuCommandSet = new Guid("D0D94348-11C2-4ED2-8A89-D8D10862DABA");

        public const string OutputPaneServerTitle = "RAD Debug Server";
        public const string OutputPaneServerId = "A183AE1B-765F-4804-B188-9E1543C4B954";
        public static readonly Guid OutputPaneServerGuid = new Guid(OutputPaneServerId);

        public const string OutputPaneExecutionResultTitle = "RAD Debug";
        public const string OutputPaneExecutionResultId = "4B10DF21-F96E-4043-9F10-BC41A9A7FD84";
        public static readonly Guid OutputPaneExecutionResultGuid = new Guid(OutputPaneExecutionResultId);

        public const string FontAndColorsCategoryTitle = "RAD Visualizer";
        public const string FontAndColorsCategoryId = "B91ADC62-1D16-46C8-8E59-30C2AC82B89F";
        public static readonly Guid FontAndColorsCategoryGuid = new Guid(FontAndColorsCategoryId);
        public const string FontAndColorDefaultsServiceId = "D1BE46F4-0DA5-4A69-9DA7-2460DA3025E2";

        public const string ToolbarIconStripResourcePackUri = "pack://application:,,,/RadeonAsmDebugger;component/Resources/DebugVisualizerWindowCommand.png";
        public const string CurrentStatementIconResourcePackUri = "pack://application:,,,/RadeonAsmDebugger;component/Resources/CurrentStatement.png";
        public static readonly Version MinimalRequiredServerVersion = new Version("2021.3.3");
    };
}