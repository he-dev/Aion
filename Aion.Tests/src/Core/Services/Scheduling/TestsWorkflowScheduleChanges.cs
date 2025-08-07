using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Scheduling;
using Aion.Core.Templates;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Core.Services.Scheduling;

public class TestsWorkflowScheduleChanges(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    private async Task<WorkflowSyncResult> Synchronize
    (
        Workflow? initialWorkflow,
        Workflow changedWorkflow
    )
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var schedulesWorkflowCron = scope.ServiceProvider.GetRequiredService<SchedulesWorkflowCron>();

        var fakeProfile = new Profile { Path = @"C:\fake\path\to\profiles\one" };
        var fakeRelativePath = @"workflows\fake-workflow.json";

        if (initialWorkflow is not null)
        {
            await schedulesWorkflowCron.For(await WorkflowMatch.Fake(fakeProfile, fakeRelativePath, initialWorkflow));
        }

        try
        {
            return await schedulesWorkflowCron.For(await WorkflowMatch.Fake(fakeProfile, fakeRelativePath, changedWorkflow));
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
        var changedWorkflow = new Workflow
        {
            IsOn = false,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(null, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseDisabled, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfEmpty()
    {
        var fakeWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { }
        };
        var result = await Synchronize(null, fakeWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseEmpty, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfUnchanged()
    {
        var initialWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var changedWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.IgnoreBecauseUnchanged, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUnscheduleWorkflowIfDisabled()
    {
        var initialWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var changedWorkflow = new Workflow
        {
            IsOn = false,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UnscheduleBecauseDisabled, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUnscheduleWorkflowIfEmpty()
    {
        var initialWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var changedWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { }
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UnscheduleBecauseEmpty, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanUpdateScheduleIfChanged()
    {
        var initialWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var changedWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/10 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(initialWorkflow, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.UpdateBecauseChanged, result.Action);
        Assert.NotNull(result.NextUtc);
    }

    [Fact]
    public async Task CanScheduleWorkflowIfNew()
    {
        var changedWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(null, changedWorkflow);
        Assert.Equal(WorkflowSyncAction.ScheduleBecauseNew, result.Action);
        Assert.NotNull(result.NextUtc);
    }
}