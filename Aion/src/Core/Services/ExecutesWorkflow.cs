using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task Now(Workflow workflow, ProfileInfo profile)
    {
        using var activity = new Activity("ExecutingWorkflow");

        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new ProfileVariableGroup { Name = engineOptions.Value.Name },
            new ArgumentVariableGroup(workflow.Args),
            new WorkflowVariableGroup(activity) { Name = workflow.Name }
        ]);

        // core: Register the workflow's logger or try to fall back to the preset.
        var logging = await workflow.Logging.OrPreset(logger, async presetRef => await FindsLoggingPreset.Where(profile.Path, presetRef));
        logging = logging.RenderFilePaths(template => RendersTemplates.In(template, variables));
        using var tempMapping = mapsLogEvent.By(new WorkflowLogEventSignature(workflow.Name), to: logging.ToLogger());

        var exitCodes = ImmutableList<int?>.Empty;

        activity.Start();
        logger.LogInformation("Executing workflow...");

        // core: Does not filter out disabled steps because we want them logged.
        foreach (var step in workflow.Steps)
        {
            using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name });

            if (stepExecutionRules.Any(stepExecutionRule => stepExecutionRule.Violated(step, exitCodes)))
            {
                exitCodes = exitCodes.Add(null);
                continue;
            }

            var exitCode = await ExecuteStep(workflow, step, profile, variables);
            exitCodes = exitCodes.Add(exitCode);
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();
        logger.LogInformation("Workflow completed in {Duration}.", activity.Duration);
    }

    private async Task<int?> ExecuteStep(Workflow workflow, Workflow.Step step, ProfileInfo profile, IImmutableList<VariableGroup> variables)
    {
        // meta: Setup logging contexts.
        using var activity = new Activity("ExecutingStep");

        // core: We need one more variable-group to render a step, its own.
        var stepVariables = variables.Add(new StepVariableGroup(activity) { Index = step.Index, Name = step.Name });

        try
        {
            // core: Failing to render variables also counts as a failed step.
            step = step.RenderTemplates(stepVariables);

            // core: Register the workflow's logger or try to fall back to the preset.
            var logging = await step.Logging.OrPreset(logger, async presetInfo => await FindsLoggingPreset.Where(profile.Path, presetInfo));
            logging = logging.RenderFilePaths(template => RendersTemplates.In(template, stepVariables));
            using var tempMapping = mapsLogEvent.By(new ConsoleLogEventSignature(workflow.Name, step.Index), to: logging.ToLogger());

            activity.Start();
            logger.LogInformation("Executing step...");
            var exitCode = await asyncProcess.Now(step.File, step.Args, step.WorkingDirectory, step.Timeout);
            activity.SetStatus(ActivityStatusCode.Ok).Stop();

            switch (exitCode)
            {
                case 0: logger.LogInformation("Step completed in {Elapsed}.", activity.Duration); break;
                default: logger.LogError("Step failed in {Elapsed} with exit code {ExitCode}.", activity.Duration, exitCode); break;
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
}

public static class ExtendsJsonObject
{
    public static async Task<JsonObject?> OrPreset(this JsonObject? logging, ILogger logger, Func<LoggingPresetInfo, Task<JsonObject?>> getsLoggingPreset)
    {
        // core: There is no configuration.
        if (logging is null)
        {
            logger.LogDebug("Logging is not specified.");
            return null;
        }

        // core: Use the logger configuration that is embedded in the workflow.
        if (logging.ContainsKey("WriteTo"))
        {
            logger.LogDebug("Logging is specified by the workflow.");
            return await Task.FromResult(logging);
        }

        // core: Use the logger configuration that is specified by the preset.
        if (logging.ContainsKey(nameof(LoggingPresetInfo.File)) && logging.ContainsKey(nameof(LoggingPresetInfo.Name)))
        {
            var presetInfo = logging.Deserialize<LoggingPresetInfo>()!;
            logger.LogDebug("Logging is specified by the preset '{PresetInfo}'.", presetInfo);
            return await getsLoggingPreset(presetInfo);
        }

        // core: Something else has been specified.
        throw new InvalidLoggingConfigurationException();
    }
}

public class InvalidLoggingConfigurationException()
    : Exception("Unknown logging configuration. Expected either 'WriteTo' property or logging preset reference.");