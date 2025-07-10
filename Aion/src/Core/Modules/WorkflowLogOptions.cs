using Aion.Util.Scriban;
using Microsoft.Extensions.Options;
using Serilog;

namespace Aion.Core.Modules;

public record WorkflowLogOptions
{
    public string DirectoryPath { get; set; } = null!;
    public string OutputTemplate { get; set; } = null!;
    public RollingInterval RollingInterval { get; set; }
    public int RetainedFileCountLimit { get; set; }
}

public class WorkflowLogPostConfigure : IPostConfigureOptions<WorkflowLogOptions>
{
    public void PostConfigure(string? name, WorkflowLogOptions options)
    {
        options.DirectoryPath = VariableTemplate.Render(options.DirectoryPath, []);
    }
}