using System;
using System.CommandLine;
using System.Threading;

namespace Aion.Npc.Home;

internal static class Program
{
    public static int Main(string[] args)
    {
        var messageOption = new Option<string>("--message") { IsRequired = false };
        var sleepOption = new Option<int>("--sleep") { IsRequired = false };
        var exitCodeOption = new Option<int>("--exit-code") { IsRequired = false };

        var rootCommand = new RootCommand { messageOption, sleepOption, exitCodeOption };
        var commandLine = rootCommand.Parse(args);

        if (commandLine.GetValueForOption(messageOption) is { } message)
        {
            Console.WriteLine(message);
        }

        if (commandLine.GetValueForOption(sleepOption) is var sleep and > 0)
        {
            Console.WriteLine($"Working hard for {sleep} seconds...");
            Thread.Sleep(sleep * 1000);
        }

        var exitCode = commandLine.GetValueForOption(exitCodeOption);
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"Oops! ExitCode: {exitCode}");
        }

        return exitCode;
    }
}