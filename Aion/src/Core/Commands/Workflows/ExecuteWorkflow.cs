using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Logging;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Home.Jobs;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Commands.Workflows;

public class ExecuteWorkflow
(
    ILogger<WorkflowJob> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    LogEventMapping logEventMapping,
    RenderWorkflow renderWorkflow,
    AsyncProcess process
)
{
    public async Task<IImmutableList<StepResult>> Now
    (
        string profileName,
        string workflowName,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var profile = schedulerOptions.Value.Profiles[profileName];
        var workflowMatch = profile.Workflows.Single(workflowName);
        var workflow = await renderWorkflow.For(workflowMatch, WorkflowTrigger.Create(profileName, workflowName, DateTimeOffset.UtcNow), stepOrder: stepOrder);
        return await Now(workflow);
    }

    public async Task<IImmutableList<StepResult>> Now(Workflow workflow)
    {
        if (workflow is { Mode: WorkflowMode.Cron, Enabled: false })
        {
            return ImmutableList<StepResult>.Empty;
        }

        using var activity = new Activity("ExecutingWorkflow").Start();
        using var scope = logger.BeginScopeFrom(new
        {
            ProfileName = workflow.Profile,
            WorkflowName = workflow.Name,
            WorkflowMode = workflow.Mode,
        });

        using var executionSignature = new WorkflowSignatureScope(logger);
        using var logging = logEventMapping.By(executionSignature, to: workflow.Logging.ToLogger());

        logger.LogInformation("Executing workflow: '{WorkflowName}'", workflow.Name);

        // core: Does not filter out disabled steps because we want them logged.
        var stats = new WorkflowStats();
        foreach (var step in workflow.Steps)
        {
            var stepResult = await ExecuteStep(step);
            stats.Add(stepResult);

            if (stepResult.ExitCode is not null and not 0 && step.OnFailure is { } onFailure)
            {
                if (onFailure.Trim().Equals("continue", StringComparison.OrdinalIgnoreCase))
                {
                    // core: Just continue with the next step.
                }

                if (onFailure.Trim().Equals("break", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Workflow execution stopped due to a failed step.");
                    break;
                }
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();

        logger.LogInformation("Workflow '{WorkflowName}' completed in {Duration:N0} ms.", workflow.Name, activity.Duration);
        logger.LogInformation
        (
            "Steps={TotalStepCount}, Executed={ExecutedStepCount}, Passed={PassedStepCount}, Failed={FailedStepCount}.",
            stats.TotalStepCount,
            stats.ExecutedStepCount,
            stats.PassedStepCount,
            stats.FailedStepCount
        );

        return stats.ToImmutableList();
    }

    private async Task<StepResult> ExecuteStep(Workflow.Step step)
    {
        if (!step.Enabled)
        {
            return new StepResult
            {
                Step = step,
            };
        }

        // meta: Setup logging contexts.
        using var activity = new Activity("ExecutingStep").Start();
        using var executionSignature = new StepSignatureScope(logger);
        using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name, step.LoggingTarget });
        using var logging = logEventMapping.By(executionSignature, to: step.Logging.ToLogger());

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
            //activity.Start();
            logger.LogInformation("Executing step: {StepIndex}", step.Index);
            var exitCode = await process.Start
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
                default: logger.LogError("Step {StepIndex} failed after {Duration:N0} ms with exit code {ExitCode}. Next: '{OnFailure}'.", step.Index, activity.Duration, exitCode, step.OnFailure); break;
            }

            return new StepResult
            {
                Step = step,
                ExitCode = exitCode,
                Duration = activity.Duration,
            };
        }
        catch (ProcessTimeout ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogWarning("Step {StepIndex} was cancelled in {Duration:N0} ms by timeout.", step.Index, activity.Duration);
            return new StepResult
            {
                Step = step,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Step {StepIndex} failed after {Duration:N0} ms with an exception.", step.Index, activity.Duration);
            return new StepResult
            {
                Step = step,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
    }
}

// meta: The core does not require this result at all, but without it, it's not possible to write tests.
public record StepResult
{
    public required Workflow.Step Step { get; init; }
    public int? ExitCode { get; init; }
    public StepStatus Status => ExitCode switch { 0 => StepStatus.Ok, null => StepStatus.Skipped, _ => StepStatus.Error };
    public Exception? Exception { get; init; }
    public TimeSpan? Duration { get; init; }
}

public enum StepStatus { Ok, Skipped, Error }

public class WorkflowStats : Collection<StepResult>
{
    public int TotalStepCount => Count;
    public int ExecutedStepCount => this.Count(result => result.ExitCode is not null);
    public int PassedStepCount => this.Count(result => result.ExitCode == 0);
    public int FailedStepCount => this.Count(result => result.ExitCode is not null && result.ExitCode != 0);
}