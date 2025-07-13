using Aion.Util.Scriban;
using Microsoft.Extensions.Options;

namespace Aion.Core;

public record SynchronizationJobOptions
{
    public string Cron { get; init; } = null!;

    public bool IsOn { get; init; } = true;
}

public record QuartzServerOptions
{
    public int StartDelaySeconds { get; init; }
}

public record ProfileOptions
{
    public string Name { get; init; } =  "Aion";
}

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