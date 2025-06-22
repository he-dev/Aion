using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Aion.Util;

public interface IAsyncProcess
{
    Task<AsyncProcess.Result> StartAsync(ProcessStartInfo startInfo, int timeoutMilliseconds);
}

public class AsyncProcess : IAsyncProcess
{
    public async Task<Result> StartAsync(ProcessStartInfo startInfo, int timeoutMilliseconds)
    {
        // If you run bash-script on Linux it is possible that ExitCode can be 255.
        // To fix it you can try to add '#!/bin/bash' header to the script.
        using var process = new Process();
        process.StartInfo = startInfo;

        var stdOutBuilder = new StringBuilder();
        var outputCloseEvent = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            // The output stream has been closed, i.e. the process has terminated.
            if (e.Data is null)
            {
                outputCloseEvent.SetResult(true);
            }
            else
            {
                stdOutBuilder.AppendLine(e.Data);
            }
        };

        var stdErrBuilder = new StringBuilder();
        var errorCloseEvent = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (s, e) =>
        {
            // The error stream has been closed i.e. the process has terminated.
            if (e.Data is null)
            {
                errorCloseEvent.SetResult(true);
            }
            else
            {
                stdErrBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            if (process.Start())
            {
                // Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Creates the task to wait for process exit using timeout.
                var waitForExit = WaitForExitAsync(process, timeoutMilliseconds);

                // Create the task to wait for process exit and closing all output streams.
                var processTask = Task.WhenAll(waitForExit, outputCloseEvent.Task, errorCloseEvent.Task);

                // Waits process completion and then checks it was not completed by timeout.
                if (await Task.WhenAny(Task.Delay(timeoutMilliseconds), processTask) == processTask && waitForExit.Result)
                {
                    return new Result(process.StartInfo, process.ExitCode)
                    {
                        Completed = true,
                        StdOut = stdOutBuilder.ToString(),
                        StdErr = stdErrBuilder.ToString(),
                        Elapsed = stopwatch.Elapsed
                    };
                }

                // Kill it if it takes too long to complete or hangs.
                try
                {
                    process.Kill();
                    return new Result(process.StartInfo, -1)
                    {
                        TimedOut = true,
                        Killed = true,
                        StdOut = stdOutBuilder.ToString(),
                        StdErr = stdErrBuilder.ToString(),
                        Elapsed = stopwatch.Elapsed
                    };
                }
                catch (Exception ex)
                {
                    return new Result(process.StartInfo, -1)
                    {
                        TimedOut = true,
                        StdOut = stdOutBuilder.ToString(),
                        StdErr = stdErrBuilder.ToString(),
                        Exception = ex,
                        Elapsed = stopwatch.Elapsed
                    };
                }
            }
        }
        catch (Exception ex)
        {
            // Usually it occurs when an executable file is not found or is not executable.
            return new Result(process.StartInfo, -1)
            {
                StdOut = stdOutBuilder.ToString(),
                StdErr = stdErrBuilder.ToString(),
                Exception = ex,
            };
        }

        return new Result(process.StartInfo, 0);
    }

    private static Task<bool> WaitForExitAsync(Process process, int timeout)
    {
        return Task.Run(() => process.WaitForExit(timeout));
    }

    public record Result(ProcessStartInfo StartInfo, int ExitCode)
    {
        public bool Completed { get; init; }
        public bool TimedOut { get; init; }
        public bool Killed { get; init; }
        public string? StdOut { get; init; }
        public string? StdErr { get; init; }
        public Exception? Exception { get; init; }
        public TimeSpan Elapsed { get; init; } = TimeSpan.Zero;

        public override string ToString()
        {
            return
                new StringBuilder()
                    .AppendLine($"FileName: {StartInfo.FileName}")
                    .Append("Arguments:").AppendLine(string.IsNullOrEmpty(StartInfo.Arguments) ? " null" : StartInfo.Arguments)
                    .AppendLine($"ExitCode: {ExitCode}")
                    .AppendLine($"Completed: {Completed}")
                    .AppendLine($"TimedOut: {TimedOut}")
                    .AppendLine($"Killed: {Killed}")
                    .Append("Output:").Append(string.IsNullOrEmpty(StdOut) ? " null" : Environment.NewLine + StdOut).AppendLine()
                    .Append("Error:").Append(string.IsNullOrEmpty(StdErr) ? " null" : Environment.NewLine + StdErr).AppendLine()
                    .Append("Exception:").Append(Exception is not null ? Environment.NewLine + Exception : " null")
                    .ToString();
        }

        public static implicit operator bool(Result result) => result.ExitCode == 0;
    }
}