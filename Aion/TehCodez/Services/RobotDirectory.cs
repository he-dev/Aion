using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reusable;

namespace Aion.Services
{
    internal static class RobotDirectory
    {
        public static string CreateMainDirectoryName(string directoryName, string fileName)
        {
            if (directoryName == null) throw new ArgumentNullException(nameof(directoryName));
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));

            return 
                Path.Combine(
                    directoryName,
                    Path.GetFileNameWithoutExtension(fileName));
        }

        public static IEnumerable<string> GetVersionDirectories(string mainDirectoryName)
        {
            if (mainDirectoryName == null) throw new ArgumentNullException(nameof(mainDirectoryName));

            const string searchPattern = "v*.*.*";
            return
                Directory
                    .GetDirectories(
                        mainDirectoryName, 
                        searchPattern);
        }

        public static string GetLatestVersion(this IEnumerable<string> directoryNames)
        {
            if (directoryNames == null) throw new ArgumentNullException(nameof(directoryNames));

            return 
                directoryNames
                    .Select(Path.GetFileName)
                    .LatestVersion();
        }

        private static string LatestVersion(this IEnumerable<string> versions)
        {
            IEnumerable<SemanticVersion> ParseVersions()
            {
                foreach (var version in versions ?? throw new ArgumentNullException(nameof(versions)))
                {
                    if (SemanticVersion.TryParse(version, out SemanticVersion semVer)) yield return semVer;
                }
            }

            return 
                ParseVersions()
                    .OrderByDescending(x => x)
                    .FirstOrDefault();
        }
    }
}