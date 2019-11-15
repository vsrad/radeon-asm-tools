using System.Linq;
using Xunit;
using static VSRAD.BuildTools.Errors.LineMapper;
using static VSRAD.BuildTools.Errors.Parser;

namespace VSRAD.BuildToolsTests.Errors
{
    public class LineMapperTests
    {
        public const string ErrString = @"
test.c:12:10: error: invalid digit 'a' in octal constant
 return 0asdfshgmgmg
         ^
1 error generated.
";

        public const string PpString = @"//# 1 ""test.c""
//# 1 ""<built-in>""
//# 1 ""<command-line>""
//# 31 ""<command-line>""
//# 1 ""/usr/include/stdc-predef.h"" 1 3 4
//# 32 ""<command-line>"" 2
//# 1 ""test.c""


int main(int argc, char** argv)
        {

            return 0asdfshgmgmg

}

";


        public const string Preprocessed = @"
int main() {
    int a = 1 + 1;
    int b = 2 + 2;
# 16 'source.c'
    int c = 3 + 3;
    int d = 4 + 4;
# 55 'source.c'
    int f = 0xDEAD;
}
";

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
        public void PreprocessMapLinesTest()
        {
            var lineMapping = MapLines(Preprocessed);
            Assert.Equal(new int[] { 1, 2, 3, 4, 0, 16, 17, 0, 55, 56, 57 }, lineMapping);
        }

        [Fact]
        public void ParseErrorsWithPreprocessedTest()
        {
            var messages = ExtractMessages(ErrString, PpString);
            Assert.Equal(5, messages.First().Line);
        }

        [Fact]
        public void HostSourcePathMappingTest()
        {
            var hostSource = HostSourcePathMapping(RemotePath, ProjectPaths);
            Assert.Equal(@"code/EVA00/pilot_controller.c", hostSource);
        }
    }
}
