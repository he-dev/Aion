using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta;
using Aion.Meta.Logging;
using Aion.Util.Scheduler;
using Aion.Util.Services.Templates;
using Aion.Util.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Util.Services;

public class CreateWorkflow
(
    ILogger<CreateWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public async Task<Workflow> From
    (
        WorkflowConfiguration configuration,
        ITrigger? trigger = null,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        using var scope = logger.BeginScopeFrom(new { configuration.Path.WorkflowName });
        logger.LogTrace("Rendering workflow.");

        CronExpression.ValidateExpression(configuration.Cron);

        var profile = schedulerOptions.Value.Profiles[configuration.Path.ProfileName];

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Profile = configuration.Path.ProfileName,
            Enabled = configuration.Enabled,
            Name = configuration.Path.WorkflowName,
            Path = configuration.Path.ToString(),
            Trigger = trigger ?? CreateTrigger.Cron(configuration.Path.ProfileName, configuration.Path.WorkflowName, configuration.Cron),
            Variables = configuration.Parameters?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty
        };

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new ContextVariableGroup(schedulerOptions.Value.Parameters),
            new ContextVariableGroup(profile.Parameters),
            new ContextVariableGroup(configuration.Parameters),
            new SchedulerVariableGroup { Name = schedulerOptions.Value.Name },
            new ProfileVariableGroup
            {
                //Root = workflowPath.ProfileRoot,
                Name = configuration.Path.ProfileName,
            },
            new WorkflowVariableGroup
            {
                Name = configuration.Path.WorkflowName,
                Mode = workflow.Mode,
            }
        ]);

        // core: Merge profile and workflow environments with intended precedence: the workflow overrides profile.
        var environment =
            profile
                .Environment
                .ToImmutableDictionary()
                .SetItems(configuration.Environment)
                .ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables));
        var stepTasks = configuration.Steps.Select((step, index) => CreateStep(profile, variables, environment, step, index));

        try
        {
            var steps = await Task.WhenAll(stepTasks);

            if (stepOrder is not null && stepOrder.Count > 0)
            {
                steps =
                    stepOrder
                        .Select(indexOrName => steps.First(s => indexOrName == s.Index || indexOrName == s.Name))
                        // core: Ensure that the step order matches the specified order.
                        .Select((step, order) => step with { Order = order })
                        .ToArray();
            }

            logger.LogTrace("Steps rendered: {StepCount}", steps.Length);

            return workflow with
            {
                Logging = await profile.GetLoggingPreset.Where(configuration.Logging).Let(jsonObject => RenderTemplate.RenderFilePaths(jsonObject, variables)),
                Steps = steps.ToImmutableList(),
            };
        }
        catch (Exception ex)
        {
            throw new CreateWorkflowException(configuration.Path, ex);
        }
    }

    private async Task<Workflow.Step> CreateStep
    (
        Profile profile,
        IImmutableList<TemplateVariableGroup> variables,
        IImmutableDictionary<string, string> environment,
        StepConfiguration configuration,
        int index
    )
    {
        using var scope = logger.BeginScopeFrom(new { StepIndex = index });
        variables = variables.Add(new StepVariableGroup { Index = index, Name = configuration.Name });
        try
        {
            return new Workflow.Step
            {
                Index = index,
                Order = index, // note: By default, steps are ordered by their index.
                Name = configuration.Name,
                Enabled = configuration.Enabled,
                FileName = configuration.FileName.Render(variables),
                Arguments = () => configuration.Arguments?.Select(argument => argument.RenderValues(variables)) ?? [],
                // core: Merge environment with intended precedence: the step overrides workflow.
                Environment = environment.SetItems(configuration.Environment),
                WorkingDirectory = (configuration.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = configuration.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
                OnError = configuration.OnError,
                Logging = await profile.GetLoggingPreset.Where(configuration.Logging).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
                LoggingTarget = configuration.Logging.Target,
            };
        }
        catch (Exception ex)
        {
            throw new CreateStepException(index, ex);
        }
    }
}

public class CreateWorkflowException(string path, Exception innerException) : Exception
(
    message: $"Failed to render workflow template from '{path}'.",
    innerException: innerException
);

public class CreateStepException(int index, Exception innerException) : Exception
(
    message: $"Failed to render step template at {index}.",
    innerException: innerException
);