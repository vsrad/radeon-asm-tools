using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VSRAD.Package.Utils
{
    public static class RegexUtils
    {
        // https://stackoverflow.com/a/33017336
        public static async Task<string> ReplaceAsync(this Regex regex, string input, Func<Match, Task<string>> evaluator)
        {
            var sb = new StringBuilder();
            var lastIndex = 0;

            foreach (Match match in regex.Matches(input))
            {
                sb.Append(input, lastIndex, match.Index - lastIndex)
                  .Append(await evaluator(match).ConfigureAwait(false));

                lastIndex = match.Index + match.Length;
            }

            sb.Append(input, lastIndex, input.Length - lastIndex);
            return sb.ToString();
        }
    }
}
