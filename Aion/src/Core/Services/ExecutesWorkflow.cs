using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.StepExecutionRules;
using Aion.Util;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
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
    public async Task Now(WorkflowMatch workflowMatch)
    {
        var workflow = workflowMatch.Value;
        using var activity = new Activity("ExecutingWorkflow");

        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new ProfileVariableGroup { Name = engineOptions.Value.Name },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup() { Name = workflowMatch.Name }
        ]);

        var logging =
            workflowMatch.Value.Logging is not null
                ? await workflowMatch.Value.Logging.RenderAsync(workflowMatch.Profile, variables)
                : null;

        using (mapsLogEvent.By(new WorkflowLogEventSignature(workflowMatch.Name), to: logging.ToLogger()))
        {
            var exitCodes = ImmutableList<int?>.Empty;

            activity.Start();
            logger.LogInformation("Executing workflow...");

            // core: Does not filter out disabled steps because we want them logged.
            foreach (var (step, index) in workflow.Steps.Select((step, index) => (step, index)))
            {
                var exitCode = await ExecuteStep(new StepContext
                {
                    WorkflowMatch = workflowMatch,
                    Step = step,
                    StepIndex = index,
                    Variables = variables,
                    ExitCodes = exitCodes
                });
                exitCodes = exitCodes.Add(exitCode);
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
            logger.LogInformation("Workflow completed in {Duration}.", activity.Duration);
        }
    }

    private async Task<int?> ExecuteStep(StepContext context)
    {
        // meta: Setup logging contexts.
        using var activity = new Activity("ExecutingStep");
        using var scope = logger.BeginScopeFrom(new { context.StepIndex, StepName = context.Step.Name });

        if (stepExecutionRules.Any(stepExecutionRule => stepExecutionRule.Violated(context.Step, context.StepIndex, context.ExitCodes)))
        {
            return null;
        }

        var variables = context.Variables.Add(new StepVariableGroup { Index = context.StepIndex, Name = context.Step.Name });
        var stepLogging =
            context.Step.Logging is not null
                ? await context.Step.Logging.RenderAsync(context.WorkflowMatch.Profile, variables)
                : null;

        using var logging = mapsLogEvent.By(new ConsoleLogEventSignature(context.WorkflowMatch.Name, context.StepIndex), to: stepLogging.ToLogger());

        try
        {
            activity.Start();
            logger.LogInformation("Executing step...");
            var exitCode = await asyncProcess.Now
            (
                context.Step.File.Render(variables),
                context.Step.Args.Render(variables),
                context.Step.WorkingDirectory?.Render(variables),
                context.Step.Timeout
            );
            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            switch (exitCode)
            {
                case 0: logger.LogInformation("Step completed in {Duration}.", activity.Duration); break;
                default: logger.LogError("Step failed in {Duration} with exit code {ExitCode}.", activity.Duration, exitCode); break;
            }

            return exitCode;
        }
        catch (ProcessTimeoutException)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogWarning("Step was cancelled in {Duration} by timeout.", activity.Duration);
            return null; // note: In case of a cancellation, there is no exit-code to use.
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Step failed in {Duration} with an exception.", activity.Duration);
            return null; // note: In case of an exception, there is no exit-code to use.
        }
    }

    private record StepContext
    {
        public WorkflowMatch WorkflowMatch { get; init; }
        public Workflow.Step Step { get; init; }
        public int StepIndex { get; init; }
        public IImmutableList<VariableGroup> Variables { get; init; }
        public IImmutableList<int?> ExitCodes { get; init; }
    }
}