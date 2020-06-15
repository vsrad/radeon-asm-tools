using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense
{
    internal static class IntellisenseTokenDescription
    {
        public static object GetColorizedTokenDescription(DocumentAnalysis documentAnalysis, AnalysisToken token)
        {
            try
            {
                return GetColorizedDescription(documentAnalysis, token);
            }
            catch (Exception e)
            {
                Error.LogError(e, "Colorized description");
                return null;
            }
        }
        private static object GetColorizedDescription(DocumentAnalysis documentAnalysis, AnalysisToken token)
        {
            var version = documentAnalysis.CurrentSnapshot;
            string description = null;
            if (token.Type == RadAsmTokenType.FunctionName
                || token.Type == RadAsmTokenType.GlobalVariable
                || token.Type == RadAsmTokenType.LocalVariable
                || token.Type == RadAsmTokenType.Label)
            {
                var tokenSpan = token.TrackingToken.GetSpan(version);
                var line = version.GetLineFromPosition(tokenSpan.Start);
                var tokens = documentAnalysis.GetTokens(new Span(tokenSpan.End, line.EndIncludingLineBreak - tokenSpan.End));

                if (!GetDescriptionFromComment(documentAnalysis, version, tokens, out description))
                {
                    line = version.GetLineFromLineNumber(line.LineNumber - 1);
                    tokens = documentAnalysis.GetTokens(new Span(line.Start, line.EndIncludingLineBreak - line.Start));

                    GetDescriptionFromComment(documentAnalysis, version, tokens, out description);
                }
            }

            if (token.Type == RadAsmTokenType.FunctionName)
            {
                var fb = GetFunctionBlockByToken(documentAnalysis, token);
                if (fb == null)
                    return null;

                var addBrackets = version.GetAsmType() == AsmType.RadAsm2;
                var nameTextRuns = new List<ClassifiedTextRun>()
                {
                    new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Functions, token.TrackingToken.GetText(version)),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, addBrackets ? "(" : " "),
                };

                var arguments = fb.Tokens
                    .Where(t => t.Type == RadAsmTokenType.FunctionParameter)
                    .ToArray();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i == arguments.Length - 1)
                        nameTextRuns.Add(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, arguments[i].TrackingToken.GetText(version)));
                    else
                        nameTextRuns.Add(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, $"{arguments[i].TrackingToken.GetText(version)}, "));
                }

                if (addBrackets)
                    nameTextRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ")"));

                return GetDescriptionElement(token.Type.GetName(), new ClassifiedTextElement(nameTextRuns), description);
            }
            else if (token.Type == RadAsmTokenType.GlobalVariable || token.Type == RadAsmTokenType.LocalVariable)
            {
                var variable = (VariableToken)token;
                var nameTextRuns = new List<ClassifiedTextRun>()
                {
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, token.TrackingToken.GetText(version)),
                };

                if (variable.DefaultValue != TrackingToken.Empty)
                {
                    nameTextRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.FormalLanguage, " = "));
                    nameTextRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Number, variable.DefaultValue.GetText(version)));
                }

                return GetDescriptionElement(token.Type.GetName(), new ClassifiedTextElement(nameTextRuns), description);
            }
            else
            {
                return GetColorizedDescription(
                    token.Type,
                    token.TrackingToken.GetText(version),
                    description);
            }
        }

        public static object GetColorizedDescription(RadAsmTokenType tokenType, string tokenName, string description = null)
        {
            var typeName = tokenType.GetName();
            var nameElement = GetNameElement(tokenType, tokenName);
            return GetDescriptionElement(typeName, nameElement, description);
        }

        private static ClassifiedTextElement GetNameElement(RadAsmTokenType type, string tokenText)
        {
            switch (type)
            {
                case RadAsmTokenType.FunctionParameter:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, tokenText));
                case RadAsmTokenType.GlobalVariable:
                case RadAsmTokenType.LocalVariable:
                    return new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, tokenText));
                case RadAsmTokenType.Label:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Labels, tokenText));
                case RadAsmTokenType.Instruction:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Instructions, tokenText));
                default:
                    return new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Other, tokenText));
            }
        }

        private static ContainerElement GetDescriptionElement(string typeName, ClassifiedTextElement nameElement, string description)
        {
            var tokenElement = new ContainerElement(
                ContainerElementStyle.Wrapped,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $"({typeName}) ")
                ),
                nameElement
                );

            if (string.IsNullOrEmpty(description))
                return tokenElement;

            return new ContainerElement(
                    ContainerElementStyle.Stacked,
                    tokenElement,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "")
                    ),
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, description)
                    )
                );
        }

        private static FunctionBlock GetFunctionBlockByToken(DocumentAnalysis documentAnalysis, AnalysisToken functionToken)
        {
            foreach (var block in documentAnalysis.LastParserResult)
            {
                if (block.Type == BlockType.Function)
                {
                    var funcBlock = (FunctionBlock)block;
                    if (funcBlock.Name == functionToken)
                        return funcBlock;
                }
            }

            return null;
        }

        private static bool GetDescriptionFromComment(DocumentAnalysis documentAnalysis, ITextSnapshot version, IEnumerable<TrackingToken> tokens, out string description)
        {
            var commentTokens = tokens.Where(t => t.Type == documentAnalysis.LINE_COMMENT || t.Type == documentAnalysis.BLOCK_COMMENT);

            if (commentTokens.Any())
            {
                description = commentTokens
                    .First()
                    .GetText(version)
                    .Trim(new char[] { '/', '*', ' ', '\r', '\n' });
                return true;
            }
            else
            {
                description = null;
                return false;
            }
        }
    }
}
