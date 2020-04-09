using System.Linq;
using Xunit;
using static VSRAD.BuildTools.IPCBuildResult;
using static VSRAD.Package.BuildTools.Errors.Parser;

namespace VSRAD.PackageTests.BuildTools.Errors
{
    public class ParserTests
    {
        [Fact]
        public void ClangErrorTest()
        {
            var messages = ParseStderr(@"
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
").ToList();

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

        public const string ScriptStderr = @"
*E,fatal: undefined reference to 'printf' (<stdin>:3)
*W,undefined: 1 undefined references found
*E,syntax error (<stdin>:12): at symbol 'printf'
    parse error: syntax error, unexpected T_PAAMAYIM_NEKUDOTAYIM
    did you really mean to use the scope resolution op here?
*E,fatal (auth.c:35): Uncaught error: Undefined variable: user
";

        public static readonly Message[] ScriptExpectedMessages = new Message[]
        {
            new Message { Kind = MessageKind.Error, Line = 3, SourceFile = "<stdin>", Text = "fatal: undefined reference to 'printf'"},
            new Message { Kind = MessageKind.Warning, Line = 0, SourceFile = "", Text = "undefined: 1 undefined references found" },
            new Message { Kind = MessageKind.Error, Line = 12, SourceFile = "<stdin>", Text =
@"syntax error: at symbol 'printf'
    parse error: syntax error, unexpected T_PAAMAYIM_NEKUDOTAYIM
    did you really mean to use the scope resolution op here?" },
            new Message { Kind = MessageKind.Error, Line = 35, SourceFile = "auth.c", Text = "fatal: Uncaught error: Undefined variable: user" }
        };

        [Fact]
        public void ScriptErrorTest()
        {
            var messages = ParseStderr(ScriptStderr).ToArray();
            Assert.Equal(ScriptExpectedMessages, messages);
        }
    }
}
