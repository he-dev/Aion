using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Flow;

public class StartsProcessAsync(ILogger<StartsProcessAsync> logger)
{
    // note: Not using external cancellation as this app does not support such a scenario.
    public async Task<int> Now
    (
        string file,
        TimeSpan timeout,
        Action<ProcessStartInfo>? customizesProcessStartInfo = null,
        Action<string>? logStdErr = null,
        Action<string>? logStdOut = null
    )
    {
        // note: If you run a bash-script on Linux, it is possible that ExitCode can be 255.
        // To fix it, you can try to add the "#!/bin/bash" header to the script.
        var process = new Process
        {
            // meta: Set reasonable defaults and let the caller customize the rest.
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        customizesProcessStartInfo?.Invoke(process.StartInfo);

        // util: Let's measure the execution time.
        var stopwatch = Stopwatch.StartNew();

        var stdOutCompletion = new TaskCompletionSource<bool>();
        var stdErrCompletion = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            OnDataReceived(e, stdOutCompletion, ConsoleStreamType.StdOut, stopwatch);
            if (e.Data is not null) logStdOut?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            OnDataReceived(e, stdErrCompletion, ConsoleStreamType.StdErr, stopwatch);
            if (e.Data is not null) logStdErr?.Invoke(e.Data);
        };

        using var scope = logger.BeginScopeFrom(new { ConsoleStreamType = ConsoleStreamType.Engine });

        try
        {
            logger.LogInformation("Executing: '{File}'.", file);
            logger.LogInformation("Arguments: [{Args}].", process.StartInfo.ArgumentList.Any() ? string.Join(", ", process.StartInfo.ArgumentList) : process.StartInfo.Arguments);

            if (process.Start())
            {
                // ReSharper disable once StringLiteralTypo - becasue it's nagging about the command.
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
                    default: logger.LogError("Process failed after {Elapsed} with code {ExitCode}.", stopwatch.Elapsed, process.ExitCode); break;
                }

                return process.ExitCode;
            }

            throw new ProcessNotStarted();
        }
        catch (OperationCanceledException)
        {
            // core: This exception is thrown when the timeout is reached.
            logger.LogWarning("Process timed out after {Elapsed} ms.", stopwatch.Elapsed);
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

            throw new ProcessTimeout();
        }
        finally
        {
            process.Dispose();
        }
    }

    // util: Let's not write this code twice...
    private void OnDataReceived(DataReceivedEventArgs e, TaskCompletionSource<bool> stdStreamCompletion, ConsoleStreamType consoleStreamType, Stopwatch stopwatch)
    {
        // core: Allow the user to use this property in the message template.
        using var scope = logger.BeginScopeFrom(new { ConsoleStreamType = consoleStreamType });

        // meta: The output stream has been closed, i.e., the process has terminated.
        if (e.Data is null)
        {
            stdStreamCompletion.TrySetResult(true);

            // core: Allow the user to use this property in the message template.
            logger.LogDebug("EOF after {Elapsed}", stopwatch.Elapsed);
        }
        else
        {
            switch (consoleStreamType)
            {
                case ConsoleStreamType.StdOut: logger.LogInformation("{Line}", e.Data); break;
                case ConsoleStreamType.StdErr: logger.LogError("{Line}", e.Data); break;
                case ConsoleStreamType.Engine:
                default:
                    // meta: This case is impossible to occur, but makes the compiler happy.
                    break;
            }
        }
    }
}

// core: Allows us to distinguish the source of various log entries.
public enum ConsoleStreamType
{
    Engine,
    StdOut,
    StdErr,
}

public class ProcessTimeout : Exception;

public class ProcessNotStarted : Exception;

