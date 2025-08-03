using System.Threading.Tasks;
using Xunit;

namespace Aion.Tests.Core.Services;

public class TestsWorkflowExecution(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    [Fact]
    public async Task CanHandleStepDependencies()
    {
        var results = await testWebApplication.ExecutesWorkflow("one", "tests-serilog");
        Assert.Equal(1, results.Count);
        Assert.Equal(0, results[0].ExitCode);
    }
}