using System;
using System.Diagnostics;
using System.IO;
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
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process { StartInfo = startInfo };

        var stdOutWriter = new StreamWriter(path: "file-name-here-output.txt", append: true, encoding: Encoding.UTF8) { AutoFlush = true };
        var stdOutCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            // The output stream has been closed, i.e., the process has terminated.
            if (e.Data is null)
            {
                stdOutCompletion.TrySetResult(true);
            }
            else
            {
                stdOutWriter.WriteLine(e.Data);
            }
        };

        var stdErrWriter = new StreamWriter(path: "file-name-here-error.txt", append: true, encoding: Encoding.UTF8) { AutoFlush = true };
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (s, e) =>
        {
            // The error stream has been closed, i.e., the process has terminated.
            if (e.Data is null)
            {
                stdErrCompletion.TrySetResult(true);
            }
            else
            {
                stdErrWriter.WriteLine(e.Data);
            }
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            if (process.Start())
            {
                process.StandardInput.Close();
                process.StandardOutput.Close();

                // Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Creates the task to wait for process exit using timeout.
                var waitForExit = WaitForExitAsync(process, timeoutMilliseconds);

                // Create the task to wait for process exit and closing all output streams.
                var processTask = Task.WhenAll(waitForExit, stdOutCompletion.Task, stdErrCompletion.Task);

                // Waits process completion and then checks it was not completed by timeout.
                if (await Task.WhenAny(Task.Delay(timeoutMilliseconds), processTask) == processTask && waitForExit.Result)
                {
                    return new Result(process.StartInfo, process.ExitCode)
                    {
                        Completed = true,
                        Elapsed = stopwatch.Elapsed
                    };
                }

                // Kill it if it takes too long to complete or hangs.
                try
                {
                    process.Kill(entireProcessTree: true);
                    return new Result(process.StartInfo, -1)
                    {
                        TimedOut = true,
                        Killed = true,
                        Elapsed = stopwatch.Elapsed
                    };
                }
                catch (Exception ex)
                {
                    return new Result(process.StartInfo, -1)
                    {
                        TimedOut = true,
                        Exception = ex,
                        Elapsed = stopwatch.Elapsed
                    };
                }
            }
        }
        finally
        {
            await stdOutWriter.DisposeAsync();
            await stdErrWriter.DisposeAsync();
            process.Dispose();
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