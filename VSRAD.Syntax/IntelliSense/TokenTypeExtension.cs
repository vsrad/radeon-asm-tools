using System;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Adornments;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense
{
    internal static class TokenTypeExtension
    {
        private static readonly Guid ImageCatalogGuid = Guid.Parse(/* known image catalog guid */ "ae27a6b0-e345-4288-96df-5eaf394ee369");

        public static string GetName(this RadAsmTokenType type)
        {
            switch (type)
            {
                case RadAsmTokenType.FunctionName:
                case RadAsmTokenType.FunctionReference:
                    return "function";
                case RadAsmTokenType.FunctionParameter:
                case RadAsmTokenType.FunctionParameterReference:
                    return "parameter";
                case RadAsmTokenType.Label:
                case RadAsmTokenType.LabelReference:
                    return "label";
                case RadAsmTokenType.GlobalVariable:
                case RadAsmTokenType.GlobalVariableReference:
                    return "global variable";
                case RadAsmTokenType.LocalVariable:
                case RadAsmTokenType.LocalVariableReference:
                    return "local variable";
                case RadAsmTokenType.Instruction:
                    return "instruction";
                default:
                    throw new ArgumentException("Invalid input type");
            }
        }

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
                case RadAsmTokenType.GlobalVariable:
                case RadAsmTokenType.GlobalVariableReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.FieldPublic);

                case RadAsmTokenType.Label:
                case RadAsmTokenType.LabelReference:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.Label);

                case RadAsmTokenType.Instruction:
                    return new ImageId(ImageCatalogGuid, KnownImageIds.Assembly);

                default:
                    throw new ArgumentException("Invalid input type");
            }
        }
    }
}
