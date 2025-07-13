using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public class AsyncProcess(ILogger logger)
{
    public required string File { get; init; }

    public required IEnumerable<string> Args { get; init; }

    public string? WorkingDirectory { get; init; }

    public Action<Process> OnProcessStarted { get; init; } = _ => { };

    public async Task<int> StartAsync(TimeSpan timeout)
    {
        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = File,
                Arguments = string.Join(' ', Args.Select(a => a.Trim())),
                WorkingDirectory = WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        using var activity = new Activity("ExecuteProcess");
        activity.Start();

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) => OnDataReceived(e, stdOutCompletion, StdStreamType.Out, activity);
        process.ErrorDataReceived += (_, e) => OnDataReceived(e, stdErrCompletion, StdStreamType.Err, activity);

        try
        {
            logger.LogInformation("Starting process '{File}' with arguments '{Args}'.", File, process.StartInfo.Arguments);
            if (process.Start())
            {
                OnProcessStarted(process);

                // meta: Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var processTask = WaitForExitAsync(process, timeout);

                // core: This task waits for the process to exit and closing all std-streams.
                var mainTask = Task.WhenAll(processTask, stdOutCompletion.Task, stdErrCompletion.Task);

                // core: Waits process completion and then checks it was not completed by timeout.
                if (await Task.WhenAny(Task.Delay(timeout), mainTask) == mainTask && processTask.Result)
                {
                    return process.ExitCode;
                }

                // core: Kill it if it takes too long to complete or hangs.
                process.Kill(entireProcessTree: true);
                throw new ProcessTimeoutException();
            }

            throw new ProcessNotStartedException();
        }
        finally
        {
            process.Dispose();
        }
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, StdStreamType stdStreamType, Activity activity)
    {
        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);
            activity.Stop();

            // core: Allow the user to use this property in the message template.
            using (logger.BeginScopeFrom(new { StdStreamType = stdStreamType }))
            {
                logger.LogInformation("EOF in {Elapsed}", activity.Duration);
            }
        }
        else
        {
            // core: Allow the user to use this property in the message template.
            using (logger.BeginScopeFrom(new { StdStreamType = stdStreamType }))
            {
                logger.LogInformation("{Line}", e.Data);
            }
        }
    }

    // hack: Helps to avoid the warning about the process being disposed outside the lambda.
    private static Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        return Task.Run(() => process.WaitForExit(timeout));
    }
}

public enum StdStreamType
{
    Out,
    Err,
}

public class ProcessTimeoutException : Exception;

public class ProcessNotStartedException : Exception;