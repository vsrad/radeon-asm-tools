using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense
{
    internal static class IntellisenseHelper
    {
        /// <summary>
        /// Returns the description text of the definition in the comment to the right or above the definition.
        /// Must be on UI thread
        /// </summary>
        /// <returns>Description text if exists otherwise null</returns>
        public static string GetDescription(this IDefinitionToken token)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceProvider = ServiceProvider.GlobalProvider;
            var documentFactory = serviceProvider.GetMefService<IDocumentFactory>();

            return documentFactory != null 
                ? GetDescription(token, documentFactory)
                : null;
        }

        /// <summary>
        /// Returns the description text of the definition in the comment to the right or above the definition.
        /// </summary>
        /// <returns>Description text if exists otherwise null</returns>
        public static string GetDescription(this IDefinitionToken token, IDocumentFactory documentFactory)
        {
            var tokenEnd = token.Span.End;
            var snapshot = token.Span.Snapshot;

            var document = documentFactory.GetOrCreateDocument(snapshot.TextBuffer);
            if (document == null)
                return null;

            var tokenizer = document.DocumentTokenizer;
            var tokenizerResult = tokenizer.CurrentResult;

            if (tokenizerResult.Snapshot != snapshot)
                return null;

            return TryGetDescriptionToTheRight(tokenizer, tokenizerResult, tokenEnd)
                   ?? TryGetDescriptionAbove(tokenizer, tokenizerResult, tokenEnd);
        }

        private static string TryGetDescriptionToTheRight(IDocumentTokenizer tokenizer, ITokenizerResult tokenizerResult, SnapshotPoint tokenEnd)
        {
            var currentLine = tokenEnd.GetContainingLine();
            var currentLineComment = tokenizerResult
                .GetTokens(new Span(tokenEnd, currentLine.End - tokenEnd))
                .Where(t => tokenizer.IsTypeOf(t.Type, RadAsmTokenType.Comment))
                .ToList();

            if (currentLineComment.Count == 0)
                return null;

            var text = currentLineComment
                .First()
                .GetText(tokenEnd.Snapshot);

            return GetCommentText(text);
        }

        private static string TryGetDescriptionAbove(IDocumentTokenizer tokenizer, ITokenizerResult tokenizerResult, SnapshotPoint tokenEnd)
        {
            var lines = new LinkedList<string>();
            var currentLineNumber = tokenEnd.GetContainingLine().LineNumber - 1;
            var snapshot = tokenEnd.Snapshot;

            while (currentLineNumber >= 0)
            {
                var currentLine = snapshot.GetLineFromLineNumber(currentLineNumber);
                var currentLineComment = tokenizerResult
                    .GetTokens(new Span(currentLine.Start, currentLine.Length))
                    .ToList();

                // if there are some other tokens, then the comment ended
                if (currentLineComment.Any(t =>
                    !tokenizer.IsTypeOf(t.Type, RadAsmTokenType.Comment) &&
                    !tokenizer.IsTypeOf(t.Type, RadAsmTokenType.Whitespace)))
                    break;

                currentLineComment.RemoveAll(t =>
                    tokenizer.IsTypeOf(t.Type, RadAsmTokenType.Whitespace));

                if (currentLineComment.Count != 1)
                    break;

                var trackingToken = currentLineComment.First();
                var tokenSpan = new SnapshotSpan(snapshot, trackingToken.GetSpan(snapshot));
                var tokenText = GetCommentText(tokenSpan.GetText());

                lines.AddFirst(tokenText);
                currentLineNumber = tokenSpan.Start.GetContainingLine().LineNumber - 1;
            }

            return lines.Count != 0
                ? string.Join(System.Environment.NewLine, lines)
                : null;
        }

        private static bool IsTypeOf(this IDocumentTokenizer tokenizer, int tokenType,
            RadAsmTokenType targetTokenType) =>
            tokenizer.GetTokenType(tokenType) == targetTokenType;

        private static string GetCommentText(string text)
        {
            var comment = text.Trim('/', '*', ' ', '\t', '\r', '\n', '\f');
            return Regex.Replace(comment, @"(?<=\n)\s*(\*|\/\/)", "", RegexOptions.Compiled);
        }
    }

    internal static class TokenTypeExtension
    {
        public static ImageElement GetImageElement(this RadAsmTokenType type) =>
            new ImageElement(GetImageId(type));

        private static ImageId GetImageId(RadAsmTokenType type)
        {
            switch (type)
            {
                case RadAsmTokenType.FunctionName:
                case RadAsmTokenType.FunctionReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.MethodPublic);

                case RadAsmTokenType.FunctionParameter:
                case RadAsmTokenType.FunctionParameterReference:
                case RadAsmTokenType.LocalVariable:
                case RadAsmTokenType.LocalVariableReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.LocalVariable);

                case RadAsmTokenType.GlobalVariable:
                case RadAsmTokenType.GlobalVariableReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.ConstantPublic);

                case RadAsmTokenType.Label:
                case RadAsmTokenType.LabelReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.Label);

                case RadAsmTokenType.Instruction:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.Assembly);

                default:
                    throw new ArgumentException("Invalid RadAsmTokenType");
            }
        }

        private static readonly Guid ImageCatalogGuid = Guid.Parse(/* image catalog guid */ "ae27a6b0-e345-4288-96df-5eaf394ee369");
    }
}
