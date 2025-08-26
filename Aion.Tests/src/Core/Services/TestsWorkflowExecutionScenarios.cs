using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aion.Tests.Core.Services;

public class TestsWorkflowExecutionScenarios(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    private async Task<IImmutableList<StepResult>> ExecutesWorkflow(string profileName, string workflowName)
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = testWebApplication.Services.CreateScope();

        var engineOptions = scope.ServiceProvider.GetRequiredService<IOptions<InstanceOptions>>();
        var executesWorkflow = scope.ServiceProvider.GetRequiredService<WorkflowExecution>();
        var workflowRendering = scope.ServiceProvider.GetRequiredService<WorkflowRendering>();
        var workflowMatch = engineOptions.Value[profileName].Workflows.Single(workflowName);
        var workflow = await workflowRendering.RenderFrom(workflowMatch);
        return await executesWorkflow.Start(workflow);
    }

    [Theory]
    [InlineData("c1.success")]
    [InlineData("c1.offline")]
    [InlineData("c1.failure-1")]
    [InlineData("c1.timeout-3s")]
    [InlineData("c2.failure-1_depends")]
    [InlineData("c3.failure-1_depends_depends")]
    [InlineData("c3.success_offline_success")]
    [InlineData("c3.success_failure-1_success")]
    [InlineData("c3.offline_offline_success")]
    public async Task CanExecuteTypicalWorkflowScenarios(string workflowName)
    {
        var expected = ParseExpectedResults(workflowName);
        var actual = await ExecutesWorkflow("one", workflowName);

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (e, a) in expected.Zip(actual, (e, a) => (e, a)))
        {
            AssertStepResult(e, a);
        }
    }

    private static readonly Regex SuccessRegex = new(@"success", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FailureRegex = new(@"failure-(?<exitCode>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimeoutRegex = new(@"timeout-(?<timeout>\d+)s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DependsRegex = new(@"depends", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OfflineRegex = new(@"offline", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static void AssertStepResult(string expected, StepResult actual)
    {
        if (SuccessRegex.Match(expected) is { Success: true })
        {
            Assert.Equal(0, actual.ExitCode);
            Assert.Null(actual.Exception);
            Assert.NotNull(actual.Duration);
            return;
        }

        if (FailureRegex.Match(expected) is { Success: true } failure)
        {
            Assert.Equal(int.Parse(failure.Groups["exitCode"].Value), actual.ExitCode);
            Assert.Null(actual.Exception);
            Assert.NotNull(actual.Duration);
            return;
        }

        if (TimeoutRegex.Match(expected) is { Success: true } timeout)
        {
            Assert.Null(actual.ExitCode);
            Assert.IsType<ProcessTimeout>(actual.Exception);
            Assert.NotNull(actual.Duration);
            var expectedDuration = TimeSpan.FromSeconds(int.Parse(timeout.Groups["timeout"].Value));
            Assert.True(expectedDuration <= actual.Duration, "Step execution too less than expected.");
            return;
        }

        if (DependsRegex.Match(expected) is { Success: true })
        {
            Assert.Null(actual.ExitCode);
            Assert.Null(actual.Exception);
            Assert.Null(actual.Duration);
            return;
        }

        if (OfflineRegex.Match(expected) is { Success: true })
        {
            Assert.Null(actual.ExitCode);
            Assert.Null(actual.Exception);
            Assert.Null(actual.Duration);
            return;
        }

        Assert.Fail("Unknown step result.");
    }

    private static IImmutableList<string> ParseExpectedResults(string workflowName)
    {
        // "c3.1-null-0" -> count=3, exitCodes=[1, null, 0]
        var parts = workflowName[1..].Split('.');
        //var count = int.Parse(parts[0]);
        return parts[1].Split('_').ToImmutableList();
    }
}