using System;
using System.Collections.Immutable;
using System.Diagnostics;
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
        using var workflowActivity = new Activity("ExecuteWorkflow");
        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new ProfileVariableGroup { Name = profile.Value.Name },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup(workflowActivity) { Name = workflow.Name, Trigger = triggerGroup }
        ]);

        using var popWorkflowSink = workflowSink.Push(workflow.Name, workflow.Serilog, variables);
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name, Trigger = triggerGroup });

        var previousExitCode = default(int?);

        workflowActivity.Start();
        logger.LogInformation("Executing workflow...");

        // core: Do not filter out disabled steps because we want them logged.
        foreach (var template in workflow.Steps)
        {
            using var stepActivity = new Activity("ExecuteStep");
            using var stepScope = logger.BeginScopeFrom(new { StepIndex = template.Index, StepName = template.Name });

            if (!template.IsOn)
            {
                logger.LogWarning("Skipping step because it is disabled.");
                continue;
            }

            // note: Currently, there is only one DependsOn rule: "$previous".
            // core: This check is irrelevant for the first step, so ignore it.
            if (template is { Index: > 0, DependsOn: "$previous" } && previousExitCode is not 0)
            {
                logger.LogWarning("Workflow aborted because this step depends on the previous one and it failed.");
                break;
            }

            // core: We need one more variable-group to render a step, its own.
            var stepVariables = variables.Add(new StepVariableGroup(stepActivity) { Name = template.Name, Index = template.Index });

            try
            {
                // core: Failing to render variables also counts as a failed step.
                var step = template.RenderTemplates(stepVariables);

                using var stepLoggerFactory = (step.Console ?? workflow.Console).ToLogger().ToLoggerFactory();
                var asyncProcess = new AsyncProcess(stepLoggerFactory.CreateLogger(nameof(Workflow.Step)))
                {
                    File = step.File,
                    Args = step.Args,
                    OnProcessStarted = process =>
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

                stepActivity.Start();
                logger.LogInformation("Executing step...");
                var exitCode = await asyncProcess.StartAsync(step.Timeout);
                stepActivity.Stop();

                switch (exitCode)
                {
                    case 0: logger.LogInformation("Step completed in {Elapsed}.", stepActivity.Duration); break;
                    default: logger.LogError("Step failed in {Elapsed} with exit code {ExitCode}.", stepActivity.Duration, exitCode); break;
                }

                previousExitCode = exitCode;
            }
            catch (Exception ex)
            {
                stepActivity.Stop();
                logger.LogError(ex, "Step failed in {Elapsed} with an exception.", stepActivity.Duration);
                previousExitCode = null; // note: In case of an exception there is not exit-code to use.
            }
        }

        workflowActivity.Stop();
        logger.LogInformation("Workflow completed in {Elapsed}.", workflowActivity.Duration);
    }

    private async Task<int?> ExecuteStep(Workflow.Step step, IImmutableList<VariableGroup> variables)
    {
        return default;
    }
}