using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Aion.Util;

public class AsyncProcess
{
    public required string FileName { get; init; } = null!;

    public required string Arguments { get; init; } = null!;

    public string? WorkingDirectory { get; init; }

    public StreamWriter? StdStream { get; set; }

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

        var stdOutputCompletion = new TaskCompletionSource<bool>();

        var syncStream = StdStream is not null ? TextWriter.Synchronized(StdStream) : null;

        if (syncStream is not null)
        {
            process.OutputDataReceived += (_, e) =>
            {
                // The output stream has been closed, i.e., the process has terminated.
                if (e.Data is null)
                {
                    stdOutputCompletion.TrySetResult(true);
                    syncStream.WriteLine($"{DateTimeOffset.UtcNow:s} | OUT | EOF");
                }
                else
                {
                    syncStream.WriteLine($"{DateTimeOffset.UtcNow:s} | OUT | {e.Data}");
                }
            };
        }
        else
        {
            stdOutputCompletion.TrySetResult(true);
        }

        // var stdErrWriter = new StreamWriter(path: "file-name-here-error.txt", append: true, encoding: Encoding.UTF8) { AutoFlush = true };
        var stdErrorCompletion = new TaskCompletionSource<bool>();

        if (syncStream is not null)
        {
            process.ErrorDataReceived += (s, e) =>
            {
                // The error stream has been closed, i.e., the process has terminated.
                if (e.Data is null)
                {
                    stdErrorCompletion.TrySetResult(true);
                    syncStream.WriteLine($"{DateTimeOffset.UtcNow:s} | ERR | EOF");
                }
                else
                {
                    syncStream.WriteLine($"{DateTimeOffset.UtcNow:s} | ERR | {e.Data}");
                }
            };
        }
        else
        {
            stdErrorCompletion.TrySetResult(true);
        }

        try
        {
            if (process.Start())
            {
                //process.StandardInput.Close();
                //process.StandardOutput.Close();

                // Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var processTask = WaitForExitAsync(process, timeout);

                // util: This task waits for the process to exit and closing all std-streams.
                var mainTask = Task.WhenAll(processTask, stdOutputCompletion.Task, stdErrorCompletion.Task);

                // Waits process completion and then checks it was not completed by timeout.
                if (await Task.WhenAny(Task.Delay(timeout), mainTask) == mainTask && processTask.Result)
                {
                    return process.ExitCode;
                }

                // Kill it if it takes too long to complete or hangs.
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

    // util: Helps to avoid the warning about the process being disposed outside the lambda.
    private static Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        return Task.Run(() => process.WaitForExit(timeout));
    }
}

public class ProcessTimeoutException : Exception;
public class ProcessNotStartedException : Exception;
