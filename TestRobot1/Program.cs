using System;
using System.Threading;
using Enumerable = System.Linq.Enumerable;

namespace Aion.Robots.TestRobot1
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine($"TestRobot1 started! args: {string.Join(", ", args ?? Enumerable.Empty<string>())}");

            //var processNames = System.Diagnostics.Process.GetProcesses().OrderBy(p => p.ProcessName).Select(p => p.ProcessName);
            //foreach (var processName in processNames)
            //{
            //    Console.WriteLine(processName);
            //}

            Thread.Sleep(7000);
            //Console.ReadKey();

            return 1;
        }
    }
}
