using System.CommandLine;

namespace AionApi.TestApp;

internal static class Program
{
    public static int Main(string[] args)
    {
        var sleepOption = new Option<int>("--sleep") { IsRequired = true };
        var exitCodeOption = new Option<int>("--exit-code") { IsRequired = true };

        var rootCommand = new RootCommand
        {
            sleepOption,
            exitCodeOption,
        };
        var commandLine = rootCommand.Parse(args);

        Console.WriteLine($"Args: {string.Join(separator: ' ', args)}!");
        Thread.Sleep(commandLine.GetValueForOption(sleepOption)! * 1000);
        return commandLine.GetValueForOption(exitCodeOption)!;
    }
}