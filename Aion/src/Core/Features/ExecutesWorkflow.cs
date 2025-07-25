using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Features;

// core: Executes workflow's enabled steps.
public class ExecutesWorkflow
(
    ILogger<ExecutesWorkflow> logger,
    ILoggerFactory loggerFactory,
    IOptions<EngineOptions> engineOptions,
    MapsLogEvents mapsLogEvents
)
{
    private static readonly Regex IntArrayRegex = new(@"^\[(-?\d+(?:,-?\d+)*)?\]$", RegexOptions.Compiled);

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
        using var tempMapping = mapsLogEvents.By(new WorkflowLogEventSignature(workflow.Name), to: logging.ToLogger());

        var exitCodes = ImmutableList<int?>.Empty;

        activity.Start();
        logger.LogInformation("Executing workflow...");

        // core: Does not filter out disabled steps because we want them logged.
        foreach (var step in workflow.Steps)
        {
            using var scope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name });

            if (!CanExecute(step, exitCodes))
            {
                exitCodes = exitCodes.Add(null);
                continue;
            }

            var exitCode = await ExecuteStep(workflow, step, profile, variables);
            exitCodes = exitCodes.Add(exitCode);
        }

        activity.Stop();
        logger.LogInformation("Workflow completed in {Elapsed}.", activity.Duration);
    }

    private bool CanExecute(Workflow.Step step, IImmutableList<int?> exitCodes)
    {
        if (!step.IsOn)
        {
            logger.LogWarning("Cannot execute this step because it is disabled.");
            return false;
        }

        // note: Currently, there is only one DependsOn rule: "$previous".
        // core: This check is irrelevant for the first step, so ignore it.
        if (step is { Index: > 0, DependsOn: not null })
        {
            if (step is { DependsOn: "$previous" } && exitCodes.Last() is not 0)
            {
                logger.LogWarning("Cannot execute this step because it depends on the previous one and it failed.");
                return false;
            }

            if (TryParseIntArray(step.DependsOn, out var indices) && indices.Any(i => exitCodes[i] is not 0))
            {
                logger.LogWarning("Cannot execute this step because it depends on [{DependsOn}] and one of them failed.", indices);
                return false;
            }
        }

        return true;
    }

    private async Task<int?> ExecuteStep(Workflow workflow, Workflow.Step step, ProfileInfo profile, IImmutableList<VariableGroup> variables)
    {
        // meta: Setup logging contexts.
        using var activity = new Activity("ExecuteStep");

        // core: We need one more variable-group to render a step, its own.
        var stepVariables = variables.Add(new StepVariableGroup(activity) { Index = step.Index, Name = step.Name });

        try
        {
            // core: Failing to render variables also counts as a failed step.
            step = step.RenderTemplates(stepVariables);

            // core: Register the workflow's logger or try to fall back to the preset.
            var logging = await step.Logging.OrPreset(logger, async presetRef => await FindsLoggingPreset.Where(profile.Path, presetRef));
            logging = logging.RenderFilePaths(template => RendersTemplates.In(template, variables));
            using var tempMapping = mapsLogEvents.By(new ConsoleLogEventSignature(workflow.Name, step.Index), to: logging.ToLogger());


            var asyncProcess = new AsyncProcess(loggerFactory.CreateLogger<AsyncProcess>())
            {
                File = step.File,
                Args = step.Args
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
        catch (ProcessTimeoutException)
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

    public static bool TryParseIntArray(string value, [MaybeNullWhen(false)] out ImmutableList<int> result)
    {
        value = value.Replace(" ", string.Empty);

        if (IntArrayRegex.Matches(value) is { Count: > 0 } matches)
        {
            result = matches.Select(m => int.Parse(m.Value)).ToImmutableList();
            return true;
        }

        result = null;
        return false;
    }
}

public interface IStepExecutionRule
{
    bool CanExecute(Workflow.Step step, IImmutableList<int?> exitCodes);
}

public class StepDependsOnPrevious(ILogger<StepDependsOnPrevious> logger) : IStepExecutionRule
{
    public bool CanExecute(Workflow.Step step, IImmutableList<int?> exitCodes)
    {
        return false;
    }
}

public static class ExtendsJsonObject
{
    public static async Task<JsonObject?> OrPreset(this JsonObject? logging, ILogger logger, Func<LoggingPresetRef, Task<JsonObject?>> getsLoggingPreset)
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
        if (logging.TryGetPropertyValue("Preset", out var node) && node is JsonObject)
        {
            var presetInfo = node.Deserialize<LoggingPresetRef>()!;
            logger.LogDebug("Logging is specified by the preset '{Preset}'.", presetInfo);
            return await getsLoggingPreset(presetInfo);
        }

        // core: Something else has been specified.
        throw new InvalidOperationException("Unknown logging configuration.");
    }
}