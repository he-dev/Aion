using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
    private async Task<ImmutableList<SynchronizeWorkflowResult>> SynchronizeWorkflow(params WorkflowConfiguration[] workflows)
    {
        using var scope = testWebApplication.Services.CreateScope();
        var scheduleWorkflow = scope.ServiceProvider.GetRequiredService<SynchronizeWorkflow>();

        try
        {
            var results = ImmutableList<SynchronizeWorkflowResult>.Empty;
            foreach (var workflow in workflows)
            {
                var result = await scheduleWorkflow.Invoke(workflow);
                results = results.Add(result);
            }

            return results;
        }
        finally
        {
            // meta: Clear all schedules for the next test.
            var scheduler = await scope.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.Clear();
        }
    }

    private static readonly WorkflowConfiguration DummyWorkflow = new WorkflowConfiguration
    {
        Enabled = true,
        Cron = "0/5 * * * * ?",
        Path = new WorkflowPath(@"C:\fake\path\to\profiles\test-cases", new WorkflowName("fake-workflow.json")),
        Steps = []
    };

    [Fact]
    public async Task IgnoresDisabledWorkflow()
    {
        var results = await SynchronizeWorkflow(DummyWorkflow with { Enabled = false });
        Assert.Single(results);
        Assert.Equal(typeof(IgnoreWorkflow), results.First().ActionType);
    }

    [Fact]
    public async Task IgnoresEmptyWorkflow()
    {
        var results = await SynchronizeWorkflow(DummyWorkflow with { Enabled = true });
        Assert.Single(results);
        Assert.Equal(typeof(IgnoreWorkflow), results.First().ActionType);
    }

    [Fact]
    public async Task IgnoresUnchangedWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            DummyWorkflow with { Enabled = true, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            DummyWorkflow with { Enabled = true, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        );
        Assert.Equal(2, results.Count);
        Assert.Equal(typeof(ScheduleNewWorkflow), results.First().ActionType);
        Assert.Equal(typeof(IgnoreWorkflow), results.Last().ActionType);
    }

    [Fact]
    public async Task UnschedulesDisabledWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            DummyWorkflow with { Enabled = true, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            DummyWorkflow with { Enabled = false, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        );
        Assert.Equal(2, results.Count);
        Assert.Equal(typeof(ScheduleNewWorkflow), results.First().ActionType);
        Assert.Equal(typeof(UnscheduleDisabledWorkflow), results.Last().ActionType);
    }

    [Fact]
    public async Task UnschedulesEmptyWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            DummyWorkflow with { Enabled = true, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            DummyWorkflow with { Enabled = true, Steps = [] }
        );
        Assert.Equal(2, results.Count);
        Assert.Equal(typeof(ScheduleNewWorkflow), results.First().ActionType);
        Assert.Equal(typeof(UnscheduleEmptyWorkflow), results.Last().ActionType);
    }

    [Fact]
    public async Task ReschedulesChangedWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            DummyWorkflow with { Enabled = true, Cron = "0/5 * * * * ?", Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] },
            DummyWorkflow with { Enabled = true, Cron = "0/6 * * * * ?",Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        );
        Assert.Equal(2, results.Count);
        Assert.Equal(typeof(ScheduleNewWorkflow), results.First().ActionType);
        Assert.Equal(typeof(RescheduleChangedWorkflow), results.Last().ActionType);
    }

    [Fact]
    public async Task SchedulesNewWorkflow()
    {
        var results = await SynchronizeWorkflow
        (
            DummyWorkflow with { Enabled = true, Steps = [new StepConfiguration { Enabled = true, FileName = @"c:\fake\path\to\fake.exe" }] }
        );
        Assert.Single(results);
        Assert.Equal(typeof(ScheduleNewWorkflow), results.First().ActionType);
    }
}