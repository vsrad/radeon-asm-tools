using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.Helpers
{
    internal static class BaseBlockExtension
    {
        public static bool IsRadeonAsmContentType(this IBaseBlock block) =>
            block.BlockSpan.Snapshot.IsRadeonAsmContentType();

        public static bool IsRadeonAsm2ContentType(this IBaseBlock block) =>
            block.BlockSpan.Snapshot.IsRadeonAsm2ContentType();
    }
}
