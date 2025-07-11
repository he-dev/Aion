using System.Collections.Immutable;
using System.Linq;
using Aion.Util.Scriban;

namespace Aion.Core.Modules;

public static class WorkflowExtensions
{
    public static Workflow.Step RenderVariables(this Workflow.Step step, IImmutableList<VariableGroup> variableGroups)
    {
        return step with
        {
            Script = VariableTemplate.Render(step.Script, variableGroups),
            Args = step.Args.Select(arg => VariableTemplate.Render(arg, variableGroups)).ToList(),
            WorkingDirectory = VariableTemplate.Render(step.WorkingDirectory ?? string.Empty, variableGroups),
            LogStdTo = step.LogStdTo is not null ? VariableTemplate.Render(step.LogStdTo, variableGroups) : null,
        };
    }
}