using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Premise.Services.Commands;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Premise.Services.Commands;

public class TestScheduleWorkflow(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    private async Task<WorkflowSyncResult.Passed> Synchronize
    (
        WorkflowTemplate? initialWorkflow,
        WorkflowTemplate changedWorkflow
    )
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<ScheduleWorkflow>();

        //var fakeProfile = new Profile { Path = @"C:\fake\path\to\profiles\one" };
        //var fakeWorkflowName = new WorkflowName("fake-workflow.json");
        //var fakeWorkflowPath = new WorkflowPath(fakeProfile.Path, fakeProfile.Name, fakeWorkflowName);

        if (initialWorkflow is not null)
        {
            await scheduleWorkflow.Invoke(initialWorkflow);
        }

        try
        {
            return await scheduleWorkflow.Invoke(changedWorkflow);
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