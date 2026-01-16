using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Logging;
using Aion.Modules.Scheduler;
using Aion.Modules.Services;
using Aion.Premise.Jobs;
using Aion.Toolbox.Logging;
using Aion.Toolbox.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Premise.Services.Commands;

public class ExecuteWorkflow
(
    ILogger<WorkflowJob> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    MapLogEvent mapLogEvent,
    CreateWorkflow createWorkflow,
    StartProcess process
)
{
    public async Task<StepResultCollection> Now
    (
        string profileName,
        string workflowName,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var profile = schedulerOptions.Value.Profiles[profileName];
        var workflowPath = profile.Workflows.Single(workflowName);
        var workflowTemplate = await WorkflowTemplate.FromFile(workflowPath);
        var workflow = await createWorkflow.From(workflowTemplate, CreateTrigger.Simple(profileName, workflowName, DateTimeOffset.UtcNow), stepOrder: stepOrder);
        return await Now(workflow);
    }

    public async Task<StepResultCollection> Now(Workflow workflow)
    {
        if (workflow is { Mode: WorkflowMode.Cron, Enabled: false })
        {
            return new StepResultCollection();
        }

        using var activity = new Activity("ExecutingWorkflow").Start();
        using var scope = logger.BeginScopeFrom(new
        {
            ProfileName = workflow.Profile,
            WorkflowName = workflow.Name,
            WorkflowMode = workflow.Mode,
        });

        using var executionSignature = new WorkflowSignatureScope(logger);
        using var logging = mapLogEvent.By(executionSignature, to: workflow.Logging.ToLogger());

        logger.LogInformation("Executing workflow: '{WorkflowName}'", workflow.Name);

        // core: Does not filter out disabled steps because we want them logged.
        var stepResults = new StepResultCollection();
        foreach (var step in workflow.Steps)
        {
            var stepResult = await ExecuteStep(step);
            stepResults.Add(stepResult);

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
            stepResults.Count,
            stepResults.ExecutedStepCount,
            stepResults.PassedStepCount,
            stepResults.FailedStepCount
        );

        return stepResults;
    }

    private async Task<StepResult> ExecuteStep(Workflow.Step step)
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
        using var activity = new Activity("ExecutingStep").Start();
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
            //activity.Start();
            logger.LogInformation("Executing step: {StepIndex}", step.Index);
            var exitCode = await process.Now
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
                Index = step.Index,
                Order = step.Order,
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
                Index = step.Index,
                Order = step.Order,
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
                Index = step.Index,
                Order = step.Order,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
    }
}

// meta: The core does not require this result at all, but without it, it's not possible to write tests.
public record StepResult
{
    public required int Index { get; init; }
    public required int Order { get; init; }
    public int? ExitCode { get; init; }

    public StepStatus Status => Exception switch
    {
        ProcessTimeout => StepStatus.Timeout,
        _ => ExitCode switch
        {
            0 => StepStatus.Ok,
            null => StepStatus.Skipped,
            _ => StepStatus.Error
        }
    };

    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }
}

public enum StepStatus
{
    Ok,
    Error,
    Timeout,
    Skipped,
}

public class StepResultCollection : Collection<StepResult>
{
    public int ExecutedStepCount => this.Count(result => result.ExitCode is not null);
    public int PassedStepCount => this.Count(result => result.ExitCode == 0);
    public int FailedStepCount => this.Count(result => result.ExitCode is not null && result.ExitCode != 0);
}