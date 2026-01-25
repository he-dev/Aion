using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Meta.Serilog;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Workflows;

public class ExecuteStep
(
    ILogger<ExecuteStep> logger,
    MapLogEvent mapLogEvent,
    StartProcess startProcess
)
{
    public async Task<StepResult> Now(Workflow.Step step)
    {
        if (!step.Enabled)
        {
            return new StepResult
            {
                Index = step.Index,
                Order = step.Order,
            };
        }

        // meta: Setup logging contexts.
        using var activity = new Activity(nameof(ExecuteStep)).Start();
        using var executionSignature = new StepSignatureScope(logger);
        using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name, step.LoggingTarget });
        using var logging = mapLogEvent.By(executionSignature, to: step.Logging.ToLogger());

        //var exitCodes = results.Select(r => r.ExitCode).ToImmutableList();
        // if (stepExecutionRules.Any(stepExecutionRule => stepExecutionRule.Violated(step, exitCodes)))
        // {
        //     return new StepResult
        //     {
        //         Step = step,
        //     };
        // }

        try
        {
            logger.LogInformation("Executing step: {StepIndex}", step.Index);
            var exitCode = await startProcess.Now
            (
                step.FileName,
                step.Timeout,
                psi =>
                {
                    psi.Arguments = step.Arguments().Join();
                    psi.WorkingDirectory = step.WorkingDirectory;
                    foreach (var (name, value) in step.Environment)
                    {
                        psi.EnvironmentVariables[name] = value;
                    }
                }
            );
            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            switch (exitCode)
            {
                case 0: logger.LogInformation("Step {StepIndex} completed in {Duration:N0} ms.", step.Index, activity.Duration); break;
                default: logger.LogError("Step {StepIndex} failed after {Duration:N0} ms with exit code {ExitCode}. Next: '{OnError}'.", step.Index, activity.Duration, exitCode, step.OnError); break;
            }

            return new StepResult
            {
                Index = step.Index,
                Order = step.Order,
                ExitCode = exitCode,
                Duration = activity.Duration,
            };
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            if (ex is ProcessTimeout)
            {
                logger.LogWarning("Step {StepIndex} was cancelled after {Duration:N0} ms by timeout.", step.Index, activity.Duration);
            }
            else
            {
                logger.LogError(ex, "Step {StepIndex} failed after {Duration:N0} ms with an exception.", step.Index, activity.Duration);
            }

            return new StepResult
            {
                Index = step.Index,
                Order = step.Order,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
    }
}

// meta: Makes testing easier.
public record StepResult
{
    public required int Index { get; init; }
    public required int Order { get; init; }
    public int? ExitCode { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }

    public StepStatus Status => Exception switch
    {
        ProcessTimeout => StepStatus.Timeout,
        _ => ExitCode switch
        {
            0 => StepStatus.Ok,
            null => StepStatus.Disabled,
            _ => StepStatus.Error
        }
    };
}

public enum StepStatus
{
    Ok,
    Error,
    Timeout,
    Disabled,
}