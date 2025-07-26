using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public class StartsProcessAsync(ILogger<StartsProcessAsync> logger)
{
    public required string File { get; init; }

    public required IEnumerable<string> Args { get; init; }

    public string? WorkingDirectory { get; init; }

    // note: Not using external cancellation as this app does not support such a scenario.
    public async Task<int> Now(TimeSpan timeout)
    {
        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = File,
                // note: Not using the ArgumentList as it does not correctly transfer the arguments. Let the process handle them.
                Arguments = string.Join(' ', Args.Select(a => a.Trim())),
                WorkingDirectory = WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        // util: Let's measure the execution time.
        var stopwatch = Stopwatch.StartNew();

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) => OnDataReceived(e, stdOutCompletion, ProcessMessageSource.StdOut, stopwatch);
        process.ErrorDataReceived += (_, e) => OnDataReceived(e, stdErrCompletion, ProcessMessageSource.StdErr, stopwatch);

        using var scope = logger.BeginScopeFrom(new { ProcessMessageSource = ProcessMessageSource.Engine });

        try
        {
            logger.LogInformation("Starting process '{File}' with arguments [{Args}].", File, process.StartInfo.Arguments);

            if (process.Start())
            {
                // util: This is a convenience ready-to-use command for killing the process.
                logger.LogInformation
                (
                    "taskkill /F /FI \"PID eq {PID}\" /FI \"SESSION eq {SID}\" /FI \"IMAGENAME eq {ImageName}\" /FI \"SERVICES eq false\"",
                    process.Id,
                    process.SessionId,
                    process.ProcessName
                );

                // core: Background processes are not allowed to expect any input. Not respecting the EOF means for them, they're gonna hit the timeout.
                process.StandardInput.Close();

                // meta: Reads the output stream first and then waits because deadlocks are possible.
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(timeout);
                await process.WaitForExitAsync(cts.Token);
                await Task.WhenAll(stdOutCompletion.Task, stdErrCompletion.Task);

                switch (process.ExitCode)
                {
                    case 0: logger.LogInformation("Process completed in {Elapsed}.", stopwatch.Elapsed); break;
                    default: logger.LogError("Process failed in {Elapsed} with exit-code {ExitCode}.", stopwatch.Elapsed, process.ExitCode); break;
                }

                return process.ExitCode;
            }

            throw new ProcessNotStartedException();
        }
        catch (OperationCanceledException)
        {
            // core: This exception is thrown when the timeout is reached.
            logger.LogWarning("Process timed out after {Elapsed}.", stopwatch.Elapsed);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    logger.LogWarning("Process was killed.");
                }
            }
            catch (InvalidOperationException)
            {
                logger.LogInformation("Process had already exited.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to kill process.");
            }

            throw new ProcessTimeoutException();
        }
        finally
        {
            process.Dispose();
        }
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, ProcessMessageSource processMessageSource, Stopwatch stopwatch)
    {
        // core: Allow the user to use this property in the message template.
        using var scope = logger.BeginScopeFrom(new { ProcessMessageSource = processMessageSource });

        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);

            // core: Allow the user to use this property in the message template.
            logger.LogDebug("EOF in {Elapsed}", stopwatch.Elapsed);
        }
        else
        {
            switch (processMessageSource)
            {
                case ProcessMessageSource.StdOut: logger.LogInformation("{Line}", e.Data); break;
                case ProcessMessageSource.StdErr: logger.LogError("{Line}", e.Data); break;
                case ProcessMessageSource.Engine:
                default:
                    // note: This case is impossible to occur, but makes the compiler happy.
                    break;
            }
        }
    }
}

// core: Allows us to distinguish the source of various log entries.
public enum ProcessMessageSource
{
    Engine,
    StdOut,
    StdErr,
}

public class ProcessTimeoutException : Exception;

public class ProcessNotStartedException : Exception;