using System.IO;
using VSRAD.Package.Options;

namespace VSRAD.Package.Utils
{
    public static class StringUtils
    {
        public static string GetFullPath(this OutputFile file) => string.Join(Path.DirectorySeparatorChar.ToString(), file.Path);

        public static string AppendByDelimiter(this string[] values, char delimiter)
        {
            var builder = new System.Text.StringBuilder();
            bool delimiterNeeded = false;
            foreach (var value in values)
            {
                if (delimiterNeeded)
                {
                    builder.Append(" ");
                    builder.Append(delimiter);
                    builder.Append(" ");
                }
                else
                    delimiterNeeded = true;

                builder.Append(value);
            }

            return builder.ToString();
        }
    }
}
