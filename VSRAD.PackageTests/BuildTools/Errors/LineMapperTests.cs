using System.Linq;
using VSRAD.Package.BuildTools.Errors;
using Xunit;
using static VSRAD.Package.BuildTools.BuildErrorProcessor;
using static VSRAD.Package.BuildTools.Errors.LineMapper;
using static VSRAD.Package.BuildTools.Errors.Parser;

namespace VSRAD.PackageTests.BuildTools.Errors
{
    public class LineMapperTests
    {
        public const string ErrString = @"code.c:14:8: error: use of undeclared identifier 'ia'
return ia;
       ^
test.c:17:10: error: invalid suffix 'uigyuigyuiguyi' on integer constant
 return 0uigyuigyuiguyi;
         ^
2 errors generated.
";

        public const string PpString = @"# 1 ""test.c""
    //# 1 ""<built-in>""
# 1 ""<command-line>""
# 31 ""<command-line>""
    # 1     ""/usr/include/stdc-predef.h"" 1 3 4
//# 32      ""<command-line>"" 2
#       1 ""test.c""


int main(int argc, char** argv)
        {
  # 1 ""code.c"" 1
            int i = 3 + 3;
            return ia;
# 5 ""test.c"" 2

            return 0uigyuigyuiguyi;

        }
";


        public const string Preprocessed = @"int main() {
    int a = 1 + 1;
    int b = 2 + 2;
  # 16 ""source.c""
    int c = 3 + 3;
    int d = 4 + 4;
   //# 55 ""source1.c""
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
            Assert.Equal(new LineMarker[] {
                new LineMarker { PpLine = 5, SourceLine = 16, SourceFile = "source.c" },
                new LineMarker { PpLine = 8, SourceLine = 55, SourceFile = "source1.c" }
            }, lineMapping);
        }

        [Fact]
        public void ParseErrorsWithPreprocessedTest()
        {
            var messages = ParseStderr(ErrString);
            UpdateErrorLocations(messages, PpString, new string[] { "source.c", "code.c" });
            Assert.Equal(2, messages.First().Line);
            Assert.Equal("code.c", messages.First().SourceFile);
            Assert.Equal(6, messages.Last().Line);
            Assert.Equal("source.c", messages.Last().SourceFile);
        }

        [Fact]
        public void EmptyLineTest()
        {
            var messages = ParseStderr(@"
*E,fatal: undefined reference to 'printf' (<stdin>:3)
*W,undefined: 1 undefined references found
*E,syntax error (<stdin>:12): at symbol 'printf'
    parse error: syntax error, unexpected T_PAAMAYIM_NEKUDOTAYIM
    did you really mean to use the scope resolution op here?
*E,fatal (auth.c:35): Uncaught error: Undefined variable: user
").ToArray();
            UpdateErrorLocations(messages, PpString, new string[] { "source.c", "code.c" });
            Assert.Equal(0, messages[1].Line);

        }

        [Fact]
        public void HostSourcePathMappingTest()
        {
            var hostSource = MapSourceToHost(RemotePath, ProjectPaths);
            Assert.Equal(@"code/EVA00/pilot_controller.c", hostSource);
        }
    }
}
