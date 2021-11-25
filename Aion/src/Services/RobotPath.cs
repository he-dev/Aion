using System;
using System.IO;

namespace Aion.Services
{
    internal static class RobotPath
    {
        public static string Combine(string robotsPath, string robotFileName)
        {
            if (robotsPath == null) throw new ArgumentNullException(nameof(robotsPath));
            if (robotFileName == null) throw new ArgumentNullException(nameof(robotFileName));

            return
                Path.Combine(
                    robotsPath,
                    Path.GetFileNameWithoutExtension(robotFileName));
        }
    }
}