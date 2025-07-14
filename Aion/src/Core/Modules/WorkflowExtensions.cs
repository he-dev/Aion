using System.Collections.Immutable;
using System.Linq;
using Aion.Util.Json;
using Aion.Util.Scriban;

namespace Aion.Core.Modules;

public static class WorkflowExtensions
{
    public static Workflow.Step RenderTemplates(this Workflow.Step step, IImmutableList<VariableGroup> variables)
    {
        return step with
        {
            File = VariableTemplate.Render(step.File, variables),
            Args = step.Args.Select(arg => VariableTemplate.Render(arg, variables)).ToList(),
            WorkingDirectory = VariableTemplate.Render(step.WorkingDirectory ?? string.Empty, variables),
            Console = step.Console.RenderPaths(variables)
        };
    }
}