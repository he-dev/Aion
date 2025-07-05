using Aion.Core.Util;
using Microsoft.Extensions.Options;

namespace Aion.Core.Modules;

public record WorkflowEngineOptions
{
    public string WorkflowDirectory { get; set; } = null!;
}

public class WorkflowEnginePostConfigure : IPostConfigureOptions<WorkflowEngineOptions>
{
    public void PostConfigure(string? name, WorkflowEngineOptions options)
    {
        options.WorkflowDirectory = VariableTemplate.Render(options.WorkflowDirectory, []);
    }
}