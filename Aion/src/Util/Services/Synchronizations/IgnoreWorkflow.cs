using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aion.Util.Services.Synchronizations;

public class IgnoreWorkflow
(
    ILogger<IgnoreWorkflow> logger
) : ISynchronizeWorkflow
{
    public Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        logger.LogInformation("Workflow '{WorkflowName}' is ignored.", workflow.Name);
        return Task.FromResult<SynchronizeWorkflowResult>(new SynchronizeWorkflowResult<IgnoreWorkflow>(workflow.Name))!;
    }
}