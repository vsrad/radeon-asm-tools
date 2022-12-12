using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    class PathExtension
    {
        private static bool IsDirectorySeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        internal static bool AreRootsEqual(string first, string second, StringComparison comparisonType)
        {
            int firstRootLength = GetRootLength(first);
            int secondRootLength = GetRootLength(second);

            return firstRootLength == secondRootLength
                   && string.Compare(
                       strA: first,
                       indexA: 0,
                       strB: second,
                       indexB: 0,
                       length: firstRootLength,
                       comparisonType: comparisonType) == 0;
        }

        internal static int GetRootLength(string path)
        {
            int i = 0;
            int volumeSeparatorLength = 2; // Length to the colon "C:"
            int uncRootLength = 2; // Length to the start of the server name "\\"

            bool extendedSyntax = path.StartsWith(ExtendedDevicePathPrefix);
            bool extendedUncSyntax = path.StartsWith(UncExtendedPathPrefix);
            if (extendedSyntax)
            {
                // Shift the position we look for the root from to account for the extended prefix
                if (extendedUncSyntax)
                {
                    // "\\" -> "\\?\UNC\"
                    uncRootLength = UncExtendedPathPrefix.Length;
                }
                else
                {
                    // "C:" -> "\\?\C:"
                    volumeSeparatorLength += ExtendedDevicePathPrefix.Length;
                }
            }

            if ((!extendedSyntax || extendedUncSyntax) && path.Length > 0 && IsDirectorySeparator(path[0]))
            {
                // UNC or simple rooted path (e.g. "\foo", NOT "\\?\C:\foo")

                i = 1; //  Drive rooted (\foo) is one character
                if (extendedUncSyntax || (path.Length > 1 && IsDirectorySeparator(path[1])))
                {
                    // UNC (\\?\UNC\ or \\), scan past the next two directory separators at most
                    // (e.g. to \\?\UNC\Server\Share or \\Server\Share\)
                    i = uncRootLength;
                    int n = 2; // Maximum separators to skip
                    while (i < path.Length && (!IsDirectorySeparator(path[i]) || --n > 0)) i++;
                }
            }
            else if (path.Length >= volumeSeparatorLength &&
                     path[volumeSeparatorLength - 1] == VolumeSeparatorChar)
            {
                // Path is at least longer than where we expect a colon, and has a colon (\\?\A:, A:)
                // If the colon is followed by a directory separator, move past it
                i = volumeSeparatorLength;
                if (path.Length >= volumeSeparatorLength + 1 && IsDirectorySeparator(path[volumeSeparatorLength])) i++;
            }

            return i;
        }

        internal static int GetCommonPathLength(string first, string second, bool ignoreCase)
        {
            int commonChars = EqualStartingCharacterCount(first, second, ignoreCase: ignoreCase);

            // If nothing matches
            if (commonChars == 0)
                return commonChars;

            // Or we're a full string and equal length or match to a separator
            if (commonChars == first.Length
                && (commonChars == second.Length || IsDirectorySeparator(second[commonChars])))
                return commonChars;

            if (commonChars == second.Length && IsDirectorySeparator(first[commonChars]))
                return commonChars;

            // It's possible we matched somewhere in the middle of a segment e.g. C:\Foodie and C:\Foobar.
            while (commonChars > 0 && !IsDirectorySeparator(first[commonChars - 1]))
                commonChars--;

            return commonChars;
        }

        private static unsafe int EqualStartingCharacterCount(string first, string second, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return 0;

            int commonChars = 0;

            fixed (char* f = first)
            fixed (char* s = second)
            {
                char* l = f;
                char* r = s;
                char* leftEnd = l + first.Length;
                char* rightEnd = r + second.Length;

                while (l != leftEnd && r != rightEnd
                                    && (*l == *r || (ignoreCase &&
                                                     char.ToUpperInvariant((*l)) == char.ToUpperInvariant((*r)))))
                {
                    commonChars++;
                    l++;
                    r++;
                }
            }

            return commonChars;
        }

        private static bool EndsInDirectorySeparator(string path) 
            => path.Length > 0 && IsDirectorySeparator(path[path.Length - 1]);

        public static string GetRelativePath(string relativeTo, string path)
        {
            return GetRelativePath(relativeTo, path, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRelativePath(string relativeTo, string path, StringComparison comparisonType)
        {
            
            if (String.IsNullOrEmpty(relativeTo))
                throw new ArgumentNullException(nameof(relativeTo));

            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            Debug.Assert(comparisonType == StringComparison.Ordinal || comparisonType == StringComparison.OrdinalIgnoreCase);

            relativeTo = Path.GetFullPath(relativeTo);
            path = Path.GetFullPath(path);

            // Need to check if the roots are different- if they are we need to return the "to" path.
            if (!AreRootsEqual(relativeTo, path, comparisonType))
                return path;

            int commonLength = GetCommonPathLength(relativeTo, path, ignoreCase: comparisonType == StringComparison.OrdinalIgnoreCase);

            // If there is nothing in common they can't share the same root, return the "to" path as is.
            if (commonLength == 0)
                return path;

            // Trailing separators aren't significant for comparison
            int relativeToLength = relativeTo.Length;
            if (EndsInDirectorySeparator(relativeTo))
                relativeToLength--;

            bool pathEndsInSeparator = EndsInDirectorySeparator(path);
            int pathLength = path.Length;
            if (pathEndsInSeparator)
                pathLength--;

            // If we have effectively the same path, return "."
            if (relativeToLength == pathLength && commonLength >= relativeToLength) return ".";

            // We have the same root, we need to calculate the difference now using the
            // common Length and Segment count past the length.
            //
            // Some examples:
            //
            //  C:\Foo C:\Bar L3, S1 -> ..\Bar
            //  C:\Foo C:\Foo\Bar L6, S0 -> Bar
            //  C:\Foo\Bar C:\Bar\Bar L3, S2 -> ..\..\Bar\Bar
            //  C:\Foo\Foo C:\Foo\Bar L7, S1 -> ..\Bar

            var sb = new StringBuilder();
            sb.EnsureCapacity(Math.Max(relativeTo.Length, path.Length));

            // Add parent segments for segments past the common on the "from" path
            if (commonLength < relativeToLength)
            {
                sb.Append("..");

                for (int i = commonLength + 1; i < relativeToLength; i++)
                {
                    if (IsDirectorySeparator(relativeTo[i]))
                    {
                        sb.Append(Path.DirectorySeparatorChar);
                        sb.Append("..");
                    }
                }
            }
            else if (IsDirectorySeparator(path[commonLength]))
            {
                // No parent segments and we need to eat the initial separator
                //  (C:\Foo C:\Foo\Bar case)
                commonLength++;
            }

            // Now add the rest of the "to" path, adding back the trailing separator
            int differenceLength = pathLength - commonLength;
            if (pathEndsInSeparator)
                differenceLength++;

            if (differenceLength > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(Path.DirectorySeparatorChar);
                }

                sb.Append(path,commonLength, differenceLength);
            }

            return sb.ToString();
        }

        private const string ExtendedDevicePathPrefix = @"\\?\";
        private const string UncExtendedPathPrefix = @"\\?\UNC\";
        private const char VolumeSeparatorChar = ':';
    }
}
