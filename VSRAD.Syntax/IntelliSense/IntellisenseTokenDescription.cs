using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.IntelliSense
{
    internal static class IntellisenseTokenDescription
    {
        public static object GetColorizedDescription(IBaseToken token)
        {
            if (token.TokenType == TokenType.Function)
            {
                var fb = ((FunctionToken)token).GetFunctionBlock();
                if (fb == null) 
                    return null;

                var nameTextRuns = new List<ClassifiedTextRun>()
                {
                    new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Functions, token.TokenName),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, token.SymbolSpan.Snapshot.IsRadeonAsm2ContentType() ? "(" : " "),
                };

                var arguments = fb.GetArgumentTokens().ToArray();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i == arguments.Length - 1)
                        nameTextRuns.Add(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, arguments[i].TokenName));
                    else
                        nameTextRuns.Add(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments,$"{arguments[i].TokenName}, "));
                }

                if (token.SymbolSpan.Snapshot.IsRadeonAsm2ContentType())
                    nameTextRuns.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ")"));

                return GetDescriptionElement(token.TokenType.GetName(), new ClassifiedTextElement(nameTextRuns), ((FunctionToken)token).Description);
            }
            else
            {
                return GetColorizedDescription(
                    token.TokenType, 
                    token.TokenName, 
                    (token as IDescriptionToken != null) ? ((IDescriptionToken)token).Description : null);
            }
        }

        public static object GetColorizedDescription(TokenType tokenType, string tokenName, string description = null)
        {
            var typeName = tokenType.GetName();
            var nameElement = GetNameElement(tokenType, tokenName);
            return GetDescriptionElement(typeName, nameElement, description);
        }

        private static ClassifiedTextElement GetNameElement(TokenType tokenType, string tokenName)
        {
            switch (tokenType)
            {
                case TokenType.Argument:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments, tokenName));
                case TokenType.GlobalVariable:
                case TokenType.LocalVariable:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Keywords, tokenName));
                case TokenType.Label:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Labels, tokenName));
                case TokenType.Instruction:
                    return new ClassifiedTextElement(new ClassifiedTextRun(SyntaxHighlighter.PredefinedClassificationTypeNames.Instructions, tokenName));
                default:
                    return new ClassifiedTextElement(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, tokenName));
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
    }
}
