using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Modules;

// core: Executes workflow's enabled steps.
public class WorkflowEngine
(
    ILogger<WorkflowEngine> logger,
    IOptions<ProfileOptions> profile,
    WorkflowSink workflowSink
)
{
    public async Task Start(Workflow workflow, WorkflowTriggerGroup triggerGroup)
    {
        using var activity = new Activity("ExecuteWorkflow");
        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new ProfileVariableGroup { Name = profile.Value.Name },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup(activity) { Name = workflow.Name, Trigger = triggerGroup }
        ]);
        using var sink = workflowSink.Push(workflow.Name, workflow.Serilog, variables);
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name, Trigger = triggerGroup });
        var previousExitCode = default(int?);

        activity.Start();
        logger.LogInformation("Executing workflow...");

        // core: Do not filter out disabled steps because we want them logged.
        foreach (var template in workflow.Steps)
        {
            if (!template.IsOn)
            {
                logger.LogWarning("Skipping step {StepIndex} because it is disabled.", template.Index);
                continue;
            }

            // note: Currently, there is only one DependsOn rule: "$previous".
            // core: This check is irrelevant for the first step, so ignore it.
            if (template is { Index: > 0, DependsOn: "$previous" } && previousExitCode is not 0)
            {
                logger.LogWarning("Workflow aborted because step {StepIndex} depends on the previous one and it failed.", template.Index);
                break;
            }

            previousExitCode = await ExecuteStep(template with { Console = template.Console ?? workflow.Console }, variables);
        }

        activity.Stop();
        logger.LogInformation("Workflow completed in {Elapsed}.", activity.Duration);
    }

    private async Task<int?> ExecuteStep(Workflow.Step template, IImmutableList<VariableGroup> variables)
    {
        using var activity = new Activity("ExecuteStep");
        using var scope = logger.BeginScopeFrom(new { StepIndex = template.Index, StepName = template.Name });

        // core: We need one more variable-group to render a step, its own.
        var stepVariables = variables.Add(new StepVariableGroup(activity) { Name = template.Name, Index = template.Index });

        try
        {
            // core: Failing to render variables also counts as a failed step.
            var step = template.RenderTemplates(stepVariables);

            using var stepLoggerFactory = step.Console.ToLogger().ToLoggerFactory();
            var asyncProcess = new AsyncProcess(stepLoggerFactory.CreateLogger(nameof(Workflow.Step)))
            {
                File = step.File,
                Args = step.Args,
                OnStarted = process =>
                {
                    // util: Log information for killing this process quickly if necessary.
                    logger.LogInformation
                    (
                        "taskkill /F /FI \"PID eq {PID}\" /FI \"SESSION eq {SID}\" /FI \"IMAGENAME eq {ImageName}\" /FI \"SERVICES eq false\"",
                        process.Id,
                        process.SessionId,
                        process.ProcessName
                    );
                }
            };

            activity.Start();
            logger.LogInformation("Executing step...");
            var exitCode = await asyncProcess.StartAsync(step.Timeout);
            activity.Stop();

            switch (exitCode)
            {
                case 0: logger.LogInformation("Step completed in {Elapsed}.", activity.Duration); break;
                default: logger.LogError("Step failed in {Elapsed} with exit code {ExitCode}.", activity.Duration, exitCode); break;
            }

            return exitCode;
        }
        catch (OperationCanceledException)
        {
            activity.Stop();
            logger.LogWarning("Step was cancelled in {Elapsed} by timeout.", activity.Duration);
            return null; // note: In case of a cancellation, there is no exit-code to use.
        }
        catch (Exception ex)
        {
            activity.Stop();
            logger.LogError(ex, "Step failed in {Elapsed} with an exception.", activity.Duration);
            return null; // note: In case of an exception, there is no exit-code to use.
        }
    }
}