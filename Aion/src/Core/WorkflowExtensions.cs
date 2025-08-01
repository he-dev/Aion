using System.Collections.Immutable;
using System.Linq;
using Aion.Home.Jobs;
using Aion.Util.Json;
using Aion.Util.Quartz;
using Aion.Util.Scriban;
using Quartz;

namespace Aion.Core;

public static class ExtendsWorkflow
{
    public static Workflow.Step RenderTemplates(this Workflow.Step step, IImmutableList<VariableGroup> variables)
    {
        return step with
        {
            File = RendersTemplates.In(step.File, variables),
            Args = step.Args.Select(arg => RendersTemplates.In(arg, variables)).ToList(),
            WorkingDirectory = RendersTemplates.In(step.WorkingDirectory ?? string.Empty, variables),
            Logging = step.Logging.RenderFilePaths(template => RendersTemplates.In(template, variables))
        };
    }
}