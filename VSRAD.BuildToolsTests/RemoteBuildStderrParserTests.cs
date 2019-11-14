using System.Linq;
using Xunit;
using static VSRAD.BuildTools.RemoteBuildStderrParser;

namespace VSRAD.BuildTools
{
    public class RemoteBuildStderrParserTests
    {
        public const string ClangErrorString = @"
input.s:267:27: error: expected absolute expression
      s_sub_u32         s[loop_xss], s[loop_x], 1
                          ^
<stdin>:392:25: warning: not a valid operand.
      s_add_u32         s[loop_xss], s[loop_x], 1
                        ^
host.c:4:2: warning: implicitly declaring library function 'printf' with type 'int (const char *, ...)'
        printf(""h"");
        ^
host.c:4:2: note: include the header<stdio.h> or explicitly provide a declaration for 'printf'
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

        [Fact]
        public void ClangErrorTest()
        {
            var messages = ExtractMessages(ClangErrorString, "").ToList();

            Assert.Equal(MessageKind.Error, messages[0].Kind);
            Assert.Equal(27, messages[0].Column);
            Assert.Equal(267, messages[0].Line);
            Assert.Equal("input.s", messages[0].SourceFile);
            Assert.Equal(@"expected absolute expression
      s_sub_u32         s[loop_xss], s[loop_x], 1
                          ^", messages[0].Text);

            Assert.Equal(MessageKind.Warning, messages[1].Kind);
            Assert.Equal(392, messages[1].Line);
            Assert.Equal(25, messages[1].Column);
            Assert.Equal("<stdin>", messages[1].SourceFile);
            Assert.Equal(@"not a valid operand.
      s_add_u32         s[loop_xss], s[loop_x], 1
                        ^", messages[1].Text);

            Assert.Equal(MessageKind.Warning, messages[2].Kind);
            Assert.Equal(4, messages[2].Line);
            Assert.Equal(2, messages[2].Column);
            Assert.Equal("host.c", messages[2].SourceFile);
            Assert.Equal(@"implicitly declaring library function 'printf' with type 'int (const char *, ...)'
        printf(""h"");
        ^", messages[2].Text);

            Assert.Equal(MessageKind.Note, messages[3].Kind);
            Assert.Equal(4, messages[3].Line);
            Assert.Equal(2, messages[3].Column);
            Assert.Equal("host.c", messages[3].SourceFile);
            Assert.Equal(@"include the header<stdio.h> or explicitly provide a declaration for 'printf'", messages[3].Text);
        }

        [Fact]
        public void PreprocessMapLinesTest()
        {
            var lineMapping = MapLines(Preprocessed);
            Assert.Equal(new int[] { 1, 2, 3, 4, 0, 16, 17, 0, 55, 56, 57 }, lineMapping);
        }
    }
}
