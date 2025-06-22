using System.CommandLine;

namespace Aion.Npc;

internal static class Program
{
    public static int Main(string[] args)
    {
        var messageOption = new Option<string>("--message") { IsRequired = false };
        var sleepOption = new Option<int>("--sleep") { IsRequired = false };
        var exitCodeOption = new Option<int>("--exit-code") { IsRequired = false };

        var rootCommand = new RootCommand
        {
            messageOption,
            sleepOption,
            exitCodeOption,
        };
        var commandLine = rootCommand.Parse(args);

        if (commandLine.GetValueForOption(messageOption) is { } message)
        {
            Console.WriteLine($"Message: {message}");
        }

        Thread.Sleep(commandLine.GetValueForOption(sleepOption)! * 1000);
        return commandLine.GetValueForOption(exitCodeOption);
    }
}