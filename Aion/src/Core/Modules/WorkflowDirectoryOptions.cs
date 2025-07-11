using Aion.Util.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core.Modules;

public record WorkflowDirectoryOptions
{
    public string Path { get; set; } = null!;
}

public class WorkflowDirectoryPostConfigure : IPostConfigureOptions<WorkflowDirectoryOptions>
{
    public void PostConfigure(string? name, WorkflowDirectoryOptions options)
    {
        options.Path = VariableTemplate.Render(options.Path, []);
    }
}