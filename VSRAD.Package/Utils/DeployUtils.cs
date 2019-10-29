using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VSRAD.Package.Utils
{
    public static class DeployUtils
    {
        private const char separator = ';';

        public static IEnumerable<string> GetPathsSemicolonSeparated(this string paths) =>
            paths.Split(separator);

        public static IEnumerable<string> GetFilePaths(this IEnumerable<string> paths) =>
            paths.Where(File.Exists);

        public static IEnumerable<string> GetDirectoriesPaths(this IEnumerable<string> paths) =>
            paths.Where(Directory.Exists);
    }
}
