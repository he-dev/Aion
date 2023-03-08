using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Data;
using Aion.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class WorkflowScheduler : IJob
{
    private readonly ILogger<WorkflowScheduler> _logger;
    private readonly IOptions<WorkflowService.Options> _options;
    private readonly WorkflowReader _workflowReader;

    public WorkflowScheduler(ILogger<WorkflowScheduler> logger, IOptions<WorkflowService.Options> options, WorkflowReader workflowReader)
    {
        _logger = logger;
        _options = options;
        _workflowReader = workflowReader;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var x = await GetActiveWorkflowTriggers(context).ToListAsync();
        var y = _workflowReader.ReadWorkflows(_options.Value.WorkflowsDirectory);

        foreach (var (workflow, action) in x.ToWorkflows().Compare(y))
        {
            switch (action)
            {
                case WorkflowAction.Remove:
                    await RemoveJob(context, workflow);
                    break;

                case WorkflowAction.Update:
                    await UpdateJob(context, workflow);
                    break;

                case WorkflowAction.Create:
                    await CreateJob(context, workflow);
                    break;
            }
        }
    }

    private async Task CreateJob(IJobExecutionContext context, Workflow workflow)
    {
        _logger.LogInformation("Create job '{Name}' at '{Schedule}'", workflow.Name, workflow.Schedule);

        await context.Scheduler.ScheduleJob
        (
            workflow.ToJobDetail(b => b.UsingJobData(nameof(WorkflowService.Options.WorkflowsDirectory), _options.Value.WorkflowsDirectory)),
            workflow.ToTrigger(),
            context.CancellationToken
        );
    }

    private async Task RemoveJob(IJobExecutionContext context, Workflow workflow)
    {
        _logger.LogInformation("Remove job '{Name}'", workflow.Name);

        await context.Scheduler.UnscheduleJob(new TriggerKey(workflow.Name, nameof(Workflow)), context.CancellationToken);
        await context.Scheduler.DeleteJob(new JobKey(workflow.Name, nameof(Workflow)), context.CancellationToken);
    }

    private async Task UpdateJob(IJobExecutionContext context, Workflow workflow)
    {
        _logger.LogInformation("Update job '{Name}' at '{Schedule}'", workflow.Name, workflow.Schedule);

        var trigger = workflow.ToTrigger();
        await context.Scheduler.RescheduleJob(trigger.Key, trigger, context.CancellationToken);
    }

    private static async IAsyncEnumerable<ITrigger> GetActiveWorkflowTriggers(IJobExecutionContext context)
    {
        var jobKeys = await context.Scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(Workflow)), context.CancellationToken);
        foreach (var jobKey in jobKeys)
        {
            yield return await context.Scheduler.GetTriggersOfJob(jobKey, context.CancellationToken).ContinueWith(x => x.Result.Single());
        }
    }
}