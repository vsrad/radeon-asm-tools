﻿using Microsoft.VisualStudio.Language.StandardClassification;
using System.Collections.Generic;

namespace VSRAD.Syntax.Core.Tokens
{
    public enum RadAsmTokenType
    {
        Comment,
        Identifier,
#pragma warning disable CA1720 // disable Identifier contains type name
        String,
#pragma warning restore CA1720
        Number,
        Operation,
        Structural,
        Comma,
        Semi,
        Colon,
        Lparen,
        Rparen,
        LsquareBracket,
        RsquareBracket,
        LcurveBracket,
        RcurveBracket,
        Whitespace,
        Keyword,
        Preprocessor,
        PreprocessorMacro,
        PreprocessorMacroReference,
        Unknown,
        BuiltinFunction,
        FunctionName,
        FunctionReference,
        FunctionParameter,
        FunctionParameterReference,
        Label,
        LabelReference,
        Instruction,
        GlobalVariable,
        GlobalVariableReference,
        LocalVariable,
        LocalVariableReference,
        Include,
    }

    public static class RadAsmTokenTypeExtension
    {
        public static string GetName(this RadAsmTokenType type)
        {
            switch (type)
            {
                case RadAsmTokenType.PreprocessorMacro:
                    return "preprocessor macro";
                case RadAsmTokenType.FunctionParameter:
                    return "argument";
                case RadAsmTokenType.Label:
                    return "label";
                case RadAsmTokenType.GlobalVariable:
                    return "global variable";
                case RadAsmTokenType.LocalVariable:
                    return "local variable";
                case RadAsmTokenType.FunctionName:
                    return "function";
                case RadAsmTokenType.Instruction:
                    return "instruction";
                case RadAsmTokenType.BuiltinFunction:
                    return "built-in function";
                default:
                    return "unknown";
            }
        }

        private static readonly Dictionary<RadAsmTokenType, string> _classificationTypeNames = new Dictionary<RadAsmTokenType, string>()
        {
            { RadAsmTokenType.Comment, PredefinedClassificationTypeNames.Comment },
            { RadAsmTokenType.Identifier, PredefinedClassificationTypeNames.Identifier },
            { RadAsmTokenType.String, PredefinedClassificationTypeNames.String },
            { RadAsmTokenType.Number, PredefinedClassificationTypeNames.Number },
            { RadAsmTokenType.Operation, PredefinedClassificationTypeNames.Operator },
            { RadAsmTokenType.Structural, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Comma, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Semi, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Colon, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Lparen, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Rparen, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.LsquareBracket, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.RsquareBracket, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.LcurveBracket, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.RcurveBracket, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Whitespace, PredefinedClassificationTypeNames.WhiteSpace },
            { RadAsmTokenType.Keyword, PredefinedClassificationTypeNames.Keyword },
            { RadAsmTokenType.Preprocessor, PredefinedClassificationTypeNames.PreprocessorKeyword },
            { RadAsmTokenType.PreprocessorMacro, "cppMacro" },
            { RadAsmTokenType.PreprocessorMacroReference, "cppMacro" },
            { RadAsmTokenType.Unknown, PredefinedClassificationTypeNames.Other },
            { RadAsmTokenType.FunctionName, SyntaxHighlighter.PredefinedClassificationTypeNames.Functions },
            { RadAsmTokenType.FunctionReference, SyntaxHighlighter.PredefinedClassificationTypeNames.Functions },
            { RadAsmTokenType.FunctionParameter, SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments },
            { RadAsmTokenType.FunctionParameterReference, SyntaxHighlighter.PredefinedClassificationTypeNames.Arguments },
            { RadAsmTokenType.Label, SyntaxHighlighter.PredefinedClassificationTypeNames.Labels },
            { RadAsmTokenType.LabelReference, SyntaxHighlighter.PredefinedClassificationTypeNames.Labels },
            { RadAsmTokenType.GlobalVariable, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.GlobalVariableReference, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.LocalVariable, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.LocalVariableReference, PredefinedClassificationTypeNames.FormalLanguage },
            { RadAsmTokenType.Instruction, SyntaxHighlighter.PredefinedClassificationTypeNames.Instructions },
            { RadAsmTokenType.BuiltinFunction, PredefinedClassificationTypeNames.Keyword }
        };

        public static string GetClassificationTypeName(this RadAsmTokenType tokenType) =>
            _classificationTypeNames[tokenType];
    }
}
