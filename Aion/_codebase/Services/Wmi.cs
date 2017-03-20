using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace Aion.Services
{
    internal static class Wmi
    {
        public static IEnumerable<string> GetCommandLines(string processName)
        {
            var query = $"SELECT CommandLine FROM Win32_Process WHERE Name = '{processName}'";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (var instance in searcher.Get())
                {
                    yield return instance["CommandLine"].ToString();
                }
            }
        }

        public static bool IsRunning(string arguments, IList<string> commandLines)
        {
            // Skip the file name.
            var currentCommandLines = commandLines.Select(x => Reusable.Shelly.CommandLineTokenizer.Tokenize(x).Skip(1).OrderBy(y => y)).ToList();
            if (!currentCommandLines.Any()) return false;

            var tokens = Reusable.Shelly.CommandLineTokenizer.Tokenize(arguments ?? string.Empty).OrderBy(x => x).ToList();
            return currentCommandLines.Any(x => x.SequenceEqual(tokens));
        }
    }
}
