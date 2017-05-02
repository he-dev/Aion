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
    }
}
