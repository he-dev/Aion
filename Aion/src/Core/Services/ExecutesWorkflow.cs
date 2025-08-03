using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.StepExecutionRules;
using Aion.Meta.Logging;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services;

// core: Executes workflow's enabled steps.
public class ExecutesWorkflow
(
    ILogger<ExecutesWorkflow> logger,
    IOptions<EngineOptions> engineOptions,
    IEnumerable<IStepExecutionRule> stepExecutionRules,
    MapsLogEvent mapsLogEvent,
    StartsProcessAsync asyncProcess
)
{
    public async Task<IImmutableList<StepResult>> Now(WorkflowMatch workflowMatch)
    {
        var stepResults = ImmutableList<StepResult>.Empty;

        var workflow = workflowMatch.Value;
        using var activity = new Activity("ExecutingWorkflow");

        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new ProfileVariableGroup { Name = engineOptions.Value.Name },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup { Name = workflowMatch.Name }
        ]);

        var logging =
            workflowMatch.Value.Logging is not null
                ? await workflowMatch.Value.Logging.RenderAsync(workflowMatch.Profile, variables)
                : null;

        using (mapsLogEvent.By(new WorkflowLogEventSignature(workflowMatch.Name), to: logging.ToLogger()))
        {
            activity.Start();
            logger.LogInformation("Executing workflow...");

            // core: Does not filter out disabled steps because we want them logged.
            foreach (var (step, index) in workflow.Steps.Select((step, index) => (step, index)))
            {
                var stepResult = await ExecuteStep(new StepContext
                {
                    WorkflowMatch = workflowMatch,
                    Step = step,
                    Index = index,
                    Variables = variables,
                    StepResults = stepResults
                });
                stepResults = stepResults.Add(stepResult);
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
            logger.LogInformation("Workflow completed in {Duration}.", activity.Duration);
        }

        return stepResults;
    }

    private async Task<StepResult> ExecuteStep(StepContext context)
    {
        // meta: Setup logging contexts.
        using var activity = new Activity("ExecutingStep");
        using var scope = logger.BeginScopeFrom(new { StepIndex = context.Index, StepName = context.Step.Name });

        var exitCodes = context.StepResults.Select(r => r.ExitCode).ToImmutableList();
        if (stepExecutionRules.Any(stepExecutionRule => stepExecutionRule.Violated(context.Step, context.Index, exitCodes)))
        {
            return new StepResult
            {
                Step = context.Step,
                Index = context.Index
            };
        }

        var variables = context.Variables.Add(new StepVariableGroup { Index = context.Index, Name = context.Step.Name });
        var stepLogging =
            context.Step.Logging is not null
                ? await context.Step.Logging.RenderAsync(context.WorkflowMatch.Profile, variables)
                : null;

        using var logging = mapsLogEvent.By(new ConsoleLogEventSignature(context.WorkflowMatch.Name, context.Index), to: stepLogging.ToLogger());

        try
        {
            activity.Start();
            logger.LogInformation("Executing step...");
            var exitCode = await asyncProcess.Now
            (
                context.Step.File.Render(variables),
                context.Step.Timeout,
                psi =>
                {
                    foreach (var arg in context.Step.Args.RenderArgList(variables))
                    {
                        psi.ArgumentList.Add(arg);
                    }

                    psi.Arguments = context.Step.Args.RenderArgString(variables);
                    psi.WorkingDirectory = context.Step.WorkingDirectory?.Render(variables);
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
                Step = context.Step,
                Index = context.Index,
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
                Step = context.Step,
                Index = context.Index,
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
                Step = context.Step,
                Index = context.Index,
                Exception = ex,
                Duration = activity.Duration,
            };
        }
    }

    // util: Reduces the number of parameters.
    private record StepContext
    {
        public required WorkflowMatch WorkflowMatch { get; init; }
        public required Workflow.Step Step { get; init; }
        public required int Index { get; init; }
        public required IImmutableList<VariableGroup> Variables { get; init; }
        public required IImmutableList<StepResult> StepResults { get; init; }
    }
}

// meta: The core does not require this result at all, but without it, it's not possible to write tests.
public record StepResult
{
    public required Workflow.Step Step { get; init; }
    public required int Index { get; init; }
    public int? ExitCode { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan? Duration { get; init; } = TimeSpan.Zero;
}