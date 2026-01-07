using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Commands;
using Aion.Core.Commands.Workflows;
using Aion.Core.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Core.Services.Scheduling;

public class TestsWorkflowScheduleChanges(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    private async Task<WorkflowSyncResult.Passed> Synchronize
    (
        WorkflowTemplate? initialWorkflow,
        WorkflowTemplate changedWorkflow
    )
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var schedulesWorkflowCron = scope.ServiceProvider.GetRequiredService<WorkflowScheduleRegistry>();
        var workflowRendering = scope.ServiceProvider.GetRequiredService<RenderWorkflow>();

        var fakeProfile = new Profile { Root = @"C:\fake\path\to\profiles", Name = "fake-profile" };
        var fakeWorkflowName = new WorkflowName("fake-workflow.json");
        var fakeWorkflowPath = new WorkflowPath(fakeProfile.Root, fakeProfile.Name, fakeWorkflowName);

        if (initialWorkflow is not null)
        {
            var workflow = await workflowRendering.For(fakeWorkflowPath, loadTemplate: _ => Task.FromResult(initialWorkflow));
            await schedulesWorkflowCron.AddOrUpdate(workflow);
        }

        try
        {
            var workflow = await workflowRendering.For(fakeWorkflowPath, loadTemplate: _ => Task.FromResult(changedWorkflow));
            return await schedulesWorkflowCron.AddOrUpdate(workflow);
        }
        finally
        {
            // meta: Clear all schedules for the next test.
            var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.Clear();
        }
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfDisabled()
    {
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = false,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var result = await Synchronize(null, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseDisabled, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfEmpty()
    {
        var fakeWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = []
        };
        var result = await Synchronize(null, fakeWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseEmpty, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfUnchanged()
    {
        var initialWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseUnchanged, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUnscheduleWorkflowIfDisabled()
    {
        var initialWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = false,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UnscheduleBecauseDisabled, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUnscheduleWorkflowIfEmpty()
    {
        var initialWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = []
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UnscheduleBecauseEmpty, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUpdateScheduleIfChanged()
    {
        var initialWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/10 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UpdateBecauseChanged, result.Action);
        Assert.NotNull(result.NextUtc);
    }

    [Fact]
    public async Task CanScheduleWorkflowIfNew()
    {
        var changedWorkflow = new WorkflowTemplate
        {
            Enabled = true,
            Cron = "0/5 * * * * ?",
            Steps = [new WorkflowStepTemplate { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
        };
        var result = await Synchronize(null, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.ScheduleBecauseNew, result.Action);
        Assert.NotNull(result.NextUtc);
    }
}