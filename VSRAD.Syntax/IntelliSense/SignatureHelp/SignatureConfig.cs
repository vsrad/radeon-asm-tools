using System;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal sealed class SignatureConfig
    {
        private static readonly Lazy<SignatureConfig> Asm1SignatureConfigLazy =
            new Lazy<SignatureConfig>(() => new SignatureConfig(' ', ' ', ',', '\n', " ", " "));

        private static readonly Lazy<SignatureConfig> Asm2SignatureConfigLazy =
            new Lazy<SignatureConfig>(() => new SignatureConfig(' ', '(', ',', ')', " (", ")"));

        public static SignatureConfig Asm1Instance => Asm1SignatureConfigLazy.Value;
        public static SignatureConfig Asm2Instance => Asm2SignatureConfigLazy.Value;

        public static SignatureConfig GetSignature(AsmType asmType)
        {
            switch (asmType)
            {
                case AsmType.RadAsm: return Asm1Instance;
                case AsmType.RadAsm2: return Asm2Instance;
                default: return null;
            }
        }

        public SignatureConfig(char triggerInstructionSigChar, char triggerFunctionSigChar, char triggerParamChar, char dismissSigChar,
            string sigStart, string sigEnd)
        {
            TriggerInstructionSignatureChar = triggerInstructionSigChar;
            TriggerFunctionSignatureChar = triggerFunctionSigChar;
            TriggerParameterChar = triggerParamChar;
            DismissSignatureChar = dismissSigChar;
            SignatureStart = sigStart;
            SignatureEnd = sigEnd;
        }

        public char TriggerInstructionSignatureChar { get; }
        public char TriggerFunctionSignatureChar { get; }
        public char TriggerParameterChar { get; }
        public char DismissSignatureChar { get; }

        public string SignatureStart { get; }
        public string SignatureEnd { get; }

        public bool Enabled { get; set; }
    }
}
