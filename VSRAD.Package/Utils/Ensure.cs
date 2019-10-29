using System;
using System.Diagnostics;

namespace VSRAD.Package.Utils
{
    public static class Ensure
    {
        /* https://stackoverflow.com/a/32139346 */
        [DebuggerStepThrough]
        public static void ArgumentNotNull(object argument, string argumentName)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }
    }
}
