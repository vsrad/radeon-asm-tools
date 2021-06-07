using System;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Adornments;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.Completion.Providers
{
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
