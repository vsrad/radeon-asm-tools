using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Helpers
{
    internal static class TextSnapshotExtension
    {
        public static bool IsRadeonAsmContentType(this ITextSnapshot textSnapshot) =>
            textSnapshot.ContentType.IsOfType(Constants.RadeonAsmSyntaxContentType);

        public static bool IsRadeonAsm2ContentType(this ITextSnapshot textSnapshot) =>
            textSnapshot.ContentType.IsOfType(Constants.RadeonAsm2SyntaxContentType);

        internal static bool IsRadeonAsmOrAsm2ContentType(this ITextSnapshot buffer) =>
            buffer.ContentType.IsOfType(Constants.RadeonAsmSyntaxContentType) || buffer.ContentType.IsOfType(Constants.RadeonAsm2SyntaxContentType);
    }
}
