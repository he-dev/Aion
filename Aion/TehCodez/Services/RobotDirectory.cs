using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reusable;

namespace Aion.Services
{
    internal static class RobotDirectory
    {        
        public static IEnumerable<string> GetVersions(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            const string searchPattern = "v*.*.*";
            return
                Directory
                    .GetDirectories(
                        path, 
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