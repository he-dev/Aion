using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Scheduling;
using Aion.Core.Templates;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aion.Tests.Core.Services.Scheduling;

public class TestsWorkflowScheduleChanges(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    public async Task<WorkflowSynchronizationSummary> Synchronize
    (
        WorkflowMatch? currentWorkflowMatch,
        WorkflowMatch changedWorkflowMatch
    )
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var schedulesWorkflowCron = scope.ServiceProvider.GetRequiredService<SchedulesWorkflowCron>();

        if (currentWorkflowMatch is not null)
        {
            await schedulesWorkflowCron.For(currentWorkflowMatch);
        }

        try
        {
            return await schedulesWorkflowCron.For(changedWorkflowMatch);
        }
        finally
        {
            // core: Clear all schedules for the next test.
            await schedulesWorkflowCron.Clear();
        }
    }


    private WorkflowMatch FakeWorkflowMatch { get; } = new(new Profile
    {
        Path = @"C:\fake\path\to\profiles\one"
    }, @"workflows\fake-workflow.json");

    [Fact]
    public async Task CanIgnoreWorkflowIfDisabled() { }

    [Fact]
    public async Task CanIgnoreWorkflowIfEmpty()
    {
        var fakeWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { }
        };
        var result = await Synchronize(null, await FakeWorkflowMatch.Load(fakeWorkflow));
        Assert.Equal(WorkflowAction.IgnoreBecauseEmpty, result.Action);
        Assert.Null(result.NextUtc);
    }

    [Fact]
    public async Task CanIgnoreWorkflowIfUnchanged()
    {
        var fakeWorkflowCurrent = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var fakeWorkflowChange = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize
        (
            await FakeWorkflowMatch.Load(fakeWorkflowCurrent),
            await FakeWorkflowMatch.Load(fakeWorkflowChange)
        );
        Assert.Equal(WorkflowAction.IgnoreBecauseUnchanged, result.Action);
        Assert.Null(result.NextUtc);
    }

    public async Task CanUnscheduleWorkflowIfDisabled() { }
    public async Task CanUnscheduleWorkflowIfEmpty() { }
    public async Task CanUpdateScheduleIfChanged() { }

    [Fact]
    public async Task CanScheduleWorkflowIfNew()
    {
        var fakeWorkflow = new Workflow
        {
            IsOn = true,
            Cron = "0/5 * * * * ?",
            Steps = { new Workflow.Step { IsOn = true, File = new StringTemplate(@"c:\fake\path\to\fake.exe") } }
        };
        var result = await Synchronize(null, await FakeWorkflowMatch.Load(fakeWorkflow));
        Assert.Equal(WorkflowAction.ScheduleBecauseNew, result.Action);
        Assert.NotNull(result.NextUtc);
    }
}