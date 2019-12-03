using Microsoft.VisualStudio.Shell;
using System;

namespace VSRAD.Syntax.Helpers
{
    public sealed class Error
    {
        public static void LogError(Exception e)
        {
#if DEBUG
            ActivityLog.LogError(Constants.RadeonAsmSyntaxContentType, e.ToString());
#else
            ActivityLog.LogError(Constants.RadeonAsmSyntaxContentType, e.Message);
#endif
        }
    }
}
