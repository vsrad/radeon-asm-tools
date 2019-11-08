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
";

        [Fact]
        public void ClangErrorTest()
        {
            var messages = ExtractMessages(ClangErrorString).ToList();

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
        }
    }
}
