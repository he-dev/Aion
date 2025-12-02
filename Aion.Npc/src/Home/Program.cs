using System;
using System.CommandLine;
using System.Threading;

namespace Aion.Npc.Home;

internal static class Program
{
    public static int Main(string[] args)
    {
        var messageOption = new Option<string>("--message") { Required = false };
        var sleepSecondsOption = new Option<int>("--sleep-seconds") { Required = false };
        var exitCodeOption = new Option<int>("--exit-code") { Required = false };

        var rootCommand = new RootCommand { messageOption, sleepSecondsOption, exitCodeOption };
        var commandLine = rootCommand.Parse(args);

        if (commandLine.GetValue(messageOption) is { } message)
        {
            Console.WriteLine(message);
        }

        if (commandLine.GetValue(sleepSecondsOption) is var sleep and > 0)
        {
            Console.WriteLine($"Waiting for {sleep} seconds...");
            Thread.Sleep(sleep * 1000);
        }

        if (commandLine.GetValue(exitCodeOption) is var exitCode && exitCode != 0)
        {
            Console.Error.WriteLine($"Oops! ({exitCode})");
        }
        else
        {
            Console.WriteLine("Done!");
        }

        return exitCode;
    }
}