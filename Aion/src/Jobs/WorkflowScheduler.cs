﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Services;
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
        private readonly IOptions<WorkflowService.Options> _options;

        public WorkflowScheduler(ILogger<WorkflowScheduler> logger, IOptions<WorkflowService.Options> options)
        {
            _logger = logger;
            _options = options;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var workflows = EnumerateWorkflows(_options.Value.WorkflowsDirectory).ToDictionary(x => x.Name);

            
            await foreach (var activeTrigger in GetActiveWorkflowTriggers(context))
            {
                if (workflows.TryGetValue(activeTrigger.Key.Name, out var other))
                {
                    if (!other.Enabled)
                    {
                        // The trigger has been disabled.
                        await RemoveJob(context, activeTrigger);
                        workflows.Remove(activeTrigger.Key.Name);
                    }
                    else
                    {
                        var schedulesMatch =
                            activeTrigger is CronTriggerImpl t1 &&
                            other.Trigger is CronTriggerImpl t2 &&
                            StringComparer.OrdinalIgnoreCase.Equals(t1.CronExpressionString, t2.CronExpressionString);

                        if (schedulesMatch)
                        {
                            // There are no changes. Skip further processing.
                            workflows.Remove(activeTrigger.Key.Name);
                        }
                        else
                        {
                            // There are changes that need to be updated.
                            await UpdateJob(context, other);
                            workflows.Remove(activeTrigger.Key.Name);
                        }
                    }
                }
                else
                {
                    // The trigger has been removed.
                    await RemoveJob(context, activeTrigger);
                    workflows.Remove(activeTrigger.Key.Name);
                }
            }

            // What's left is new but process only the enabled ones.
            foreach (var workflow in workflows.Values.Where(w => w.Enabled))
            {
                await CreateJob(context, workflow);
            }
        }

        private IEnumerable<Workflow> EnumerateWorkflows(string path)
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
                //workflow.JobDetail,
                JobBuilder
                    .Create<WorkflowLauncher>()
                    .WithIdentity(workflow.Name, nameof(Workflow))
                    .UsingJobData(nameof(WorkflowService.Options.WorkflowsDirectory), _options.Value.WorkflowsDirectory)
                    .Build(),
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

        private async IAsyncEnumerable<ITrigger> GetActiveWorkflowTriggers(IJobExecutionContext context)
        {
            var jobKeys = await context.Scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(Workflow)), context.CancellationToken);
            foreach (var jobKey in jobKeys)
            {
                yield return await context.Scheduler.GetTriggersOfJob(jobKey, context.CancellationToken).ContinueWith(x => x.Result.Single());
            }
        }

        
    }
}