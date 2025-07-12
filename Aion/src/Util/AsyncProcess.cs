using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public class AsyncProcess
{
    public required string FileName { get; init; }

    public required string Arguments { get; init; }

    public string? WorkingDirectory { get; init; }

    public ILogger? Logger { get; set; }

    public async Task<int> StartAsync(TimeSpan timeout)
    {
        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FileName,
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        if (Logger is not null)
        {
            process.OutputDataReceived += (_, e) => OnDataReceived(e, stdOutCompletion, StdStreamType.Out);
            process.ErrorDataReceived += (_, e) => OnDataReceived(e, stdErrCompletion, StdStreamType.Err);
        }
        else
        {
            stdOutCompletion.TrySetResult(true);
            stdErrCompletion.TrySetResult(true);
        }

        try
        {
            Logger?.LogInformation("Starting process '{FileName}' with arguments '{Arguments}'.", FileName, Arguments);
            if (process.Start())
            {
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
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, StdStreamType stdStreamType)
    {
        // The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);
            Logger?.LogInformation("{StdStreamType} | EOF", stdStreamType);
        }
        else
        {
            Logger?.LogInformation("{StdStreamType} | {Line}", stdStreamType, e.Data);
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