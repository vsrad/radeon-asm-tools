using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    public static class TextTagListExtension
    {
        public static void AddTag(this IList<TextTag> tags, RadAsmTokenType type, int start, int length) =>
            tags.Add(new TextTag(type, new Span(start, length)));

        public static void AddTag(this IList<TextTag> tags, RadAsmTokenType type, Span span) =>
            tags.Add(new TextTag(type, span));

        public static int GetCurrentParameter(this SnapshotSpan span, char splitChar)
        {
            var searchText = span.GetText();
            return searchText.Split(splitChar).Length - 1;
        }
    }
}
