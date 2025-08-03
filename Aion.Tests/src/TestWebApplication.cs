using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Home;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aion.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public class TestWebApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }

    public async Task<IImmutableList<StepResult>> ExecutesWorkflow(string profileName, string workflowName)
    {
        using var activity = new Activity("TestingWorkflowExecution").Start();
        using var scope = Services.CreateScope();

        var engineOptions = scope.ServiceProvider.GetRequiredService<IOptions<EngineOptions>>();
        var executesWorkflow = scope.ServiceProvider.GetRequiredService<ExecutesWorkflow>();

        var workflowMatch = await engineOptions.Value[profileName].Workflows.Single(workflowName).Load();
        return await executesWorkflow.Now(workflowMatch);
    }
}