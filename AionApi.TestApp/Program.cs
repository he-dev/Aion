// See https://aka.ms/new-console-template for more information

using System.CommandLine;

namespace AionApi.TestApp;

internal static class Program
{
    public static int Main(string[] args)
    {
        var sourceOption = new Option<string>("--source") { IsRequired = true };
        var sleepOption = new Option<int>("--sleep") { IsRequired = true };
        var exitCodeOption = new Option<int>("--exit-code") { IsRequired = true };

        var rootCommand = new RootCommand
        {
            sourceOption,
            sleepOption,
            exitCodeOption,
        };
        var commandLine = rootCommand.Parse(args);

        Console.WriteLine($"Source: {commandLine.GetValueForOption(sourceOption)}!");
        Thread.Sleep(commandLine.GetValueForOption(sleepOption)! * 1000);
        return commandLine.GetValueForOption(exitCodeOption)!;
    }
}