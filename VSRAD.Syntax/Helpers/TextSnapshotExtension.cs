using System;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.Helpers
{
    [Flags]
#pragma warning disable CA1714 // Flags enums should have plural names
    public enum AsmType
#pragma warning restore CA1714 // Flags enums should have plural names
    {
        RadAsm = 1,
        RadAsm2 = 2,
        RadAsmDoc = 4,
        Unknown = 8,
        RadAsmCode = RadAsm | RadAsm2,
    }

    internal static class TextSnapshotExtension
    {
        internal static AsmType GetAsmType(this ITextSnapshot textSnapshot)
        {
            if (textSnapshot.ContentType.IsOfType(Constants.RadeonAsmDocumentationContentType))
                return AsmType.RadAsmDoc;
            if (textSnapshot.ContentType.IsOfType(Constants.RadeonAsm2SyntaxContentType))
                return AsmType.RadAsm2;
            if (textSnapshot.ContentType.IsOfType(Constants.RadeonAsmSyntaxContentType))
                return AsmType.RadAsm;

            return AsmType.Unknown;
        }

        internal static AsmType GetAsmType(this ITextBuffer textBuffer) =>
            GetAsmType(textBuffer.CurrentSnapshot);

        internal static bool GetTextDocument(this ITextBuffer textBuffer, out ITextDocument textDocument) =>
            textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out textDocument);
    }
}
