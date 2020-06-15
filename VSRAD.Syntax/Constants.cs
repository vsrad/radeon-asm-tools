using System.Collections.Generic;

namespace VSRAD.Syntax
{
    static class Constants
    {
        /*
         * Guid const definition
         */
        internal const string PackageGuid = "bb4a9205-af03-4a7e-8d30-7a8649cc70a4";
        internal const string FunctionListToolWindowPaneGuid = "7220fd29-7d89-42ae-a15c-c4fc6889b54f";
        internal const string NavigationListToolWindowPaneGuid = "c518eee2-289b-47cf-a877-d48a55f13f9b";
        internal const string FunctionListCommandSetGuid = "a1d46795-2324-4b58-9f8c-aa69414c3e9e";
        internal const string NavigationListCommandSetGuid = "0c7aa63d-bc2a-4d0e-83c2-80bbb30a5ceb";

        /*
         * Command ID definition
         */
        internal const int FunctionListCommandId = 0x0100;
        internal const int ClearSearchFieldCommandId = 0x0200;
        internal const int SelectItemCommandId = 0x0201;
        internal const int FunctionListMenu = 0x1000;
        internal const int FunctionListGroup = 0x1100;
        internal const int ShowHideLineNumberCommandId = 0x102;
        internal const int NavigationListCommandId = 0x0203;

        /*
         * File extensions definition
         */
        internal const string FileExtensionInc = ".inc";
        internal const string FileExtensionS = ".s";
        internal const string FileExtensionAsm1 = ".gas";
        internal const string FileExtensionAsm2 = ".asm2";
        internal const string FileExtensionAsm1Doc = ".radasm1";
        internal const string FileExtensionAsm2Doc = ".radasm2";
        internal static readonly List<string> DefaultFileExtensionAsm1 = new List<string>()
        {
            FileExtensionAsm1,
            FileExtensionInc,
            FileExtensionS
        };
        internal static readonly List<string> DefaultFileExtensionAsm2 = new List<string>()
        {
            FileExtensionAsm2
        };

        /*
         * Content type definition
         */
        internal const string RadeonAsmSyntaxBaseContentType = "code";
        internal const string RadeonAsmSyntaxContentType = "RadeonAsmSyntax";
        internal const string RadeonAsm2SyntaxContentType = "RadeonAsm2Syntax";
        internal const string RadeonAsmDocumentationContentType = "RadeonAsmDocumentation";

        /*
         * RadeonAsmSyntax options definition
         */
        internal const string RadeonAsmOptionsBasePageName = "General";
        internal const string RadeonAsmOptionsInstructionListPageName = "Instruction list";
        internal const string RadeonAsmOptionsCategoryName = "RadeonAsm options";

        /*
         * Indent guide adornment layer
         */
        internal const string IndentGuideAdornmentLayerName = "RadeonAsmIndentGuide";

        /*
         * Completion set name
         */
        internal const string CompletionSetName = "RadAsm";
    }
}
