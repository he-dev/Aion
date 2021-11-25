using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using Reusable.Extensions;

namespace Aion.Jobs
{
    [UsedImplicitly]
    [DisallowConcurrentExecution]
    internal class WorkflowScheduler : IJob
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings = new()
        {
            DefaultValueHandling = DefaultValueHandling.Populate
        };

        private readonly ILogger<WorkflowScheduler> _logger;
        private readonly IOptions<WorkflowScheduler.Options> _options;

        public WorkflowScheduler(ILogger<WorkflowScheduler> logger, IOptions<WorkflowScheduler.Options> options)
        {
            _logger = logger;
            _options = options;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var workflows = GetRobotConfigurations(_options.Value.WorkflowsDirectory).ToDictionary(x => x.Name);

            //foreach (var trigger in await GetWorkflowTriggers(context))
            await foreach (var trigger in GetWorkflowTriggers(context))
            {
                if (workflows.TryGetValue(trigger.Key.Name, out var other))
                {
                    if (trigger is CronTriggerImpl t1 && other.Trigger is CronTriggerImpl t2 && t1.CronExpressionString.Equals(t2.CronExpressionString))
                    {
                        // Nothing has changes.
                        workflows.Remove(trigger.Key.Name);
                    }
                    else
                    {
                        // There are changes that need to be updated.
                        await UpdateJob(context, other);
                        workflows.Remove(trigger.Key.Name);
                    }
                }
                else
                {
                    // The trigger has been removed.
                    await RemoveJob(context, trigger);
                    workflows.Remove(trigger.Key.Name);
                }
            }

            // What's left is new.
            foreach (var workflow in workflows.Values)
            {
                await CreateJob(context, workflow);
            }
        }

        private IEnumerable<Workflow> GetRobotConfigurations(string path)
        {
            return
                Directory
                    .GetFiles(path, "*.json")
                    .Select(fileName => new { fileName, json = File.ReadAllText(fileName) })
                    .Select(x =>
                    {
                        try
                        {
                            return JsonConvert.DeserializeObject<Workflow>(x.json, _jsonSerializerSettings)!.Also(s => { s.Name = Path.GetFileNameWithoutExtension(x.fileName); });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reading robot settings for: {fileName}", x.fileName);
                            return null!;
                        }
                    })
                    .Where(Conditional.IsNotNull);
        }

        private async Task CreateJob(IJobExecutionContext context, Workflow workflow)
        {
            await context.Scheduler.ScheduleJob
            (
                workflow.JobDetail,
                workflow.Trigger,
                context.CancellationToken
            );

            _logger.LogInformation("Created job '{Name}' at '{Schedule}'.", workflow.Name, workflow.Schedule);
        }

        private async Task RemoveJob(IJobExecutionContext context, ITrigger trigger)
        {
            await context.Scheduler.UnscheduleJob(trigger.Key, context.CancellationToken);
            await context.Scheduler.DeleteJob(trigger.JobKey, context.CancellationToken);

            _logger.LogInformation("Removed job '{Name}'.", trigger.Key.Name);
        }

        private async Task UpdateJob(IJobExecutionContext context, Workflow workflow)
        {
            //var trigger = await context.Scheduler.GetTriggersOfJob(workflow, context.CancellationToken).ContinueWith(triggers => triggers.Result.Single(), context.CancellationToken)!;
            await context.Scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger, context.CancellationToken);

            _logger.LogInformation("Updated job '{Name}' at '{Schedule}'.", workflow.Name, workflow.Schedule);
        }

        //private async Task<IList<ITrigger>> GetWorkflowTriggers(IJobExecutionContext context)
        private async IAsyncEnumerable<ITrigger> GetWorkflowTriggers(IJobExecutionContext context)
        {
            var jobKeys = await context.Scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(Workflow)), context.CancellationToken);
            //var triggers = new List<ITrigger>();
            foreach (var jobKey in jobKeys)
            {
                //triggers.Add(await context.Scheduler.GetTriggersOfJob(jobKey, context.CancellationToken).ContinueWith(x => x.Result.Single()));
                yield return await context.Scheduler.GetTriggersOfJob(jobKey, context.CancellationToken).ContinueWith(x => x.Result.Single());
            }

            //return triggers;
        }

        [UsedImplicitly]
        internal class Options
        {
            public string Schedule { get; set; } = null!;
            public string WorkflowsDirectory { get; set; } = null!;
        }
    }

    internal enum TriggerAction
    {
        Create,
        Remove,
        Update
    }
}