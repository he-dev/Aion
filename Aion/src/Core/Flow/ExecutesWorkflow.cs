using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Data;
using Aion.Core.Data.StepExecutionRules;
using Aion.Util.Flow;
using Aion.Util.Flow.Serilog;
using Aion.Util.Logging;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Flow;

// core: Executes workflow's enabled steps.
public class ExecutesWorkflow
(
    ILogger<ExecutesWorkflow> logger,
    IEnumerable<IStepExecutionRule> stepExecutionRules,
    MapsLogEvent mapsLogEvent,
    StartsProcessAsync asyncProcess
)
{
    public async Task<IImmutableList<StepResult>> Now(Workflow workflow)
    {
        using var activity = new Activity("ExecutingWorkflow").Start();
        using var executionSignature = new WorkflowSignatureScope(logger);
        using (mapsLogEvent.By(executionSignature, to: workflow.Logging.ToLogger()))
        {
            logger.LogInformation("Executing workflow...");

            // core: Does not filter out disabled steps because we want them logged.
            var stepResults = ImmutableList<StepResult>.Empty;
            foreach (var step in workflow.Steps)
            {
                var stepResult = await ExecuteStep(step, stepResults);
                stepResults = stepResults.Add(stepResult);
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
            logger.LogInformation("Workflow completed in {Duration}.", activity.Duration);

            return stepResults;
        }
    }

    private async Task<StepResult> ExecuteStep(Workflow.Step step, IImmutableList<StepResult> results)
    {
        // meta: Setup logging contexts.
        using var activity = new Activity("ExecutingStep").Start();
        using var executionSignature = new StepSignatureScope(logger);
        using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name });
        using var logging = mapsLogEvent.By(executionSignature, to: step.Logging.ToLogger());

        var exitCodes = results.Select(r => r.ExitCode).ToImmutableList();
        if (stepExecutionRules.Any(stepExecutionRule => stepExecutionRule.Violated(step, exitCodes)))
        {
            return new StepResult
            {
                Step = step,
            };
        }

        try
        {
            //activity.Start();
            logger.LogInformation("Executing step...");
            var exitCode = await asyncProcess.Now
            (
                step.FileName,
                step.Timeout,
                psi =>
                {
                    // foreach (var arg in context.Step.Arguments.RenderArgList(variables))
                    // {
                    //     psi.ArgumentList.Add(arg);
                    // }

                    psi.Arguments = step.Arguments();
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
                case 0: logger.LogInformation("Step completed in {Duration}.", activity.Duration); break;
                default: logger.LogError("Step failed in {Duration} with exit code {ExitCode}.", activity.Duration, exitCode); break;
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
            logger.LogWarning("Step was cancelled in {Duration} by timeout.", activity.Duration);
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
            logger.LogError(ex, "Step failed in {Duration} with an exception.", activity.Duration);
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
    public Exception? Exception { get; init; }
    public TimeSpan? Duration { get; init; }
}