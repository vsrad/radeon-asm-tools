namespace VSRAD.Package.Utils
{
    public static class StringExtensions
    {
        public static bool StartsWith(this string str, char c)
        {
            return str.Length > 0 && str[0] == c;
        }

        public static bool EndsWith(this string str, char c)
        {
            return str.Length > 0 && str[str.Length - 1] == c;
        }
    }
}
