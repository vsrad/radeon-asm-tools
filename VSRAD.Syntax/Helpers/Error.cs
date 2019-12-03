using Microsoft.VisualStudio.Shell;
using System;

namespace VSRAD.Syntax.Helpers
{
    public sealed class Error
    {
        public static void LogError(Exception e, string module = "")
        {
            var source = string.IsNullOrEmpty(module) ? Constants.RadeonAsmSyntaxContentType : $"{module} - {Constants.RadeonAsmSyntaxContentType}";
#if DEBUG
            ActivityLog.LogError(source, e.ToString());
#else
            ActivityLog.LogError(source, e.Message);
#endif
        }
    }
}
