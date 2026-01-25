using System;
using System.CommandLine;
using System.Threading;

namespace Aion.Npc.Core;

internal static class Program
{
    public static int Main(string[] args)
    {
        var workflowOption = new Option<string>("--workflow") { Required = false };
        var stepOption = new Option<int>("--step") { Required = false };
        var messageOption = new Option<string>("--message") { Required = false };
        var sleepOption = new Option<int>("--sleep") { Required = false };
        var exitCodeOption = new Option<int>("--exit-code") { Required = false };

        var rootCommand = new RootCommand { messageOption, sleepOption, exitCodeOption };
        var commandLine = rootCommand.Parse(args);

        if (Environment.GetEnvironmentVariable("NPC_MODE") is { } mode)
        {
            Console.WriteLine($"NPC mode: {mode}");
        }

        if (commandLine.GetValue(workflowOption) is { } workflow)
        {
            Console.WriteLine($"Workflow: {workflow}");
        }

        if (commandLine.GetValue(stepOption) is var step)
        {
            Console.WriteLine($"Step: {step}");
        }

        if (commandLine.GetValue(messageOption) is { } message)
        {
            Console.WriteLine($"Message: {message}");
        }

        if (commandLine.GetValue(sleepOption) is var sleep and > 0)
        {
            Console.WriteLine($"Waiting {sleep} seconds...");
            Thread.Sleep(sleep * 1000);
        }

        if (commandLine.GetValue(exitCodeOption) is var exitCode && exitCode != 0)
        {
            Console.Error.WriteLine($"Error: {exitCode}");
        }
        else
        {
            Console.WriteLine("Bye!");
        }

        return exitCode;
    }
}