using System;
using System.Collections.Generic;

namespace VSRAD.Package.BuildTools.Errors
{
    public static class LineMapper
    {
        public static string MapSourceToHost(string remotePath, IEnumerable<string> projectPaths)
        {
            var remotePathArray = remotePath.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
            Array.Reverse(remotePathArray);
            string probablePath = remotePath;
            int longestMatch = 0;
            foreach (var path in projectPaths)
            {
                var pathArray = path.Split(new[] { @"\", @"/" }, StringSplitOptions.None);
                Array.Reverse(pathArray);
                int matchCount = 0;

                while (matchCount < pathArray.Length && matchCount < remotePathArray.Length && remotePathArray[matchCount] == pathArray[matchCount])
                    matchCount++;

                if (matchCount > longestMatch)
                {
                    probablePath = path;
                    longestMatch = matchCount;
                }
            }
            return probablePath;
        }
    }
}
