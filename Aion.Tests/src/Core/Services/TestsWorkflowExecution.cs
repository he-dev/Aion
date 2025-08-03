using System.Threading.Tasks;
using Aion.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aion.Tests.Core.Services;

public class TestsWorkflowExecution(TestWebApplication testWebApplication) : IClassFixture<TestWebApplication>
{
    [Fact]
    public async Task CanHandleStepDependencies()
    {
        using var scope = testWebApplication.Services.CreateScope();
        var executesWorkflow = scope.ServiceProvider.GetRequiredService<ExecutesWorkflow>();
        await executesWorkflow.Now(null);
    }
}