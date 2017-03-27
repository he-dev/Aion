using Reusable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aion.Services
{
    internal delegate IEnumerable<string> GetDirectoriesCallback(string path, string searchPattern);

    internal class RobotDirectory
    {
        private readonly GetDirectoriesCallback _getDirectories;

        public const string SearchPattern = "v*.*.*";

        public RobotDirectory(GetDirectoriesCallback getDirectories)
        {
            _getDirectories = getDirectories ?? throw new ArgumentNullException(nameof(getDirectories));
        }

        public RobotDirectory() : this(Directory.GetDirectories) { }

        // Builds robot-file-name e.g.:
        // robotsDirectoryName: "C:\Robots"
        // RobotConfig.FileName: Aion.TestRobot1.exe
        // FullPath: C:\Robots\Aion.TestRobot1\v1.0.2\Aion.TestRobot1.exe

        public string GetRobotFileName(string path, string fileName)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (Path.IsPathRooted(fileName)) return fileName;

            var versions = GetVersions(Path.Combine(path, Path.GetFileNameWithoutExtension(fileName)));
            var latestVersion = FindLatestVersion(versions);

            return string.IsNullOrEmpty(latestVersion) ? null : Path.Combine(
                path,
                Path.GetFileNameWithoutExtension(fileName),
                $"v{latestVersion}",
                fileName
            );
        }

        public IEnumerable<string> GetVersions(string path)
        {
            return _getDirectories(path, SearchPattern).Select(x => x.Split('\\').Last());
        }

        public static string FindLatestVersion(IEnumerable<string> versions)
        {
            return ParseVersions().OrderByDescending(x => x).FirstOrDefault();

            IEnumerable<SemanticVersion> ParseVersions()
            {
                foreach (var version in versions ?? throw new ArgumentNullException(nameof(versions)))
                {
                    if (SemanticVersion.TryParse(version, out SemanticVersion semVer)) yield return semVer;
                }
            }
        }
    }
}