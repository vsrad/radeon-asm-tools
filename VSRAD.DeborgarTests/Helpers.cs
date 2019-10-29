using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using VSRAD.Deborgar;
using Xunit;

namespace VSRAD.DeborgarTests
{
    static class Helpers
    {
        public static void VerifyBreakFrameLocation(Program program, string expectedFile, uint expectedLine)
        {
            Assert.Equal(VSConstants.S_OK, program.EnumFrameInfo(enum_FRAMEINFO_FLAGS.FIF_ARGS_ALL, nRadix: 16, out var frameEnum));
            var frameInfo = new FRAMEINFO[1];
            uint fetched = 0;
            Assert.Equal(VSConstants.S_OK, frameEnum.Next(1, frameInfo, ref fetched));
            var frame = frameInfo[0].m_pFrame;
            Assert.Equal(VSConstants.S_OK, frame.GetDocumentContext(out var context));
            Assert.Equal(VSConstants.S_OK, context.GetName(default, out var documentName));
            var position = new TEXT_POSITION[1];
            Assert.Equal(VSConstants.S_OK, context.GetStatementRange(position, position));

            Assert.Equal(expectedFile, documentName);
            Assert.Equal(expectedLine, position[0].dwLine);
        }
    }
}
