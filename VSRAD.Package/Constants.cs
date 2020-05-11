using System;

namespace VSRAD.Package
{
    public static class Constants
    {
        public const string PackageId = "033a507d-aaaa-4a75-b906-895d8cc0616e";
        public static readonly Guid PackageGuid = new Guid(PackageId);

        public const string ProjectTypeId = "{BFE1F494-F7E3-4EA1-AD50-435F95335B74}"; // braces required
        public const string ProjectFileExtension = "radproj";

        /// <summary>
        /// A unique capability that may be used together with the [AppliesTo] attribute to load our MEF components for RAD projects only.
        /// </summary>
        /// <remarks>
        /// This value should be kept in sync with the capability in BuildSystem/DeployedBuildSystem/RADProject.targets.
        /// </remarks>
        public const string ProjectCapability = "RADProject";

        public const int MenuCommandId = 0x0100;
        public const int ToolWindowVisualizerCommandId = 0x0100;
        public const int ToolWindowOptionsCommandId = 0x0101;
        public const int ToolWindowSliceVisualizerCommandId = 0x0102;
        public const int ProfileCommandId = 0x0100;
        public const int DisassembleCommandId = 0x0100;
        public const int PreprocessCommandId = 0x0100;
        public const int EvaluateSelectedCommandId = 0x0100;
        public const int AddArrayToWatchesIndexCount = 16;
        public const int AddArrayToWatchesFromHeaderId = 0x1031;
        public const int AddArrayToWatchesToIdOffset = 0x1400;
        public const int AddArrayToWatchesToFromOffset = 0x100;
        public const int AddArrayToWatchesToHeaderOffset = 0x1200;
        public const string ToolWindowCommandSet = "03c8f3ba-2e44-4159-ac37-b08fc295a0cc";
        public const string ForceRunToCursorCommandSet = "cefc8250-7cd1-46c1-b4f6-46a0a22a1c81";
        public const string AddToWatchesCommandSet = "8560BD12-1D31-40BA-B300-1A31FC901E93";
        public const string AddArrayToWatchesCommandSet = "A03BE90E-E3E1-47F8-815B-387605FDCB73";
        public const string EvaluateSelectedCommandSet = "6624A31D-4C20-4675-84D7-67D140842579";
        public const string ProfileCommandSet = "A74163CE-732B-4570-8374-21D51EF7C3AD";
        public const string DisassemblyCommandSet = "03E6AC6D-6562-46AC-B6D0-AD9D64CBF0AE";
        public const string PreprocessCommandSet = "7D6BA5AC-29C2-4E44-BD92-376A7A62FCA1";
        public const string ToolbarCommandSet = "E1436EB5-1D47-4714-85CB-6177E62AB2AD";

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
    };
}