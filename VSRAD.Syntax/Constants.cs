using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VSRAD.Syntax
{
    static class Constants
    {
        /*
         * Guid const definition
         */
        internal const string PackageGuid = "bb4a9205-af03-4a7e-8d30-7a8649cc70a4";
        internal const string FunctionListToolWindowPaneGuid = "7220fd29-7d89-42ae-a15c-c4fc6889b54f";
        internal const string FunctionListCommandSetGuid = "a1d46795-2324-4b58-9f8c-aa69414c3e9e";

        /*
         * Command ID definition
         */
        internal const int FunctionListCommandId = 0x0100;
        internal const int ClearSearchFieldCommandId = 0x0200;
        internal const int SelectItemCommandId = 0x0201;
        internal const int FunctionListMenu = 0x1000;
        internal const int FunctionListGroup = 0x1100;
        internal const int ShowHideLineNumberCommandId = 0x102;

        /*
         * File extensions definition
         */
        internal const string InstructionsFileExtension = ".radasm";

        internal const string FileExtensionInc = ".inc";
        internal const string FileExtensionS = ".s";
        internal const string FileExtensionAsm1 = ".gas";
        internal const string FileExtensionAsm2 = ".asm2";
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

        /*
         * RadeonAsmSyntax options definition
         */
        internal const string RadeonAsmOptionsBasePageName = "General";
        internal const string RadeonAsmOptionsInstructionListPageName = "Instruction list";
        internal const string RadeonAsmOptionsCategoryName = "RadeonAsm options";

        #region syntax constants
        internal static readonly string[] preprocessorStart =
        {
            "#if",
            "#ifdef",
            "#ifndef",
        };
        internal static readonly string[] preprocessorMiddle =
        {
            "#else",
            "#elsif",
            "#elif",
        };
        internal static readonly string[] preprocessorEnd =
        {
            "#endif",
        };
        internal static readonly string[] preprocessorKeywords =
        {
            "#include",
            "#define",
            "#undef",
            "#pragma",
            "#error",
            "#import",
            "#include_next",
            "#line",
            "#warning",
        };

        internal static readonly string[] asm1Start =
        {
            ".if",
            ".ifdef",
            ".ifb",
            ".ifc",
            ".ifeq",
            ".ifeqs",
            ".ifge",
            ".ifgt",
            ".ifle",
            ".iflt",
            ".ifnb",
            ".ifnc",
            ".ifndef",
            ".ifnotdef",
            ".ifne",
            ".ifnes",
            ".rept",
            ".irp",
            ".irpc",
            ".def",
        };
        internal static readonly string[] asm1Middle =
        {
            ".elseif",
            ".else",
        };
        internal static readonly string[] asm1End =
        {
            ".endm",
            ".endr",
            ".endef",
            ".endif",
        };
        internal static readonly string[] asm1Keywords =
        {
            ".byte",
            ".short",
            ".text",
            ".long",
            ".exitm",
            ".include",
            ".set",
            ".altmacro",
            ".noaltmacro",
            ".local",
            ".line",
            ".size",
            ".ln",
            ".nops",
            ".error",
            ".end",
        };
        internal static readonly string[] asm1ExtraKeywords =
        {
            ".hsa_code_object_version",
            ".hsa_code_object_isa",
            ".amdgpu_hsa_kernel",
            ".amd_kernel_code_t",
            ".end_amd_kernel_code_t",
        };
        internal const string asm1CommentStart = "//";
        internal const string asm1MultilineCommentStart = "/*";
        internal const string asm1MultilineCommentEnd = "*/";
        internal const string asm1FunctionKeyword = ".macro";
        internal static readonly Regex asm1FunctionDefinitionRegular = new Regex(@"\.macro\s+(?<func_name>[\w.]+)");
        internal static readonly Dictionary<string, Regex> asm1VariableDefinition = new Dictionary<string, Regex>
        {
            { ".set", new Regex(@"\.set\s+(?<var_name>\.?\w+)")},
            { " = ", new Regex(@"(?<!\\)(?<var_name>\.?\w+)\s+=[^=]")},
        };
        internal static readonly Regex asm1LabelDefinitionRegular = new Regex(@"(?<label_name>[\w.]+):");
        #endregion

        internal const string RadeonAsm2SyntaxContentType = "RadeonAsm2Syntax";

        #region syntax constants
        internal static readonly string[] asm2Start =
        {
            "if",
            "for",
            "while",
            "repeat",
        };
        internal static readonly string[] asm2Middle =
        {
            "elsif",
            "else",
        };
        internal static readonly string[] asm2End =
        {
            "end",
            "until",
        };
        internal static readonly string[] asm2Keywords =
        {
            "var",
            "vmcnt",
            "expcnt",
            "lgkmcnt",
            "hwreg",
            "sendmsg",
            "asic",
            "type",
            "assert",
        };
        internal const string asm2CommentStart = asm1CommentStart;
        internal const string asm2MultilineCommentStart = asm1MultilineCommentStart;
        internal const string asm2MultilineCommentEnd = asm1MultilineCommentEnd;
        internal const string asm2FunctionKeyword = "function";
        internal static readonly Regex asm2FunctionDefinitionRegular = new Regex(@"function\s+(?<func_name>[\w.]+)");
        internal const string asm2FunctionDeclorationStartPattern = "(";
        internal const string asm2FunctionDefinitionEndPattern = ")";
        internal static readonly Dictionary<string, Regex> asm2VariableDefinition = new Dictionary<string, Regex>
        {
            { "var", new Regex(@"var\s+(?<var_name>\.?\w+)")},
        };
        internal static readonly Regex asm2LabelDefinitionRegular = asm1LabelDefinitionRegular;
        #endregion

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
