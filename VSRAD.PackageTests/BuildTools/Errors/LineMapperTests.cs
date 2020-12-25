using Xunit;
using static VSRAD.Package.BuildTools.Errors.LineMapper;

namespace VSRAD.PackageTests.BuildTools.Errors
{
    public class LineMapperTests
    {
        public static readonly string[] ProjectPaths = new[] {
            @"code/EVA00/main.c",
            @"code/EVA00/sensors_controller.c",
            @"code/EVA00/pilot_controller.c",
            @"code/EVA00/motors_controller.c",
            @"code/EVA00/battery_controller.c",
            @"code/EVA00/connection_controller.c",
            @"code/EVA01/main.c",
            @"code/EVA01/sensors_controller.c",
            @"code/EVA01/pilot_controller.c",
            @"code/EVA01/motors_controller.c",
            @"code/EVA01/battery_controller.c",
            @"code/EVA01/connection_controller.c",
        };

        public const string RemotePath = @"MAIN_MODULE\EVA00\pilot_controller.c";

        [Fact]
        public void HostSourcePathMappingTest()
        {
            var hostSource = MapSourceToHost(RemotePath, ProjectPaths);
            Assert.Equal(@"code/EVA00/pilot_controller.c", hostSource);
        }
    }
}
