using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Context.Services.Commands;
using Aion.Modules;
using Aion.Modules.Services;
using Aion.Modules.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace Aion.Tests.Premise.Services.Commands;

public class TestSynchronizeWorkflow(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    private async Task<SynchronizeWorkflowResult> SynchronizeWorkflow
    (
        WorkflowConfiguration? initialWorkflow,
        WorkflowConfiguration changedWorkflow
    )
    {
        //using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<SynchronizeWorkflow>();

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
        var changedWorkflow = new WorkflowConfiguration
        {
            Enabled = false,
            Cron = "0/5 * * * * ?",
            Path = new WorkflowPath( @"C:\fake\path\to\profiles\test-cases", "test-cases", new WorkflowName("fake-workflow.json")),
            Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]

        };
        var result = await SynchronizeWorkflow(null, changedWorkflow);
        Assert.Equal(typeof(UnscheduleDisabledWorkflow), result.ActionType);
    }

    // [Fact]
    // public async Task CanIgnoreWorkflowIfEmpty()
    // {
    //     var fakeWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = []
    //     };
    //     var result = await SynchronizeWorkflow(null, fakeWorkflow);
    //     Assert.Equal(WorkflowAction.IgnoreBecauseEmpty, result.Action);
    //     Assert.Null(result.NextUtc);
    // }
    //
    // [Fact]
    // public async Task CanIgnoreWorkflowIfUnchanged()
    // {
    //     var initialWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var changedWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var result = await SynchronizeWorkflow(initialWorkflow, changedWorkflow);
    //     Assert.Equal(WorkflowAction.IgnoreBecauseUnchanged, result.Action);
    //     Assert.Null(result.NextUtc);
    // }
    //
    // [Fact]
    // public async Task CanUnscheduleWorkflowIfDisabled()
    // {
    //     var initialWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var changedWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = false,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var result = await SynchronizeWorkflow(initialWorkflow, changedWorkflow);
    //     Assert.Equal(WorkflowAction.UnscheduleBecauseDisabled, result.Action);
    //     Assert.Null(result.NextUtc);
    // }
    //
    // [Fact]
    // public async Task CanUnscheduleWorkflowIfEmpty()
    // {
    //     var initialWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var changedWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = []
    //     };
    //     var result = await SynchronizeWorkflow(initialWorkflow, changedWorkflow);
    //     Assert.Equal(WorkflowAction.UnscheduleBecauseEmpty, result.Action);
    //     Assert.Null(result.NextUtc);
    // }
    //
    // [Fact]
    // public async Task CanUpdateScheduleIfChanged()
    // {
    //     var initialWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var changedWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/10 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var result = await SynchronizeWorkflow(initialWorkflow, changedWorkflow);
    //     Assert.Equal(WorkflowAction.UpdateBecauseChanged, result.Action);
    //     Assert.NotNull(result.NextUtc);
    // }
    //
    // [Fact]
    // public async Task CanScheduleWorkflowIfNew()
    // {
    //     var changedWorkflow = new WorkflowConfiguration
    //     {
    //         Enabled = true,
    //         Cron = "0/5 * * * * ?",
    //         Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }]
    //     };
    //     var result = await SynchronizeWorkflow(null, changedWorkflow);
    //     Assert.Equal(WorkflowAction.ScheduleBecauseNew, result.Action);
    //     Assert.NotNull(result.NextUtc);
    // }
}